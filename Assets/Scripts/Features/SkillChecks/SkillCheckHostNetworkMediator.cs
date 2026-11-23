#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Entities;
using TinCan.Core.Ship;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace TinCan.Features.SkillChecks
{
    /// <summary>
    /// Infrastructure Layer: the skill check exchange on a networked ship part (a station, or a breakable point for its
    /// repair check). It adds itself to <see cref="SkillCheckHostRegistry"/> while spawned. Two RPCs: the server opens a check on one player's peer, and that peer's grade comes
    /// back as a result the server validates (<see cref="SkillCheckUseCase"/>). Replicates only whether a challenge
    /// lights the station (its title and the seconds left), for every peer's HUD.
    /// </summary>
    public class SkillCheckHostNetworkMediator : NetworkBehaviour, ISkillCheckHost
    {
        [Tooltip("The check this part runs.")]
        [SerializeField] private SkillCheckDefinition? _check;

        private readonly NetworkVariable<FixedString64Bytes> _litChallenge = new(default,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _litSeconds = new(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly List<SkillCheckResult> _results = new();
        private (int CheckId, int Level)? _opened;
        private ActorIdentity? _identity;
        private SkillCheckHostRegistry? _hosts;

        [Inject]
        public void Construct(SkillCheckHostRegistry hosts) => _hosts = hosts;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _hosts?.Add(this);
        }

        public override void OnNetworkDespawn()
        {
            _hosts?.Remove(this);
            base.OnNetworkDespawn();
        }

        public Guid Id => (_identity ??= new ActorIdentity(this)).Id;
        public bool IsSimulating => IsSpawned;

        public IAirshipView? Ship => ShipParts.ShipOf(this);
        public SkillCheckDefinition? Definition => _check;

        public string? LitChallenge => IsSpawned && _litChallenge.Value.Length > 0 ? _litChallenge.Value.ToString() : null;
        public int LitSecondsLeft => _litSeconds.Value;

        public void ServerOpen(ulong clientId, int checkId, int level)
        {
            if (!IsServer) return;
            OpenRpc(checkId, (byte)Math.Clamp(level, 0, byte.MaxValue), RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        public bool TryTakeOpened(out int checkId, out int level)
        {
            (checkId, level) = _opened ?? (0, 0);
            if (_opened == null) return false;
            _opened = null;
            return true;
        }

        public void SendGrade(int checkId, SkillCheckGrade grade)
        {
            if (IsSpawned) GradeRpc(checkId, (byte)grade);
        }

        public IReadOnlyList<SkillCheckResult> TakeResults()
        {
            if (_results.Count == 0) return Array.Empty<SkillCheckResult>();
            var taken = _results.ToArray();
            _results.Clear();
            return taken;
        }

        public void ServerShowLit(string? challenge, int secondsLeft)
        {
            if (!IsServer) return;
            var title = new FixedString64Bytes();
            if (challenge != null) title.CopyFromTruncated(challenge);
            _litChallenge.Value = title; // NGO only sends a value that changed
            _litSeconds.Value = challenge != null ? secondsLeft : 0;
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void OpenRpc(int checkId, byte level, RpcParams rpcParams) => _opened = (checkId, level);

        [Rpc(SendTo.Server)]
        private void GradeRpc(int checkId, byte grade, RpcParams rpcParams = default) =>
            _results.Add(new SkillCheckResult(rpcParams.Receive.SenderClientId, checkId, (SkillCheckGrade)Math.Min(grade, (byte)SkillCheckGrade.Perfect)));
    }
}
