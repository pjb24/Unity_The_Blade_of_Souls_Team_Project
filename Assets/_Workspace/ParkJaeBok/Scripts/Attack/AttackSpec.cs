using UnityEngine;

/// <summary>
/// 공격 타입별 판정/데미지/태그 규칙을 데이터로 정의하는 ScriptableObject입니다.
/// </summary>
[CreateAssetMenu(fileName = "AttackSpec", menuName = "AttackSystem/Attack Spec")]
public class AttackSpec : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string _attackTypeId = "AttackA"; // 공격 식별 문자열(디버깅/로깅/확장용)입니다.

    [Header("Damage")]
    [SerializeField] private float _baseDamage = 10f; // HitRequest.RawDamage로 전달할 기본 데미지 값입니다.

    [Header("Targeting")]
    [SerializeField] private E_AttackAreaType _areaType = E_AttackAreaType.Circle; // 타겟 탐지에 사용할 판정 도형 타입입니다.
    [Tooltip("공격자 기준 로컬 판정 오프셋입니다. X 양수는 캐릭터가 바라보는 전방, X 음수는 후방이며 Y 값은 좌우 반전에 영향받지 않습니다.")]
    [SerializeField] private Vector2 _localOffset = new Vector2(1f, 0f); // 공격자 바라보는 방향 기준 로컬 판정 오프셋입니다.
    [SerializeField] private float _radius = 1.5f; // Circle 판정 반경 값입니다.
    [SerializeField] private Vector2 _boxSize = new Vector2(2.5f, 1.2f); // Box 판정 크기 값입니다.
    [SerializeField] private LayerMask _targetLayerMask = ~0; // 타겟 탐지 레이어 마스크입니다.
    [SerializeField] private bool _requireTargetTag; // 타겟 태그 필터 강제 여부입니다.
    [SerializeField] private string _targetTag = "Enemy"; // 타겟 태그 필터 문자열입니다.
    [SerializeField] private int _maxTargets = 4; // 1회 공격에서 허용할 최대 타겟 수입니다.

    [Header("Hit Rules")]
    [SerializeField] private string _statusTag = "AttackA"; // HitRequest.StatusTag에 전달할 상태 태그입니다.
    [SerializeField] private bool _allowMultiHitPerSwing; // 동일 실행에서 동일 대상 중복 타격 허용 여부입니다.

    /// <summary>
    /// 공격 식별 문자열을 반환합니다.
    /// </summary>
    public string AttackTypeId => _attackTypeId;

    /// <summary>
    /// 기본 데미지 값을 반환합니다.
    /// </summary>
    public float BaseDamage => _baseDamage;

    /// <summary>
    /// 판정 도형 타입을 반환합니다.
    /// </summary>
    public E_AttackAreaType AreaType => _areaType;

    /// <summary>
    /// 로컬 판정 오프셋을 반환합니다.
    /// </summary>
    public Vector2 LocalOffset => _localOffset;

    /// <summary>
    /// 원형 판정 반경을 반환합니다.
    /// </summary>
    public float Radius => _radius;

    /// <summary>
    /// 박스 판정 크기를 반환합니다.
    /// </summary>
    public Vector2 BoxSize => _boxSize;

    /// <summary>
    /// 타겟 탐지 레이어 마스크를 반환합니다.
    /// </summary>
    public LayerMask TargetLayerMask => _targetLayerMask;

    /// <summary>
    /// 타겟 태그 필터 강제 여부를 반환합니다.
    /// </summary>
    public bool RequireTargetTag => _requireTargetTag;

    /// <summary>
    /// 타겟 태그 필터 문자열을 반환합니다.
    /// </summary>
    public string TargetTag => _targetTag;

    /// <summary>
    /// 최대 타겟 수를 반환합니다.
    /// </summary>
    public int MaxTargets => _maxTargets;

    /// <summary>
    /// 상태 태그 문자열을 반환합니다.
    /// </summary>
    public string StatusTag => _statusTag;

    /// <summary>
    /// 동일 실행에서 동일 대상 중복 타격 허용 여부를 반환합니다.
    /// </summary>
    public bool AllowMultiHitPerSwing => _allowMultiHitPerSwing;

    /// <summary>
    /// 런타임에서 사용할 안전한 기본 데미지 값을 계산합니다.
    /// </summary>
    public float GetSafeBaseDamage()
    {
        return Mathf.Max(0f, _baseDamage);
    }

    /// <summary>
    /// 런타임에서 사용할 안전한 반경 값을 계산합니다.
    /// </summary>
    public float GetSafeRadius()
    {
        return Mathf.Max(0.05f, _radius);
    }

    /// <summary>
    /// 런타임에서 사용할 안전한 박스 크기를 계산합니다.
    /// </summary>
    public Vector2 GetSafeBoxSize()
    {
        return new Vector2(Mathf.Max(0.05f, _boxSize.x), Mathf.Max(0.05f, _boxSize.y));
    }

    /// <summary>
    /// 런타임에서 사용할 안전한 최대 타겟 수를 계산합니다.
    /// </summary>
    public int GetSafeMaxTargets()
    {
        return Mathf.Max(1, _maxTargets);
    }
}

/// <summary>
/// 공격 판정 도형 타입을 정의하는 enum입니다.
/// </summary>
public enum E_AttackAreaType
{
    Circle,
    Box,
}
