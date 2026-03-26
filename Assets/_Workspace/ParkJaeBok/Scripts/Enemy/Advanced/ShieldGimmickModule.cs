using UnityEngine;

/// <summary>
/// 체력 임계치 이상에서 무적 상태를 유지하는 보호막 기믹 모듈입니다.
/// </summary>
public class ShieldGimmickModule : MonoBehaviour, IEnemyGimmickModule, IGimmickStateProvider
{
    [Tooltip("보호막 기믹 상태를 저장/복원할 때 사용할 고유 ID입니다.")]
    [SerializeField] private string _gimmickId = "gimmick.shield"; // 보호막 기믹 저장/복원 식별자입니다.

    [Tooltip("보호막 유지 여부 판단에 사용할 체력 컴포넌트 참조입니다.")]
    [SerializeField] private HealthComponent _healthComponent; // 보호막 유지 여부 판단에 사용할 체력 컴포넌트 참조입니다.
    [Tooltip("무적 상태를 적용할 피격 리시버 참조입니다.")]
    [SerializeField] private HitReceiver _hitReceiver; // 무적 상태를 적용할 피격 리시버 참조입니다.
    [Tooltip("정규화 체력이 이 값 이하로 내려가면 보호막을 해제하는 임계치입니다.")]
    [SerializeField, Range(0f, 1f)] private float _shieldBreakThreshold = 0.7f; // 정규화 체력이 이 값 이하로 내려가면 보호막을 해제하는 임계치입니다.

    [Tooltip("복구 복원으로 강제 파괴된 보호막 상태를 유지할지 여부입니다.")]
    [SerializeField] private bool _forceShieldBroken; // 복원 시 강제 보호막 파괴 상태 유지 여부입니다.

    [System.Serializable]
    private class ShieldGimmickStatePayload
    {
        public bool ForceShieldBroken; // 복원 시 강제 보호막 파괴 상태 여부입니다.
    }

    /// <summary>
    /// 기믹 고유 식별자를 반환합니다.
    /// </summary>
    public string GimmickId => _gimmickId;

    /// <summary>
    /// 초기 참조를 자동 보정합니다.
    /// </summary>
    private void Awake()
    {
        if (_healthComponent == null)
        {
            _healthComponent = GetComponent<HealthComponent>();
        }

        if (_hitReceiver == null)
        {
            _hitReceiver = GetComponent<HitReceiver>();
        }
    }

    /// <summary>
    /// 매 프레임 보호막 유지 여부를 계산해 HitReceiver 무적 상태를 동기화합니다.
    /// </summary>
    public void OnBrainTick(in EnemyBrainContext context)
    {
        if (_healthComponent == null || _hitReceiver == null)
        {
            return;
        }

        if (_forceShieldBroken)
        {
            _hitReceiver.SetInvincible(false);
            return;
        }

        float hpNormalized = _healthComponent.GetHealthNormalized(); // 보호막 판정에 사용할 현재 체력 정규화 값입니다.
        bool shouldBeInvincible = hpNormalized >= Mathf.Clamp01(_shieldBreakThreshold); // 이번 프레임 보호막 무적 활성화 여부입니다.
        _hitReceiver.SetInvincible(shouldBeInvincible);
    }

    /// <summary>
    /// 현재 보호막 기믹 상태를 JSON 문자열로 직렬화합니다.
    /// </summary>
    public string CaptureStateJson()
    {
        ShieldGimmickStatePayload payload = new ShieldGimmickStatePayload
        {
            ForceShieldBroken = _forceShieldBroken
        };

        return JsonUtility.ToJson(payload);
    }

    /// <summary>
    /// JSON 문자열에서 보호막 기믹 상태를 복원합니다.
    /// </summary>
    public void RestoreStateJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        ShieldGimmickStatePayload payload = JsonUtility.FromJson<ShieldGimmickStatePayload>(json);
        if (payload == null)
        {
            return;
        }

        _forceShieldBroken = payload.ForceShieldBroken;

        if (_hitReceiver != null && _forceShieldBroken)
        {
            _hitReceiver.SetInvincible(false);
        }
    }
}
