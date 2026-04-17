using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Client Join 실패 유형별 UI 문구/표시 정책을 디자이너가 편집할 수 있도록 제공하는 카탈로그입니다.
/// </summary>
[CreateAssetMenu(fileName = "MultiplayerJoinFailureMessageCatalog", menuName = "Game/UI/Multiplayer Join Failure Message Catalog")]
public class MultiplayerJoinFailureMessageCatalog : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        [Tooltip("매핑할 Client Join 실패 유형입니다.")]
        public E_ClientJoinFailureType FailureType; // 특정 실패 유형을 식별하기 위한 키입니다.

        [Tooltip("UI 제목 표시 여부입니다.")]
        public bool UseTitle; // View에서 제목 라벨을 노출할지 여부입니다.

        [Tooltip("실패 팝업 제목 문자열입니다.")]
        [TextArea(1, 2)]
        public string Title; // 실패 팝업 제목 텍스트입니다.

        [Tooltip("실패 팝업 본문 문자열입니다.")]
        [TextArea(2, 4)]
        public string Body; // 실패 팝업 본문 텍스트입니다.

        [Tooltip("표시 후 자동 닫힘을 사용할지 여부입니다.")]
        public bool AutoClose; // 메시지 자동 닫힘 사용 여부입니다.

        [Tooltip("자동 닫힘 대기 시간(초)입니다.")]
        [Min(0f)]
        public float AutoCloseDelaySeconds; // AutoClose 활성화 시 대기할 시간입니다.

        [Tooltip("수동 닫기 버튼을 표시할지 여부입니다.")]
        public bool UseManualCloseButton; // Close 버튼 표시 여부입니다.
    }

    [Header("Fallback")]
    [Tooltip("실패 유형 매핑을 찾지 못했을 때 제목 표시 여부입니다.")]
    [SerializeField] private bool _fallbackUseTitle = true; // 매핑 누락 시 제목 표시 여부 기본값입니다.

    [Tooltip("실패 유형 매핑을 찾지 못했을 때 사용할 제목입니다.")]
    [SerializeField] private string _fallbackTitle = "접속 실패"; // 매핑 누락 시 제목 기본 문자열입니다.

    [Tooltip("실패 유형 매핑을 찾지 못했을 때 사용할 본문입니다.")]
    [SerializeField] private string _fallbackBody = "세션 입장에 실패했습니다. 잠시 후 다시 시도해 주세요."; // 매핑 누락 시 본문 기본 문자열입니다.

    [Tooltip("실패 유형 매핑을 찾지 못했을 때 자동 닫힘을 사용할지 여부입니다.")]
    [SerializeField] private bool _fallbackAutoClose = false; // 매핑 누락 시 자동 닫힘 정책 기본값입니다.

    [Tooltip("실패 유형 매핑을 찾지 못했을 때 자동 닫힘 대기 시간(초)입니다.")]
    [Min(0f)]
    [SerializeField] private float _fallbackAutoCloseDelaySeconds = 0f; // 매핑 누락 시 자동 닫힘 지연 시간 기본값입니다.

    [Tooltip("실패 유형 매핑을 찾지 못했을 때 수동 닫기 버튼 표시 여부입니다.")]
    [SerializeField] private bool _fallbackUseManualCloseButton = true; // 매핑 누락 시 수동 닫기 버튼 표시 정책입니다.

    [Header("Entries")]
    [Tooltip("Client Join 실패 유형별 메시지/표시 정책 매핑 목록입니다.")]
    [SerializeField] private List<Entry> _entries = new(); // 실패 유형별 UI 정책을 보관하는 목록입니다.

    /// <summary>
    /// 지정한 실패 유형에 대한 메시지 엔트리를 조회합니다.
    /// </summary>
    public bool TryGetEntry(E_ClientJoinFailureType failureType, out Entry entry)
    {
        for (int index = 0; index < _entries.Count; index++)
        {
            Entry candidate = _entries[index]; // 현재 검사 중인 메시지 엔트리 후보입니다.
            if (candidate.FailureType != failureType)
            {
                continue;
            }

            entry = candidate;
            return true;
        }

        entry = default;
        return false;
    }

    /// <summary>
    /// 매핑 누락 시 사용할 폴백 메시지 엔트리를 생성합니다.
    /// </summary>
    public Entry BuildFallbackEntry()
    {
        return new Entry
        {
            FailureType = E_ClientJoinFailureType.Unknown,
            UseTitle = _fallbackUseTitle,
            Title = _fallbackTitle,
            Body = _fallbackBody,
            AutoClose = _fallbackAutoClose,
            AutoCloseDelaySeconds = _fallbackAutoCloseDelaySeconds,
            UseManualCloseButton = _fallbackUseManualCloseButton
        };
    }
}
