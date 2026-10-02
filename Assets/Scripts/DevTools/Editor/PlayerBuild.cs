#nullable enable
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// Player builds from the enabled scenes of the build settings (scene 0 is the game), Mono:
    /// <list type="bullet">
    /// <item>the Linux dedicated server (headless) into <c>Builds/LinuxServer/</c>; needs the Editor module
    /// "Linux Dedicated Server Build Support", in an Editor started after it was installed;</item>
    /// <item>the Windows client into <c>Builds/Win64/</c>, so clients and the server come from the same code (NGO
    /// refuses a client whose network prefabs differ).</item>
    /// </list>
    /// The menus queue the build so a <c>unity cmd menu</c> call returns at once; the outcome is written to the build
    /// folder's <c>build-result.txt</c> for <c>.tools/build.ps1</c> to poll. The <c>*FromCommandLine</c> methods
    /// are the <c>-executeMethod</c> entries for a batch-mode Editor. Plan: .docs/plans/dedicated-server-container.md.
    /// </summary>
    public static class PlayerBuild
    {
        public const string ResultFileName = "build-result.txt";

        private static readonly Target LinuxServer = new("Builds/LinuxServer", "TinCanServer.x86_64",
            BuildTarget.StandaloneLinux64, StandaloneBuildSubtarget.Server);

        private static readonly Target WindowsClient = new("Builds/Win64", "TinCan.exe",
            BuildTarget.StandaloneWindows64, StandaloneBuildSubtarget.Player);

        [MenuItem("TinCan/Build/Linux Server")]
        public static void LinuxServerFromMenu() => Queue(LinuxServer);

        [MenuItem("TinCan/Build/Windows Client")]
        public static void WindowsClientFromMenu() => Queue(WindowsClient);

        public static void LinuxServerFromCommandLine() => EditorApplication.Exit(Build(LinuxServer) ? 0 : 1);

        public static void WindowsClientFromCommandLine() => EditorApplication.Exit(Build(WindowsClient) ? 0 : 1);

        private static void Queue(Target target)
        {
            WriteResult(target, "building");
            EditorApplication.delayCall += () => Build(target);
        }

        private static bool Build(Target target)
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && File.Exists(scene.path))
                .Select(scene => scene.path)
                .ToArray();

            var activeTarget = EditorUserBuildSettings.activeBuildTarget;
            var activeSubtarget = EditorUserBuildSettings.standaloneBuildSubtarget;
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = target.ExecutablePath,
                    target = target.BuildTarget,
                    subtarget = (int)target.Subtarget,
                    options = BuildOptions.None
                });
            }
            catch (Exception exception)
            {
                WriteResult(target, $"failed: {exception.Message}");
                Debug.LogException(exception);
                return false;
            }
            finally
            {
                // Leave the Editor on the target it was on, so the next Play or build is unaffected.
                EditorUserBuildSettings.standaloneBuildSubtarget = activeSubtarget;
                if (EditorUserBuildSettings.activeBuildTarget != activeTarget)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(activeTarget), activeTarget);
            }

            var summary = report.summary;
            // Unity can report Succeeded when its postprocess threw (an Editor started before the Linux module was
            // installed does that), so the executable on disk is the verdict.
            bool succeeded = summary.result == BuildResult.Succeeded && File.Exists(target.ExecutablePath);
            string line = succeeded
                ? $"succeeded: {summary.outputPath} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:0} s, {scenes.Length} scenes)"
                : summary.result == BuildResult.Succeeded
                    ? "failed: Unity reported success but wrote no player (restart the Editor after installing a build module; see the Editor log)"
                    : $"failed: {summary.result}, {summary.totalErrors} errors (see the Editor log)";
            WriteResult(target, line);
            Debug.Log($"[PlayerBuild] {target.ExecutableName} build {line}");
            return succeeded;
        }

        private static void WriteResult(Target target, string line)
        {
            Directory.CreateDirectory(target.OutputDirectory);
            File.WriteAllText(Path.Combine(target.OutputDirectory, ResultFileName), line + Environment.NewLine);
        }

        private sealed class Target
        {
            public Target(string outputDirectory, string executableName, BuildTarget buildTarget, StandaloneBuildSubtarget subtarget)
            {
                OutputDirectory = outputDirectory;
                ExecutableName = executableName;
                BuildTarget = buildTarget;
                Subtarget = subtarget;
            }

            public string OutputDirectory { get; }
            public string ExecutableName { get; }
            public BuildTarget BuildTarget { get; }
            public StandaloneBuildSubtarget Subtarget { get; }
            public string ExecutablePath => Path.Combine(OutputDirectory, ExecutableName);
        }
    }
}
