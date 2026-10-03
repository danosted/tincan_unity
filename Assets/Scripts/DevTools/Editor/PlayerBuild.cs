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
    /// <item>"Perf" variants of both (<c>Builds/LinuxServerPerf/</c>, <c>Builds/Win64Perf/</c>): development builds, so the
    /// profiler counters and the network simulator the perf runs read are compiled in (.docs/plans/performance-budgets.md).</item>
    /// </list>
    /// The menus build at once, on the main thread, and write the outcome to the build folder's <c>build-result.txt</c>
    /// for <c>.tools/build.ps1</c> to poll (a <c>unity cmd menu</c> call times out meanwhile; the script ignores that).
    /// A build that never starts usually means the Editor's main thread is blocked, for example by a Windows UAC or
    /// firewall prompt waiting for an answer (2026-10-03). The <c>*FromCommandLine</c> methods
    /// are the <c>-executeMethod</c> entries for a batch-mode Editor. Plan: .docs/plans/dedicated-server-container.md.
    /// </summary>
    public static class PlayerBuild
    {
        public const string ResultFileName = "build-result.txt";

        private static readonly Target LinuxServer = new("Builds/LinuxServer", "TinCanServer.x86_64",
            BuildTarget.StandaloneLinux64, StandaloneBuildSubtarget.Server);

        private static readonly Target WindowsClient = new("Builds/Win64", "TinCan.exe",
            BuildTarget.StandaloneWindows64, StandaloneBuildSubtarget.Player);

        private static readonly Target LinuxServerPerf = new("Builds/LinuxServerPerf", "TinCanServer.x86_64",
            BuildTarget.StandaloneLinux64, StandaloneBuildSubtarget.Server, BuildOptions.Development);

        private static readonly Target WindowsClientPerf = new("Builds/Win64Perf", "TinCan.exe",
            BuildTarget.StandaloneWindows64, StandaloneBuildSubtarget.Player, BuildOptions.Development);

        [MenuItem("TinCan/Build/Linux Server")]
        public static void LinuxServerFromMenu() => Run(LinuxServer);

        [MenuItem("TinCan/Build/Windows Client")]
        public static void WindowsClientFromMenu() => Run(WindowsClient);

        [MenuItem("TinCan/Build/Linux Server (Perf)")]
        public static void LinuxServerPerfFromMenu() => Run(LinuxServerPerf);

        [MenuItem("TinCan/Build/Windows Client (Perf)")]
        public static void WindowsClientPerfFromMenu() => Run(WindowsClientPerf);

        public static void LinuxServerFromCommandLine() => EditorApplication.Exit(Build(LinuxServer) ? 0 : 1);

        public static void WindowsClientFromCommandLine() => EditorApplication.Exit(Build(WindowsClient) ? 0 : 1);

        public static void LinuxServerPerfFromCommandLine() => EditorApplication.Exit(Build(LinuxServerPerf) ? 0 : 1);

        public static void WindowsClientPerfFromCommandLine() => EditorApplication.Exit(Build(WindowsClientPerf) ? 0 : 1);

        private static void Run(Target target)
        {
            WriteResult(target, "building");
            Build(target);
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
                    options = target.Options
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
            public Target(string outputDirectory, string executableName, BuildTarget buildTarget, StandaloneBuildSubtarget subtarget,
                BuildOptions options = BuildOptions.None)
            {
                OutputDirectory = outputDirectory;
                ExecutableName = executableName;
                BuildTarget = buildTarget;
                Subtarget = subtarget;
                Options = options;
            }

            public string OutputDirectory { get; }
            public string ExecutableName { get; }
            public BuildTarget BuildTarget { get; }
            public StandaloneBuildSubtarget Subtarget { get; }
            public BuildOptions Options { get; }
            public string ExecutablePath => Path.Combine(OutputDirectory, ExecutableName);
        }
    }
}
