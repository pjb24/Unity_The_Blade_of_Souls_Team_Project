using UnityEngine;

/// <summary>
/// 로컬 오너 입력만 처리해 Buff 토글 요청을 PlayerBuffController로 전달하는 입력 컨트롤러입니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerBuffController))]
public class PlayerBuffInputController : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Buff 토글 요청을 처리할 PlayerBuffController 참조입니다. 비어 있으면 자동 탐색합니다.")]
    [SerializeField] private PlayerBuffController _buffController; // Buff 토글 요청을 처리할 컨트롤러 참조입니다.

    [Header("Debug")]
    [Tooltip("디버그용: 현재 로컬 입력 처리 활성 여부입니다.")]
    [SerializeField] private bool _isDrivingLocalInput; // 로컬 입력 처리 활성 여부를 나타내는 디버그 값입니다.

    /// <summary>
    /// 참조를 보정합니다.
    /// </summary>
    private void Awake()
    {
        if (_buffController == null)
        {
            _buffController = ResolveBuffController();
        }
    }

    /// <summary>
    /// 매 프레임 Buff 입력 스냅샷을 감지해 토글 요청을 전달합니다.
    /// </summary>
    private void Update()
    {
        if (_buffController == null)
        {
            return;
        }

        UpdateInputDriveState();
        if (!_isDrivingLocalInput)
        {
            return;
        }

        if (!InputManager.BuffWasPressed)
        {
            return;
        }

        _buffController.RequestToggleBuffFromInput();
    }

    /// <summary>
    /// 현재 인스턴스가 로컬 입력을 처리해야 하는지 판정합니다.
    /// </summary>
    private void UpdateInputDriveState()
    {
        if (_buffController == null)
        {
            _isDrivingLocalInput = false;
            return;
        }

        // 입력 드라이브 판정은 BuffController 내부 네트워크 릴레이를 통해 구조적으로 처리됩니다.
        _isDrivingLocalInput = _buffController.CanDriveLocalInput();
    }

    /// <summary>
    /// Buff 컨트롤러를 동일/자식/부모 계층 순서로 해석합니다.
    /// </summary>
    private PlayerBuffController ResolveBuffController()
    {
        PlayerBuffController resolved = GetComponent<PlayerBuffController>();
        if (resolved != null)
        {
            return resolved;
        }

        resolved = GetComponentInChildren<PlayerBuffController>(true);
        if (resolved != null)
        {
            return resolved;
        }

        resolved = GetComponentInParent<PlayerBuffController>();
        return resolved;
    }
}
