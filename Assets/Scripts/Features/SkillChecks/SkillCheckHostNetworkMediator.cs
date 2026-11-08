#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Entities;
using TinCan.Core.Ship;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.SkillChecks
{
    /// <summary>
    /// Infrastructure Layer: the skill check exchange on a networked ship part (with an EntityNetworkMediator, which
    /// registers it as an actor). Two RPCs: the server opens a check on one player's peer, and that peer's grade comes
    /// back as a result the server validates (<see cref="SkillCheckUseCase"/>). Holds no replicated state.
    /// </summary>
    public class SkillCheckHostNetworkMediator : NetworkBehaviour, ISkillCheckHost
    {
        [Tooltip("The check this part runs.")]
        [SerializeField] private SkillCheckDefinition? _check;

        private readonly List<SkillCheckResult> _results = new();
        private int? _opened;
        private ActorIdentity? _identity;

        public Guid Id => (_identity ??= new ActorIdentity(this)).Id;
        public bool IsSimulating => IsSpawned;

        public IAirshipView? Ship => transform.parent != null ? transform.parent.GetComponentInParent<IAirshipView>() : null;
        public SkillCheckDefinition? Definition => _check;

        public void ServerOpen(ulong clientId, int checkId)
        {
            if (!IsServer) return;
            OpenRpc(checkId, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        public bool TryTakeOpened(out int checkId)
        {
            checkId = _opened ?? 0;
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

        [Rpc(SendTo.SpecifiedInParams)]
        private void OpenRpc(int checkId, RpcParams rpcParams) => _opened = checkId;

        [Rpc(SendTo.Server)]
        private void GradeRpc(int checkId, byte grade, RpcParams rpcParams = default) =>
            _results.Add(new SkillCheckResult(rpcParams.Receive.SenderClientId, checkId, (SkillCheckGrade)Math.Min(grade, (byte)SkillCheckGrade.Perfect)));
    }
}
