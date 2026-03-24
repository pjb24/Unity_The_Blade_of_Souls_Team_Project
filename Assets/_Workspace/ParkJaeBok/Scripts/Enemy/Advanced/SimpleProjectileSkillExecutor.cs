using UnityEngine;

/// <summary>
/// 원거리 Enemy 전용으로 투사체 발사 스킬 액션 요청을 수행하는 간단한 스킬 실행기입니다.
/// </summary>
public class SimpleProjectileSkillExecutor : MonoBehaviour, IEnemySkillExecutor
{
    [Tooltip("투사체 발사를 표현할 액션 타입입니다.")]
    [SerializeField] private E_ActionType _projectileActionType = E_ActionType.AttackAir; // 투사체 발사를 표현할 액션 타입입니다.

    /// <summary>
    /// 지정 액션 타입으로 스킬 액션 요청을 시도하고 수락 여부를 반환합니다.
    /// </summary>
    public bool TryExecuteSkill(in EnemyBrainContext context, ActionController actionController)
    {
        if (actionController == null)
        {
            return false;
        }

        if (_projectileActionType == E_ActionType.None)
        {
            return false;
        }

        return actionController.RequestAction(_projectileActionType);
    }
}
