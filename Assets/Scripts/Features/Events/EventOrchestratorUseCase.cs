using System;
using VContainer.Unity;
using UnityEngine;
using TinCan.Core.Domain;

namespace TinCan.Features.Events
{
    public class EventOrchestratorUseCase : IEventOrchestrator, ITickable
    {
        private readonly IShipState _shipState;
        private readonly ITimeService _timeService;

        public event Action<CoordinatedEventDefinition> OnEventStarted;
        public event Action<CoordinatedEventDefinition, bool> OnEventEnded;

        public bool IsEventActive { get; private set; }
        public CoordinatedEventDefinition CurrentEvent { get; private set; }
        public float RemainingTime { get; private set; }

        public EventOrchestratorUseCase(IShipState shipState, ITimeService timeService)
        {
            _shipState = shipState;
            _timeService = timeService;
        }

        public void TriggerEvent(CoordinatedEventDefinition definition)
        {
            if (IsEventActive) return;

            CurrentEvent = definition;
            RemainingTime = definition.Duration;
            IsEventActive = true;

            Debug.Log($"[EventOrchestrator] Starting event: {definition.EventName}");
            OnEventStarted?.Invoke(definition);
        }

        public void Tick()
        {
            if (!IsEventActive) return;

            RemainingTime -= _timeService.DeltaTime;

            if (RemainingTime <= 0)
            {
                EndEvent();
            }
        }

        private void EndEvent()
        {
            bool success = true;
            var controller = _shipState.Controller;

            foreach (var tag in CurrentEvent.RequiredTags)
            {
                if (controller == null || !controller.HasTag(tag))
                {
                    success = false;
                    break;
                }
            }

            Debug.Log($"[EventOrchestrator] Event ended: {CurrentEvent.EventName}. Success: {success}");
            OnEventEnded?.Invoke(CurrentEvent, success);

            IsEventActive = false;
            CurrentEvent = null;
        }
    }
}
