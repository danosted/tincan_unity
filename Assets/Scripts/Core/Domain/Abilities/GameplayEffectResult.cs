namespace TinCan.Core.Domain.Abilities
{
    /// <summary>
    /// Domain Layer: Represents the result of a gameplay effect application.
    /// Used to provide feedback to systems like Visual Scripting or UI.
    /// </summary>
    public struct GameplayEffectResult
    {
        public bool Success;
        public string Message;

        public static GameplayEffectResult Successful() => new GameplayEffectResult { Success = true, Message = "Effect applied successfully." };
        public static GameplayEffectResult Failure(string msg) => new GameplayEffectResult { Success = false, Message = msg };
    }
}
