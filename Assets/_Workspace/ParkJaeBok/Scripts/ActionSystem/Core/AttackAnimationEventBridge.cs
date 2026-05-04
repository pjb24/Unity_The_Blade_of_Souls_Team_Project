using UnityEngine;

/// <summary>
/// Animator Animation Event를 AttackExecutor로 전달합니다.
/// </summary>
public sealed class AttackAnimationEventBridge : MonoBehaviour
{
    [Header("연결 대상")]
    [Tooltip("Animation Event를 전달할 AttackExecutor입니다. 비워두면 부모 오브젝트 하위에서 자동 탐색합니다.")]
    [SerializeField] private AttackExecutor _attackExecutor;

    private void Awake()
    {
        if (_attackExecutor != null)
        {
            return;
        }

        _attackExecutor = GetComponentInParent<AttackExecutor>();

        if (_attackExecutor == null)
        {
            _attackExecutor = GetComponentInParent<Transform>().GetComponentInChildren<AttackExecutor>();
        }

        if (_attackExecutor == null)
        {
            Debug.LogWarning($"[AttackAnimationEventBridge] AttackExecutor를 찾지 못했습니다. object={name}", this);
        }
    }

    /// <summary>
    /// 공격 애니메이션 시작 프레임 Animation Event에서 호출합니다.
    /// </summary>
    public void BeginAttackWallSlideSuppression()
    {
        if (_attackExecutor == null)
        {
            Debug.LogWarning($"[AttackAnimationEventBridge] AttackExecutor가 없어 공격 시작 이벤트를 전달할 수 없습니다. object={name}", this);
            return;
        }

        _attackExecutor.BeginAttackWallSlideSuppression();
    }

    /// <summary>
    /// 공격 애니메이션 종료 프레임 Animation Event에서 호출합니다.
    /// </summary>
    public void EndAttackWallSlideSuppression()
    {
        if (_attackExecutor == null)
        {
            Debug.LogWarning($"[AttackAnimationEventBridge] AttackExecutor가 없어 공격 종료 이벤트를 전달할 수 없습니다. object={name}", this);
            return;
        }

        _attackExecutor.EndAttackWallSlideSuppression();
    }
}
