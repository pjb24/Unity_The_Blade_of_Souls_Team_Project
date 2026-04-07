using System.Collections.Generic;

/// <summary>
/// 옵션 패널 Row Binder들을 조합해 Apply/TryBuild 흐름을 제어하는 Presenter입니다.
/// </summary>
public sealed class OptionsPanelPresenter
{
    private readonly IReadOnlyList<IOptionRowBinder> _rowBinders; // 패널에 연결된 Row Binder 목록입니다.

    /// <summary>
    /// Presenter를 생성합니다.
    /// </summary>
    public OptionsPanelPresenter(IReadOnlyList<IOptionRowBinder> rowBinders)
    {
        _rowBinders = rowBinders;
    }

    /// <summary>
    /// 전달된 옵션 스냅샷을 모든 Row 위젯에 반영합니다.
    /// </summary>
    public void ApplyToView(OptionSaveData optionData)
    {
        for (int i = 0; i < _rowBinders.Count; i++)
        {
            _rowBinders[i].ApplyToWidget(optionData);
        }
    }

    /// <summary>
    /// 모든 Row 위젯의 입력값을 순회해 OptionSaveData에 기록합니다.
    /// </summary>
    public bool TryBuildFromView(OptionSaveData seedData, out OptionSaveData builtData, out string errorMessage)
    {
        builtData = seedData ?? new OptionSaveData(); // null seed 입력 방지를 위한 안전 초기 데이터입니다.
        errorMessage = string.Empty;

        for (int i = 0; i < _rowBinders.Count; i++)
        {
            if (_rowBinders[i].TryWriteToData(ref builtData, out string rowErrorMessage) == false)
            {
                errorMessage = rowErrorMessage;
                return false;
            }
        }

        return true;
    }
}
