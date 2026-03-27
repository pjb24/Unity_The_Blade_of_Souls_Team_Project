using UnityEngine;

/// <summary>
/// Quit 요청 시 확인 절차 후 애플리케이션 종료를 시도하는 액션입니다.
/// </summary>
public class RequestQuitAction : MonoBehaviour, ITitleMenuAction
{
    /// <summary>
    /// 현재 문맥에서 Quit 실행 가능 여부를 반환합니다.
    /// </summary>
    public bool CanExecute(TitleMenuActionContext context)
    {
        return context != null;
    }

    /// <summary>
    /// Quit 확인 후 플랫폼 정책에 맞게 종료를 수행합니다.
    /// </summary>
    public bool Execute(TitleMenuActionContext context)
    {
        if (context == null)
        {
            return false;
        }

        if (context.DialogService != null)
        {
            bool confirmed = context.DialogService.ConfirmQuit(); // Quit 요청 확인 결과입니다.
            if (confirmed == false)
            {
                return false;
            }
        }

#if UNITY_EDITOR
        Debug.LogWarning("[RequestQuitAction] UNITY_EDITOR 환경에서는 Application.Quit이 동작하지 않습니다.", this);
        return true;
#else
        Application.Quit();
        return true;
#endif
    }
}
