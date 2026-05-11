using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CameraEffect Fade/Overlay 색을 모든 씬 렌더링 위에 덮는 전역 UI 오버레이입니다.
/// </summary>
public sealed class CameraEffectScreenOverlay : MonoBehaviour
{
    private struct OverlayState
    {
        public int Priority; // 여러 효과가 동시에 오버레이를 요청할 때 선택 기준으로 사용하는 우선순위입니다.
        public int Sequence; // 같은 우선순위 요청 중 가장 최근 요청을 선택하기 위한 순번입니다.
        public Color Color; // 화면 전체에 덮을 최종 오버레이 색상입니다.
        public bool IsRetained; // 원본 효과가 종료된 뒤에도 유지 중인 오버레이 상태인지 여부입니다.
        public bool IsProtectedFromEffectCleanup; // 일반 CameraEffect 갱신이 자동 정리하지 않아야 하는 시스템 오버레이인지 여부입니다.
    }

    private static CameraEffectScreenOverlay _instance; // 런타임에서 자동 생성되어 유지되는 전역 오버레이 인스턴스입니다.

    [Tooltip("Fade/Overlay 색상을 실제로 그리는 전체 화면 Image입니다.")]
    [SerializeField] private Image _overlayImage; // 씬 오브젝트보다 위에 그려지는 전체 화면 UI Image입니다.

    [Tooltip("오버레이 Canvas가 다른 UI보다 위에 그려지도록 사용하는 Sorting Order입니다.")]
    [SerializeField] private int _sortingOrder = short.MaxValue; // 카메라 이펙트가 최상단에 보이도록 하는 Canvas 정렬 값입니다.

    private readonly Dictionary<int, OverlayState> _overlayStates = new Dictionary<int, OverlayState>(); // 효과 인스턴스별 오버레이 요청 상태입니다.
    private readonly List<int> _retainedRemovalBuffer = new List<int>(); // 새 오버레이 시작 시 이전 유지 상태를 정리하기 위한 임시 버퍼입니다.
    private int _nextSequence; // 오버레이 요청 순서를 추적하는 증가 값입니다.

    /// <summary>
    /// 전역 오버레이 인스턴스를 반환하고 없으면 자동 생성합니다.
    /// </summary>
    public static CameraEffectScreenOverlay Instance
    {
        get
        {
            if (_instance == null)
            {
                CreateRuntimeInstance();
            }

            return _instance;
        }
    }

    /// <summary>
    /// 지정한 효과 인스턴스의 오버레이 색상을 등록하거나 갱신합니다.
    /// </summary>
    public static void SetOverlay(int sourceId, int priority, Color color)
    {
        if (sourceId == 0)
        {
            return;
        }

        Instance.SetOverlayInternal(sourceId, priority, color, false);
    }

    /// <summary>
    /// 지정한 소스의 시스템 오버레이 색상을 등록하고 일반 CameraEffect 정리 정책에서 보호합니다.
    /// </summary>
    public static void SetProtectedOverlay(int sourceId, int priority, Color color)
    {
        if (sourceId == 0)
        {
            return;
        }

        Instance.SetOverlayInternal(sourceId, priority, color, true);
    }

    /// <summary>
    /// 지정한 효과 인스턴스가 등록한 오버레이 색상을 제거합니다.
    /// </summary>
    public static void ClearOverlay(int sourceId)
    {
        if (_instance == null || sourceId == 0)
        {
            return;
        }

        _instance.ClearOverlayInternal(sourceId);
    }

    /// <summary>
    /// 지정한 효과 인스턴스의 마지막 오버레이 색상을 효과 종료 후에도 유지 상태로 표시합니다.
    /// </summary>
    public static void RetainOverlay(int sourceId)
    {
        if (_instance == null || sourceId == 0)
        {
            return;
        }

        _instance.RetainOverlayInternal(sourceId);
    }

    /// <summary>
    /// 자동 생성된 Canvas와 Image 참조를 검증합니다.
    /// </summary>
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureOverlayImage();
        RefreshOverlayImage();
    }

    /// <summary>
    /// 파괴 시 전역 인스턴스 참조를 정리합니다.
    /// </summary>
    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    /// <summary>
    /// 런타임 전역 오버레이 GameObject를 생성합니다.
    /// </summary>
    private static void CreateRuntimeInstance()
    {
        GameObject overlayObject = new GameObject("--- Camera Effect Screen Overlay ---"); // 자동 생성되는 전역 화면 오버레이 오브젝트입니다.
        _instance = overlayObject.AddComponent<CameraEffectScreenOverlay>();
    }

    /// <summary>
    /// 전체 화면 Image를 포함한 Screen Space Overlay Canvas를 구성합니다.
    /// </summary>
    private void EnsureOverlayImage()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = _sortingOrder;

        CanvasScaler canvasScaler = GetComponent<CanvasScaler>();
        if (canvasScaler == null)
        {
            canvasScaler = gameObject.AddComponent<CanvasScaler>();
        }

        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.matchWidthOrHeight = 0.5f;

        if (_overlayImage == null)
        {
            GameObject imageObject = new GameObject("Camera Effect Overlay Image"); // 실제 Fade/Overlay 색상을 표시하는 자식 Image 오브젝트입니다.
            imageObject.transform.SetParent(transform, false);
            _overlayImage = imageObject.AddComponent<Image>();
        }

        _overlayImage.raycastTarget = false;
        RectTransform overlayRectTransform = _overlayImage.rectTransform;
        overlayRectTransform.anchorMin = Vector2.zero;
        overlayRectTransform.anchorMax = Vector2.one;
        overlayRectTransform.offsetMin = Vector2.zero;
        overlayRectTransform.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// 지정한 효과 인스턴스의 오버레이 상태를 저장하고 표시 색을 갱신합니다.
    /// </summary>
    private void SetOverlayInternal(int sourceId, int priority, Color color, bool isProtectedFromEffectCleanup)
    {
        RemoveRetainedOverlaysExcept(sourceId);

        OverlayState state = new OverlayState();
        state.Priority = priority;
        state.Sequence = _nextSequence++;
        state.Color = color;
        state.IsRetained = false;
        state.IsProtectedFromEffectCleanup = isProtectedFromEffectCleanup;

        _overlayStates[sourceId] = state;
        RefreshOverlayImage();
    }

    /// <summary>
    /// 지정한 효과 인스턴스의 오버레이 상태를 유지 상태로 전환합니다.
    /// </summary>
    private void RetainOverlayInternal(int sourceId)
    {
        if (!_overlayStates.TryGetValue(sourceId, out OverlayState state))
        {
            return;
        }

        state.IsRetained = true;
        state.Sequence = _nextSequence++;
        _overlayStates[sourceId] = state;
        RefreshOverlayImage();
    }

    /// <summary>
    /// 지정한 효과 인스턴스의 오버레이 상태를 제거하고 표시 색을 갱신합니다.
    /// </summary>
    private void ClearOverlayInternal(int sourceId)
    {
        if (!_overlayStates.Remove(sourceId))
        {
            return;
        }

        RefreshOverlayImage();
    }

    /// <summary>
    /// 새 Fade/Overlay 이펙트가 시작될 때 이전에 유지된 오버레이를 제거합니다.
    /// </summary>
    private void RemoveRetainedOverlaysExcept(int activeSourceId)
    {
        _retainedRemovalBuffer.Clear();

        foreach (KeyValuePair<int, OverlayState> overlayStatePair in _overlayStates)
        {
            if (overlayStatePair.Key == activeSourceId ||
                !overlayStatePair.Value.IsRetained ||
                overlayStatePair.Value.IsProtectedFromEffectCleanup)
            {
                continue;
            }

            _retainedRemovalBuffer.Add(overlayStatePair.Key);
        }

        for (int index = 0; index < _retainedRemovalBuffer.Count; index++)
        {
            _overlayStates.Remove(_retainedRemovalBuffer[index]);
        }

        _retainedRemovalBuffer.Clear();
    }

    /// <summary>
    /// 현재 등록된 오버레이 요청 중 가장 우선순위가 높은 색상을 화면에 반영합니다.
    /// </summary>
    private void RefreshOverlayImage()
    {
        EnsureOverlayImage();

        if (_overlayStates.Count == 0)
        {
            _overlayImage.enabled = false;
            _overlayImage.color = Color.clear;
            return;
        }

        OverlayState selectedState = default;
        bool hasSelectedState = false;

        foreach (OverlayState state in _overlayStates.Values)
        {
            if (!hasSelectedState ||
                state.Priority > selectedState.Priority ||
                state.Priority == selectedState.Priority && state.Sequence > selectedState.Sequence)
            {
                selectedState = state;
                hasSelectedState = true;
            }
        }

        _overlayImage.enabled = selectedState.Color.a > 0f;
        _overlayImage.color = selectedState.Color;
    }
}
