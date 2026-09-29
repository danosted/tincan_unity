#nullable enable
using UnityEngine;

namespace TinCan.Features.SkyHazards
{
    /// <summary>Domain, pure: how a drifting hazard closes on the ship. It homes on a point on the ship (the middle of its body) at a fixed speed.</summary>
    public class HazardDriftProcessor
    {
        public Vector3 Step(Vector3 hazard, Vector3 ship, float speed, float deltaTime) =>
            speed <= 0f || deltaTime <= 0f ? hazard : Vector3.MoveTowards(hazard, ship, speed * deltaTime);
    }
}
