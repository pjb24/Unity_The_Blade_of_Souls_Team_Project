using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Client Join 실패 메시지의 실제 렌더링(텍스트/닫기/자동 닫힘)을 담당하는 View 컴포넌트입니다.
/// </summary>
public class MultiplayerJoinFailureView : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("실패 메시지 패널 루트 오브젝트입니다.")]
    [SerializeField] private GameObject _messageRoot; // 메시지 표시/숨김을 제어할 루트 오브젝트 참조입니다.

    [Tooltip("실패 메시지 제목 TMP_Text입니다.")]
    [SerializeField] private TMP_Text _titleText; // 제목 문자열을 출력할 TMP 텍스트 참조입니다.

    [Tooltip("실패 메시지 본문 TMP_Text입니다.")]
    [SerializeField] private TMP_Text _bodyText; // 본문 문자열을 출력할 TMP 텍스트 참조입니다.

    [Tooltip("수동 닫기 버튼 참조입니다.")]
    [SerializeField] private Button _closeButton; // 수동 닫기 입력을 처리할 버튼 참조입니다.

    [Header("Events")]
    [Tooltip("실패 메시지 패널이 닫힐 때 호출할 이벤트입니다.")]
    [SerializeField] private UnityEvent _onClosed; // 패널 닫힘 시 외부 UI 후처리를 연결할 이벤트입니다.

    private Coroutine _autoCloseCoroutine; // 자동 닫힘 타이머를 관리하는 코루틴 핸들입니다.

    /// <summary>
    /// 닫기 버튼 이벤트를 초기화하고 기본 상태를 숨김으로 설정합니다.
    /// </summary>
    private void Awake()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(Hide);
        }

        HideImmediate();
    }

    /// <summary>
    /// 오브젝트가 파괴될 때 닫기 버튼 이벤트를 해제합니다.
    /// </summary>
    private void OnDestroy()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(Hide);
        }
    }

    /// <summary>
    /// 전달받은 표시 모델로 실패 메시지 UI를 렌더링합니다.
    /// </summary>
    public void Show(MultiplayerJoinFailurePresentationModel presentation)
    {
        ValidateReferences();

        if (_messageRoot != null)
        {
            _messageRoot.SetActive(true);
        }

        if (_titleText != null)
        {
            _titleText.gameObject.SetActive(presentation.UseTitle);
            _titleText.text = presentation.Title;
        }

        if (_bodyText != null)
        {
            _bodyText.text = presentation.Body;
        }

        if (_closeButton != null)
        {
            _closeButton.gameObject.SetActive(presentation.UseManualCloseButton);
        }

        StopAutoClose();
        if (presentation.AutoClose)
        {
            _autoCloseCoroutine = StartCoroutine(AutoCloseAfterDelay(presentation.AutoCloseDelaySeconds));
        }
    }

    /// <summary>
    /// 실패 메시지 패널을 닫고 닫힘 이벤트를 발행합니다.
    /// </summary>
    public void Hide()
    {
        StopAutoClose();

        if (_messageRoot != null)
        {
            _messageRoot.SetActive(false);
        }

        _onClosed?.Invoke();
    }

    /// <summary>
    /// 닫힘 이벤트를 호출하지 않고 즉시 패널을 숨깁니다.
    /// </summary>
    private void HideImmediate()
    {
        StopAutoClose();

        if (_messageRoot != null)
        {
            _messageRoot.SetActive(false);
        }
    }

    /// <summary>
    /// 자동 닫힘 코루틴을 안전하게 중지합니다.
    /// </summary>
    private void StopAutoClose()
    {
        if (_autoCloseCoroutine == null)
        {
            return;
        }

        StopCoroutine(_autoCloseCoroutine);
        _autoCloseCoroutine = null;
    }

    /// <summary>
    /// 지정한 시간 대기 후 메시지 패널을 닫습니다.
    /// </summary>
    private IEnumerator AutoCloseAfterDelay(float delaySeconds)
    {
        float safeDelay = Mathf.Max(0f, delaySeconds); // 음수 입력을 방지하기 위해 보정한 자동 닫힘 지연 시간입니다.
        if (safeDelay > 0f)
        {
            yield return new WaitForSeconds(safeDelay);
        }

        _autoCloseCoroutine = null;
        Hide();
    }

    /// <summary>
    /// 인스펙터 연결 누락을 사전에 감지해 경고 로그를 남깁니다.
    /// </summary>
    private void ValidateReferences()
    {
        if (_messageRoot == null)
        {
            Debug.LogWarning("[MultiplayerJoinFailureView] MessageRoot가 비어 있어 실패 메시지를 표시할 수 없습니다.", this);
        }

        if (_bodyText == null)
        {
            Debug.LogWarning("[MultiplayerJoinFailureView] BodyText가 비어 있어 실패 본문을 표시할 수 없습니다.", this);
        }
    }
}
