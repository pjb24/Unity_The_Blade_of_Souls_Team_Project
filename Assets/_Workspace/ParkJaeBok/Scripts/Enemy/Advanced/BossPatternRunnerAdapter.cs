using UnityEngine;

/// <summary>
/// BossPatternController를 EnemyBrain 오버라이드 체계에 연결하기 위한 어댑터입니다.
/// </summary>
public class BossPatternRunnerAdapter : MonoBehaviour, IEnemyPatternRunner
{
    [Tooltip("활성 상태에서 Brain 기본 흐름을 항상 오버라이드할지 여부입니다.")]
    [SerializeField] private bool _overrideBrainWhileEnabled = true; // 활성 상태에서 Brain 기본 흐름을 항상 오버라이드할지 여부입니다.

    /// <summary>
    /// 패턴 틱을 처리하고 Brain 기본 로직 오버라이드 여부를 반환합니다.
    /// </summary>
    public bool TickAndShouldOverride(in EnemyBrainContext context)
    {
        return _overrideBrainWhileEnabled && isActiveAndEnabled;
    }
}
