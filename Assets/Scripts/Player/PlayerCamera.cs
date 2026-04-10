using UnityEngine;
using UnityEngine.InputSystem;
using TinCan.Core.Domain;
using VContainer;
using System;

/// <summary>
/// Handles first-person camera control with mouse input.
/// Supports mouse look with pitch and yaw rotation.
/// </summary>
public class PlayerCamera : MonoBehaviour
{
    [SerializeField] private float _mouseSensitivity = 0.5f;
    [SerializeField] private float _maxLookAngle = 90f;

    private IInputService _inputService;

    [Inject]
    public void Construct(IInputService inputService)
    {
        _inputService = inputService;
        Debug.Log(_inputService);
    }

    private Camera _camera;
    private Transform _cameraTransform => _camera.transform;

    private IControllable? _controllableActor;
    private float _xRotation = 0f;

    private void Start()
    {
        // If no camera transform assigned, use child Camera component
        if (_camera == null)
        {
            _camera = GetComponentInChildren<Camera>();
        }

        _controllableActor = GetComponentInParent<ControllableActorBase>();
        if (_controllableActor is not null)
        {
            _controllableActor.OnControlEnableChangedEvent += (enabled) =>
            {
                // Optionally handle enabling/disabling camera control here
                SetCameraEnabled(enabled);
            };
        }

        // Lock and hide cursor
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void SetCameraEnabled(bool enabled)
    {
        if (_camera != null)
        {
            _camera.enabled = enabled;
        }
    }

    private void Update()
    {
        if (!_controllableActor.IsControlsEnabled) return;
        HandleMouseInput();
        HandleCursorToggle();
    }

    /// <summary>
    /// Processes mouse input for camera rotation.
    /// </summary>
    private void HandleMouseInput()
    {
        Vector2 mouseDelta = _inputService != null ? _inputService.GetMouseDelta() : Vector2.zero;
        float mouseX = mouseDelta.x * _mouseSensitivity;
        float mouseY = mouseDelta.y * _mouseSensitivity;

        // Rotate body left/right (yaw)
        transform.Rotate(Vector3.up * mouseX);

        // Rotate camera up/down (pitch)
        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, -_maxLookAngle, _maxLookAngle);

        _cameraTransform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
    }

    /// <summary>
    /// Toggles cursor lock with Escape key.
    /// </summary>
    private void HandleCursorToggle()
    {
        if (_inputService != null && _inputService.WasActionTriggered(ActionNames.Cancel))
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
            }
        }
    }

    void OnDestroy()
    {
        if (_controllableActor is null) return;
        _controllableActor.OnControlEnableChangedEvent -= (enabled) =>
        {
            // Cleanup if needed
            SetCameraEnabled(enabled);
        };
    }

}
