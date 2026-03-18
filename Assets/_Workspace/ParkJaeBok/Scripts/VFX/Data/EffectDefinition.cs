using UnityEngine;

[CreateAssetMenu(menuName = "VFX/Effect Definition", fileName = "EffectDefinition")]
public class EffectDefinition : ScriptableObject
{
    [SerializeField]
    [Tooltip("카탈로그 조회 및 런타임 재생 요청에서 사용하는 고유 이펙트 ID")]
    private E_EffectId _effectId = E_EffectId.None; // 카탈로그에서 조회할 고유 ID

    [SerializeField]
    [Tooltip("풀에서 생성/재사용할 원본 이펙트 프리팹")]
    private GameObject _prefab; // 풀에 생성할 이펙트 프리팹

    [SerializeField]
    [Min(1)]
    [Tooltip("서비스 초기화 후 이 이펙트 풀에서 미리 생성할 인스턴스 수")]
    private int _defaultPoolSize = 4; // 초기 풀 생성 개수

    [SerializeField]
    [Min(1)]
    [Tooltip("풀 확장 허용 상한. 초과 시 FallbackPolicy로 처리")]
    private int _maxPoolSize = 16; // 풀 최대 확장 개수

    [SerializeField]
    [Tooltip("요청에서 모드를 명시하지 않았거나 보정이 필요할 때 사용할 기본 재생 모드")]
    private E_EffectPlayMode _defaultPlayMode = E_EffectPlayMode.OneShot; // 요청 미지정 시 기본 재생 모드

    [SerializeField]
    [Tooltip("요청 오버라이드가 없을 때 자동 반환(풀 복귀) 여부")]
    private bool _autoReturn = true; // 종료 후 자동 반환 기본값

    [SerializeField]
    [Min(0.1f)]
    [Tooltip("자동 반환/안전 정리에 사용할 최대 생존 시간(초)")]
    private float _maxLifetime = 3f; // 안전장치용 최대 생존 시간

    [SerializeField]
    [Tooltip("Spawn/Attach 시작 위치에 공통으로 더해지는 기본 오프셋")]
    private Vector3 _defaultLocalOffset = Vector3.zero; // 기본 위치 보정 오프셋

    [SerializeField]
    [Tooltip("Follow 모드 요청을 허용할지 여부")]
    private bool _allowFollow = true; // Follow 모드 허용 여부

    [SerializeField]
    [Tooltip("Attach 모드 요청을 허용할지 여부")]
    private bool _allowAttach = true; // Attach 모드 허용 여부

    [SerializeField]
    [Tooltip("동일 Owner + EffectId 중복 재생 허용 여부")]
    private bool _allowDuplicatePlay = true; // 동일 Owner+EffectId 중복 허용 여부

    [SerializeField]
    [Tooltip("폴백 재사용(예: 오래된 인스턴스 회수) 시 우선순위 판단에 사용하는 값")]
    private int _priority = 0; // 폴백 재사용 시 우선순위 판단 값

    [SerializeField]
    [Tooltip("풀 부족 시 처리 정책(신규 생성/오래된 인스턴스 재사용/요청 드롭)")]
    private E_EffectFallbackPolicy _fallbackPolicy = E_EffectFallbackPolicy.InstantiateNew; // 풀 부족 시 처리 정책

    public E_EffectId EffectId => _effectId;
    public GameObject Prefab => _prefab;
    public int DefaultPoolSize => _defaultPoolSize;
    public int MaxPoolSize => _maxPoolSize;
    public E_EffectPlayMode DefaultPlayMode => _defaultPlayMode;
    public bool AutoReturn => _autoReturn;
    public float MaxLifetime => _maxLifetime;
    public Vector3 DefaultLocalOffset => _defaultLocalOffset;
    public bool AllowFollow => _allowFollow;
    public bool AllowAttach => _allowAttach;
    public bool AllowDuplicatePlay => _allowDuplicatePlay;
    public int Priority => _priority;
    public E_EffectFallbackPolicy FallbackPolicy => _fallbackPolicy;
}
