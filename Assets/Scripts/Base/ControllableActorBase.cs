using System;
using UnityEngine;

public abstract class ControllableActorBase : MonoBehaviour, IControllable
{
    public bool IsControlsEnabled { get; private set; } = false;

    public void DisableControls()
    {
        IsControlsEnabled = false;
    }

    public void EnableControls()
    {
        IsControlsEnabled = true;
    }
}
