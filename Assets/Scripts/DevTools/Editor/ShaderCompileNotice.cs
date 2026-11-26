#nullable enable
using System;
using UnityEditor;
using UnityEngine;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// Says on the Game and Scene views when shaders are still compiling. The Editor compiles shaders in the background,
    /// and until a variant is ready it draws the old one or nothing, with no sign of it, so a shader change can be judged
    /// before it shows (.docs/CODE_MAP.md, "Legacy, oddities and traps"). While anything compiles: "Compiling shaders…";
    /// when it ends: "Shaders ready" with how long it took, also in the Console when it took a while.
    /// </summary>
    [InitializeOnLoad]
    internal static class ShaderCompileNotice
    {
        // How often the notice is shown again while compiling, so it stays up; it fades a little after.
        private const double RefreshSeconds = 1.0;
        private const double FadeSeconds = 1.5;
        // The Editor compiles small variants all the time: compiles shorter than this show nothing, longer ones than
        // LogAfterSeconds also go to the Console.
        private const double ShowAfterSeconds = 0.5;
        private const double LogAfterSeconds = 2.0;

        private static readonly Type? GameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");

        private static bool _compiling;
        private static bool _noticed;
        private static double _started;
        private static double _shown;

        static ShaderCompileNotice() => EditorApplication.update += Update;

        private static void Update()
        {
            bool compiling = ShaderUtil.anythingCompiling;
            double now = EditorApplication.timeSinceStartup;

            if (compiling)
            {
                if (!_compiling)
                {
                    _started = now;
                    _noticed = false;
                }

                bool due = _noticed ? now - _shown >= RefreshSeconds : now - _started >= ShowAfterSeconds;
                if (due)
                {
                    Show($"Compiling shaders… {now - _started:0} s", FadeSeconds);
                    _shown = now;
                    _noticed = true;
                }
            }
            else if (_compiling && _noticed)
            {
                double took = now - _started;
                Show($"Shaders ready ({took:0.0} s)", 2.5);
                if (took >= LogAfterSeconds) Debug.Log($"[ShaderCompileNotice] Shaders ready after {took:0.0} s.");
            }

            _compiling = compiling;
        }

        private static void Show(string text, double fade)
        {
            var content = new GUIContent(text);
            if (GameViewType != null)
            {
                foreach (var view in Resources.FindObjectsOfTypeAll(GameViewType))
                {
                    ((EditorWindow)view).ShowNotification(content, fade);
                }
            }

            foreach (var view in SceneView.sceneViews)
            {
                ((SceneView)view).ShowNotification(content, fade);
            }
        }
    }
}
