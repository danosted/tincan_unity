using UnityEngine;
using System.Collections.Generic;
using TinCan.Core.Domain.Abilities.Tags;

namespace TinCan.Features.Events
{
    [CreateAssetMenu(fileName = "NewCoordinatedEvent", menuName = "TinCan/Events/Coordinated Event")]
    public class CoordinatedEventDefinition : ScriptableObject
    {
        public string EventName;
        public float Duration = 30f;
        public List<GameplayTag> RequiredTags = new List<GameplayTag>();

        // Side effects are kept simple for now as per plan
        public string SuccessMessage = "Event Succeeded!";
        public string FailureMessage = "Event Failed!";
    }
}
