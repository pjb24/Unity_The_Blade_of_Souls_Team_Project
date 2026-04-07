/// <summary>
/// 옵션 Row 단위 UI <-> 데이터 변환 계약입니다.
/// </summary>
public interface IOptionRowBinder
{
    /// <summary>
    /// 이 Binder가 담당하는 옵션 키를 반환합니다.
    /// </summary>
    E_OptionBindingKey BindingKey { get; }

    /// <summary>
    /// 런타임 옵션 스냅샷을 UI 위젯에 반영합니다.
    /// </summary>
    void ApplyToWidget(OptionSaveData optionData);

    /// <summary>
    /// 현재 UI 위젯 값을 OptionSaveData에 기록합니다.
    /// </summary>
    bool TryWriteToData(ref OptionSaveData optionData, out string errorMessage);
}
