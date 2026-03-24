using UnityEngine;

/// <summary>
/// OnEnable/OnDisable 이벤트로 타겟을 TargetRegistry에 등록/해제하는 브리지 컴포넌트입니다.
/// </summary>
public class TargetRegistryMember : MonoBehaviour
{
    [Tooltip("레지스트리에 등록할 타겟 태그 문자열입니다. 비어 있으면 GameObject.tag를 사용합니다.")]
    [SerializeField] private string _targetTag = string.Empty; // 레지스트리 등록/해제에 사용할 타겟 태그 문자열입니다.

    /// <summary>
    /// 컴포넌트 활성화 시 타겟 Transform을 레지스트리에 등록합니다.
    /// </summary>
    private void OnEnable()
    {
        TargetRegistry.Register(ResolveTag(), transform);
    }

    /// <summary>
    /// 컴포넌트 비활성화 시 타겟 Transform을 레지스트리에서 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        TargetRegistry.Unregister(ResolveTag(), transform);
    }

    /// <summary>
    /// 등록/해제에 사용할 최종 태그 문자열을 계산합니다.
    /// </summary>
    private string ResolveTag()
    {
        if (!string.IsNullOrWhiteSpace(_targetTag))
        {
            return _targetTag;
        }

        return gameObject.tag;
    }
}
