using UnityEngine;

/// <summary>
/// 체력 구간 기반으로 BossPatternController 패턴 전환을 수행하는 페이즈 컨트롤러입니다.
/// </summary>
public class BossPhaseController : MonoBehaviour
{
    [Tooltip("현재 보스 체력 정규화 값을 조회할 체력 컴포넌트 참조입니다.")]
    [SerializeField] private HealthComponent _healthComponent; // 현재 보스 체력 정규화 값을 조회할 체력 컴포넌트 참조입니다.
    [Tooltip("페이즈 전환 시 패턴 시작을 위임할 보스 패턴 컨트롤러 참조입니다.")]
    [SerializeField] private BossPatternController _patternController; // 페이즈 전환 시 패턴 시작을 위임할 보스 패턴 컨트롤러 참조입니다.
    [Tooltip("페이즈 임계치/패턴 ID를 제공하는 데이터 자산 참조입니다.")]
    [SerializeField] private BossPhaseData _phaseData; // 페이즈 임계치/패턴 ID를 제공하는 데이터 자산 참조입니다.

    private int _nextPhaseIndex; // 다음으로 검사할 페이즈 인덱스입니다.

    /// <summary>
    /// 초기 참조를 자동 보정합니다.
    /// </summary>
    private void Awake()
    {
        if (_healthComponent == null)
        {
            _healthComponent = GetComponent<HealthComponent>();
        }

        if (_patternController == null)
        {
            _patternController = GetComponent<BossPatternController>();
        }
    }

    /// <summary>
    /// 매 프레임 체력 임계치를 검사해 필요한 페이즈 전환을 수행합니다.
    /// </summary>
    private void Update()
    {
        if (_healthComponent == null || _patternController == null || _phaseData == null)
        {
            return;
        }

        BossPhaseData.PhaseEntry[] phases = _phaseData.Phases; // 이번 프레임 검사할 페이즈 배열 스냅샷입니다.
        if (phases == null || phases.Length == 0)
        {
            return;
        }

        if (_nextPhaseIndex >= phases.Length)
        {
            return;
        }

        float hpNormalized = _healthComponent.GetHealthNormalized(); // 현재 보스 체력 정규화 값입니다.

        while (_nextPhaseIndex < phases.Length)
        {
            BossPhaseData.PhaseEntry entry = phases[_nextPhaseIndex]; // 현재 검사 중인 페이즈 엔트리입니다.
            if (hpNormalized > entry.TriggerHealthNormalized)
            {
                break;
            }

            if (!string.IsNullOrWhiteSpace(entry.PatternId))
            {
                _patternController.StartPattern(entry.PatternId);
            }

            _nextPhaseIndex++;
        }
    }
}
