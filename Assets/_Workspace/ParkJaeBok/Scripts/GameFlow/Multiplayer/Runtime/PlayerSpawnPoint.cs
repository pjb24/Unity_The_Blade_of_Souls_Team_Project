using UnityEngine;

/// <summary>
/// 씬에 배치되는 플레이어 스폰 포인트 정의 컴포넌트입니다.
/// </summary>
public class PlayerSpawnPoint : MonoBehaviour
{
    [Header("Spawn Slot")]
    [Tooltip("이 스폰 포인트가 담당하는 플레이어 슬롯입니다. (Single/Host/Client)")]
    [SerializeField] private E_PlayerSpawnSlot _slot; // 이 포인트가 담당하는 스폰 슬롯 유형입니다.

    [Header("Debug")]
    [Tooltip("Scene 뷰 Gizmo 라벨 노출 여부입니다.")]
    [SerializeField] private bool _showLabel = true; // Scene 뷰에서 슬롯 라벨을 표시할지 여부입니다.

    /// <summary>
    /// 이 포인트가 담당하는 스폰 슬롯을 반환합니다.
    /// </summary>
    public E_PlayerSpawnSlot Slot => _slot;

    /// <summary>
    /// 이 포인트의 최종 스폰 위치를 반환합니다.
    /// </summary>
    public Vector3 Position => transform.position;

    /// <summary>
    /// 이 포인트의 최종 스폰 회전을 반환합니다.
    /// </summary>
    public Quaternion Rotation => transform.rotation;

    /// <summary>
    /// Scene 뷰에서 슬롯별 색상 기즈모를 표시합니다.
    /// </summary>
    private void OnDrawGizmos()
    {
        Gizmos.color = ResolveColorBySlot(_slot);
        Gizmos.DrawWireSphere(transform.position, 0.35f);
        Gizmos.DrawLine(transform.position, transform.position + transform.right * 0.75f);

#if UNITY_EDITOR
        if (_showLabel)
        {
            UnityEditor.Handles.color = Gizmos.color;
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, $"Spawn:{_slot}");
        }
#endif
    }

    /// <summary>
    /// 슬롯별 식별 색상을 반환합니다.
    /// </summary>
    private Color ResolveColorBySlot(E_PlayerSpawnSlot slot)
    {
        switch (slot)
        {
            case E_PlayerSpawnSlot.Single:
                return Color.green;
            case E_PlayerSpawnSlot.Host:
                return new Color(0.25f, 0.7f, 1f, 1f);
            case E_PlayerSpawnSlot.Client:
                return new Color(1f, 0.65f, 0.2f, 1f);
            default:
                return Color.white;
        }
    }
}
