using UnityEngine;

/// <summary>
/// Owner/Host 조건과 테스트 룰 타입을 기반으로 플레이어 외형 타입(Y/Z)을 결정하는 해석기 컴포넌트입니다.
/// </summary>
[DisallowMultipleComponent]
public class PlayerVisualRuleResolver : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Owner/Observer 조건별 외형 매핑을 제공하는 규칙 ScriptableObject입니다.")]
    [SerializeField] private PlayerVisualRule _playerVisualRule; // 외형 룰 해석에 사용할 ScriptableObject 참조입니다.

    [Header("Policy")]
    [Tooltip("현재 세션에서 적용할 A/B 테스트 룰 타입입니다.")]
    [SerializeField] private E_PlayerVisualRuleType _ruleType = E_PlayerVisualRuleType.TestA; // 외형 해석 시 사용할 현재 테스트 룰 타입입니다.

    [Tooltip("룰 데이터가 없거나 매칭 실패 시 적용할 기본 외형 타입입니다.")]
    [SerializeField] private E_PlayerVisualVariant _fallbackVariant = E_PlayerVisualVariant.Y; // 룰 해석 실패 시 반환할 기본 외형 타입입니다.

    [Tooltip("룰 해석 실패 시 Warning 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _verboseWarningLogging = true; // 룰 해석 실패에 대한 경고 로그 출력 여부를 제어하는 플래그입니다.

    /// <summary>
    /// 현재 RuleType과 플레이어 컨텍스트(Owner/Host)에 맞는 외형 타입을 계산합니다.
    /// </summary>
    public E_PlayerVisualVariant ResolveVariant(bool isOwner, bool isHost)
    {
        if (_playerVisualRule == null)
        {
            if (_verboseWarningLogging)
            {
                Debug.LogWarning("[PlayerVisualRuleResolver] PlayerVisualRule이 비어 있어 fallback variant를 사용합니다.", this);
            }

            return _fallbackVariant;
        }

        if (_playerVisualRule.TryResolveVariant(_ruleType, isOwner, isHost, out E_PlayerVisualVariant resolvedVariant))
        {
            return resolvedVariant;
        }

        if (_verboseWarningLogging)
        {
            Debug.LogWarning($"[PlayerVisualRuleResolver] ruleType={_ruleType} 엔트리가 없어 fallback variant를 사용합니다.", this);
        }

        return _fallbackVariant;
    }
}
