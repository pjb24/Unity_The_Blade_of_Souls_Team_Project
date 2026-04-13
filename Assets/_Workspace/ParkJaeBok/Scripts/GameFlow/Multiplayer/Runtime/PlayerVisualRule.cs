using System;
using UnityEngine;

/// <summary>
/// A/B 테스트 룰 타입별로 Owner/Observer 조건에 따른 외형 결정 규칙을 보관하는 ScriptableObject입니다.
/// </summary>
[CreateAssetMenu(fileName = "PlayerVisualRule", menuName = "GameFlow/Multiplayer/Player Visual Rule")]
public class PlayerVisualRule : ScriptableObject
{
    [Serializable]
    public struct RuleEntry
    {
        [Tooltip("규칙 엔트리가 대응하는 테스트 타입입니다.")]
        public E_PlayerVisualRuleType RuleType; // 현재 엔트리가 담당하는 A/B 테스트 규칙 타입입니다.

        [Tooltip("로컬 소유자이면서 Host인 경우 적용할 외형입니다.")]
        public E_PlayerVisualVariant OwnerHostVariant; // Host 소유자 플레이어에 적용할 외형 타입입니다.

        [Tooltip("로컬 소유자이면서 Client인 경우 적용할 외형입니다.")]
        public E_PlayerVisualVariant OwnerClientVariant; // Client 소유자 플레이어에 적용할 외형 타입입니다.

        [Tooltip("로컬 비소유자이며 Host가 관찰하는 원격 플레이어에 적용할 외형입니다.")]
        public E_PlayerVisualVariant ObserverHostVariant; // Host 관찰자 관점에서 원격 플레이어에 적용할 외형 타입입니다.

        [Tooltip("로컬 비소유자이며 Client가 관찰하는 원격 플레이어에 적용할 외형입니다.")]
        public E_PlayerVisualVariant ObserverClientVariant; // Client 관찰자 관점에서 원격 플레이어에 적용할 외형 타입입니다.
    }

    [Tooltip("A/B 테스트 타입별 외형 규칙 엔트리 목록입니다.")]
    [SerializeField]
    private RuleEntry[] _ruleEntries =
    {
        new RuleEntry
        {
            RuleType = E_PlayerVisualRuleType.TestA,
            OwnerHostVariant = E_PlayerVisualVariant.Y,
            OwnerClientVariant = E_PlayerVisualVariant.Y,
            ObserverHostVariant = E_PlayerVisualVariant.Z,
            ObserverClientVariant = E_PlayerVisualVariant.Z
        },
        new RuleEntry
        {
            RuleType = E_PlayerVisualRuleType.TestB,
            OwnerHostVariant = E_PlayerVisualVariant.Y,
            OwnerClientVariant = E_PlayerVisualVariant.Z,
            ObserverHostVariant = E_PlayerVisualVariant.Z,
            ObserverClientVariant = E_PlayerVisualVariant.Y
        }
    }; // 룰 타입별로 Owner/Observer 조건에서 적용할 외형 매핑 데이터 배열입니다.

    /// <summary>
    /// 룰 타입과 소유자/호스트 조건에 따라 적용할 외형 타입을 계산합니다.
    /// </summary>
    public bool TryResolveVariant(E_PlayerVisualRuleType ruleType, bool isOwner, bool isHost, out E_PlayerVisualVariant resolvedVariant)
    {
        resolvedVariant = E_PlayerVisualVariant.Y;

        for (int index = 0; index < _ruleEntries.Length; index++)
        {
            RuleEntry entry = _ruleEntries[index]; // 현재 조회 중인 룰 엔트리입니다.
            if (entry.RuleType != ruleType)
            {
                continue;
            }

            if (isOwner)
            {
                resolvedVariant = isHost ? entry.OwnerHostVariant : entry.OwnerClientVariant;
            }
            else
            {
                resolvedVariant = isHost ? entry.ObserverHostVariant : entry.ObserverClientVariant;
            }

            return true;
        }

        return false;
    }
}

/// <summary>
/// 플레이어 외형 A/B 테스트 타입 식별자입니다.
/// </summary>
public enum E_PlayerVisualRuleType
{
    TestA = 0,
    TestB = 1
}
