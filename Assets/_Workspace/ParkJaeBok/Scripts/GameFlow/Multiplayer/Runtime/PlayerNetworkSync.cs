using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// 플레이어 이동 상태를 네트워크로 동기화하기 위한 NetworkTransform 보유/검증 컴포넌트입니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public class PlayerNetworkSync : NetworkBehaviour
{
    [Header("Dependencies")]
    [Tooltip("플레이어 Transform 동기화를 담당하는 NetworkTransform 참조입니다. 비어 있으면 자동 탐색합니다.")]
    [SerializeField] private NetworkTransform _networkTransform; // 원격 플레이어 위치 동기화를 처리하는 NetworkTransform 참조입니다.

    /// <summary>
    /// 초기화 시 NetworkTransform 참조를 캐시합니다.
    /// </summary>
    private void Awake()
    {
        if (_networkTransform == null)
        {
            _networkTransform = GetComponent<NetworkTransform>();
        }
    }
}
