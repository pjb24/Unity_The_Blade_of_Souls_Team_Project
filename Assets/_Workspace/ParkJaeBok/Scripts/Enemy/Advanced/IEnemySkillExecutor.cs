using UnityEngine;

/// <summary>
/// Enemy 공격 상태에서 전용 스킬 실행(투사체/패턴/돌진)을 위임하기 위한 인터페이스입니다.
/// </summary>
public interface IEnemySkillExecutor
{
    /// <summary>
    /// 현재 문맥에서 전용 스킬 실행을 시도하고 성공 여부를 반환합니다.
    /// </summary>
    bool TryExecuteSkill(in EnemyBrainContext context, ActionController actionController);
}
