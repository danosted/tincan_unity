#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Look;
using TinCan.Core.Domain.Networking;
using UnityEngine;
using VContainer;

namespace TinCan.Core.Humanoid
{
    /// <summary>
    /// View: a player's look (yaw and pitch, read into the predicted input) and the camera the local player sees through.
    /// It switches the camera and its ears on for the local possessor and off otherwise. Where the camera sits is the
    /// profile's camera rig (<see cref="IViewRig"/>): each frame the rig places it from where the body is drawn, after
    /// the visual smoothing (order -50). The rig also gives the aim height targeting uses, on every peer.
    /// </summary>
    public class LookView : MonoBehaviour, ILookView, IPossessionReceiver
    {
        [SerializeField] private Transform? _cameraPivot;
        [SerializeField] private float _sensitivity = 0.5f;
        [SerializeField] private float _maxPitch = 85f;

        private INetworkService? _network;
        private IViewRig? _rig;
        private string? _rigWarning;
        private IHumanoidVisualAnchor? _anchor;
        private IHumanoidMovementView? _movement;

        public bool IsActive { get; private set; }
        public float Pitch { get; set; }
        public float Yaw { get; set; }
        public float Sensitivity => _sensitivity;
        public float MaxPitch => _maxPitch;
        public float AimHeight => _rig?.AimHeight(EyeHeight) ?? 0f;
        public Camera Camera => _cameraPivot != null ? _cameraPivot.GetComponent<Camera>() : null!;

        private float EyeHeight => _movement?.EyeHeight ?? 0f;

        [Inject]
        public void Construct(INetworkService network, IReadOnlyList<IViewRig> rigs)
        {
            _network = network;
            _rig = ViewRigSelection.Select(rigs, out _rigWarning);
        }

        private void Awake()
        {
            _anchor = GetComponent<IHumanoidVisualAnchor>();
            _movement = GetComponent<IHumanoidMovementView>();
        }

        private void Start()
        {
            if (_cameraPivot == null) return;

            Vector3 euler = _cameraPivot.eulerAngles;
            Yaw = euler.y;
            Pitch = euler.x > 180f ? euler.x - 360f : euler.x;
        }

        private void LateUpdate()
        {
            if (!IsActive || _cameraPivot == null || _rig == null) return;

            Vector3 drawn = _anchor != null ? _anchor.VisualPosition : transform.position;
            var (position, rotation) = _rig.Place(new ViewPose(drawn, EyeHeight, Pitch, Yaw));
            _cameraPivot.SetPositionAndRotation(position, rotation);
        }

        public void ApplyLook(float pitch, float yaw)
        {
            Pitch = pitch;
            Yaw = yaw;
        }

        public void OnPossessed(ulong playerId)
        {
            if (_cameraPivot == null || (_network?.LocalClientId ?? 0) != playerId) return;

            if (_rigWarning != null) Debug.LogWarning($"[{nameof(LookView)}] {_rigWarning}", this);
            IsActive = true;
            _cameraPivot.gameObject.SetActive(true);
            Camera.enabled = true;
            EnsureAudioListener(_cameraPivot);
            _rig?.Take(Camera, Body);
        }

        public void OnUnpossessed()
        {
            if (IsActive && _cameraPivot != null) _rig?.Release(Camera, Body);
            IsActive = false;
            if (_cameraPivot != null) _cameraPivot.gameObject.SetActive(false);
        }

        private IReadOnlyList<Renderer> Body => _anchor?.BodyRenderers ?? System.Array.Empty<Renderer>();

        // The ears go with the camera the local player looks through. Scenes hold no camera (it lives on what you
        // possess), so without this nothing hears gameplay cue sounds and Unity warns every frame. Unpossessing
        // deactivates the pivot, which switches its listener off, so only one is ever active.
        private static void EnsureAudioListener(Transform pivot)
        {
            if (!pivot.TryGetComponent<AudioListener>(out var listener)) listener = pivot.gameObject.AddComponent<AudioListener>();
            listener.enabled = true;
        }
    }
}
