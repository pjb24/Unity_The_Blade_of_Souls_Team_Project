using System.Threading.Tasks;
using Unity.Netcode;

/// <summary>
/// Pause 버튼 입력으로 시작되는 게임 흐름 제어를 모드별로 위임하는 Coordinator입니다.
/// </summary>
public sealed class PauseFlowCoordinator
{
    private readonly GameFlowController _gameFlowController; // 현재 플레이 모드와 공통 흐름 API를 조회할 GameFlowController 참조입니다.
    private readonly SinglePlayerPauseService _singlePlayerPauseService; // 싱글플레이 Pause 상태 제어를 위임할 서비스 참조입니다.
    private readonly MultiplayerSessionOrchestrator _multiplayerSessionOrchestrator; // 멀티플레이 세션 종료/이탈 흐름을 위임할 오케스트레이터 참조입니다.
    private readonly object _multiplayerPauseGameplayInputBlocker = new object(); // 멀티플레이 로컬 Pause 중 Gameplay 입력만 차단하기 위한 고유 토큰입니다.
    private bool _isMultiplayerPauseOpened; // 멀티플레이 로컬 Pause 패널 열림 상태를 추적하는 런타임 플래그입니다.
    private bool _isMultiplayerGameplayInputBlocked; // 멀티플레이 Pause 입력 차단 토큰 등록 여부를 추적하는 런타임 플래그입니다.

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
        E_GamePlayMode playMode = ResolveEffectivePlayMode(); // GameFlow와 NGO 런타임 상태를 합쳐 판정한 실제 플레이 모드입니다.

        if (playMode == E_GamePlayMode.SinglePlayer)
        {
            return _singlePlayerPauseService != null && _singlePlayerPauseService.CanPauseNow();
        }

        if (IsMultiplayerMode(playMode))
        {
            return _gameFlowController != null
                && (_gameFlowController.CurrentState == GameFlowState.Town || _gameFlowController.CurrentState == GameFlowState.StagePlaying);
        }

        return false;
    }

    /// <summary>
    /// 현재 플레이 모드 기준 Pause 상태 여부를 반환합니다.
    /// </summary>
    public bool IsPaused()
    {
        E_GamePlayMode playMode = ResolveEffectivePlayMode(); // GameFlow와 NGO 런타임 상태를 합쳐 판정한 실제 플레이 모드입니다.

        if (playMode == E_GamePlayMode.SinglePlayer)
        {
            return _singlePlayerPauseService != null && _singlePlayerPauseService.IsPaused;
        }

        if (IsMultiplayerMode(playMode))
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
        E_GamePlayMode playMode = ResolveEffectivePlayMode(); // GameFlow와 NGO 런타임 상태를 합쳐 판정한 실제 플레이 모드입니다.

        if (playMode == E_GamePlayMode.SinglePlayer)
        {
            return _singlePlayerPauseService != null && _singlePlayerPauseService.TryEnterPause(reason);
        }

        if (IsMultiplayerMode(playMode))
        {
            if (_isMultiplayerPauseOpened)
            {
                return true;
            }

            SetMultiplayerGameplayInputBlocked(true);
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
        E_GamePlayMode playMode = ResolveEffectivePlayMode(); // GameFlow와 NGO 런타임 상태를 합쳐 판정한 실제 플레이 모드입니다.

        if (playMode == E_GamePlayMode.SinglePlayer)
        {
            return _singlePlayerPauseService != null && _singlePlayerPauseService.TryResume(reason);
        }

        if (IsMultiplayerMode(playMode))
        {
            SetMultiplayerGameplayInputBlocked(false);
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
        E_GamePlayMode playMode = ResolveEffectivePlayMode(); // GameFlow와 NGO 런타임 상태를 합쳐 판정한 실제 플레이 모드입니다.

        if (playMode == E_GamePlayMode.SinglePlayer)
        {
            if (_gameFlowController == null)
            {
                return false;
            }

            if (_singlePlayerPauseService != null)
            {
                _singlePlayerPauseService.ForceResume("Pause.ReturnToMainMenu");
            }

            return _gameFlowController.RequestExit(true);
        }

        if (IsMultiplayerMode(playMode))
        {
            _isMultiplayerPauseOpened = false;
            SetMultiplayerGameplayInputBlocked(false);
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
        SetMultiplayerGameplayInputBlocked(false);
        _isMultiplayerPauseOpened = false;
        _singlePlayerPauseService?.ForceResume(reason);
    }

    /// <summary>
    /// PlayerInput 컴포넌트 상태는 건드리지 않고 멀티플레이 로컬 Gameplay 입력 값만 차단합니다.
    /// </summary>
    private void SetMultiplayerGameplayInputBlocked(bool isBlocked)
    {
        if (isBlocked)
        {
            if (_isMultiplayerGameplayInputBlocked)
            {
                return;
            }

            InputManager.AddGameplayInputBlocker(_multiplayerPauseGameplayInputBlocker);
            _isMultiplayerGameplayInputBlocked = true;
            return;
        }

        if (!_isMultiplayerGameplayInputBlocked)
        {
            return;
        }

        InputManager.RemoveGameplayInputBlocker(_multiplayerPauseGameplayInputBlocker);
        _isMultiplayerGameplayInputBlocked = false;
    }

    /// <summary>
    /// GameFlow 상태가 늦게 동기화된 Client에서도 NGO 런타임 상태로 실제 플레이 모드를 보정합니다.
    /// </summary>
    private E_GamePlayMode ResolveEffectivePlayMode()
    {
        if (_gameFlowController != null && IsMultiplayerMode(_gameFlowController.CurrentPlayMode))
        {
            return _gameFlowController.CurrentPlayMode;
        }

        NetworkManager networkManager = NetworkManager.Singleton; // 현재 로컬 피어의 NGO 연결 상태를 확인할 NetworkManager 참조입니다.
        if (networkManager != null && networkManager.IsListening)
        {
            if (networkManager.IsServer)
            {
                return E_GamePlayMode.MultiplayerHost;
            }

            if (networkManager.IsClient)
            {
                return E_GamePlayMode.MultiplayerClient;
            }
        }

        return _gameFlowController != null ? _gameFlowController.CurrentPlayMode : E_GamePlayMode.SinglePlayer;
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
