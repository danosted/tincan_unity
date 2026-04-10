using UnityEngine;

public interface IControllable
{
    bool IsControlsEnabled { get; }
    void EnableControls();
    void DisableControls();
    event System.Action<bool> OnControlEnableChangedEvent;
}
