using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Phase 10 테스트 환경에서 샘플 카메라 효과를 버튼으로 실행/중지하고 디버그 정보를 표시하는 프리젠터입니다.
/// </summary>
public class CameraEffectTestEnvironmentPresenter : MonoBehaviour
{
    /// <summary>
    /// Phase 9 샘플 프리셋 식별자입니다.
    /// </summary>
    public enum E_CameraEffectSamplePreset
    {
        FadeIn = 0,
        FadeOut = 1,
        HitSmall = 2,
        HitHeavy = 3,
        DashBurst = 4,
        LowHealthWarning = 5
    }

    [Serializable]
    private class SamplePresetEntry
    {
        [Tooltip("테스트 대상 샘플 프리셋 식별자입니다.")]
        public E_CameraEffectSamplePreset PresetType; // 샘플 프리셋을 코드에서 식별하기 위한 enum 값입니다.

        [Tooltip("인스펙터/디버그 패널에 표시할 라벨 문자열입니다.")]
        public string DisplayName = "Sample"; // 디버그 텍스트에 출력할 샘플 프리셋 표시 이름입니다.

        [Tooltip("버튼에서 재생할 CameraEffectPreset 참조입니다.")]
        public CameraEffectPresetBase Preset; // 버튼 클릭 시 재생할 카메라 효과 프리셋 참조입니다.

        [Tooltip("해당 프리셋을 재생할 UI 버튼 참조입니다. 비워두면 자동 바인딩하지 않습니다.")]
        public Button PlayButton; // 샘플 프리셋 재생 요청을 연결할 UI Button 참조입니다.

        [Tooltip("해당 프리셋을 중지할 UI 버튼 참조입니다. 비워두면 자동 바인딩하지 않습니다.")]
        public Button StopButton; // 샘플 프리셋 중지 요청을 연결할 UI Button 참조입니다.

        public CameraEffectHandle RuntimeHandle; // 마지막 재생 요청에서 반환된 런타임 핸들입니다.
        public bool WasPlayedAtLeastOnce; // 디버그 패널에서 재생 이력 표시를 위한 플래그입니다.
        public UnityAction BoundPlayAction; // 자동 바인딩 시 해제를 위해 보관하는 Play 버튼 UnityAction 참조입니다.
        public UnityAction BoundStopAction; // 자동 바인딩 시 해제를 위해 보관하는 Stop 버튼 UnityAction 참조입니다.
    }

    [Header("Dependencies")]
    [Tooltip("테스트 실행 대상 CameraEffectManager입니다. 비어 있으면 CameraEffectManager.Instance를 자동 사용합니다.")]
    [SerializeField] private CameraEffectManager _effectManager; // 샘플 프리셋 재생/중지 요청을 전달할 매니저 참조입니다.

    [Header("Sample Presets")]
    [Tooltip("Phase 9 샘플 프리셋과 버튼 바인딩 정보를 정의한 목록입니다.")]
    [SerializeField] private SamplePresetEntry[] _sampleEntries = Array.Empty<SamplePresetEntry>(); // 샘플 프리셋별 설정/버튼/핸들 상태를 보관하는 배열입니다.

    [Header("Debug UI")]
    [Tooltip("현재 적용 효과 요약 문자열을 출력할 TMP_Text입니다.")]
    [SerializeField] private TMP_Text _activeEffectsText; // 활성 효과 요약을 화면에 표시할 TMP 텍스트 참조입니다.

    [Tooltip("런타임 로그를 콘솔에 출력할지 여부입니다.")]
    [SerializeField] private bool _verboseLogging = true; // 버튼 동작과 오류 상황 로그 출력 여부를 제어하는 플래그입니다.

    [Tooltip("TMP 텍스트가 없어도 OnGUI 오버레이 디버그 패널을 표시할지 여부입니다.")]
    [SerializeField] private bool _showOnGuiOverlay = true; // TMP_Text 미연결 환경에서 디버그 문자열을 표시할지 여부입니다.

    [Tooltip("OnGUI 오버레이 영역(픽셀)입니다.")]
    [SerializeField] private Rect _overlayRect = new Rect(16f, 16f, 640f, 360f); // OnGUI 디버그 정보 출력에 사용할 화면 영역입니다.

    private readonly StringBuilder _textBuilder = new StringBuilder(512); // 디버그 문자열을 GC 최소화로 조합하기 위한 버퍼입니다.
    private string _cachedDebugText = string.Empty; // 최근 프레임에 조합한 디버그 텍스트 캐시입니다.

    /// <summary>
    /// 의존성 검증 및 버튼 이벤트 자동 바인딩을 수행합니다.
    /// </summary>
    private void Awake()
    {
        ResolveManager("Awake");
        ValidateEntries();
        BindButtons();
        RefreshDebugText();
    }

    /// <summary>
    /// 매 프레임 활성 효과 정보를 갱신해 디버그 텍스트를 최신 상태로 유지합니다.
    /// </summary>
    private void Update()
    {
        ResolveManager("Update");
        RefreshDebugText();
    }

    /// <summary>
    /// 오브젝트 파괴 시점에 버튼 이벤트 바인딩을 해제합니다.
    /// </summary>
    private void OnDestroy()
    {
        UnbindButtons();
    }

    /// <summary>
    /// FadeIn 샘플 프리셋 재생 버튼에서 직접 호출할 수 있는 엔트리 메서드입니다.
    /// </summary>
    public void PlayFadeIn() => PlaySample(E_CameraEffectSamplePreset.FadeIn);

    /// <summary>
    /// FadeOut 샘플 프리셋 재생 버튼에서 직접 호출할 수 있는 엔트리 메서드입니다.
    /// </summary>
    public void PlayFadeOut() => PlaySample(E_CameraEffectSamplePreset.FadeOut);

    /// <summary>
    /// Hit_Small 샘플 프리셋 재생 버튼에서 직접 호출할 수 있는 엔트리 메서드입니다.
    /// </summary>
    public void PlayHitSmall() => PlaySample(E_CameraEffectSamplePreset.HitSmall);

    /// <summary>
    /// Hit_Heavy 샘플 프리셋 재생 버튼에서 직접 호출할 수 있는 엔트리 메서드입니다.
    /// </summary>
    public void PlayHitHeavy() => PlaySample(E_CameraEffectSamplePreset.HitHeavy);

    /// <summary>
    /// Dash_Burst 샘플 프리셋 재생 버튼에서 직접 호출할 수 있는 엔트리 메서드입니다.
    /// </summary>
    public void PlayDashBurst() => PlaySample(E_CameraEffectSamplePreset.DashBurst);

    /// <summary>
    /// LowHealth_Warning 샘플 프리셋 재생 버튼에서 직접 호출할 수 있는 엔트리 메서드입니다.
    /// </summary>
    public void PlayLowHealthWarning() => PlaySample(E_CameraEffectSamplePreset.LowHealthWarning);

    /// <summary>
    /// FadeIn 샘플 프리셋 중지 버튼에서 직접 호출할 수 있는 엔트리 메서드입니다.
    /// </summary>
    public void StopFadeIn() => StopSample(E_CameraEffectSamplePreset.FadeIn, "Phase10.StopFadeIn");

    /// <summary>
    /// FadeOut 샘플 프리셋 중지 버튼에서 직접 호출할 수 있는 엔트리 메서드입니다.
    /// </summary>
    public void StopFadeOut() => StopSample(E_CameraEffectSamplePreset.FadeOut, "Phase10.StopFadeOut");

    /// <summary>
    /// Hit_Small 샘플 프리셋 중지 버튼에서 직접 호출할 수 있는 엔트리 메서드입니다.
    /// </summary>
    public void StopHitSmall() => StopSample(E_CameraEffectSamplePreset.HitSmall, "Phase10.StopHitSmall");

    /// <summary>
    /// Hit_Heavy 샘플 프리셋 중지 버튼에서 직접 호출할 수 있는 엔트리 메서드입니다.
    /// </summary>
    public void StopHitHeavy() => StopSample(E_CameraEffectSamplePreset.HitHeavy, "Phase10.StopHitHeavy");

    /// <summary>
    /// Dash_Burst 샘플 프리셋 중지 버튼에서 직접 호출할 수 있는 엔트리 메서드입니다.
    /// </summary>
    public void StopDashBurst() => StopSample(E_CameraEffectSamplePreset.DashBurst, "Phase10.StopDashBurst");

    /// <summary>
    /// LowHealth_Warning 샘플 프리셋 중지 버튼에서 직접 호출할 수 있는 엔트리 메서드입니다.
    /// </summary>
    public void StopLowHealthWarning() => StopSample(E_CameraEffectSamplePreset.LowHealthWarning, "Phase10.StopLowHealthWarning");

    /// <summary>
    /// 등록된 모든 샘플 프리셋의 활성 핸들을 정지시킵니다.
    /// </summary>
    public void StopAllSamples()
    {
        for (int index = 0; index < _sampleEntries.Length; index++)
        {
            StopSampleInternal(index, "Phase10.StopAllSamples");
        }

        RefreshDebugText();
    }

    /// <summary>
    /// 지정 샘플 프리셋을 재생하고 핸들 상태를 갱신합니다.
    /// </summary>
    public void PlaySample(E_CameraEffectSamplePreset presetType)
    {
        if (!TryFindEntryIndex(presetType, out int entryIndex))
        {
            Debug.LogWarning($"[CameraEffectTestEnvironmentPresenter] 샘플 항목을 찾지 못해 재생을 건너뜁니다. presetType={presetType}", this);
            return;
        }

        if (!ResolveManager("PlaySample"))
        {
            Debug.LogWarning($"[CameraEffectTestEnvironmentPresenter] CameraEffectManager가 없어 재생을 건너뜁니다. presetType={presetType}", this);
            return;
        }

        SamplePresetEntry entry = _sampleEntries[entryIndex];
        if (entry.Preset == null)
        {
            Debug.LogWarning($"[CameraEffectTestEnvironmentPresenter] Preset이 비어 있어 재생을 건너뜁니다. presetType={presetType}", this);
            return;
        }

        CameraEffectHandle handle = _effectManager.Play(entry.Preset, gameObject);
        entry.RuntimeHandle = handle;
        entry.WasPlayedAtLeastOnce = true;
        _sampleEntries[entryIndex] = entry;

        if (_verboseLogging)
        {
            Debug.Log($"[CameraEffectTestEnvironmentPresenter] 샘플 재생 요청. presetType={presetType}, isValid={handle.IsValid}", this);
        }

        RefreshDebugText();
    }

    /// <summary>
    /// 지정 샘플 프리셋의 현재 핸들을 중지합니다.
    /// </summary>
    public void StopSample(E_CameraEffectSamplePreset presetType, string reason = "Phase10.StopSample")
    {
        if (!TryFindEntryIndex(presetType, out int entryIndex))
        {
            Debug.LogWarning($"[CameraEffectTestEnvironmentPresenter] 샘플 항목을 찾지 못해 중지를 건너뜁니다. presetType={presetType}", this);
            return;
        }

        StopSampleInternal(entryIndex, reason);
        RefreshDebugText();
    }

    /// <summary>
    /// TMP 텍스트와 캐시 문자열을 최신 상태로 갱신합니다.
    /// </summary>
    private void RefreshDebugText()
    {
        _textBuilder.Clear();
        _textBuilder.AppendLine("[CameraEffect Phase10 Test Environment]");
        _textBuilder.Append("Manager: ");
        _textBuilder.AppendLine(_effectManager != null ? "Connected" : "Missing");
        _textBuilder.AppendLine("Entries:");

        for (int index = 0; index < _sampleEntries.Length; index++)
        {
            SamplePresetEntry entry = _sampleEntries[index];
            if (entry == null)
            {
                _textBuilder.Append("- Entry");
                _textBuilder.Append(index);
                _textBuilder.AppendLine(" | state=Null");
                continue;
            }

            bool isActive = entry.RuntimeHandle.IsValid;
            _textBuilder.Append("- ");
            _textBuilder.Append(entry.DisplayName);
            _textBuilder.Append(" | preset=");
            _textBuilder.Append(entry.Preset != null ? "Assigned" : "Missing");
            _textBuilder.Append(" | played=");
            _textBuilder.Append(entry.WasPlayedAtLeastOnce ? "Yes" : "No");
            _textBuilder.Append(" | active=");
            _textBuilder.AppendLine(isActive ? "Yes" : "No");
        }

        _cachedDebugText = _textBuilder.ToString();

        if (_activeEffectsText != null)
        {
            _activeEffectsText.text = _cachedDebugText;
        }
    }

    /// <summary>
    /// TMP 텍스트 없이도 디버그 정보를 확인할 수 있도록 OnGUI 오버레이를 그립니다.
    /// </summary>
    private void OnGUI()
    {
        if (!_showOnGuiOverlay)
        {
            return;
        }

        GUI.Box(_overlayRect, _cachedDebugText);
    }

    /// <summary>
    /// 설정된 샘플 항목의 중복/누락을 검사해 Warning으로 알립니다.
    /// </summary>
    private void ValidateEntries()
    {
        for (int index = 0; index < _sampleEntries.Length; index++)
        {
            SamplePresetEntry entry = _sampleEntries[index];
            if (entry == null)
            {
                Debug.LogWarning($"[CameraEffectTestEnvironmentPresenter] Sample Entry가 null입니다. index={index}", this);
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.DisplayName))
            {
                Debug.LogWarning($"[CameraEffectTestEnvironmentPresenter] DisplayName이 비어 있습니다. index={index}, presetType={entry.PresetType}", this);
            }

            if (entry.Preset == null)
            {
                Debug.LogWarning($"[CameraEffectTestEnvironmentPresenter] Preset이 비어 있습니다. index={index}, presetType={entry.PresetType}", this);
            }

            for (int compareIndex = index + 1; compareIndex < _sampleEntries.Length; compareIndex++)
            {
                SamplePresetEntry compareEntry = _sampleEntries[compareIndex];
                if (compareEntry == null)
                {
                    continue;
                }

                if (entry.PresetType == compareEntry.PresetType)
                {
                    Debug.LogWarning($"[CameraEffectTestEnvironmentPresenter] PresetType이 중복 등록되었습니다. presetType={entry.PresetType}, firstIndex={index}, secondIndex={compareIndex}", this);
                }
            }
        }
    }

    /// <summary>
    /// 샘플 항목에 지정된 버튼 클릭 이벤트를 자동으로 바인딩합니다.
    /// </summary>
    private void BindButtons()
    {
        for (int index = 0; index < _sampleEntries.Length; index++)
        {
            SamplePresetEntry entry = _sampleEntries[index];
            if (entry == null)
            {
                continue;
            }

            E_CameraEffectSamplePreset capturedType = entry.PresetType; // 람다 캡처 시 루프 변수 오염 방지를 위한 지역 복사본입니다.
            if (entry.PlayButton != null)
            {
                entry.BoundPlayAction = () => PlaySample(capturedType);
                entry.PlayButton.onClick.AddListener(entry.BoundPlayAction);
            }

            if (entry.StopButton != null)
            {
                entry.BoundStopAction = () => StopSample(capturedType, "Phase10.ButtonStop");
                entry.StopButton.onClick.AddListener(entry.BoundStopAction);
            }
        }
    }

    /// <summary>
    /// 파괴 시점에 자동 바인딩한 버튼 이벤트를 제거합니다.
    /// </summary>
    private void UnbindButtons()
    {
        for (int index = 0; index < _sampleEntries.Length; index++)
        {
            SamplePresetEntry entry = _sampleEntries[index];
            if (entry == null)
            {
                continue;
            }

            if (entry.PlayButton != null)
            {
                if (entry.BoundPlayAction != null)
                {
                    entry.PlayButton.onClick.RemoveListener(entry.BoundPlayAction);
                    entry.BoundPlayAction = null;
                }
            }

            if (entry.StopButton != null)
            {
                if (entry.BoundStopAction != null)
                {
                    entry.StopButton.onClick.RemoveListener(entry.BoundStopAction);
                    entry.BoundStopAction = null;
                }
            }
        }
    }

    /// <summary>
    /// enum 식별자로 샘플 항목 인덱스를 조회합니다.
    /// </summary>
    private bool TryFindEntryIndex(E_CameraEffectSamplePreset presetType, out int entryIndex)
    {
        for (int index = 0; index < _sampleEntries.Length; index++)
        {
            SamplePresetEntry entry = _sampleEntries[index];
            if (entry != null && entry.PresetType == presetType)
            {
                entryIndex = index;
                return true;
            }
        }

        entryIndex = -1;
        return false;
    }

    /// <summary>
    /// 내부 인덱스 기반으로 샘플 핸들 중지를 수행합니다.
    /// </summary>
    private void StopSampleInternal(int entryIndex, string reason)
    {
        if (entryIndex < 0 || entryIndex >= _sampleEntries.Length)
        {
            Debug.LogWarning($"[CameraEffectTestEnvironmentPresenter] 중지 인덱스가 범위를 벗어났습니다. entryIndex={entryIndex}", this);
            return;
        }

        SamplePresetEntry entry = _sampleEntries[entryIndex];
        if (entry == null)
        {
            Debug.LogWarning($"[CameraEffectTestEnvironmentPresenter] 중지 대상 Entry가 null입니다. entryIndex={entryIndex}", this);
            return;
        }

        if (!entry.RuntimeHandle.IsValid)
        {
            if (_verboseLogging)
            {
                Debug.LogWarning($"[CameraEffectTestEnvironmentPresenter] 유효한 핸들이 없어 중지를 건너뜁니다. presetType={entry.PresetType}", this);
            }

            return;
        }

        bool stopped = entry.RuntimeHandle.Stop(reason);
        if (!stopped)
        {
            Debug.LogWarning($"[CameraEffectTestEnvironmentPresenter] 핸들 Stop 호출이 실패했습니다. presetType={entry.PresetType}, reason={reason}", this);
        }

        _sampleEntries[entryIndex] = entry;
    }

    /// <summary>
    /// CameraEffectManager 참조를 확인하고 필요 시 Instance를 자동 연결합니다.
    /// </summary>
    private bool ResolveManager(string context)
    {
        if (_effectManager != null)
        {
            return true;
        }

        _effectManager = CameraEffectManager.Instance;
        if (_effectManager == null)
        {
            if (_verboseLogging)
            {
                Debug.LogWarning($"[CameraEffectTestEnvironmentPresenter] CameraEffectManager 연결 실패. context={context}", this);
            }

            return false;
        }

        if (_verboseLogging)
        {
            Debug.Log($"[CameraEffectTestEnvironmentPresenter] CameraEffectManager 자동 연결 성공. context={context}", this);
        }

        return true;
    }
}
