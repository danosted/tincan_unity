using System;
using VContainer.Unity;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;

namespace TinCan.Features.Events
{
    public class EventOrchestratorUseCase : IEventOrchestrator, ITickable
    {
        private readonly IShipState _shipState;
        private readonly ITimeService _timeService;
        private readonly IEventPublisher _eventPublisher;

        public event Action<CoordinatedEventDefinition> OnEventStarted;
        public event Action<CoordinatedEventDefinition, bool> OnEventEnded;

        public bool IsEventActive { get; private set; }
        public CoordinatedEventDefinition CurrentEvent { get; private set; }
        public float RemainingTime { get; private set; }

        public EventOrchestratorUseCase(IShipState shipState, ITimeService timeService, IEventPublisher eventPublisher)
        {
            _shipState = shipState;
            _timeService = timeService;
            _eventPublisher = eventPublisher;
        }

        public void TriggerEvent(CoordinatedEventDefinition definition)
        {
            if (IsEventActive) return;

            CurrentEvent = definition;
            RemainingTime = definition.Duration;
            IsEventActive = true;

            _eventPublisher.LogInfo("EventOrchestrator", $"Starting event: {definition.EventName}");
            _eventPublisher.Publish(new CoordinatedEventStartedEvent(definition.EventName));
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

            _eventPublisher.LogInfo("EventOrchestrator", $"Event ended: {CurrentEvent.EventName}. Success: {success}");
            _eventPublisher.Publish(new CoordinatedEventEndedEvent(CurrentEvent.EventName, success));
            OnEventEnded?.Invoke(CurrentEvent, success);

            IsEventActive = false;
            CurrentEvent = null;
        }
    }
}
