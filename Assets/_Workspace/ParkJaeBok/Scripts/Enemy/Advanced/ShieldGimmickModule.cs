using UnityEngine;

/// <summary>
/// 체력 임계치 이상에서 무적 상태를 유지하는 보호막 기믹 모듈입니다.
/// </summary>
public class ShieldGimmickModule : MonoBehaviour, IEnemyGimmickModule
{
    [Tooltip("보호막 유지 여부 판단에 사용할 체력 컴포넌트 참조입니다.")]
    [SerializeField] private HealthComponent _healthComponent; // 보호막 유지 여부 판단에 사용할 체력 컴포넌트 참조입니다.
    [Tooltip("무적 상태를 적용할 피격 리시버 참조입니다.")]
    [SerializeField] private HitReceiver _hitReceiver; // 무적 상태를 적용할 피격 리시버 참조입니다.
    [Tooltip("정규화 체력이 이 값 이하로 내려가면 보호막을 해제하는 임계치입니다.")]
    [SerializeField, Range(0f, 1f)] private float _shieldBreakThreshold = 0.7f; // 정규화 체력이 이 값 이하로 내려가면 보호막을 해제하는 임계치입니다.

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

        float hpNormalized = _healthComponent.GetHealthNormalized(); // 보호막 판정에 사용할 현재 체력 정규화 값입니다.
        bool shouldBeInvincible = hpNormalized >= Mathf.Clamp01(_shieldBreakThreshold); // 이번 프레임 보호막 무적 활성화 여부입니다.
        _hitReceiver.SetInvincible(shouldBeInvincible);
    }
}
