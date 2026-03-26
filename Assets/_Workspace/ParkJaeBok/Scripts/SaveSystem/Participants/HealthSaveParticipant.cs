using UnityEngine;

/// <summary>
/// HealthComponent 상태를 저장/복원하고 사망 시 Recovery 스냅샷을 기록하는 participant입니다.
/// </summary>
public class HealthSaveParticipant : MonoBehaviour, ISaveParticipant, IHealthListener
{
    [Tooltip("저장 레코드에서 Health participant를 식별하는 고유 ID입니다.")]
    [SerializeField] private string _participantId = "core.health"; // Health participant 식별자입니다.

    [Tooltip("저장/복원을 수행할 대상 HealthComponent 참조입니다.")]
    [SerializeField] private HealthComponent _healthComponent; // 저장 대상으로 사용할 HealthComponent 참조입니다.

    [Tooltip("Recovery 복원 시 RecoveryPolicy의 체력 복원 플래그를 따를지 여부입니다.")]
    [SerializeField] private bool _respectRecoveryPolicy = true; // 복구 채널 복원 시 정책 적용 여부입니다.

    [Tooltip("사망 시 Recovery 채널 스냅샷 저장을 자동 실행할지 여부입니다.")]
    [SerializeField] private bool _autoSnapshotOnDeath = true; // 사망 이벤트 시 복구 스냅샷 자동 저장 여부입니다.

    [System.Serializable]
    private class HealthPayload
    {
        public float CurrentHealth; // 저장 시점 현재 체력 값입니다.
        public float MaxHealth; // 저장 시점 최대 체력 값입니다.
        public bool IsDead; // 저장 시점 사망 상태입니다.
    }

    public string ParticipantId => _participantId;
    public int PayloadVersion => 1;

    /// <summary>
    /// HealthComponent 참조를 보정하고 리스너를 등록합니다.
    /// </summary>
    private void Awake()
    {
        if (_healthComponent == null)
        {
            _healthComponent = GetComponent<HealthComponent>();
        }
    }

    /// <summary>
    /// 활성화 시 HealthComponent 리스너를 등록합니다.
    /// </summary>
    private void OnEnable()
    {
        if (_healthComponent != null)
        {
            _healthComponent.AddListener(this);
        }
    }

    /// <summary>
    /// 비활성화 시 HealthComponent 리스너를 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        if (_healthComponent != null)
        {
            _healthComponent.RemoveListener(this);
        }
    }

    /// <summary>
    /// 현재 저장 문맥에서 저장 가능 여부를 반환합니다.
    /// </summary>
    public bool CanSave(in SaveContext context)
    {
        return _healthComponent != null && _healthComponent.IsInitialized;
    }

    /// <summary>
    /// HealthComponent 상태를 JSON 문자열로 직렬화합니다.
    /// </summary>
    public string CaptureAsJson(in SaveContext context)
    {
        HealthPayload payload = new HealthPayload
        {
            CurrentHealth = _healthComponent.GetCurrentHealth(),
            MaxHealth = _healthComponent.GetMaxHealth(),
            IsDead = _healthComponent.IsDead
        };

        return JsonUtility.ToJson(payload);
    }

    /// <summary>
    /// JSON 문자열에서 HealthComponent 상태를 복원합니다.
    /// </summary>
    public void RestoreFromJson(string payloadJson, in SaveContext context)
    {
        if (_healthComponent == null || string.IsNullOrWhiteSpace(payloadJson))
        {
            return;
        }

        if (context.ChannelType == E_SaveChannelType.Recovery && _respectRecoveryPolicy)
        {
            RecoveryPolicy recoveryPolicy = SaveCoordinator.Instance != null ? SaveCoordinator.Instance.RecoveryPolicy : null;
            if (!RecoveryPolicyRuleHelper.ShouldRestoreHealth(recoveryPolicy, _participantId))
            {
                return;
            }
        }

        HealthPayload payload = JsonUtility.FromJson<HealthPayload>(payloadJson);
        if (payload == null)
        {
            return;
        }

        _healthComponent.SetMaxHealth(Mathf.Max(1f, payload.MaxHealth), false);

        if (payload.IsDead && payload.CurrentHealth <= 0f)
        {
            _healthComponent.SetCurrentHealth(0f);
            return;
        }

        if (_healthComponent.IsDead)
        {
            _healthComponent.Revive(Mathf.Max(1f, payload.CurrentHealth));
            return;
        }

        _healthComponent.SetCurrentHealth(Mathf.Max(0f, payload.CurrentHealth));
    }

    /// <summary>
    /// 체력 변경 이벤트를 수신합니다.
    /// </summary>
    public void OnHealthChanged(HealthChangeData data)
    {
    }

    /// <summary>
    /// 데미지 적용 이벤트를 수신합니다.
    /// </summary>
    public void OnDamaged(DamageResult result)
    {
    }

    /// <summary>
    /// 회복 적용 이벤트를 수신합니다.
    /// </summary>
    public void OnHealed(HealResult result)
    {
    }

    /// <summary>
    /// 사망 이벤트 수신 시 Recovery 스냅샷 저장을 시도합니다.
    /// </summary>
    public void OnDied()
    {
        if (!_autoSnapshotOnDeath || SaveCoordinator.Instance == null)
        {
            return;
        }

        RecoveryPolicy recoveryPolicy = SaveCoordinator.Instance.RecoveryPolicy;
        if (recoveryPolicy != null && !recoveryPolicy.SnapshotOnDeath)
        {
            return;
        }

        SaveCoordinator.Instance.SaveChannel(E_SaveChannelType.Recovery, E_SaveTriggerType.Death, gameObject.scene.name);
    }

    /// <summary>
    /// 부활 이벤트를 수신합니다.
    /// </summary>
    public void OnRevived()
    {
    }

    /// <summary>
    /// 최대 체력 변경 이벤트를 수신합니다.
    /// </summary>
    public void OnMaxHealthChanged(float previousMaxHealth, float currentMaxHealth)
    {
    }
}
