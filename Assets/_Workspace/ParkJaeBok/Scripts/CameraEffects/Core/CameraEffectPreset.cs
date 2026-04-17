using System;
using UnityEngine;

/// <summary>
/// 디자이너가 복제해서 수치만 수정해 사용할 수 있는 카메라 효과 프리셋 ScriptableObject입니다.
/// </summary>
[CreateAssetMenu(fileName = "CameraEffectPreset", menuName = "Game/Camera Effects/Camera Effect Preset")]
public class CameraEffectPreset : CameraEffectPresetBase
{
    [Serializable]
    private struct CameraEffectModuleSetting
    {
        [Tooltip("모듈을 활성화할지 여부입니다.")]
        public bool Enabled; // 해당 모듈의 런타임 적용 여부를 제어하는 플래그입니다.

        [Tooltip("모듈 강도입니다. 0 이상 값을 사용합니다.")]
        public float Intensity; // 모듈 영향량 계산에 사용할 강도 값입니다.

        [Tooltip("모듈 개별 지속 시간(초)입니다. 0 이하이면 프리셋 Duration을 폴백으로 사용합니다.")]
        public float Duration; // 모듈 전용 재생 시간 기준값입니다.

        [Tooltip("시간(0~1)에 따른 모듈 강도 곡선입니다.")]
        public AnimationCurve Curve; // 모듈 강도 보간에 사용할 커브 데이터입니다.
    }

    [Header("Modules/Fade")]
    [Tooltip("Fade 모듈 설정입니다.")]
    [SerializeField] private CameraEffectModuleSetting _fadeModule = CreateDefaultModule(); // Fade 모듈 동작 파라미터입니다.

    [Header("Modules/Shake")]
    [Tooltip("Shake 모듈 설정입니다.")]
    [SerializeField] private CameraEffectModuleSetting _shakeModule = CreateDefaultModule(); // Shake 모듈 동작 파라미터입니다.

    [Header("Modules/Zoom")]
    [Tooltip("Zoom 모듈 설정입니다.")]
    [SerializeField] private CameraEffectModuleSetting _zoomModule = CreateDefaultModule(); // Zoom 모듈 동작 파라미터입니다.

    [Header("Modules/Overlay")]
    [Tooltip("Overlay 모듈 설정입니다.")]
    [SerializeField] private CameraEffectModuleSetting _overlayModule = CreateDefaultModule(); // Overlay 모듈 동작 파라미터입니다.

    [Tooltip("Overlay 모듈이 적용할 목표 색상입니다.")]
    [SerializeField] private Color _overlayColor = Color.red; // Overlay 모듈이 최종 보간할 색상 값입니다.

    [Header("Designer")]
    [Tooltip("디자이너 작업 메모입니다. 런타임 동작에는 영향을 주지 않습니다.")]
    [TextArea]
    [SerializeField] private string _designerNote; // 프리셋 의도/튜닝 히스토리를 기록하기 위한 디자이너 메모 문자열입니다.

    private Camera _runtimeCamera; // 현재 프리셋이 상태 스냅샷을 보관 중인 대상 카메라 참조입니다.
    private bool _hasRuntimeSnapshot; // 시작 시점 원본 상태 스냅샷 보관 여부입니다.
    private Vector3 _initialLocalPosition; // 효과 종료 시 복원할 카메라 로컬 위치입니다.
    private float _initialOrthographicSize; // 효과 종료 시 복원할 카메라 Orthographic Size 값입니다.
    private float _initialFieldOfView; // 효과 종료 시 복원할 카메라 Field Of View 값입니다.
    private Color _initialBackgroundColor; // 효과 종료 시 복원할 카메라 배경색 값입니다.

    /// <summary>
    /// 현재 프리셋 기반 최소 재생 요청을 생성합니다.
    /// </summary>
    public CameraEffectRequest CreateRequest(GameObject requester = null)
    {
        return CameraEffectRequest.Create(this, requester);
    }

    /// <summary>
    /// 디자이너 메모 텍스트를 조회합니다.
    /// </summary>
    public string DesignerNote => _designerNote;

    /// <summary>
    /// 효과 시작 시 모듈 설정을 검증하고 원본 카메라 상태를 스냅샷합니다.
    /// </summary>
    public override void OnEffectStarted(Camera targetCamera, in CameraEffectRequest request)
    {
        if (targetCamera == null)
        {
            Debug.LogWarning(CameraEffectWarningCatalog.BuildKeyed(
                CameraEffectWarningCatalog.KeyDependencyMissing,
                $"[CameraEffectPreset] targetCamera가 없어 효과를 시작할 수 없습니다. preset={name}"));
            return;
        }

        _runtimeCamera = targetCamera;
        _initialLocalPosition = targetCamera.transform.localPosition;
        _initialOrthographicSize = targetCamera.orthographicSize;
        _initialFieldOfView = targetCamera.fieldOfView;
        _initialBackgroundColor = targetCamera.backgroundColor;
        _hasRuntimeSnapshot = true;

        ValidateModule("Fade", _fadeModule);
        ValidateModule("Shake", _shakeModule);
        ValidateModule("Zoom", _zoomModule);
        ValidateModule("Overlay", _overlayModule);
    }

    /// <summary>
    /// 활성 모듈을 조합해 카메라 상태를 매 프레임 갱신합니다.
    /// </summary>
    public override void OnEffectUpdated(Camera targetCamera, in CameraEffectRequest request, float elapsedSeconds, float normalizedTime)
    {
        if (!_hasRuntimeSnapshot || targetCamera == null || targetCamera != _runtimeCamera)
        {
            return;
        }

        ApplyShakeModule(targetCamera, request, elapsedSeconds);
        ApplyZoomModule(targetCamera, request, elapsedSeconds);
        ApplyColorModules(targetCamera, request, elapsedSeconds);
    }

    /// <summary>
    /// 효과 종료 시 시작 시점에 저장한 카메라 원본 상태를 복원합니다.
    /// </summary>
    public override void OnEffectStopped(Camera targetCamera, in CameraEffectRequest request, string stopReason)
    {
        if (!_hasRuntimeSnapshot || _runtimeCamera == null)
        {
            return;
        }

        if (_shakeModule.Enabled)
        {
            _runtimeCamera.transform.localPosition = _initialLocalPosition;
        }

        if (_zoomModule.Enabled)
        {
            if (_runtimeCamera.orthographic)
            {
                _runtimeCamera.orthographicSize = _initialOrthographicSize;
            }
            else
            {
                _runtimeCamera.fieldOfView = _initialFieldOfView;
            }
        }

        if (_fadeModule.Enabled || _overlayModule.Enabled)
        {
            _runtimeCamera.backgroundColor = _initialBackgroundColor;
        }

        _runtimeCamera = null;
        _hasRuntimeSnapshot = false;
    }

    /// <summary>
    /// Shake 모듈 값으로 카메라 위치 흔들림을 계산해 적용합니다.
    /// </summary>
    private void ApplyShakeModule(Camera targetCamera, in CameraEffectRequest request, float elapsedSeconds)
    {
        if (!_shakeModule.Enabled)
        {
            return;
        }

        float moduleWeight = EvaluateModuleWeight("Shake", _shakeModule, request, elapsedSeconds) * request.ResolveShakeScale();
        Vector3 nextLocalPosition = _initialLocalPosition;

        if (moduleWeight > 0f)
        {
            Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * moduleWeight;
            nextLocalPosition += new Vector3(randomOffset.x, randomOffset.y, 0f);
        }

        targetCamera.transform.localPosition = nextLocalPosition;
    }

    /// <summary>
    /// Zoom 모듈 값으로 카메라 확대/축소를 계산해 적용합니다.
    /// </summary>
    private void ApplyZoomModule(Camera targetCamera, in CameraEffectRequest request, float elapsedSeconds)
    {
        if (!_zoomModule.Enabled)
        {
            return;
        }

        float moduleWeight = EvaluateModuleWeight("Zoom", _zoomModule, request, elapsedSeconds);

        if (targetCamera.orthographic)
        {
            float zoomedSize = Mathf.Max(0.01f, _initialOrthographicSize - moduleWeight);
            targetCamera.orthographicSize = zoomedSize;
            return;
        }

        float zoomedFieldOfView = Mathf.Clamp(_initialFieldOfView - moduleWeight, 1f, 179f);
        targetCamera.fieldOfView = zoomedFieldOfView;
    }

    /// <summary>
    /// Fade/Overlay 모듈 값을 조합해 카메라 배경색을 계산해 적용합니다.
    /// </summary>
    private void ApplyColorModules(Camera targetCamera, in CameraEffectRequest request, float elapsedSeconds)
    {
        if (!_fadeModule.Enabled && !_overlayModule.Enabled)
        {
            return;
        }

        float flashScale = request.ResolveFlashScale(); // 플래시 접근성 옵션을 Fade/Overlay 강도에 반영하는 스케일 값입니다.
        float colorScale = request.ResolveColorScale(); // 색상 변화 접근성 옵션을 최종 색 변화 강도에 반영하는 스케일 값입니다.
        float fadeWeight = EvaluateModuleWeight("Fade", _fadeModule, request, elapsedSeconds) * flashScale;
        float overlayWeight = EvaluateModuleWeight("Overlay", _overlayModule, request, elapsedSeconds) * flashScale;

        Color fadedColor = Color.Lerp(_initialBackgroundColor, Color.black, Mathf.Clamp01(fadeWeight));
        Color finalColor = Color.Lerp(fadedColor, _overlayColor, Mathf.Clamp01(overlayWeight));
        targetCamera.backgroundColor = Color.Lerp(_initialBackgroundColor, finalColor, colorScale);
    }

    /// <summary>
    /// 모듈 설정을 기반으로 현재 프레임 강도 가중치를 계산합니다.
    /// </summary>
    private float EvaluateModuleWeight(string moduleName, CameraEffectModuleSetting module, in CameraEffectRequest request, float elapsedSeconds)
    {
        if (!module.Enabled)
        {
            return 0f;
        }

        float safeIntensity = Mathf.Max(0f, module.Intensity);
        float moduleDuration = module.Duration > 0f ? module.Duration : ResolveDurationSeconds(request);

        if (moduleDuration <= 0f)
        {
            moduleDuration = 0.01f;
        }

        CameraEffectPresetBase.E_CameraEffectLifecycleMode lifecycleMode = ResolveLifecycleMode(request); // 현재 요청 기준 수명주기 모드입니다.
        float normalizedTime = lifecycleMode == CameraEffectPresetBase.E_CameraEffectLifecycleMode.Loop
            ? Mathf.Repeat(elapsedSeconds, moduleDuration) / moduleDuration
            : Mathf.Clamp01(elapsedSeconds / moduleDuration);
        AnimationCurve curve = module.Curve == null ? AnimationCurve.Linear(0f, 0f, 1f, 1f) : module.Curve;
        float curveWeight = Mathf.Clamp01(curve.Evaluate(normalizedTime));
        return safeIntensity * curveWeight;
    }

    /// <summary>
    /// 모듈 데이터 이상치를 검사하고 경고 로그를 출력합니다.
    /// </summary>
    private void ValidateModule(string moduleName, CameraEffectModuleSetting module)
    {
        if (!module.Enabled)
        {
            return;
        }

        if (module.Intensity < 0f)
        {
            Debug.LogWarning(CameraEffectWarningCatalog.BuildKeyed(
                CameraEffectWarningCatalog.KeyInvalidModuleData,
                $"[CameraEffectPreset] Intensity가 음수여서 0으로 폴백됩니다. preset={name}, module={moduleName}, intensity={module.Intensity}"));
        }

        if (module.Duration <= 0f && DurationSeconds <= 0f)
        {
            Debug.LogWarning(CameraEffectWarningCatalog.BuildKeyed(
                CameraEffectWarningCatalog.KeyInvalidModuleData,
                $"[CameraEffectPreset] 모듈 Duration과 프리셋 Duration이 모두 0 이하입니다. preset={name}, module={moduleName}, moduleDuration={module.Duration}, presetDuration={DurationSeconds}"));
        }

        if (module.Curve == null)
        {
            Debug.LogWarning(CameraEffectWarningCatalog.BuildKeyed(
                CameraEffectWarningCatalog.KeyInvalidModuleData,
                $"[CameraEffectPreset] Curve가 비어 있어 Linear(0~1)로 폴백됩니다. preset={name}, module={moduleName}"));
        }
    }

    /// <summary>
    /// 모듈 기본값을 생성합니다.
    /// </summary>
    private static CameraEffectModuleSetting CreateDefaultModule()
    {
        CameraEffectModuleSetting setting = new CameraEffectModuleSetting();
        setting.Enabled = false;
        setting.Intensity = 0f;
        setting.Duration = 0f;
        setting.Curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        return setting;
    }
}
