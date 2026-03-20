using UnityEngine;

/// <summary>
/// 액션 타입과 공격 스펙 자산을 연결하는 직렬화 매핑 데이터입니다.
/// </summary>
[System.Serializable]
public struct AttackActionMap
{
    [SerializeField] private E_ActionType _actionType; // ActionController에서 실행 중인 액션 타입 키입니다.
    [SerializeField] private AttackSpec _attackSpec; // 액션 타입에 대응하는 공격 스펙 자산 참조입니다.

    /// <summary>
    /// 매핑된 액션 타입 키를 반환합니다.
    /// </summary>
    public E_ActionType ActionType => _actionType;

    /// <summary>
    /// 매핑된 공격 스펙 자산 참조를 반환합니다.
    /// </summary>
    public AttackSpec AttackSpec => _attackSpec;
}
