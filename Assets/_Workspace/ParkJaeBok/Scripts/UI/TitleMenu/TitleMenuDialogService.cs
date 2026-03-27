using UnityEngine;

/// <summary>
/// 타이틀 메뉴 확인 요청을 간단한 정책으로 처리하는 기본 대화상자 서비스입니다.
/// </summary>
public class TitleMenuDialogService : MonoBehaviour, ITitleDialogService
{
    [Header("Confirm Policy")]
    [Tooltip("기존 진행 데이터가 있을 때 New Game 시작 전에 확인 절차를 요구할지 여부입니다.")]
    [SerializeField] private bool _requireNewGameOverwriteConfirm = true; // New Game 덮어쓰기 확인 절차 사용 여부입니다.

    [Tooltip("Quit 선택 시 확인 절차를 요구할지 여부입니다.")]
    [SerializeField] private bool _requireQuitConfirm = true; // Quit 확인 절차 사용 여부입니다.

    [Tooltip("확인 절차를 사용하는 경우 자동 승인으로 처리할지 여부입니다.")]
    [SerializeField] private bool _autoApproveConfirm = true; // UI 팝업 미연동 상태에서 확인 요청 자동 승인 여부입니다.

    /// <summary>
    /// New Game 덮어쓰기 확인 결과를 반환합니다.
    /// </summary>
    public bool ConfirmStartNewGameWithOverwrite()
    {
        if (_requireNewGameOverwriteConfirm == false)
        {
            return true;
        }

        if (_autoApproveConfirm == false)
        {
            Debug.LogWarning("[TitleMenuDialogService] New Game 확인이 거부되어 요청을 취소합니다.", this);
            return false;
        }

        Debug.LogWarning("[TitleMenuDialogService] New Game 확인 UI 미연동 상태라 자동 승인으로 처리했습니다.", this);
        return true;
    }

    /// <summary>
    /// Quit 확인 결과를 반환합니다.
    /// </summary>
    public bool ConfirmQuit()
    {
        if (_requireQuitConfirm == false)
        {
            return true;
        }

        if (_autoApproveConfirm == false)
        {
            Debug.LogWarning("[TitleMenuDialogService] Quit 확인이 거부되어 요청을 취소합니다.", this);
            return false;
        }

        Debug.LogWarning("[TitleMenuDialogService] Quit 확인 UI 미연동 상태라 자동 승인으로 처리했습니다.", this);
        return true;
    }
}
