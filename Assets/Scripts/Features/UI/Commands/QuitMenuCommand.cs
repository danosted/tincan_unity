#nullable enable
using UnityEngine;

namespace TinCan.Features.UI.Commands
{
    public class QuitMenuCommand : IMenuCommand
    {
        public const string Id = "Quit";

        public string CommandId => Id;

        public void Execute(MenuContext context)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
