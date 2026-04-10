using System;
using System.Collections.Generic;
using UnityEngine;
using TinCan.Features.Possession;

public abstract class ControllableActorBase : MonoBehaviour, IControllable
{
    public bool IsControlsEnabled { get; private set; } = false;

    public event Action<bool> OnControlEnableChangedEvent;

    private IPossessionResponder[] _responders;

    protected virtual void Awake()
    {
        _responders = GetComponentsInChildren<IPossessionResponder>(true);
    }

    public void DisableControls()
    {
        IsControlsEnabled = false;
        NotifyResponders(false);
        OnControlEnableChangedEvent?.Invoke(IsControlsEnabled);
    }

    public void EnableControls()
    {
        IsControlsEnabled = true;
        NotifyResponders(true);
        OnControlEnableChangedEvent?.Invoke(IsControlsEnabled);
    }

    private void NotifyResponders(bool enabled)
    {
        if (_responders == null) return;
        foreach (var responder in _responders)
        {
            if (enabled) responder.OnPossessed();
            else responder.OnUnpossessed();
        }
    }
}
