#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Entities;
using TinCan.Core.Gas;
using TinCan.Core.Humanoid;
using TinCan.Core.Interaction;
using TinCan.Features.Stations;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Weapons.Cannon
{
    /// <summary>
    /// Infrastructure Layer: a cannon fixture on the ship. It is a station (interact to occupy, IA_OccupyCannon) and a
    /// cannon. Server-written state: the occupant's client id and the barrel angles, so every peer and a late joiner
    /// sees who mans it and where it points. Shots are events, not state: one RPC when fired and one when it ends; each
    /// peer draws the ball from the arc. Parts are found by name: YawPivot > PitchPivot > Muzzle, Seat, AimPreview
    /// (a LineRenderer), and PitchPivot > CameraMount (a disabled Camera the gunner looks through, over the barrel).
    /// </summary>
    public class CannonNetworkMediator : NetworkBehaviour, ICannon, IStation
    {
        private const ulong NoOccupant = ulong.MaxValue;
        private const int MaxQueuedEvents = 32;

        [SerializeField] private InteractionDefinition? _interactionDefinition;
        [Tooltip("Runs on the occupant while they man the cannon (GA_OccupyCannon).")]
        [SerializeField] private AbilityDefinition? _occupyAbility;
        [Tooltip("The occupant's abilities while manning (GA_FireCannon).")]
        [SerializeField] private List<AbilityDefinition> _grantedAbilities = new();

        private readonly NetworkVariable<ulong> _occupantClientId = new(NoOccupant);
        private readonly NetworkVariable<float> _yaw = new();
        private readonly NetworkVariable<float> _elevation = new();
        private readonly Queue<CannonShotEvent> _shotEvents = new();
        private ActorIdentity? _identity;
        private LineRenderer? _aimPreview;

        public Guid Id => (_identity ??= new ActorIdentity(this)).Id;
        public bool IsSimulating => IsSpawned;

        public InteractionDefinition Definition => _interactionDefinition!;
        public AbilityDefinition? OccupyAbility => _occupyAbility;
        public IReadOnlyList<AbilityDefinition> GrantedAbilities => _grantedAbilities;
        public ulong? OccupantClientId => _occupantClientId.Value == NoOccupant ? null : _occupantClientId.Value;

        public Transform? Base => this != null ? transform : null;
        public Transform? YawPivot { get; private set; }
        public Transform? PitchPivot { get; private set; }
        public Transform? Muzzle { get; private set; }
        public Transform? Seat { get; private set; }
        public Camera? ViewCamera { get; private set; }
        public Transform? ShipRoot => this != null ? transform.parent : null; // fixtures are parented to the ship root

        public float Yaw => _yaw.Value;
        public float Elevation => _elevation.Value;

        private void Awake()
        {
            YawPivot = transform.FindDescendant("YawPivot");
            PitchPivot = transform.FindDescendant("PitchPivot");
            Muzzle = transform.FindDescendant("Muzzle");
            Seat = transform.FindDescendant("Seat");
            _aimPreview = transform.FindDescendant("AimPreview")?.GetComponent<LineRenderer>();
            if (_aimPreview != null) _aimPreview.enabled = false;
            ViewCamera = transform.FindDescendant("CameraMount")?.GetComponent<Camera>();
            if (ViewCamera != null) ViewCamera.enabled = false; // switched on only for the local gunner (StationViewPresenter)
            if (YawPivot == null || PitchPivot == null || Muzzle == null)
            {
                Debug.LogError($"[{nameof(CannonNetworkMediator)}] {name} needs children YawPivot > PitchPivot > Muzzle.", this);
            }
        }

        public void ServerSetOccupant(IHumanoidCharacterView? occupant)
        {
            if (!IsServer) return;
            _occupantClientId.Value = occupant is Component component && component != null && component.TryGetComponent(out NetworkObject body)
                ? body.OwnerClientId
                : NoOccupant;
        }

        public void ServerSetAim(float yaw, float elevation)
        {
            if (!IsServer) return;
            // Skip tiny changes so a still barrel sends nothing.
            if (Mathf.Abs(Mathf.DeltaAngle(_yaw.Value, yaw)) > 0.05f) _yaw.Value = yaw;
            if (Mathf.Abs(_elevation.Value - elevation) > 0.05f) _elevation.Value = elevation;
        }

        public void ServerShotFired(int shotId, Vector3 origin, Vector3 velocity)
        {
            if (IsServer) ShotFiredRpc(shotId, origin, velocity);
        }

        public void ServerShotEnded(int shotId, Vector3 point)
        {
            if (IsServer) ShotEndedRpc(shotId, point);
        }

        [Rpc(SendTo.Everyone)]
        private void ShotFiredRpc(int shotId, Vector3 origin, Vector3 velocity) => Enqueue(CannonShotEvent.Fired(shotId, origin, velocity));

        [Rpc(SendTo.Everyone)]
        private void ShotEndedRpc(int shotId, Vector3 point) => Enqueue(CannonShotEvent.Finished(shotId, point));

        private void Enqueue(CannonShotEvent shotEvent)
        {
            // Nothing drains the queue without the presenter (a dedicated server); keep it bounded.
            if (_shotEvents.Count >= MaxQueuedEvents) _shotEvents.Dequeue();
            _shotEvents.Enqueue(shotEvent);
        }

        public bool TryTakeShotEvent(out CannonShotEvent shotEvent) => _shotEvents.TryDequeue(out shotEvent);

        public void ApplyAim(float yaw, float elevation)
        {
            if (YawPivot != null) YawPivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (PitchPivot != null) PitchPivot.localRotation = Quaternion.Euler(-elevation, 0f, 0f);
        }

        public void ShowAimPreview(Vector3[]? points, int count)
        {
            if (_aimPreview == null) return;
            _aimPreview.enabled = points != null && count > 1;
            if (!_aimPreview.enabled) return;

            _aimPreview.positionCount = count;
            _aimPreview.SetPositions(points);
        }
    }
}
