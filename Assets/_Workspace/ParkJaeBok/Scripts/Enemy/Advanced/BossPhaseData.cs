using UnityEngine;

/// <summary>
/// 보스 페이즈 전환 임계치와 페이즈별 패턴 ID를 정의하는 ScriptableObject 데이터입니다.
/// </summary>
[CreateAssetMenu(fileName = "BossPhaseData", menuName = "Enemy/Boss Phase Data")]
public class BossPhaseData : ScriptableObject
{
    [System.Serializable]
    public struct PhaseEntry
    {
        [Tooltip("현재 체력 정규화 값이 이 수치 이하가 되면 진입할 페이즈 임계치입니다.")]
        [Range(0f, 1f)] public float TriggerHealthNormalized; // 현재 체력 정규화 값이 이 수치 이하가 되면 진입할 페이즈 임계치입니다.
        [Tooltip("해당 페이즈에서 시작할 패턴 식별 문자열입니다.")]
        public string PatternId; // 해당 페이즈에서 시작할 패턴 식별 문자열입니다.
    }

    [Tooltip("보스 전투 중 순차 전환할 페이즈 목록입니다.")]
    [SerializeField] private PhaseEntry[] _phases = new PhaseEntry[0]; // 보스 전투 중 순차 전환할 페이즈 목록입니다.

    /// <summary>
    /// 현재 설정된 보스 페이즈 목록을 반환합니다.
    /// </summary>
    public PhaseEntry[] Phases => _phases;
}
