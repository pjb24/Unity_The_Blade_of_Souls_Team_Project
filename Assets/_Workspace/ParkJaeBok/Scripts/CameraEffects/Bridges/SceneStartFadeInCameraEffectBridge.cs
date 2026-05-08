using System.Collections;
using UnityEngine;

/// <summary>
/// 씬 시작 직후 화면을 검은 오버레이로 먼저 가리고 지정된 FadeIn CameraEffectPreset을 재생하는 브리지입니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class SceneStartFadeInCameraEffectBridge : MonoBehaviour
{
    [Header("Camera Effect")]
    [Tooltip("씬 시작 시 검은 화면에서 자연스럽게 밝아지도록 재생할 FadeIn CameraEffectPreset입니다.")]
    [SerializeField] private CameraEffectPresetBase _fadeInPreset; // 씬 시작 검은 화면을 걷어내는 FadeIn 카메라 이펙트 프리셋입니다.

    [Tooltip("FadeIn 재생 전 화면 전체를 덮을 초기 색상입니다.")]
    [SerializeField] private Color _initialCoverColor = Color.black; // 씬 첫 프레임부터 표시할 화면 덮개 색상입니다.

    [Tooltip("초기 화면 덮개가 다른 CameraEffect 오버레이보다 우선 보이도록 사용할 우선순위입니다.")]
    [SerializeField] private int _initialCoverPriority = int.MaxValue; // FadeIn 시작 전 검은 덮개의 오버레이 우선순위입니다.

    [Tooltip("씬 시작 후 FadeIn 재생을 몇 초 늦출지 설정합니다. 씬 로딩 직후 UI/카메라 바인딩 대기가 필요할 때 사용합니다.")]
    [Min(0f)]
    [SerializeField] private float _fadeInDelaySeconds; // FadeIn 프리셋 재생 전 대기할 시간입니다.

    [Tooltip("FadeIn 프리셋이 비어 있을 때 초기 검은 화면 덮개를 자동으로 제거할지 여부입니다.")]
    [SerializeField] private bool _clearInitialCoverWhenPresetMissing = true; // 프리셋 누락 시 화면이 영구히 가려지는 것을 방지할지 결정합니다.

    private int _overlaySourceId; // 초기 화면 덮개를 등록하고 정리할 때 사용하는 오버레이 식별자입니다.
    private Coroutine _fadeInRoutine; // 지연 재생과 정리를 처리하는 코루틴입니다.

    /// <summary>
    /// 첫 프레임 렌더링 전에 검은 화면 덮개를 등록합니다.
    /// </summary>
    private void Awake()
    {
        _overlaySourceId = GetInstanceID();
        Color coverColor = _initialCoverColor;
        coverColor.a = Mathf.Clamp01(coverColor.a);

        CameraEffectScreenOverlay.SetOverlay(_overlaySourceId, _initialCoverPriority, coverColor);
        CameraEffectScreenOverlay.RetainOverlay(_overlaySourceId);
    }

    /// <summary>
    /// 씬 준비가 시작된 뒤 FadeIn 재생 코루틴을 시작합니다.
    /// </summary>
    private void OnEnable()
    {
        StopFadeInRoutine();
        _fadeInRoutine = StartCoroutine(PlayFadeInRoutine());
    }

    /// <summary>
    /// 비활성화 시 초기 화면 덮개와 진행 중인 코루틴을 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        StopFadeInRoutine();
        CameraEffectScreenOverlay.ClearOverlay(_overlaySourceId);
    }

    /// <summary>
    /// 파괴 시 초기 화면 덮개를 안전하게 정리합니다.
    /// </summary>
    private void OnDestroy()
    {
        StopFadeInRoutine();
        CameraEffectScreenOverlay.ClearOverlay(_overlaySourceId);
    }

    /// <summary>
    /// 설정된 지연 시간 후 FadeIn 프리셋을 재생합니다.
    /// </summary>
    private IEnumerator PlayFadeInRoutine()
    {
        if (_fadeInDelaySeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(_fadeInDelaySeconds);
        }

        if (_fadeInPreset == null)
        {
            if (_clearInitialCoverWhenPresetMissing)
            {
                CameraEffectScreenOverlay.ClearOverlay(_overlaySourceId);
            }

            _fadeInRoutine = null;
            yield break;
        }

        CameraEffectPlaybackUtility.Play(_fadeInPreset, gameObject);
        _fadeInRoutine = null;
    }

    /// <summary>
    /// 실행 중인 FadeIn 코루틴을 중지합니다.
    /// </summary>
    private void StopFadeInRoutine()
    {
        if (_fadeInRoutine == null)
        {
            return;
        }

        StopCoroutine(_fadeInRoutine);
        _fadeInRoutine = null;
    }
}
