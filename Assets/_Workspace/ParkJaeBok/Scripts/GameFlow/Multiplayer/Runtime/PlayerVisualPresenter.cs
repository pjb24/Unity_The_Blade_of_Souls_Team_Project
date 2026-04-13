using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 소유권/호스트 조건과 룰 해석 결과를 기반으로 플레이어 외형(Y/Z)을 실제 렌더러/애니메이터에 적용하는 컴포넌트입니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class PlayerVisualPresenter : NetworkBehaviour
{
    [Header("Dependencies")]
    [Tooltip("외형 타입별 표현 데이터를 제공하는 ScriptableObject입니다.")]
    [SerializeField] private PlayerVisualSet _playerVisualSet; // 외형 타입별 컬러/애니메이터 데이터를 보관하는 ScriptableObject 참조입니다.

    [Tooltip("Owner/Host 조건을 외형 타입으로 해석하는 룰 리졸버입니다.")]
    [SerializeField] private PlayerVisualRuleResolver _ruleResolver; // 현재 플레이어 컨텍스트를 Y/Z 외형으로 해석할 리졸버 참조입니다.

    [Tooltip("외형 컬러를 적용할 SpriteRenderer 목록입니다. 비어 있으면 자동 탐색합니다.")]
    [SerializeField] private SpriteRenderer[] _targetSpriteRenderers; // 외형 타입 컬러를 반영할 대상 SpriteRenderer 배열입니다.

    [Tooltip("외형 Animator Controller를 적용할 Animator 목록입니다. 비어 있으면 자동 탐색합니다.")]
    [SerializeField] private Animator[] _targetAnimators; // 외형 타입에 맞는 Animator Controller를 반영할 대상 Animator 배열입니다.

    [Header("Debug")]
    [Tooltip("디버그용: 마지막으로 적용된 외형 타입입니다.")]
    [SerializeField] private E_PlayerVisualVariant _lastAppliedVariant = E_PlayerVisualVariant.Y; // 마지막 외형 적용 결과를 Inspector에서 확인하기 위한 디버그 값입니다.

    [Tooltip("Awake 시점에 대상 Renderer/Animator 자동 탐색을 수행할지 여부입니다.")]
    [SerializeField] private bool _autoResolveTargetsOnAwake = true; // 렌더러/애니메이터 대상 자동 탐색 활성화 여부를 제어하는 플래그입니다.

    /// <summary>
    /// 초기화 시 의존성/대상 참조 누락을 자동 보정합니다.
    /// </summary>
    private void Awake()
    {
        if (_ruleResolver == null)
        {
            _ruleResolver = GetComponent<PlayerVisualRuleResolver>();
        }

        if (!_autoResolveTargetsOnAwake)
        {
            return;
        }

        ResolveTargetsIfNeeded();
    }

    /// <summary>
    /// 네트워크 스폰 완료 시 컨텍스트 기반 외형을 즉시 적용합니다.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        ApplyResolvedVisual();
    }

    /// <summary>
    /// 외부 호출 시 현재 컨텍스트를 다시 평가해 외형을 재적용합니다.
    /// </summary>
    public void RefreshVisual()
    {
        ApplyResolvedVisual();
    }

    /// <summary>
    /// 현재 Owner/Host 컨텍스트에 맞는 외형 타입을 계산하고 렌더러/애니메이터에 반영합니다.
    /// </summary>
    private void ApplyResolvedVisual()
    {
        if (_playerVisualSet == null)
        {
            Debug.LogWarning("[PlayerVisualPresenter] PlayerVisualSet이 비어 있어 외형 적용을 건너뜁니다.", this);
            return;
        }

        if (_ruleResolver == null)
        {
            Debug.LogWarning("[PlayerVisualPresenter] PlayerVisualRuleResolver가 비어 있어 외형 적용을 건너뜁니다.", this);
            return;
        }

        ResolveTargetsIfNeeded();

        E_PlayerVisualVariant resolvedVariant = _ruleResolver.ResolveVariant(IsOwner, IsHost);
        ApplyVariantToRenderers(resolvedVariant);
        ApplyVariantToAnimators(resolvedVariant);
        _lastAppliedVariant = resolvedVariant;
    }

    /// <summary>
    /// 대상 Renderer/Animator 배열이 비어 있으면 현재 오브젝트 하위에서 자동 탐색합니다.
    /// </summary>
    private void ResolveTargetsIfNeeded()
    {
        if (_targetSpriteRenderers == null || _targetSpriteRenderers.Length == 0)
        {
            _targetSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        if (_targetAnimators == null || _targetAnimators.Length == 0)
        {
            _targetAnimators = GetComponentsInChildren<Animator>(true);
        }
    }

    /// <summary>
    /// 계산된 외형 타입에 맞는 색상을 SpriteRenderer 대상에 적용합니다.
    /// </summary>
    private void ApplyVariantToRenderers(E_PlayerVisualVariant visualVariant)
    {
        Color targetColor = _playerVisualSet.GetColor(visualVariant); // 현재 외형 타입에 대응하는 적용 컬러 값입니다.

        for (int index = 0; index < _targetSpriteRenderers.Length; index++)
        {
            SpriteRenderer targetRenderer = _targetSpriteRenderers[index]; // 현재 외형 컬러를 적용할 대상 SpriteRenderer 참조입니다.
            if (targetRenderer == null)
            {
                continue;
            }

            targetRenderer.color = targetColor;
        }
    }

    /// <summary>
    /// 계산된 외형 타입에 맞는 Animator Controller를 Animator 대상에 적용합니다.
    /// </summary>
    private void ApplyVariantToAnimators(E_PlayerVisualVariant visualVariant)
    {
        RuntimeAnimatorController targetController = _playerVisualSet.GetAnimatorController(visualVariant); // 현재 외형 타입에 대응하는 Animator Controller 참조입니다.
        if (targetController == null)
        {
            return;
        }

        for (int index = 0; index < _targetAnimators.Length; index++)
        {
            Animator targetAnimator = _targetAnimators[index]; // 현재 외형 컨트롤러를 적용할 대상 Animator 참조입니다.
            if (targetAnimator == null)
            {
                continue;
            }

            targetAnimator.runtimeAnimatorController = targetController;
        }
    }
}
