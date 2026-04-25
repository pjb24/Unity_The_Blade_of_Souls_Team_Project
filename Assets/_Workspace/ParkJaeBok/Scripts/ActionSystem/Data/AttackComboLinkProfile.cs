using UnityEngine;

/// <summary>
/// 액션 완료 이후 콤보 전이를 결정하는 링크 규칙을 제공하는 ScriptableObject 프로필입니다.
/// </summary>
[CreateAssetMenu(fileName = "AttackComboLinkProfile", menuName = "ActionSystem/Attack Combo Link Profile")]
public sealed class AttackComboLinkProfile : ScriptableObject
{
    [Tooltip("완료된 콤보 액션 기준 다음 콤보 액션과 입력 예약 허용 시간을 정의한 링크 배열입니다.")]
    [SerializeField] private AttackComboLinkData[] _links = new AttackComboLinkData[0]; // 완료된 콤보 액션 기준 다음 콤보 액션과 입력 예약 허용 시간을 정의한 링크 배열입니다.

    /// <summary>
    /// 원본 액션 타입에 매핑된 콤보 링크를 조회합니다.
    /// </summary>
    public bool TryGetLink(E_ActionType fromActionType, out AttackComboLinkData linkData)
    {
        linkData = default;

        if (_links == null || _links.Length == 0)
        {
            Debug.LogWarning($"[{nameof(AttackComboLinkProfile)}] has no combo links.");
            return false;
        }

        for (int index = 0; index < _links.Length; index++)
        {
            AttackComboLinkData candidate = _links[index];
            if (candidate.FromActionType != fromActionType)
            {
                continue;
            }

            linkData = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 원본 액션 타입의 다음 액션 타입을 간편 조회합니다.
    /// </summary>
    public bool TryGetNextAction(E_ActionType fromActionType, out E_ActionType nextActionType)
    {
        nextActionType = E_ActionType.None;

        if (!TryGetLink(fromActionType, out AttackComboLinkData linkData))
        {
            return false;
        }

        nextActionType = linkData.ToActionType;
        return true;
    }
}
