using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Bootstrap Scene 진입 직후 Title Scene 로드를 1회 트리거하는 컴포넌트입니다.
/// </summary>
public sealed class BootstrapTitleSceneLoader : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("씬 전환 실행에 사용할 SceneTransitionService 참조입니다. 비어 있으면 SceneTransitionService.Instance를 사용합니다.")]
    [SerializeField] private SceneTransitionService _sceneTransitionService; // Bootstrap에서 Title 로드 요청을 보낼 씬 전환 서비스 참조입니다.

    [Header("Scene Config")]
    [Tooltip("Bootstrap 완료 후 로드할 Title Scene 이름입니다.")]
    [SerializeField] private string _titleSceneName = "Title"; // Bootstrap 이후 진입할 타이틀 씬 이름입니다.

    [Tooltip("Start 시점에 자동으로 Title Scene 로드 요청을 보낼지 여부입니다.")]
    [SerializeField] private bool _loadOnStart = true; // Bootstrap 시작 직후 자동 로드를 수행할지 제어하는 플래그입니다.

    [Tooltip("이미 대상 씬이 활성화된 경우 로드 요청을 생략할지 여부입니다.")]
    [SerializeField] private bool _skipIfAlreadyActive = true; // 활성 씬이 이미 Title일 때 중복 요청을 막기 위한 가드입니다.

    [Tooltip("중복 로드 요청을 전역으로 한 번만 허용할지 여부입니다.")]
    [SerializeField] private bool _loadOnlyOncePerPlaySession = true; // 동일 플레이 세션에서 Bootstrap 중복 진입 시 중복 전환을 방지하는 플래그입니다.

    private static bool s_hasLoadedTitleInSession; // 플레이 세션 동안 Title 로드 요청이 이미 발생했는지 추적하는 정적 플래그입니다.

    /// <summary>
    /// 컴포넌트 시작 시 자동 로드 옵션이 활성화된 경우 Title 로드를 시도합니다.
    /// </summary>
    private void Start()
    {
        if (_loadOnStart)
        {
            TryLoadTitleScene("BootstrapTitleSceneLoader.Start");
        }
    }

    /// <summary>
    /// 수동 호출 또는 자동 호출로 Title Scene 로드를 1회 시도합니다.
    /// </summary>
    public bool TryLoadTitleScene(string reason = "Manual")
    {
        if (string.IsNullOrWhiteSpace(_titleSceneName))
        {
            Debug.LogWarning("[BootstrapTitleSceneLoader] titleSceneName이 비어 있어 로드를 중단합니다.", this);
            return false;
        }

        if (_loadOnlyOncePerPlaySession && s_hasLoadedTitleInSession)
        {
            Debug.Log($"[BootstrapTitleSceneLoader] 이미 Title 로드를 수행해 요청을 건너뜁니다. reason={reason}", this);
            return false;
        }

        string activeSceneName = SceneManager.GetActiveScene().name; // 현재 활성 씬 이름입니다.
        if (_skipIfAlreadyActive && string.Equals(activeSceneName, _titleSceneName, System.StringComparison.Ordinal))
        {
            Debug.Log($"[BootstrapTitleSceneLoader] 활성 씬이 이미 Title이라 요청을 건너뜁니다. scene={activeSceneName}", this);
            return false;
        }

        SceneTransitionService transitionService = ResolveSceneTransitionService(); // 실제 로드 요청에 사용할 전환 서비스입니다.
        if (transitionService == null)
        {
            Debug.LogWarning("[BootstrapTitleSceneLoader] SceneTransitionService를 찾지 못해 로드를 중단합니다.", this);
            return false;
        }

        bool started = transitionService.TryLoadScene(_titleSceneName);
        if (!started)
        {
            Debug.LogWarning($"[BootstrapTitleSceneLoader] Title 로드 요청 시작에 실패했습니다. scene={_titleSceneName}, reason={reason}", this);
            return false;
        }

        if (_loadOnlyOncePerPlaySession)
        {
            s_hasLoadedTitleInSession = true;
        }

        Debug.Log($"[BootstrapTitleSceneLoader] Title 로드 요청을 시작했습니다. scene={_titleSceneName}, reason={reason}", this);
        return true;
    }

    /// <summary>
    /// 직렬화 참조 또는 싱글톤에서 SceneTransitionService를 해석합니다.
    /// </summary>
    private SceneTransitionService ResolveSceneTransitionService()
    {
        if (_sceneTransitionService != null)
        {
            return _sceneTransitionService;
        }

        return SceneTransitionService.Instance;
    }
}
