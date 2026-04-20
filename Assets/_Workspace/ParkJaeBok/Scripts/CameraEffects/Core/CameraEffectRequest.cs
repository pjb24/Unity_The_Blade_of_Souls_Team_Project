using UnityEngine;

/// <summary>
/// 카메라 효과 재생 요청 파라미터를 캡슐화하는 구조체입니다. 프리셋 복제 기반 데이터 워크플로를 전제로 합니다.
/// </summary>
public struct CameraEffectRequest
{
    public CameraEffectPresetBase Preset; // 재생할 카메라 효과 프리셋 참조입니다.
    public GameObject Requester; // 요청 발신자를 추적하기 위한 선택 참조입니다.
    public bool UseDurationOverride; // 지속 시간 오버라이드 사용 여부입니다.
    public float DurationOverrideSeconds; // 지속 시간 오버라이드 값(초)입니다.
    public bool UseChannelOverride; // 채널 오버라이드 사용 여부입니다.
    public E_CameraEffectChannel Channel; // 채널 오버라이드가 활성일 때 사용할 채널 식별자입니다.
    public bool UsePriorityOverride; // 우선순위 오버라이드 사용 여부입니다.
    public int Priority; // 우선순위 오버라이드가 활성일 때 사용할 우선순위 값입니다.
    public bool UseAccessibilityOverride; // 접근성 스케일 오버라이드 사용 여부입니다.
    public float AccessibilityShakeScale; // 흔들림 강도에 적용할 접근성 배율(0~1)입니다.
    public float AccessibilityFlashScale; // 플래시/페이드 강도에 적용할 접근성 배율(0~1)입니다.
    public float AccessibilityColorScale; // 색상 변화 강도에 적용할 접근성 배율(0~1)입니다.

    /// <summary>
    /// 프리셋만으로 최소 요청을 생성합니다.
    /// </summary>
    public static CameraEffectRequest Create(CameraEffectPresetBase preset, GameObject requester = null)
    {
        CameraEffectRequest request = new CameraEffectRequest();
        request.Preset = preset;
        request.Requester = requester;
        request.UseDurationOverride = false;
        request.DurationOverrideSeconds = 0f;
        request.UseChannelOverride = false;
        request.Channel = E_CameraEffectChannel.System;
        request.UsePriorityOverride = false;
        request.Priority = 0;
        request.UseAccessibilityOverride = false;
        request.AccessibilityShakeScale = 1f;
        request.AccessibilityFlashScale = 1f;
        request.AccessibilityColorScale = 1f;
        return request;
    }

    /// <summary>
    /// 현재 요청에 적용할 흔들림 접근성 배율을 계산합니다.
    /// </summary>
    public float ResolveShakeScale()
    {
        return UseAccessibilityOverride ? Mathf.Clamp01(AccessibilityShakeScale) : 1f;
    }

    /// <summary>
    /// 현재 요청에 적용할 플래시/페이드 접근성 배율을 계산합니다.
    /// </summary>
    public float ResolveFlashScale()
    {
        return UseAccessibilityOverride ? Mathf.Clamp01(AccessibilityFlashScale) : 1f;
    }

    /// <summary>
    /// 현재 요청에 적용할 색상 변화 접근성 배율을 계산합니다.
    /// </summary>
    public float ResolveColorScale()
    {
        return UseAccessibilityOverride ? Mathf.Clamp01(AccessibilityColorScale) : 1f;
    }
}
