using System;
using UnityEngine;

/// <summary>
/// AI 의도 신호를 Animator/ActionController에 전달하고 애니메이션 이벤트를 AI로 전달하는 브리지입니다.
/// </summary>
[DisallowMultipleComponent]
public class EnemyAnimationBridge : MonoBehaviour
{
    [Header("References")]
    [Tooltip("AI 상태/의도를 반영할 Animator 참조입니다.")]
    [SerializeField] private Animator _animator; // 애니메이션 파라미터를 적용할 Animator 참조입니다.
    [Tooltip("공격 액션 요청에 사용할 ActionController 참조입니다. 비어 있으면 Animator Trigger만 사용합니다.")]
    [SerializeField] private ActionController _actionController; // 공격 액션 실행에 사용할 ActionController 참조입니다.

    [Header("Animator Parameters")]
    [Tooltip("현재 상태명을 전달할 Animator 파라미터 이름입니다.")]
    [SerializeField] private string _stateNameParameter = "AIState"; // 상태명 전달용 파라미터 이름입니다.
    [Tooltip("이동 여부를 전달할 Animator Bool 파라미터 이름입니다.")]
    [SerializeField] private string _isMovingParameter = "IsMoving"; // 이동 여부 전달용 파라미터 이름입니다.
    [Tooltip("공격 트리거 파라미터 이름입니다.")]
    [SerializeField] private string _attackTriggerParameter = "Attack"; // 공격 트리거 파라미터 이름입니다.
    [Tooltip("피격 트리거 파라미터 이름입니다.")]
    [SerializeField] private string _hitTriggerParameter = "Hit"; // 피격 트리거 파라미터 이름입니다.
    [Tooltip("사망 트리거 파라미터 이름입니다.")]
    [SerializeField] private string _deathTriggerParameter = "Death"; // 사망 트리거 파라미터 이름입니다.

    [Header("Action Mapping")]
    [Tooltip("ActionController가 있을 때 공격 요청 시 사용할 액션 타입입니다.")]
    [SerializeField] private E_ActionType _attackActionType = E_ActionType.Attack; // 액션 시스템 공격 타입 설정 값입니다.

    /// <summary>
    /// 공격 애니메이션 종료 이벤트입니다.
    /// </summary>
    public event Action AttackAnimationFinished;

    /// <summary>
    /// 피격 애니메이션 종료 이벤트입니다.
    /// </summary>
    public event Action HitReactionFinished;

    /// <summary>
    /// 의존성을 자동 연결합니다.
    /// </summary>
    private void Awake()
    {
        if (_animator == null)
        {
            _animator = GetComponent<Animator>();
        }

        if (_actionController == null)
        {
            _actionController = GetComponent<ActionController>();
        }
    }

    /// <summary>
    /// AI 상태 변경 의도를 Animator에 반영합니다.
    /// </summary>
    public void ApplyStateIntent(EnemyAIStateId stateId, bool isMoving)
    {
        if (_animator == null)
        {
            Debug.LogWarning($"[EnemyAnimationBridge] Missing Animator on {name}. State visual sync skipped.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(_stateNameParameter))
        {
            _animator.SetInteger(_stateNameParameter, (int)stateId);
        }

        if (!string.IsNullOrWhiteSpace(_isMovingParameter))
        {
            _animator.SetBool(_isMovingParameter, isMoving);
        }
    }

    /// <summary>
    /// 공격 의도를 실행 계층에 전달합니다.
    /// </summary>
    public void TriggerAttackIntent()
    {
        bool actionRequested = false;

        if (_actionController != null)
        {
            actionRequested = _actionController.RequestAction(_attackActionType);
            if (!actionRequested)
            {
                Debug.LogWarning($"[EnemyAnimationBridge] ActionController attack request failed on {name}. action={_attackActionType}");
            }
        }

        if (_animator != null && !string.IsNullOrWhiteSpace(_attackTriggerParameter))
        {
            _animator.SetTrigger(_attackTriggerParameter);
        }

        if (_actionController == null && _animator == null)
        {
            Debug.LogWarning($"[EnemyAnimationBridge] No ActionController/Animator found on {name}. Attack intent cannot be visualized.");
        }
    }

    /// <summary>
    /// 피격 의도를 Animator에 전달합니다.
    /// </summary>
    public void TriggerHitIntent()
    {
        if (_animator == null)
        {
            Debug.LogWarning($"[EnemyAnimationBridge] Missing Animator on {name}. Hit trigger skipped.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(_hitTriggerParameter))
        {
            _animator.SetTrigger(_hitTriggerParameter);
        }
    }

    /// <summary>
    /// 사망 의도를 Animator에 전달합니다.
    /// </summary>
    public void TriggerDeathIntent()
    {
        if (_animator == null)
        {
            Debug.LogWarning($"[EnemyAnimationBridge] Missing Animator on {name}. Death trigger skipped.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(_deathTriggerParameter))
        {
            _animator.SetTrigger(_deathTriggerParameter);
        }
    }

    /// <summary>
    /// 애니메이션 이벤트에서 공격 종료 시점 신호를 전달합니다.
    /// </summary>
    public void AnimationEvent_AttackFinished()
    {
        AttackAnimationFinished?.Invoke();
    }

    /// <summary>
    /// 애니메이션 이벤트에서 피격 종료 시점 신호를 전달합니다.
    /// </summary>
    public void AnimationEvent_HitReactionFinished()
    {
        HitReactionFinished?.Invoke();
    }
}
