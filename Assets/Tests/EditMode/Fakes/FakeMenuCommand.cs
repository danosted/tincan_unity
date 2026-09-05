#nullable enable
using System.Collections.Generic;
using TinCan.Features.UI;

namespace TinCan.Tests.EditMode.Fakes
{
    public class FakeMenuCommand : IMenuCommand
    {
        public FakeMenuCommand(string commandId)
        {
            CommandId = commandId;
        }

        public string CommandId { get; }
        public List<MenuContext> Executions { get; } = new();
        public string? LastAddressValue { get; private set; }

        public void Execute(MenuContext context)
        {
            Executions.Add(context);
            LastAddressValue = context.GetValue("address");
        }
    }
}
