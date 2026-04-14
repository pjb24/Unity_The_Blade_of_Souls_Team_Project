using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 네트워크 소유권 기준으로 로컬 플레이어만 입력을 읽어 PlayerMovement에 전달하는 입력 드라이버입니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement))]
public class PlayerInputDriver : NetworkBehaviour
{
    [Header("Dependencies")]
    [Tooltip("입력 프레임을 전달할 PlayerMovement 참조입니다. 비어 있으면 같은 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private PlayerMovement _playerMovement; // 로컬 입력 프레임을 소비할 PlayerMovement 참조입니다.

    [Header("Debug")]
    [Tooltip("디버그용: 현재 이 드라이버가 로컬 소유자 입력을 처리 중인지 여부입니다.")]
    [SerializeField] private bool _isDrivingLocalInput; // 소유권 판정 결과로 로컬 입력 처리 활성 여부를 확인하기 위한 디버그 값입니다.

    /// <summary>
    /// 컴포넌트 초기화 시 PlayerMovement 참조를 보정합니다.
    /// </summary>
    private void Awake()
    {
        if (_playerMovement == null)
        {
            _playerMovement = GetComponent<PlayerMovement>();
        }
    }

    /// <summary>
    /// 네트워크 스폰 이후 소유권 기반 입력 처리 모드를 초기화합니다.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        UpdateDriveStateByOwnership();
    }

    /// <summary>
    /// 네트워크 디스폰 시 입력 주입을 비활성화하고 안전한 기본값을 전달합니다.
    /// </summary>
    public override void OnNetworkDespawn()
    {
        _isDrivingLocalInput = false;

        if (_playerMovement == null)
        {
            return;
        }

        _playerMovement.SetDrivenInputEnabled(false);
        _playerMovement.SetDrivenInputFrame(Vector2.zero, false, false, false, false);
    }

    /// <summary>
    /// 매 프레임 소유권을 확인하고 로컬 소유자일 때만 입력을 PlayerMovement에 전달합니다.
    /// </summary>
    private void Update()
    {
        if (!IsSpawned || _playerMovement == null)
        {
            return;
        }

        UpdateDriveStateByOwnership();
        if (!_isDrivingLocalInput)
        {
            _playerMovement.SetDrivenInputFrame(Vector2.zero, false, false, false, false);
            return;
        }

        Vector2 movement = InputManager.Movement; // 로컬 이동 축 입력값입니다.
        bool runHeld = InputManager.RunIsHeld; // 로컬 달리기 유지 입력값입니다.
        bool jumpPressed = InputManager.JumpWasPressed; // 로컬 점프 눌림 입력값입니다.
        bool jumpReleased = InputManager.JumpWasReleased; // 로컬 점프 해제 입력값입니다.
        bool dashPressed = InputManager.DashWasPressed; // 로컬 대시 눌림 입력값입니다.

        _playerMovement.SetDrivenInputFrame(movement, runHeld, jumpPressed, jumpReleased, dashPressed);
    }

    /// <summary>
    /// 네트워크 소유권 기준으로 PlayerMovement의 입력 주입 모드를 갱신합니다.
    /// </summary>
    private void UpdateDriveStateByOwnership()
    {
        _isDrivingLocalInput = IsOwner;
        if (_playerMovement == null)
        {
            return;
        }

        _playerMovement.SetDrivenInputEnabled(true);
    }
}
