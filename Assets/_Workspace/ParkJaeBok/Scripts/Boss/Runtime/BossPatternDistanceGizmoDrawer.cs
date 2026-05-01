using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// BossPatternData에 설정된 패턴별 거리 조건을 Scene View에서 원형 Gizmo로 표시한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossPatternDistanceGizmoDrawer : MonoBehaviour
{
    [Header("Data Source")]
    [Tooltip("거리 정보를 표시할 BossPatternData입니다. 비어 있으면 BossController.PatternData를 사용합니다.")]
    [SerializeField] private BossPatternData _patternData; // 씬에 표시할 패턴 거리 조건 데이터 소스

    [Tooltip("PatternData를 자동으로 가져올 BossController입니다. 비어 있으면 같은 GameObject에서 자동으로 찾습니다.")]
    [SerializeField] private BossController _bossController; // PatternData fallback 조회에 사용할 보스 컨트롤러

    [Tooltip("거리 원을 그릴 중심 Transform입니다. 비어 있으면 이 컴포넌트의 Transform을 사용합니다.")]
    [SerializeField] private Transform _drawOrigin; // 패턴 거리 Gizmo를 그릴 기준 위치

    [Header("Draw Options")]
    [Tooltip("선택된 오브젝트에서만 거리 Gizmo를 표시할지 여부입니다.")]
    [SerializeField] private bool _drawOnlyWhenSelected = true; // Scene View 표시 범위를 선택 상태로 제한할지 여부

    [Tooltip("비활성화된 PatternCommonSettings 항목도 Scene View에 표시할지 여부입니다.")]
    [SerializeField] private bool _drawDisabledPatterns; // 비활성 패턴 설정까지 표시할지 여부

    [Tooltip("최소 거리 원을 Scene View에 표시할지 여부입니다.")]
    [SerializeField] private bool _drawMinimumDistance = true; // MinimumTargetSqrDistance를 반경으로 변환해 표시할지 여부

    [Tooltip("최대 거리 원을 Scene View에 표시할지 여부입니다. MaximumTargetSqrDistance가 0이면 무제한으로 간주해 그리지 않습니다.")]
    [SerializeField] private bool _drawMaximumDistance = true; // MaximumTargetSqrDistance를 반경으로 변환해 표시할지 여부

    [Tooltip("Scene View에 PatternId와 거리 값을 텍스트로 표시할지 여부입니다.")]
    [SerializeField] private bool _drawLabels = true; // 각 거리 원의 패턴 ID와 거리 값을 Scene View 라벨로 표시할지 여부

    [Tooltip("텍스트 라벨이 원과 겹치지 않도록 위쪽으로 띄우는 거리입니다.")]
    [Min(0f)]
    [SerializeField] private float _labelYOffset = 0.35f; // Scene View 라벨의 Y축 추가 오프셋

    [Header("Pattern Colors")]
    [Tooltip("FanProjectile 패턴 거리 표시 색상입니다.")]
    [SerializeField] private Color _fanProjectileColor = new Color(1f, 0.45f, 0.1f, 0.85f); // FanProjectile 패턴 거리 원 색상

    [Tooltip("GroundSpike 패턴 거리 표시 색상입니다.")]
    [SerializeField] private Color _groundSpikeColor = new Color(0.9f, 0.15f, 0.15f, 0.85f); // GroundSpike 패턴 거리 원 색상

    [Tooltip("SummonMonster 패턴 거리 표시 색상입니다.")]
    [SerializeField] private Color _summonMonsterColor = new Color(0.2f, 0.75f, 0.25f, 0.85f); // SummonMonster 패턴 거리 원 색상

    [Tooltip("WeakPoint 패턴 거리 표시 색상입니다.")]
    [SerializeField] private Color _weakPointColor = new Color(0.25f, 0.55f, 1f, 0.85f); // WeakPoint 패턴 거리 원 색상

    [Tooltip("알 수 없는 패턴 타입 거리 표시 색상입니다.")]
    [SerializeField] private Color _fallbackColor = new Color(1f, 1f, 1f, 0.65f); // 정의되지 않은 패턴 타입 거리 원 색상

    /// <summary>
    /// 컴포넌트 초기화 시 자동 참조를 보정한다.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
    }

    /// <summary>
    /// Inspector 값 변경 시 자동 참조와 음수 값을 보정한다.
    /// </summary>
    private void OnValidate()
    {
        if (_labelYOffset < 0f)
        {
            Debug.LogWarning($"[BossPatternDistanceGizmoDrawer] LabelYOffset이 0보다 작아 0으로 보정됩니다. object={name}, value={_labelYOffset}", this);
            _labelYOffset = 0f;
        }

        ResolveReferences();
    }

    /// <summary>
    /// 선택 여부와 상관없이 표시하도록 설정된 경우 패턴 거리 Gizmo를 그린다.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (_drawOnlyWhenSelected)
        {
            return;
        }

        DrawPatternDistanceGizmos();
    }

    /// <summary>
    /// 선택된 오브젝트에서만 표시하도록 설정된 경우 패턴 거리 Gizmo를 그린다.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!_drawOnlyWhenSelected)
        {
            return;
        }

        DrawPatternDistanceGizmos();
    }

    /// <summary>
    /// 현재 설정된 BossPatternData의 모든 패턴 거리 조건을 Scene View에 표시한다.
    /// </summary>
    private void DrawPatternDistanceGizmos()
    {
        BossPatternData patternData = ResolvePatternData();
        if (patternData == null || patternData.CommonSettings == null)
        {
            return;
        }

        Transform originTransform = _drawOrigin != null ? _drawOrigin : transform; // 거리 원 중심으로 사용할 Transform
        Vector3 origin = originTransform.position; // Scene View에 그릴 원 중심 좌표
        PatternCommonSettings[] commonSettings = patternData.CommonSettings; // 거리 조건을 포함한 패턴 공통 설정 목록

        for (int index = 0; index < commonSettings.Length; index++)
        {
            PatternCommonSettings settings = commonSettings[index]; // 현재 표시할 패턴 공통 설정
            if (!settings.Enabled && !_drawDisabledPatterns)
            {
                continue;
            }

            DrawPatternDistance(origin, settings, index);
        }
    }

    /// <summary>
    /// 단일 PatternCommonSettings 항목의 최소/최대 거리 원을 표시한다.
    /// </summary>
    private void DrawPatternDistance(Vector3 origin, PatternCommonSettings settings, int index)
    {
        Color patternColor = ResolvePatternColor(settings.PatternType);

        if (_drawMinimumDistance && settings.MinimumTargetSqrDistance > 0f)
        {
            float minimumRadius = Mathf.Sqrt(settings.MinimumTargetSqrDistance); // 제곱 최소 거리를 Scene View 반경으로 변환
            DrawDistanceCircle(origin, minimumRadius, patternColor, $"{settings.PatternId} Min {minimumRadius:0.##}", index, -1f);
        }

        if (!_drawMaximumDistance || settings.MaximumTargetSqrDistance <= 0f)
        {
            return;
        }

        float maximumRadius = Mathf.Sqrt(settings.MaximumTargetSqrDistance); // 제곱 최대 거리를 Scene View 반경으로 변환
        DrawDistanceCircle(origin, maximumRadius, patternColor, $"{settings.PatternId} Max {maximumRadius:0.##}", index, 1f);
    }

    /// <summary>
    /// 지정된 반경의 거리 원과 선택적 라벨을 Scene View에 표시한다.
    /// </summary>
    private void DrawDistanceCircle(Vector3 origin, float radius, Color color, string label, int index, float labelSide)
    {
        if (radius <= 0f)
        {
            return;
        }

        Gizmos.color = color;
        Gizmos.DrawWireSphere(origin, radius);

#if UNITY_EDITOR
        if (!_drawLabels)
        {
            return;
        }

        Handles.color = color;
        Vector3 labelPosition = origin + new Vector3(radius * labelSide, _labelYOffset + index * 0.18f, 0f); // 원 테두리 근처에 겹침을 줄여 표시할 라벨 위치
        Handles.Label(labelPosition, label);
#endif
    }

    /// <summary>
    /// 패턴 타입에 맞는 Gizmo 색상을 반환한다.
    /// </summary>
    private Color ResolvePatternColor(E_BossPatternType patternType)
    {
        switch (patternType)
        {
            case E_BossPatternType.FanProjectile:
                return _fanProjectileColor;
            case E_BossPatternType.GroundSpike:
                return _groundSpikeColor;
            case E_BossPatternType.SummonMonster:
                return _summonMonsterColor;
            case E_BossPatternType.WeakPoint:
                return _weakPointColor;
            default:
                return _fallbackColor;
        }
    }

    /// <summary>
    /// 직접 지정된 PatternData 또는 BossController의 PatternData를 반환한다.
    /// </summary>
    private BossPatternData ResolvePatternData()
    {
        if (_patternData != null)
        {
            return _patternData;
        }

        ResolveReferences();
        return _bossController != null ? _bossController.PatternData : null;
    }

    /// <summary>
    /// 같은 GameObject 기준으로 누락된 참조를 자동 보정한다.
    /// </summary>
    private void ResolveReferences()
    {
        if (_bossController == null)
        {
            _bossController = GetComponent<BossController>();
        }

        if (_drawOrigin == null)
        {
            _drawOrigin = transform;
        }
    }
}
