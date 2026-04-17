using UnityEngine;

/// <summary>
/// MultiplayerSessionOrchestrator의 Client Join 실패 이벤트를 수신해 View에 표시하는 Presenter입니다.
/// </summary>
public class MultiplayerJoinFailurePresenter : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Client Join 실패 이벤트를 발행하는 오케스트레이터 참조입니다. 비어 있으면 런타임에서 자동 탐색합니다.")]
    [SerializeField] private MultiplayerSessionOrchestrator _multiplayerSessionOrchestrator; // 실패 이벤트를 구독할 세션 오케스트레이터 참조입니다.

    [Tooltip("실패 메시지를 렌더링할 View 컴포넌트 참조입니다.")]
    [SerializeField] private MultiplayerJoinFailureView _view; // 실패 메시지 UI 표시를 수행할 뷰 참조입니다.

    [Tooltip("실패 유형별 문구/표시 정책 ScriptableObject 카탈로그입니다.")]
    [SerializeField] private MultiplayerJoinFailureMessageCatalog _messageCatalog; // 실패 유형별 메시지 정책을 제공하는 카탈로그 참조입니다.

    private MultiplayerJoinFailurePresentationMapper _presentationMapper; // 실패 결과를 표시 모델로 변환하는 매퍼 인스턴스입니다.

    /// <summary>
    /// 의존성을 보정하고 메시지 매퍼를 초기화합니다.
    /// </summary>
    private void Awake()
    {
        if (_multiplayerSessionOrchestrator == null)
        {
            _multiplayerSessionOrchestrator = MultiplayerSessionOrchestrator.Instance != null
                ? MultiplayerSessionOrchestrator.Instance
                : FindAnyObjectByType<MultiplayerSessionOrchestrator>();
        }

        _presentationMapper = new MultiplayerJoinFailurePresentationMapper(_messageCatalog);
    }

    /// <summary>
    /// 활성화 시 Client Join 실패 이벤트를 구독합니다.
    /// </summary>
    private void OnEnable()
    {
        if (_multiplayerSessionOrchestrator != null)
        {
            _multiplayerSessionOrchestrator.ClientJoinFailed += HandleClientJoinFailed;
            return;
        }

        Debug.LogWarning("[MultiplayerJoinFailurePresenter] MultiplayerSessionOrchestrator를 찾지 못해 실패 메시지 구독을 수행하지 못했습니다.", this);
    }

    /// <summary>
    /// 비활성화 시 이벤트 구독을 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        if (_multiplayerSessionOrchestrator != null)
        {
            _multiplayerSessionOrchestrator.ClientJoinFailed -= HandleClientJoinFailed;
        }
    }

    /// <summary>
    /// 오케스트레이터 실패 결과를 표시 모델로 변환해 View에 전달합니다.
    /// </summary>
    private void HandleClientJoinFailed(ClientJoinFailureResult failureResult)
    {
        if (_view == null)
        {
            Debug.LogWarning("[MultiplayerJoinFailurePresenter] View가 비어 있어 실패 메시지를 표시할 수 없습니다.", this);
            return;
        }

        MultiplayerJoinFailurePresentationModel presentation = _presentationMapper.Map(failureResult); // 실패 결과에서 생성한 UI 표시 모델입니다.
        _view.Show(presentation);
    }
}
