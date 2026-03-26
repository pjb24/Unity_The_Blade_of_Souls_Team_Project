using UnityEngine;

/// <summary>
/// Enemy 런타임 상태를 저장/복원하는 participant입니다.
/// </summary>
public class EnemyRuntimeStateSaveParticipant : MonoBehaviour, ISaveParticipant
{
    [Tooltip("저장 레코드에서 EnemyRuntimeState participant를 식별하는 고유 ID입니다.")]
    [SerializeField] private string _participantId = "enemy.runtime_state"; // EnemyRuntimeState participant 식별자입니다.

    [Tooltip("상태 저장/복원을 수행할 EnemyBrain 참조입니다.")]
    [SerializeField] private EnemyBrain _enemyBrain; // Enemy 런타임 상태 저장/복원 대상 브레인 참조입니다.

    [Tooltip("체력 상태 저장/복원을 수행할 HealthComponent 참조입니다.")]
    [SerializeField] private HealthComponent _healthComponent; // Enemy 체력 상태 저장/복원 대상 참조입니다.

    [Tooltip("거리 기반 Save 필터 계산에 사용할 플레이어 Transform 참조입니다.")]
    [SerializeField] private Transform _playerTransform; // Enemy 저장 대상 필터링에 사용할 플레이어 참조입니다.

    [Tooltip("Recovery 복원 시 EnemyResetRuleSet 규칙을 적용할지 여부입니다.")]
    [SerializeField] private bool _respectRecoveryPolicy = true; // 복구 채널에서 Enemy 규칙 적용 여부입니다.

    [System.Serializable]
    private class EnemyRuntimePayload
    {
        public string EnemyRuntimeId; // 저장 시점 Enemy 런타임 식별자입니다.
        public string ArchetypeId; // 저장 시점 Enemy 아키타입 식별자입니다.
        public bool IsDead; // 저장 시점 Enemy 사망 여부입니다.
        public float HpNormalized; // 저장 시점 Enemy 체력 정규화 값입니다.
        public Vector3 Position; // 저장 시점 Enemy 월드 좌표입니다.
        public E_EnemyLocomotionType LocomotionType; // 저장 시점 Enemy 로코모션 타입입니다.
        public bool HasTarget; // 저장 시점 Enemy 타겟 보유 여부입니다.
    }

    public string ParticipantId => BuildParticipantId();
    public int PayloadVersion => 1;

    /// <summary>
    /// 참조가 비어 있을 경우 동일 오브젝트 기준으로 자동 보정합니다.
    /// </summary>
    private void Awake()
    {
        if (_enemyBrain == null)
        {
            _enemyBrain = GetComponent<EnemyBrain>();
        }

        if (_healthComponent == null)
        {
            _healthComponent = GetComponent<HealthComponent>();
        }
    }

    /// <summary>
    /// 현재 저장 문맥에서 저장 가능 여부를 반환합니다.
    /// </summary>
    public bool CanSave(in SaveContext context)
    {
        if (_enemyBrain == null || _healthComponent == null || !_healthComponent.IsInitialized)
        {
            return false;
        }

        if (context.ChannelType != E_SaveChannelType.Recovery)
        {
            return true;
        }

        EnemyResetRuleSet enemyRuleSet = GetEnemyResetRuleSet(); // Recovery 저장 대상 필터링에 사용할 Enemy 규칙 참조입니다.
        if (enemyRuleSet == null)
        {
            return true;
        }

        return enemyRuleSet.ShouldIncludeForSave(transform, _playerTransform);
    }

    /// <summary>
    /// Enemy 런타임 상태를 JSON 문자열로 직렬화합니다.
    /// </summary>
    public string CaptureAsJson(in SaveContext context)
    {
        EnemyRuntimePayload payload = new EnemyRuntimePayload
        {
            EnemyRuntimeId = _enemyBrain.EnemyRuntimeId,
            ArchetypeId = _enemyBrain.ArchetypeId,
            IsDead = _healthComponent.IsDead,
            HpNormalized = _healthComponent.GetHealthNormalized(),
            Position = transform.position,
            LocomotionType = _enemyBrain.CurrentLocomotionType,
            HasTarget = _enemyBrain.HasTarget
        };

        return JsonUtility.ToJson(payload);
    }

    /// <summary>
    /// JSON 문자열에서 Enemy 런타임 상태를 복원합니다.
    /// </summary>
    public void RestoreFromJson(string payloadJson, in SaveContext context)
    {
        if (string.IsNullOrWhiteSpace(payloadJson) || _enemyBrain == null || _healthComponent == null)
        {
            return;
        }

        EnemyRuntimePayload payload = JsonUtility.FromJson<EnemyRuntimePayload>(payloadJson);
        if (payload == null)
        {
            return;
        }

        EnemyResetRuleSet.ResolvedRule resolvedRule = ResolveRule(payload.EnemyRuntimeId, payload.ArchetypeId); // 현재 Enemy에 적용할 복원 규칙 결과입니다.
        Vector3 restorePosition = ResolveRestorePosition(resolvedRule.RestorePositionMode, payload.Position); // 복원 규칙에 따라 계산한 최종 위치 좌표입니다.

        if (resolvedRule.ResetOnRecovery)
        {
            _enemyBrain.ResetToSpawnState(resolvedRule.RestorePositionMode, payload.Position, resolvedRule.RespawnIfDead, resolvedRule.RestoreHpPercent);
            return;
        }

        float hpPercent = Mathf.Clamp01(payload.HpNormalized * resolvedRule.RestoreHpPercent); // 저장 체력 비율과 규칙 체력 배수를 조합한 최종 체력 비율입니다.
        bool recoveredDeadState = payload.IsDead && !resolvedRule.RespawnIfDead; // 규칙상 부활 비허용일 때 유지할 최종 사망 상태입니다.
        _enemyBrain.ApplyRecoveredState(restorePosition, recoveredDeadState, hpPercent, payload.LocomotionType, payload.HasTarget);
    }

    /// <summary>
    /// Enemy 복원 규칙을 RecoveryPolicy와 RuleSet 기반으로 해석합니다.
    /// </summary>
    private EnemyResetRuleSet.ResolvedRule ResolveRule(string enemyRuntimeId, string archetypeId)
    {
        if (!_respectRecoveryPolicy)
        {
            return EnemyResetRuleSet.ResolvedRule.CreateDefault();
        }

        RecoveryPolicy recoveryPolicy = SaveCoordinator.Instance != null ? SaveCoordinator.Instance.RecoveryPolicy : null;
        return RecoveryPolicyRuleHelper.ResolveEnemyResetRule(recoveryPolicy, enemyRuntimeId, archetypeId);
    }

    /// <summary>
    /// 복원 위치 모드에 따라 최종 복원 위치를 계산합니다.
    /// </summary>
    private Vector3 ResolveRestorePosition(EnemyResetRuleSet.RestorePositionMode restorePositionMode, Vector3 lastKnownPosition)
    {
        if (restorePositionMode == EnemyResetRuleSet.RestorePositionMode.LastKnown)
        {
            return lastKnownPosition;
        }

        if (restorePositionMode == EnemyResetRuleSet.RestorePositionMode.CheckpointArea && StageSession.Instance != null)
        {
            return StageSession.Instance.LastCheckpointWorldPosition;
        }

        return _enemyBrain.SpawnPosition;
    }

    /// <summary>
    /// RecoveryPolicy에 연결된 EnemyResetRuleSet 참조를 반환합니다.
    /// </summary>
    private EnemyResetRuleSet GetEnemyResetRuleSet()
    {
        if (SaveCoordinator.Instance == null || SaveCoordinator.Instance.RecoveryPolicy == null)
        {
            return null;
        }

        return SaveCoordinator.Instance.RecoveryPolicy.EnemyResetRuleSet;
    }

    /// <summary>
    /// Enemy 런타임 ID를 결합해 participant 식별자를 구성합니다.
    /// </summary>
    private string BuildParticipantId()
    {
        if (_enemyBrain == null || string.IsNullOrWhiteSpace(_enemyBrain.EnemyRuntimeId))
        {
            return _participantId;
        }

        return $"{_participantId}.{_enemyBrain.EnemyRuntimeId}";
    }
}
