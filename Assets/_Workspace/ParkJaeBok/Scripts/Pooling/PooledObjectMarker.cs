using UnityEngine;

/// <summary>
/// Pool에서 생성한 인스턴스와 원본 Prefab, 반환 상태를 추적하는 내부 마커입니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PooledObjectMarker : MonoBehaviour
{
    public GameObject SourcePrefab { get; private set; } // 이 인스턴스를 생성한 원본 Prefab입니다.
    public bool IsInPool { get; private set; } // 중복 반환 감지를 위한 현재 Pool 보관 상태입니다.

    /// <summary>
    /// Pool 생성 직후 원본 Prefab 참조를 연결합니다.
    /// </summary>
    public void BindSourcePrefab(GameObject sourcePrefab)
    {
        SourcePrefab = sourcePrefab;
        IsInPool = true;
    }

    /// <summary>
    /// 대여 상태로 전환합니다.
    /// </summary>
    public void MarkBorrowed()
    {
        IsInPool = false;
    }

    /// <summary>
    /// 반환 상태로 전환합니다.
    /// </summary>
    public void MarkReturned()
    {
        IsInPool = true;
    }
}
