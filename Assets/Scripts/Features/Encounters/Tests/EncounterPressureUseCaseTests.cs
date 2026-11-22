#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Tests.Fakes;

namespace TinCan.Features.Encounters.Tests
{
    public class EncounterPressureUseCaseTests
    {
        private sealed class RecordingParticipant : ISessionParticipant
        {
            public List<string> Calls { get; } = new();
            public void ResetForSession() => Calls.Add("reset");
            public void SetSessionActive(bool active) => Calls.Add(active ? "on" : "off");
        }

        [Test]
        public void TheFirstServerTickTurnsEveryParticipantsPressureOff()
        {
            var first = new RecordingParticipant();
            var second = new RecordingParticipant();
            var pressure = new EncounterPressureUseCase(new FakeNetworkService(), new ISessionParticipant[] { first, second });

            pressure.Tick();

            Assert.That(first.Calls, Is.EqualTo(new[] { "off" }));
            Assert.That(second.Calls, Is.EqualTo(new[] { "off" }));
            Assert.That(pressure.PressureOn, Is.False);
        }

        [Test]
        public void LaterTicksDoNotCallTheParticipantsAgain()
        {
            var participant = new RecordingParticipant();
            var pressure = new EncounterPressureUseCase(new FakeNetworkService(), new ISessionParticipant[] { participant });

            pressure.Tick();
            pressure.Tick();
            pressure.Tick();

            Assert.That(participant.Calls, Is.EqualTo(new[] { "off" }));
        }

        [Test]
        public void NoParticipantsIsFine()
        {
            var pressure = new EncounterPressureUseCase(new FakeNetworkService(), new ISessionParticipant[0]);

            Assert.DoesNotThrow(pressure.Tick);
        }
    }
}
