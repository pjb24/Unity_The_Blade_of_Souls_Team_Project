using TMPro;
using UnityEngine;

/// <summary>
/// Town 씬에서 Host의 현재 Join Code를 표시하는 프리젠터입니다.
/// </summary>
public class TownMultiplayerJoinCodePresenter : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Join Code를 조회할 MultiplayerSessionOrchestrator 참조입니다. 비어 있으면 런타임에서 자동 탐색합니다.")]
    [SerializeField] private MultiplayerSessionOrchestrator _multiplayerSessionOrchestrator; // Host Join Code를 조회할 세션 오케스트레이터 참조입니다.

    [Tooltip("Join Code 문자열을 출력할 TMP_Text 참조입니다.")]
    [SerializeField] private TMP_Text _joinCodeText; // Town UI에서 Join Code를 렌더링할 텍스트 컴포넌트 참조입니다.

    [Header("View Policy")]
    [Tooltip("Host가 아니거나 Join Code가 없을 때 표시할 대체 문자열입니다.")]
    [SerializeField] private string _emptyJoinCodeText = "-"; // 표시 가능한 Join Code가 없을 때 사용할 대체 문자열입니다.

    [Tooltip("Host 전용으로 표시할 루트 GameObject입니다. 비어 있으면 가시성 제어를 생략합니다.")]
    [SerializeField] private GameObject _hostOnlyRoot; // Host 전용 Join Code 패널 표시/숨김을 제어할 루트 오브젝트 참조입니다.

    [Tooltip("Host가 아닐 때 Host 전용 루트를 자동으로 숨길지 여부입니다.")]
    [SerializeField] private bool _hideRootWhenNotHosting = true; // 비Host 상태에서 Join Code 패널 루트를 숨길지 제어하는 플래그입니다.

    /// <summary>
    /// 의존성을 보정합니다.
    /// </summary>
    private void Awake()
    {
        if (_multiplayerSessionOrchestrator == null)
        {
            _multiplayerSessionOrchestrator = MultiplayerSessionOrchestrator.Instance != null
                ? MultiplayerSessionOrchestrator.Instance
                : FindAnyObjectByType<MultiplayerSessionOrchestrator>();
        }
    }

    /// <summary>
    /// 오브젝트가 활성화될 때 Join Code UI를 즉시 동기화합니다.
    /// </summary>
    private void OnEnable()
    {
        RefreshJoinCodeUi();
    }

    /// <summary>
    /// 현재 세션 상태를 기준으로 Town Join Code UI를 갱신합니다.
    /// </summary>
    public void RefreshJoinCodeUi()
    {
        bool isHosting = _multiplayerSessionOrchestrator != null && _multiplayerSessionOrchestrator.IsHosting; // 현재 피어가 Host인지 판정한 결과입니다.
        string joinCode = isHosting && !string.IsNullOrWhiteSpace(_multiplayerSessionOrchestrator.ActiveJoinCode)
            ? _multiplayerSessionOrchestrator.ActiveJoinCode
            : _emptyJoinCodeText; // UI에 최종 표시할 Join Code 문자열입니다.

        if (_hideRootWhenNotHosting && _hostOnlyRoot != null)
        {
            _hostOnlyRoot.SetActive(isHosting);
        }

        if (_joinCodeText != null)
        {
            _joinCodeText.text = joinCode;
        }
    }
}
