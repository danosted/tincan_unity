using UnityEngine;

public interface IControllableAirship : IControllable
{
    void SetThrottleInput(float throttle);
    void SetTurnInput(float turn);
    void SetPitchInput(float pitch);
}
