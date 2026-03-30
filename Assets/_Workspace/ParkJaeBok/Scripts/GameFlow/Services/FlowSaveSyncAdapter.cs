using System;

/// <summary>
/// SaveCoordinator 이벤트와 GameFlow 저장 실패 동기화를 연결하는 어댑터입니다.
/// </summary>
internal sealed class FlowSaveSyncAdapter
{
    private SaveCoordinator _saveCoordinator; // 저장 결과 이벤트를 구독할 SaveCoordinator 참조입니다.
    private Action<SaveCoordinator.SaveOperationStatus> _onSaveFailed; // 저장 실패 이벤트를 상위로 전달할 콜백입니다.

    /// <summary>
    /// SaveCoordinator 이벤트 구독을 시작합니다.
    /// </summary>
    internal void Bind(SaveCoordinator saveCoordinator, Action<SaveCoordinator.SaveOperationStatus> onSaveFailed)
    {
        Unbind();

        _saveCoordinator = saveCoordinator;
        _onSaveFailed = onSaveFailed;

        if (_saveCoordinator == null)
        {
            return;
        }

        _saveCoordinator.OnSaveOperationCompleted += HandleSaveOperationCompleted;
    }

    /// <summary>
    /// SaveCoordinator 이벤트 구독을 해제합니다.
    /// </summary>
    internal void Unbind()
    {
        if (_saveCoordinator != null)
        {
            _saveCoordinator.OnSaveOperationCompleted -= HandleSaveOperationCompleted;
        }

        _saveCoordinator = null;
        _onSaveFailed = null;
    }

    /// <summary>
    /// 저장 결과 이벤트를 받아 실패 케이스를 상위 콜백으로 전달합니다.
    /// </summary>
    private void HandleSaveOperationCompleted(SaveCoordinator.SaveOperationStatus status)
    {
        if (status.Succeeded)
        {
            return;
        }

        _onSaveFailed?.Invoke(status);
    }
}
