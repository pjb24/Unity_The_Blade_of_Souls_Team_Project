using UnityEngine;

/// <summary>
/// Prefab 단위 ObjectPool 생성 정책을 Inspector에서 설정하기 위한 데이터입니다.
/// </summary>
[System.Serializable]
public sealed class PoolPrefabConfig
{
    public string Name;

    [Tooltip("Pool에서 생성하고 재사용할 원본 Prefab입니다.")]
    [SerializeField] private GameObject _prefab; // Pool Key로 사용되는 원본 Prefab입니다.

    [Tooltip("Awake 시점에 미리 생성할 인스턴스 개수입니다.")]
    [Min(0)]
    [SerializeField] private int _initialCount = 0; // 최초 로딩 중 미리 만들어 둘 개수입니다.

    [Tooltip("이 Prefab Pool의 권장 최대 인스턴스 수입니다. 런타임에서 초과 생성이 필요하면 Warning 후 Pool 관리 대상으로 추가합니다. 0 이하는 제한 없음입니다.")]
    [Min(0)]
    [SerializeField] private int _maxCount = 0; // 런타임 초과 생성 Warning 기준이 되는 권장 최대 개수입니다.

    [Tooltip("Pool이 비었을 때 추가 인스턴스 생성을 허용할지 결정합니다.")]
    [SerializeField] private bool _allowExpand = true; // 부족 시 추가 생성 허용 여부입니다.

    [Tooltip("대여 후 일정 시간이 지나면 자동으로 Pool에 반환할지 결정합니다.")]
    [SerializeField] private bool _useAutoReturn = false; // 자동 반환 Coroutine 사용 여부입니다.

    [Tooltip("자동 반환을 사용할 때 대여 후 Pool에 반환되기까지의 시간(초)입니다.")]
    [Min(0.01f)]
    [SerializeField] private float _autoReturnSeconds = 3f; // 자동 반환 지연 시간입니다.

    public GameObject Prefab => _prefab;
    public int InitialCount => _initialCount;
    public int MaxCount => _maxCount;
    public bool AllowExpand => _allowExpand;
    public bool UseAutoReturn => _useAutoReturn;
    public float AutoReturnSeconds => _autoReturnSeconds;
}
