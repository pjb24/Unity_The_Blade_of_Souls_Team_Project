using UnityEngine;

/// <summary>
/// Enemy AI 상태 식별자입니다.
/// </summary>
public enum EnemyAIStateId
{
    Idle,
    Chase,
    Attack,
    Return,
    Death,
    HitReaction,
    Patrol
}

/// <summary>
/// Enemy AI 상태 패턴 인터페이스입니다.
/// </summary>
public interface IEnemyAIState
{
    /// <summary>
    /// 상태 식별자를 반환합니다.
    /// </summary>
    EnemyAIStateId StateId { get; }

    /// <summary>
    /// 상태 진입 가능 여부를 판단합니다.
    /// </summary>
    bool CanEnter();

    /// <summary>
    /// 상태 진입 시 1회 호출됩니다.
    /// </summary>
    void Enter();

    /// <summary>
    /// 상태가 유지되는 동안 매 프레임 호출됩니다.
    /// </summary>
    void Tick(float deltaTime);

    /// <summary>
    /// 상태 이탈 시 1회 호출됩니다.
    /// </summary>
    void Exit();
}

/// <summary>
/// Enemy AI 상태 기본 구현입니다.
/// </summary>
public abstract class EnemyAIStateBase : IEnemyAIState
{
    /// <summary>
    /// 상태 공통 컨텍스트입니다.
    /// </summary>
    protected readonly EnemyAIController Controller;

    /// <summary>
    /// 상태 생성자입니다.
    /// </summary>
    protected EnemyAIStateBase(EnemyAIController controller)
    {
        Controller = controller;
    }

    /// <summary>
    /// 상태 식별자를 반환합니다.
    /// </summary>
    public abstract EnemyAIStateId StateId { get; }

    /// <summary>
    /// 상태 진입 가능 여부를 판단합니다.
    /// </summary>
    public virtual bool CanEnter()
    {
        return true;
    }

    /// <summary>
    /// 상태 진입 시 호출됩니다.
    /// </summary>
    public virtual void Enter()
    {
    }

    /// <summary>
    /// 상태 유지 중 호출됩니다.
    /// </summary>
    public virtual void Tick(float deltaTime)
    {
    }

    /// <summary>
    /// 상태 이탈 시 호출됩니다.
    /// </summary>
    public virtual void Exit()
    {
    }
}
