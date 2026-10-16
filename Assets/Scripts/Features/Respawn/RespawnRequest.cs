#nullable enable
namespace TinCan.Features.Respawn
{
    /// <summary>A player's choice while overboard or leashed, as the server received it.</summary>
    public readonly struct RespawnRequest
    {
        public readonly ulong ClientId;
        public readonly OverboardChoiceKind Choice;

        public RespawnRequest(ulong clientId, OverboardChoiceKind choice)
        {
            ClientId = clientId;
            Choice = choice;
        }
    }
}
