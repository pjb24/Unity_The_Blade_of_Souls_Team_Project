/// <summary>
/// AttackExecutor가 최종 공격 데미지를 계산할 때 사용할 수정자 제공 인터페이스입니다.
/// </summary>
public interface IAttackDamageModifierProvider
{
    /// <summary>
    /// 입력 데미지를 받아 수정된 데미지를 반환합니다.
    /// </summary>
    float ModifyDamage(float baseDamage, AttackSpec attackSpec, E_ActionType actionType);
}
