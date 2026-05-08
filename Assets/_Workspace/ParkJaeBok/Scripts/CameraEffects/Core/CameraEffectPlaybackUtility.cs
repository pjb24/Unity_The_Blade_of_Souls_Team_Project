using System.Collections;
using UnityEngine;

/// <summary>
/// CameraEffectPreset 재생과 프리셋 지속 시간 대기를 공통으로 처리하는 유틸리티입니다.
/// </summary>
public static class CameraEffectPlaybackUtility
{
    /// <summary>
    /// 지정한 프리셋을 현재 CameraEffectManager에서 재생하고 핸들을 반환합니다.
    /// </summary>
    public static CameraEffectHandle Play(CameraEffectPresetBase preset, GameObject requester)
    {
        if (preset == null || CameraEffectManager.Instance == null)
        {
            return default;
        }

        return CameraEffectManager.Instance.Play(preset, requester);
    }

    /// <summary>
    /// 지정한 프리셋을 재생한 뒤 Timed 프리셋 지속 시간 또는 fallback 시간만큼 대기합니다.
    /// </summary>
    public static IEnumerator PlayAndWait(CameraEffectPresetBase preset, GameObject requester, float fallbackSeconds)
    {
        Play(preset, requester);

        float waitSeconds = ResolveWaitSeconds(preset, fallbackSeconds);
        if (waitSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(waitSeconds);
        }
    }

    /// <summary>
    /// 프리셋의 수명 주기와 지속 시간 설정을 기준으로 대기 시간을 계산합니다.
    /// </summary>
    public static float ResolveWaitSeconds(CameraEffectPresetBase preset, float fallbackSeconds)
    {
        if (preset == null)
        {
            return Mathf.Max(0f, fallbackSeconds);
        }

        CameraEffectRequest request = CameraEffectRequest.Create(preset);
        CameraEffectPresetBase.E_CameraEffectLifecycleMode lifecycleMode = preset.ResolveLifecycleMode(request);
        if (lifecycleMode != CameraEffectPresetBase.E_CameraEffectLifecycleMode.Timed)
        {
            return Mathf.Max(0f, fallbackSeconds);
        }

        float presetDuration = preset.ResolveDurationSeconds(request);
        return presetDuration > 0f ? presetDuration : Mathf.Max(0f, fallbackSeconds);
    }
}
