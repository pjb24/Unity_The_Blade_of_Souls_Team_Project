using TMPro;
using UnityEngine;

/// <summary>
/// Title 씬의 Join Code 입력 필드를 MultiplayerSessionOrchestrator에 연결하는 바인더입니다.
/// </summary>
[RequireComponent(typeof(TMP_InputField))]
public class TitleJoinCodeInputFieldBinder : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Join Code 입력 필드를 등록할 MultiplayerSessionOrchestrator 참조입니다. 비어 있으면 런타임에서 자동 탐색합니다.")]
    [SerializeField] private MultiplayerSessionOrchestrator _multiplayerSessionOrchestrator; // 입력 필드를 주입할 멀티 세션 오케스트레이터 참조입니다.

    [Tooltip("오브젝트에 붙은 TMP_InputField입니다. 비어 있으면 동일 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private TMP_InputField _joinCodeInputField; // 오케스트레이터에 등록할 Join Code 입력 필드 참조입니다.

    [Tooltip("비활성화 시 등록한 InputField 참조를 오케스트레이터에서 해제할지 여부입니다.")]
    [SerializeField] private bool _releaseInputFieldOnDisable = true; // 바인더 비활성화/파괴 시 참조 해제 수행 여부를 제어하는 플래그입니다.

    /// <summary>
    /// 의존성 참조를 보정합니다.
    /// </summary>
    private void Awake()
    {
        if (_joinCodeInputField == null)
        {
            _joinCodeInputField = GetComponent<TMP_InputField>();
        }

        if (_multiplayerSessionOrchestrator == null)
        {
            _multiplayerSessionOrchestrator = MultiplayerSessionOrchestrator.Instance != null
                ? MultiplayerSessionOrchestrator.Instance
                : FindAnyObjectByType<MultiplayerSessionOrchestrator>();
        }
    }

    /// <summary>
    /// 활성화 시 Join Code 입력 필드를 오케스트레이터에 등록합니다.
    /// </summary>
    private void OnEnable()
    {
        _multiplayerSessionOrchestrator?.SetUiJoinCodeInputField(_joinCodeInputField);
    }

    /// <summary>
    /// 비활성화 시 필요하면 오케스트레이터의 입력 필드 참조를 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        if (!_releaseInputFieldOnDisable)
        {
            return;
        }

        _multiplayerSessionOrchestrator?.ReleaseUiJoinCodeInputField(_joinCodeInputField);
    }
}
