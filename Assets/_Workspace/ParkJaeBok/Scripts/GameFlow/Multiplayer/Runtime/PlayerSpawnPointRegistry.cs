using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬 내 PlayerSpawnPoint를 수집/검증/조회하는 레지스트리입니다.
/// </summary>
public class PlayerSpawnPointRegistry : MonoBehaviour
{
    [Header("Discovery")]
    [Tooltip("true면 자기 자신과 자식 계층에서 스폰 포인트를 자동 수집합니다.")]
    [SerializeField] private bool _autoCollectFromChildren = true; // 자식 계층 자동 수집 사용 여부입니다.

    [Tooltip("auto collect를 끄면 수동으로 사용하는 스폰 포인트 목록입니다.")]
    [SerializeField] private List<PlayerSpawnPoint> _spawnPoints = new List<PlayerSpawnPoint>(); // 수동 등록 또는 자동 수집 결과를 보관하는 스폰 포인트 목록입니다.

    [Header("Validation")]
    [Tooltip("Single/Host/Client 슬롯 중복 여부를 경고 로그로 검증할지 여부입니다.")]
    [SerializeField] private bool _warnOnDuplicateSlots = true; // 슬롯 중복 배치 경고 사용 여부입니다.

    [Tooltip("Single/Host/Client 슬롯 누락 여부를 경고 로그로 검증할지 여부입니다.")]
    [SerializeField] private bool _warnOnMissingSlots = true; // 슬롯 누락 경고 사용 여부입니다.

    /// <summary>
    /// Inspector 값 변경 시 자동 수집/검증을 수행합니다.
    /// </summary>
    private void OnValidate()
    {
        if (_autoCollectFromChildren)
        {
            CollectFromChildren();
        }

        ValidateSlots();
    }

    /// <summary>
    /// 런타임 초기화 시 스폰 포인트 구성을 재검증합니다.
    /// </summary>
    private void Awake()
    {
        if (_autoCollectFromChildren)
        {
            CollectFromChildren();
        }

        ValidateSlots();
    }

    /// <summary>
    /// 요청된 슬롯과 일치하는 스폰 포인트를 조회합니다.
    /// </summary>
    public bool TryGetSpawnPoint(E_PlayerSpawnSlot slot, out PlayerSpawnPoint spawnPoint)
    {
        spawnPoint = null;

        for (int index = 0; index < _spawnPoints.Count; index++)
        {
            PlayerSpawnPoint candidate = _spawnPoints[index]; // 현재 조회 중인 스폰 포인트 후보입니다.
            if (candidate == null)
            {
                continue;
            }

            if (candidate.Slot == slot)
            {
                spawnPoint = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 레지스트리 하위 계층에서 PlayerSpawnPoint를 자동 수집합니다.
    /// </summary>
    private void CollectFromChildren()
    {
        PlayerSpawnPoint[] points = GetComponentsInChildren<PlayerSpawnPoint>(true); // 현재 레지스트리 하위 계층에서 탐색한 스폰 포인트 배열입니다.
        _spawnPoints.Clear();

        for (int index = 0; index < points.Length; index++)
        {
            _spawnPoints.Add(points[index]);
        }
    }

    /// <summary>
    /// 슬롯 누락/중복을 검증하고 경고 로그를 출력합니다.
    /// </summary>
    private void ValidateSlots()
    {
        Dictionary<E_PlayerSpawnSlot, int> slotCounts = new Dictionary<E_PlayerSpawnSlot, int>(); // 슬롯별 배치 개수를 집계하는 사전입니다.
        for (int index = 0; index < _spawnPoints.Count; index++)
        {
            PlayerSpawnPoint point = _spawnPoints[index]; // 현재 검증 중인 스폰 포인트입니다.
            if (point == null)
            {
                continue;
            }

            if (!slotCounts.ContainsKey(point.Slot))
            {
                slotCounts.Add(point.Slot, 0);
            }

            slotCounts[point.Slot] += 1;
        }

        ValidateSingleSlot(slotCounts, E_PlayerSpawnSlot.Single);
        ValidateSingleSlot(slotCounts, E_PlayerSpawnSlot.Host);
        ValidateSingleSlot(slotCounts, E_PlayerSpawnSlot.Client);
    }

    /// <summary>
    /// 단일 슬롯 기준으로 누락/중복을 검사하고 필요 시 경고를 출력합니다.
    /// </summary>
    private void ValidateSingleSlot(Dictionary<E_PlayerSpawnSlot, int> slotCounts, E_PlayerSpawnSlot slot)
    {
        slotCounts.TryGetValue(slot, out int count);

        if (_warnOnMissingSlots && count == 0)
        {
            Debug.LogWarning($"[PlayerSpawnPointRegistry] Slot 누락: {slot}. scene={gameObject.scene.name}, registry={name}", this);
        }

        if (_warnOnDuplicateSlots && count > 1)
        {
            Debug.LogWarning($"[PlayerSpawnPointRegistry] Slot 중복: {slot}, count={count}. scene={gameObject.scene.name}, registry={name}", this);
        }
    }
}
