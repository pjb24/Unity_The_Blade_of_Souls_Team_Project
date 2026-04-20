using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Pause 버튼 입력으로 시작되는 게임 흐름 제어를 모드별로 위임하는 Coordinator입니다.
/// </summary>
public sealed class PauseFlowCoordinator
{
    private readonly GameFlowController _gameFlowController; // 현재 플레이 모드와 공통 흐름 API를 조회할 GameFlowController 참조입니다.
    private readonly SinglePlayerPauseService _singlePlayerPauseService; // 싱글플레이 Pause 상태 제어를 위임할 서비스 참조입니다.
    private readonly MultiplayerSessionOrchestrator _multiplayerSessionOrchestrator; // 멀티플레이 세션 종료/이탈 흐름을 위임할 오케스트레이터 참조입니다.
    private bool _isMultiplayerPauseOpened; // 멀티플레이 로컬 Pause 패널 열림 상태를 추적하는 런타임 플래그입니다.
    private bool _wasLocalPlayerInputEnabledBeforeMultiplayerPause = true; // 멀티 Pause 진입 전 로컬 PlayerInput 활성 상태를 복원하기 위해 저장하는 런타임 값입니다.

    /// <summary>
    /// Pause 흐름 제어에 필요한 서비스 의존성을 주입합니다.
    /// </summary>
    public PauseFlowCoordinator(
        GameFlowController gameFlowController,
        SinglePlayerPauseService singlePlayerPauseService,
        MultiplayerSessionOrchestrator multiplayerSessionOrchestrator)
    {
        _gameFlowController = gameFlowController;
        _singlePlayerPauseService = singlePlayerPauseService;
        _multiplayerSessionOrchestrator = multiplayerSessionOrchestrator;
    }

    /// <summary>
    /// 현재 플레이 모드 기준 Pause 패널 표시 가능 여부를 반환합니다.
    /// </summary>
    public bool CanOpenPausePanel()
    {
        if (_gameFlowController == null)
        {
            return false;
        }

        if (_gameFlowController.CurrentState != GameFlowState.Town
            && _gameFlowController.CurrentState != GameFlowState.StagePlaying)
        {
            return false;
        }

        if (_gameFlowController.CurrentPlayMode == E_GamePlayMode.SinglePlayer)
        {
            return _singlePlayerPauseService != null && _singlePlayerPauseService.CanPauseNow();
        }

        if (IsMultiplayerMode(_gameFlowController.CurrentPlayMode))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 현재 플레이 모드 기준 Pause 상태 여부를 반환합니다.
    /// </summary>
    public bool IsPaused()
    {
        if (_gameFlowController == null)
        {
            return false;
        }

        if (_gameFlowController.CurrentPlayMode == E_GamePlayMode.SinglePlayer)
        {
            return _singlePlayerPauseService != null && _singlePlayerPauseService.IsPaused;
        }

        if (IsMultiplayerMode(_gameFlowController.CurrentPlayMode))
        {
            return _isMultiplayerPauseOpened;
        }

        return false;
    }

    /// <summary>
    /// Pause 열기를 모드별 정책으로 처리합니다.
    /// </summary>
    public bool TryOpenPause(string reason)
    {
        if (_gameFlowController == null)
        {
            return false;
        }

        if (_gameFlowController.CurrentPlayMode == E_GamePlayMode.SinglePlayer)
        {
            return _singlePlayerPauseService != null && _singlePlayerPauseService.TryEnterPause(reason);
        }

        if (IsMultiplayerMode(_gameFlowController.CurrentPlayMode))
        {
            if (_isMultiplayerPauseOpened)
            {
                return true;
            }

            SetLocalPlayerInputPaused(true);
            _isMultiplayerPauseOpened = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Pause 해제를 모드별 정책으로 처리합니다.
    /// </summary>
    public bool TryResume(string reason)
    {
        if (_gameFlowController == null)
        {
            return false;
        }

        if (_gameFlowController.CurrentPlayMode == E_GamePlayMode.SinglePlayer)
        {
            return _singlePlayerPauseService != null && _singlePlayerPauseService.TryResume(reason);
        }

        if (IsMultiplayerMode(_gameFlowController.CurrentPlayMode))
        {
            SetLocalPlayerInputPaused(false);
            _isMultiplayerPauseOpened = false;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Main Menu 버튼 동작을 모드별 흐름으로 처리합니다.
    /// </summary>
    public async Task<bool> ReturnToMainMenuAsync()
    {
        if (_gameFlowController == null)
        {
            return false;
        }

        if (_gameFlowController.CurrentPlayMode == E_GamePlayMode.SinglePlayer)
        {
            if (_singlePlayerPauseService != null)
            {
                _singlePlayerPauseService.ForceResume("Pause.ReturnToMainMenu");
            }

            return _gameFlowController.RequestExit(true);
        }

        if (IsMultiplayerMode(_gameFlowController.CurrentPlayMode))
        {
            _isMultiplayerPauseOpened = false;
            if (_multiplayerSessionOrchestrator == null)
            {
                return false;
            }

            return await _multiplayerSessionOrchestrator.ReturnToMainMenuFromPauseAsync();
        }

        return false;
    }

    /// <summary>
    /// 외부 강제 종료 시 모드별 Pause 상태를 즉시 정리합니다.
    /// </summary>
    public void ForceClearPauseState(string reason)
    {
        SetLocalPlayerInputPaused(false);
        _isMultiplayerPauseOpened = false;
        _singlePlayerPauseService?.ForceResume(reason);
    }

    /// <summary>
    /// 멀티플레이 Pause 열림/해제 상태에 맞춰 로컬 PlayerInput 활성 여부를 제어합니다.
    /// </summary>
    private void SetLocalPlayerInputPaused(bool isPaused)
    {
        if (InputManager.PlayerInput == null)
        {
            return;
        }

        if (isPaused)
        {
            _wasLocalPlayerInputEnabledBeforeMultiplayerPause = InputManager.PlayerInput.enabled;
            InputManager.PlayerInput.enabled = false;
            return;
        }

        InputManager.PlayerInput.enabled = _wasLocalPlayerInputEnabledBeforeMultiplayerPause;
    }

    /// <summary>
    /// 현재 플레이 모드가 멀티플레이(Host/Client) 계열인지 판정합니다.
    /// </summary>
    private bool IsMultiplayerMode(E_GamePlayMode playMode)
    {
        return playMode == E_GamePlayMode.MultiplayerHost
            || playMode == E_GamePlayMode.MultiplayerClient;
    }
}
