using System;

/// <summary>
/// 게임 실행 중 옵션 상태를 메모리에 유지하는 런타임 컨테이너입니다.
/// </summary>
[Serializable]
public class OptionRuntimeState
{
    private OptionSaveData _currentData; // 현재 런타임에서 참조하는 옵션 데이터입니다.

    /// <summary>
    /// 지정된 옵션 데이터로 런타임 상태를 초기화합니다.
    /// </summary>
    public void Initialize(OptionSaveData source)
    {
        _currentData = DeepCopy(source);
    }

    /// <summary>
    /// 현재 런타임 옵션 데이터를 읽기 전용으로 반환합니다.
    /// </summary>
    public OptionSaveData GetSnapshot()
    {
        return DeepCopy(_currentData);
    }

    /// <summary>
    /// 현재 런타임 옵션 데이터를 교체합니다.
    /// </summary>
    public void Replace(OptionSaveData source)
    {
        _currentData = DeepCopy(source);
    }

    /// <summary>
    /// 런타임 상태를 JSON 직렬화 가능한 안전 복사본으로 생성합니다.
    /// </summary>
    private OptionSaveData DeepCopy(OptionSaveData source)
    {
        OptionSaveData safeSource = source ?? new OptionSaveData(); // null 입력을 방지하기 위한 안전 소스입니다.
        KeyBindingEntry[] copiedBindings = safeSource.Input.KeyBindings != null
            ? (KeyBindingEntry[])safeSource.Input.KeyBindings.Clone()
            : Array.Empty<KeyBindingEntry>(); // 배열 참조 공유를 막기 위한 키 바인딩 복사본입니다.

        InputOptionsData copiedInput = safeSource.Input; // 키 바인딩 교체를 위한 임시 Input 복사본입니다.
        copiedInput.KeyBindings = copiedBindings;

        return new OptionSaveData
        {
            SchemaVersion = safeSource.SchemaVersion,
            Display = safeSource.Display,
            Audio = safeSource.Audio,
            Input = copiedInput,
            Accessibility = safeSource.Accessibility,
            Gameplay = safeSource.Gameplay
        };
    }
}
