using UnityEngine;

/// <summary>
/// Buff 상태에 따라 지정된 VFX 오브젝트를 활성/비활성하는 컨트롤러입니다.
/// </summary>
public class PlayerBuffVisualController : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Buff VFX로 사용할 루트 오브젝트입니다. 비어 있으면 경고만 출력하고 기능은 계속 동작합니다.")]
    [SerializeField] private GameObject _buffVfxRoot; // Buff VFX 활성/비활성 대상 루트 오브젝트입니다.

    [Tooltip("Buff 수치를 읽어올 설정 ScriptableObject입니다.")]
    [SerializeField] private BuffConfigSO _buffConfig; // Buff VFX 기본 상태 정책을 읽을 설정 참조입니다.

    [Header("Runtime")]
    [Tooltip("디버그용: 현재 VFX가 Buff 활성 상태로 표시 중인지 여부입니다.")]
    [SerializeField] private bool _isBuffVisualActive; // 현재 Buff VFX 표시 상태입니다.

    /// <summary>
    /// 시작 시 기본 VFX 상태 정책을 반영합니다.
    /// </summary>
    private void Awake()
    {
        bool defaultActive = _buffConfig != null && _buffConfig.DefaultVfxActiveWhenBuffOff; // Buff 비활성 기본 VFX 활성 정책 값입니다.
        SetVisualState(defaultActive, "AwakeDefault");
    }

    /// <summary>
    /// Buff 활성 여부에 맞춰 VFX 상태를 반영합니다.
    /// </summary>
    public void SetBuffVisualActive(bool isBuffActive)
    {
        SetVisualState(isBuffActive, "BuffStateChanged");
    }

    /// <summary>
    /// 내부 VFX 오브젝트 활성 상태를 실제로 갱신합니다.
    /// </summary>
    private void SetVisualState(bool isActive, string reason)
    {
        _isBuffVisualActive = isActive;

        if (_buffVfxRoot == null)
        {
            Debug.LogWarning($"[PlayerBuffVisualController] Buff VFX root is missing. reason={reason}, object={name}", this);
            return;
        }

        _buffVfxRoot.SetActive(isActive);
    }
}
