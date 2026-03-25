using UnityEngine;

/// <summary>
/// 강제 종료/수명주기 이벤트 대응 저장 기준을 정의하는 ScriptableObject입니다.
/// </summary>
[CreateAssetMenu(fileName = "ForceClosePolicy", menuName = "Game/Save System/Force Close Policy")]
public class ForceClosePolicy : ScriptableObject
{
    [Header("Scene Transition")]
    [Tooltip("씬 전환 직전에 Persistent 채널 저장을 수행할지 여부입니다.")]
    [SerializeField] private bool _savePersistentOnSceneTransition = true; // 씬 전환 직전 영구 저장 실행 여부입니다.

    [Tooltip("씬 전환 직전에 Session 채널 저장을 수행할지 여부입니다.")]
    [SerializeField] private bool _saveSessionOnSceneTransition = true; // 씬 전환 직전 세션 저장 실행 여부입니다.

    [Header("Periodic")]
    [Tooltip("주기 저장 기능을 사용할지 여부입니다.")]
    [SerializeField] private bool _usePeriodicSave = true; // 주기 저장 루프 사용 여부입니다.

    [Tooltip("주기 저장 간격(초)입니다.")]
    [SerializeField] private float _periodicSaveIntervalSeconds = 20f; // 주기 저장 루프의 실행 간격(초)입니다.

    [Header("Lifecycle")]
    [Tooltip("앱 일시정지(OnApplicationPause)에서 Persistent 저장을 시도할지 여부입니다.")]
    [SerializeField] private bool _savePersistentOnPause = true; // 일시정지 이벤트에서 영구 저장을 수행할지 여부입니다.

    [Tooltip("포커스 해제(OnApplicationFocus false)에서 Session 저장을 시도할지 여부입니다.")]
    [SerializeField] private bool _saveSessionOnFocusLost = true; // 포커스 손실 이벤트에서 세션 저장을 수행할지 여부입니다.

    [Tooltip("앱 종료(OnApplicationQuit)에서 Persistent 저장을 시도할지 여부입니다.")]
    [SerializeField] private bool _savePersistentOnQuit = true; // 종료 이벤트에서 영구 저장을 수행할지 여부입니다.

    public bool SavePersistentOnSceneTransition => _savePersistentOnSceneTransition;
    public bool SaveSessionOnSceneTransition => _saveSessionOnSceneTransition;
    public bool UsePeriodicSave => _usePeriodicSave;
    public float PeriodicSaveIntervalSeconds => Mathf.Max(1f, _periodicSaveIntervalSeconds);
    public bool SavePersistentOnPause => _savePersistentOnPause;
    public bool SaveSessionOnFocusLost => _saveSessionOnFocusLost;
    public bool SavePersistentOnQuit => _savePersistentOnQuit;
}
