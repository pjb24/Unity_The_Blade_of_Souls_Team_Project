using UnityEngine;

/// <summary>
/// Pool 대여 시 개체에 전달되는 위치, 소유자, 사용자 정의 초기화 데이터입니다.
/// </summary>
public readonly struct PoolSpawnContext
{
    public readonly GameObject Prefab; // 대여 요청에 사용된 원본 Prefab 참조입니다.
    public readonly Vector3 Position; // 대여된 개체가 배치될 월드 위치입니다.
    public readonly Quaternion Rotation; // 대여된 개체가 사용할 월드 회전입니다.
    public readonly Transform Parent; // 활성 사용 중 연결할 부모 Transform입니다.
    public readonly GameObject Owner; // 생성 요청 소유자 또는 공격 주체 참조입니다.
    public readonly object Payload; // 기능별 초기화에 사용할 선택적 데이터입니다.

    /// <summary>
    /// Pool 대여 초기화에 필요한 값을 구성합니다.
    /// </summary>
    public PoolSpawnContext(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent, GameObject owner, object payload)
    {
        Prefab = prefab;
        Position = position;
        Rotation = rotation;
        Parent = parent;
        Owner = owner;
        Payload = payload;
    }
}
