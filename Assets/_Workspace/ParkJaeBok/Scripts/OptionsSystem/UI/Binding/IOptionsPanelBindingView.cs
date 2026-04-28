/// <summary>
/// 옵션 패널 View가 OptionManager와 데이터를 주고받기 위해 구현하는 바인딩 인터페이스입니다.
/// </summary>
public interface IOptionsPanelBindingView
{
    /// <summary>
    /// 옵션 UI가 사용할 OptionManager를 연결하고 현재 런타임 옵션을 UI에 즉시 반영합니다.
    /// </summary>
    void BindOptionManager(OptionManager optionManager);

    /// <summary>
    /// 런타임 옵션 스냅샷을 받아 UI 위젯 상태를 갱신합니다.
    /// </summary>
    void ApplyOptionsToView(OptionSaveData optionData);

    /// <summary>
    /// 현재 UI 입력값을 OptionSaveData로 구성합니다.
    /// </summary>
    bool TryBuildOptions(out OptionSaveData optionData);
}
