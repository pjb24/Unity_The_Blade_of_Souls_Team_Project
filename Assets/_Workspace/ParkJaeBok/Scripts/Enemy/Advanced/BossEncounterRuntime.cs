using System;
using UnityEngine;

/// <summary>
/// 보스전 진입/이탈/처치 상태와 체크포인트 정보를 단일 진입점으로 관리하는 런타임입니다.
/// </summary>
public class BossEncounterRuntime : MonoBehaviour
{
    [Tooltip("보스전을 식별하는 고유 encounter ID입니다.")]
    [SerializeField] private string _bossEncounterId = "boss.encounter.default"; // 보스전 저장/복원 규칙 매칭에 사용할 고유 encounter ID입니다.

    [Tooltip("현재 보스전 진입 상태입니다.")]
    [SerializeField] private bool _isEncounterStarted; // 현재 보스전 진입 여부 상태입니다.

    [Tooltip("현재 보스 처치 상태입니다.")]
    [SerializeField] private bool _isDefeated; // 현재 보스 처치 여부 상태입니다.

    [Tooltip("보스전 중 저장된 페이즈 체크포인트 인덱스입니다.")]
    [SerializeField] private int _phaseCheckpointIndex; // 보스전 복원 시 사용할 페이즈 체크포인트 인덱스입니다.

    public string BossEncounterId => _bossEncounterId;
    public bool IsEncounterStarted => _isEncounterStarted;
    public bool IsDefeated => _isDefeated;
    public int PhaseCheckpointIndex => Mathf.Max(0, _phaseCheckpointIndex);

    public event Action OnEncounterStarted;
    public event Action OnEncounterEnded;
    public event Action OnDefeated;

    /// <summary>
    /// 보스전 진입 상태를 시작으로 전환합니다.
    /// </summary>
    public void StartEncounter()
    {
        _isEncounterStarted = true;
        OnEncounterStarted?.Invoke();
    }

    /// <summary>
    /// 보스전 이탈 상태로 전환합니다.
    /// </summary>
    public void EndEncounter()
    {
        _isEncounterStarted = false;
        OnEncounterEnded?.Invoke();
    }

    /// <summary>
    /// 보스 처치 상태를 설정합니다.
    /// </summary>
    public void MarkDefeated(bool defeated)
    {
        _isDefeated = defeated;
        if (_isDefeated)
        {
            OnDefeated?.Invoke();
        }
    }

    /// <summary>
    /// 페이즈 체크포인트 인덱스를 갱신합니다.
    /// </summary>
    public void SetPhaseCheckpoint(int phaseIndex)
    {
        _phaseCheckpointIndex = Mathf.Max(0, phaseIndex);
    }

    /// <summary>
    /// 보스전 런타임 상태를 초기 상태로 리셋합니다.
    /// </summary>
    public void ResetEncounterRuntime()
    {
        _isEncounterStarted = false;
        _isDefeated = false;
        _phaseCheckpointIndex = 0;
    }

    /// <summary>
    /// 보스 아레나 초기화 시점 훅을 발행합니다.
    /// </summary>
    public void ResetArenaState()
    {
        OnEncounterEnded?.Invoke();
    }
}
