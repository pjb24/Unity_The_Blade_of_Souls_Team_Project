using UnityEngine;

/// <summary>
/// 씬 내 플레이어 배치 지점을 식별하기 위한 엔트리 포인트 마커입니다.
/// </summary>
public class StageEntryPoint : MonoBehaviour
{
    [Tooltip("StageSession의 요청 ID와 매칭할 엔트리 포인트 식별자입니다.")]
    [SerializeField] private string _entryPointId = "Default"; // 스폰 리졸버가 비교할 엔트리 포인트 고유 ID입니다.

    [Tooltip("ID가 비어있거나 매칭 실패 시 fallback로 사용할지 여부입니다.")]
    [SerializeField] private bool _isFallbackPoint = true; // 정상 매칭 실패 시 대체 스폰 지점으로 허용할지 여부입니다.

    /// <summary>
    /// 엔트리 포인트 ID를 반환합니다.
    /// </summary>
    public string EntryPointId => _entryPointId;

    /// <summary>
    /// fallback 포인트 여부를 반환합니다.
    /// </summary>
    public bool IsFallbackPoint => _isFallbackPoint;
}
