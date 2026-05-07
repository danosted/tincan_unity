using System;

namespace TinCan.Features.Events
{
    public interface IEventOrchestrator
    {
        event Action<CoordinatedEventDefinition> OnEventStarted;
        event Action<CoordinatedEventDefinition, bool> OnEventEnded; // bool is success

        bool IsEventActive { get; }
        CoordinatedEventDefinition CurrentEvent { get; }
        float RemainingTime { get; }

        void TriggerEvent(CoordinatedEventDefinition definition);
    }
}
