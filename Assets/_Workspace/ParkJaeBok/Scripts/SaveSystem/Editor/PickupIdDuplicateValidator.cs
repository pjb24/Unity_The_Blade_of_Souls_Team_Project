#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 월드 픽업의 pickupId 중복 여부를 검사하는 에디터 유틸리티입니다.
/// </summary>
public static class PickupIdDuplicateValidator
{
    /// <summary>
    /// 현재 열린 씬의 WorldPickupState pickupId 중복 여부를 검사합니다.
    /// </summary>
    [MenuItem("Tools/SaveSystem/Validate PickupId Duplicates")]
    public static void ValidateCurrentScenePickupIds()
    {
        WorldPickupState[] pickups = Object.FindObjectsByType<WorldPickupState>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 현재 씬에서 검사할 픽업 상태 컴포넌트 목록입니다.
        Dictionary<string, List<WorldPickupState>> byPickupId = new Dictionary<string, List<WorldPickupState>>(); // pickupId별 충돌 대상을 그룹화할 맵입니다.

        for (int index = 0; index < pickups.Length; index++)
        {
            WorldPickupState pickup = pickups[index]; // 현재 중복 검사를 수행할 픽업 상태 컴포넌트입니다.
            if (pickup == null)
            {
                continue;
            }

            string pickupId = pickup.PickupId;
            if (string.IsNullOrWhiteSpace(pickupId))
            {
                Debug.LogWarning($"[PickupIdDuplicateValidator] 빈 pickupId가 발견되었습니다. object={pickup.name}", pickup);
                continue;
            }

            if (!byPickupId.TryGetValue(pickupId, out List<WorldPickupState> list))
            {
                list = new List<WorldPickupState>();
                byPickupId.Add(pickupId, list);
            }

            list.Add(pickup);
        }

        int duplicateCount = 0; // 충돌된 pickupId 개수를 집계하기 위한 카운터입니다.

        foreach (KeyValuePair<string, List<WorldPickupState>> pair in byPickupId)
        {
            if (pair.Value.Count <= 1)
            {
                continue;
            }

            duplicateCount++;
            Debug.LogError($"[PickupIdDuplicateValidator] 중복 pickupId 발견: {pair.Key}, count={pair.Value.Count}");
            for (int index = 0; index < pair.Value.Count; index++)
            {
                Debug.LogError($"[PickupIdDuplicateValidator] - duplicate object: {pair.Value[index].name}", pair.Value[index]);
            }
        }

        if (duplicateCount == 0)
        {
            Debug.Log("[PickupIdDuplicateValidator] pickupId 중복이 없습니다.");
        }
        else
        {
            Debug.LogWarning($"[PickupIdDuplicateValidator] 중복 pickupId 그룹 수: {duplicateCount}");
        }
    }
}
#endif
