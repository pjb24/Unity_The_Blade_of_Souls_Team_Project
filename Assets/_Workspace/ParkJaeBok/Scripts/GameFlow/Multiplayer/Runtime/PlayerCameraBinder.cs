using System;
using System.Collections;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 네트워크 소유권 기준으로 로컬 플레이어만 Cinemachine 카메라 타깃을 바인딩하는 컴포넌트입니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class PlayerCameraBinder : NetworkBehaviour
{
    [Header("Binding")]
    [Tooltip("카메라에 바인딩할 목표 Transform입니다. 비어 있으면 현재 오브젝트 Transform을 사용합니다.")]
    [SerializeField] private Transform _cameraTarget; // 로컬 플레이어 카메라 추적 대상으로 바인딩할 Transform 참조입니다.

    [Tooltip("Follow/TrackingTarget뿐 아니라 LookAt 계열 속성도 함께 바인딩할지 여부입니다.")]
    [SerializeField] private bool _bindLookAtProperties; // LookAt 관련 속성까지 함께 갱신할지 제어하는 플래그입니다.

    [Tooltip("씬 전환 후에도 로컬 소유자 기준으로 카메라 타깃 재바인딩을 수행할지 여부입니다.")]
    [SerializeField] private bool _rebindOnSceneLoaded = true; // 씬 로드 이벤트마다 카메라 타깃 재바인딩을 수행할지 제어하는 플래그입니다.

    [Tooltip("싱글플레이 모드에서 네트워크 스폰이 없는 경우에도 카메라 타깃 바인딩을 수행할지 여부입니다.")]
    [SerializeField] private bool _bindInSinglePlayerWithoutNetworkSpawn = true; // 단일 플레이 흐름에서 카메라 바인딩 폴백을 활성화할지 제어하는 플래그입니다.

    [Tooltip("비어 있지 않으면 컴포넌트 타입 이름에 이 문자열이 포함된 Cinemachine 컴포넌트만 바인딩 대상으로 사용합니다.")]
    [SerializeField] private string _componentTypeNameFilter; // 카메라 컴포넌트 탐색 시 타입 이름 필터링에 사용할 문자열입니다.

    [Tooltip("씬 로드 직후 카메라 오브젝트 생성 순서 차이를 흡수하기 위한 재바인딩 재시도 횟수입니다.")]
    [Min(1)]
    [SerializeField] private int _sceneRebindRetryCount = 30; // 씬 전환 직후 Cinemachine 카메라 준비 지연을 흡수할 재바인딩 재시도 횟수입니다.

    [Tooltip("씬 로드 직후 카메라 재바인딩 재시도 간격(초)입니다.")]
    [Min(0.01f)]
    [SerializeField] private float _sceneRebindRetryInterval = 0.1f; // 카메라 재바인딩 재시도 사이에 대기할 시간입니다.

    [Tooltip("켜져 있으면 현재 활성 씬에 있는 Cinemachine 컴포넌트만 바인딩합니다.")]
    [SerializeField] private bool _bindOnlyActiveSceneCameras = true; // 이전 씬 또는 DontDestroyOnLoad 카메라가 새 씬 바인딩을 가로채지 않도록 제한하는 설정입니다.

    [Tooltip("타겟을 바인딩한 뒤 Cinemachine의 이전 카메라 상태를 무효화해 체크포인트 이동 직후에도 즉시 새 타겟을 보게 합니다.")]
    [SerializeField] private bool _invalidateCinemachinePreviousStateOnBind = true; // 원거리 체크포인트 진입 시 이전 카메라 상태가 추적을 지연시키지 않도록 하는 설정입니다.

    [Header("Debug")]
    [Tooltip("카메라 바인딩 상세 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _verboseLogging; // 카메라 바인딩 성공/실패 상세 로그 출력 여부를 제어하는 플래그입니다.

    [Tooltip("디버그용: 가장 최근 바인딩 시도에서 카메라 타깃 바인딩 성공 여부입니다.")]
    [SerializeField] private bool _hasBoundCameraTarget; // 최근 바인딩 시도에서 최소 1개 Cinemachine 컴포넌트에 타깃 적용이 성공했는지 추적하는 디버그 값입니다.

    private bool _isSceneLoadedHookRegistered; // sceneLoaded 콜백 등록 상태를 추적하는 런타임 플래그입니다.
    private GameFlowController _cachedGameFlowController; // 싱글플레이 모드 판별에 사용할 GameFlowController 캐시 참조입니다.

    private Coroutine _sceneRebindRoutine; // 씬 로드 직후 진행 중인 카메라 재바인딩 코루틴입니다.

    /// <summary>
    /// 가장 최근 바인딩 시도에서 카메라 타깃 바인딩 성공 여부를 조회합니다.
    /// </summary>
    public bool HasBoundCameraTarget => _hasBoundCameraTarget;

    /// <summary>
    /// 네트워크 스폰 이전(singleplayer)에도 카메라 바인딩 폴백을 시도합니다.
    /// </summary>
    private void OnEnable()
    {
        if (!ShouldUseSinglePlayerFallbackBinding())
        {
            return;
        }

        StartCameraRebindRoutine();

        if (_rebindOnSceneLoaded)
        {
            RegisterSceneLoadedHook();
        }
    }

    /// <summary>
    /// 네트워크 스폰 이후 로컬 소유자일 때만 카메라 타깃 바인딩을 수행합니다.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            return;
        }

        StartCameraRebindRoutine();

        if (_rebindOnSceneLoaded)
        {
            RegisterSceneLoadedHook();
        }
    }

    /// <summary>
    /// 네트워크 디스폰 시 sceneLoaded 콜백을 정리합니다.
    /// </summary>
    public override void OnNetworkDespawn()
    {
        StopCameraRebindRoutine();
        UnregisterSceneLoadedHook();
    }

    /// <summary>
    /// 鍮꾪솢?깊솕 ??吏꾪뻾 以묒씤 移대찓???щ컮?몃뵫怨????濡쒕뱶 肄쒕갚???뺣━?⑸땲??
    /// </summary>
    private void OnDisable()
    {
        StopCameraRebindRoutine();
        UnregisterSceneLoadedHook();
    }

    /// <summary>
    /// 씬 로드 후 로컬 소유자 기준으로 카메라 타깃 재바인딩을 수행합니다.
    /// </summary>
    private void HandleSceneLoaded(Scene _, LoadSceneMode __)
    {
        if (IsSpawned)
        {
            if (!IsOwner)
            {
                return;
            }
        }
        else if (!ShouldUseSinglePlayerFallbackBinding())
        {
            return;
        }

        StartCameraRebindRoutine();
    }

    /// <summary>
    /// ???移대찓??以鍮???대컢???≪닔?섍린 ?꾪빐 移대찓???щ컮?몃뵫 猷⑦떞???쒖옉?⑸땲??
    /// </summary>
    private void StartCameraRebindRoutine()
    {
        StopCameraRebindRoutine();
        _sceneRebindRoutine = StartCoroutine(CameraRebindRoutine());
    }

    /// <summary>
    /// 吏꾪뻾 以묒씤 移대찓???щ컮?몃뵫 猷⑦떞??以묐떒?⑸땲??
    /// </summary>
    private void StopCameraRebindRoutine()
    {
        if (_sceneRebindRoutine == null)
        {
            return;
        }

        StopCoroutine(_sceneRebindRoutine);
        _sceneRebindRoutine = null;
    }

    /// <summary>
    /// Cinemachine 移대찓?쇨? ???꾪솚 吏곹썑 ?먮뒦寃??앹꽦?섎뒗 寃쎌슦源뚯? 濡쒖뺄 ?뚮젅?댁뼱 ?寃잛쓣 諛붿씤?⑺빀?덈떎.
    /// </summary>
    private IEnumerator CameraRebindRoutine()
    {
        int safeRetryCount = Mathf.Max(1, _sceneRebindRetryCount); // 移대찓???щ컮?몃뵫 ?ъ떆???잛닔???섑븳媛믪엯?덈떎.
        float safeRetryInterval = Mathf.Max(0.01f, _sceneRebindRetryInterval); // 移대찓???щ컮?몃뵫 ?ъ떆??媛꾧꺽???섑븳媛믪엯?덈떎.

        for (int retryIndex = 0; retryIndex < safeRetryCount; retryIndex++)
        {
            if (IsSpawned && !IsOwner)
            {
                _sceneRebindRoutine = null;
                yield break;
            }

            if (!IsSpawned && !ShouldUseSinglePlayerFallbackBinding())
            {
                _sceneRebindRoutine = null;
                yield break;
            }

            if (BindCameraToOwnerTarget())
            {
                _sceneRebindRoutine = null;
                yield break;
            }

            yield return new WaitForSecondsRealtime(safeRetryInterval);
        }

        if (_verboseLogging)
        {
            Debug.LogWarning($"[PlayerCameraBinder] Camera rebind retry failed. owner={OwnerClientId}, target={ResolveCameraTarget().name}", this);
        }

        _sceneRebindRoutine = null;
    }

    /// <summary>
    /// 현재 씬의 Cinemachine 관련 컴포넌트를 탐색해 로컬 플레이어 타깃으로 바인딩합니다.
    /// </summary>
    private bool BindCameraToOwnerTarget()
    {
        Transform target = ResolveCameraTarget(); // 카메라 추적 대상으로 사용할 최종 Transform 참조입니다.
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None); // 씬에서 탐색한 MonoBehaviour 후보 목록입니다.

        int boundComponentCount = 0; // 이번 호출에서 실제 타깃 바인딩이 적용된 컴포넌트 수입니다.
        for (int index = 0; index < behaviours.Length; index++)
        {
            MonoBehaviour behaviour = behaviours[index]; // 현재 검사 중인 MonoBehaviour 후보입니다.
            if (behaviour == null)
            {
                continue;
            }

            if (!CanBindCameraComponentInCurrentScene(behaviour))
            {
                continue;
            }

            Type behaviourType = behaviour.GetType(); // 후보 컴포넌트의 런타임 타입 정보입니다.
            if (!IsCinemachineType(behaviourType))
            {
                continue;
            }

            if (!PassesTypeFilter(behaviourType))
            {
                continue;
            }

            bool changed = false;
            changed |= TryAssignTransformMember(behaviour, "Follow", target);
            changed |= TryAssignTransformMember(behaviour, "FollowTarget", target);
            changed |= TryAssignTransformMember(behaviour, "TrackingTarget", target);

            if (_bindLookAtProperties)
            {
                changed |= TryAssignTransformMember(behaviour, "LookAt", target);
                changed |= TryAssignTransformMember(behaviour, "LookAtTarget", target);
            }

            if (changed)
            {
                InvalidateCinemachinePreviousState(behaviour);
                boundComponentCount++;
            }
        }

        if (_verboseLogging)
        {
            Debug.Log($"[PlayerCameraBinder] Bound components={boundComponentCount}, owner={OwnerClientId}, target={target.name}", this);
        }

        _hasBoundCameraTarget = boundComponentCount > 0;
        return _hasBoundCameraTarget;
    }

    /// <summary>
    /// Inspector에 지정된 타겟이 있으면 우선 사용하고, 없으면 플레이어 루트 Transform을 반환합니다.
    /// </summary>
    private Transform ResolveCameraTarget()
    {
        return _cameraTarget != null ? _cameraTarget : transform;
    }

    /// <summary>
    /// 현재 활성 씬의 Cinemachine 컴포넌트만 바인딩 대상으로 사용할지 판정합니다.
    /// </summary>
    private bool CanBindCameraComponentInCurrentScene(MonoBehaviour behaviour)
    {
        if (!_bindOnlyActiveSceneCameras || behaviour == null)
        {
            return true;
        }

        return behaviour.gameObject.scene == SceneManager.GetActiveScene();
    }

    /// <summary>
    /// Cinemachine의 이전 상태 캐시를 무효화해 체크포인트 진입 직후 새 타겟을 즉시 기준으로 사용하게 합니다.
    /// </summary>
    private void InvalidateCinemachinePreviousState(MonoBehaviour cinemachineComponent)
    {
        if (!_invalidateCinemachinePreviousStateOnBind || cinemachineComponent == null)
        {
            return;
        }

        Type componentType = cinemachineComponent.GetType(); // 이전 상태 플래그를 찾기 위한 Cinemachine 컴포넌트 타입입니다.
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        PropertyInfo propertyInfo = componentType.GetProperty("PreviousStateIsValid", flags);
        if (propertyInfo != null && propertyInfo.CanWrite && propertyInfo.PropertyType == typeof(bool))
        {
            propertyInfo.SetValue(cinemachineComponent, false);
            return;
        }

        FieldInfo fieldInfo = componentType.GetField("PreviousStateIsValid", flags);
        if (fieldInfo != null && fieldInfo.FieldType == typeof(bool))
        {
            fieldInfo.SetValue(cinemachineComponent, false);
        }
    }

    /// <summary>
    /// 대상 컴포넌트의 지정 멤버(Property/Field)에 Transform 값을 할당할 수 있으면 반영합니다.
    /// </summary>
    private bool TryAssignTransformMember(MonoBehaviour targetComponent, string memberName, Transform value)
    {
        if (targetComponent == null || value == null)
        {
            return false;
        }

        Type componentType = targetComponent.GetType(); // 리플렉션 조회에 사용할 컴포넌트 타입 정보입니다.
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic; // 멤버 탐색에 사용할 바인딩 옵션입니다.

        PropertyInfo propertyInfo = componentType.GetProperty(memberName, flags); // 동일 이름 프로퍼티 조회 결과입니다.
        if (propertyInfo != null && propertyInfo.CanWrite && typeof(Transform).IsAssignableFrom(propertyInfo.PropertyType))
        {
            propertyInfo.SetValue(targetComponent, value);
            return true;
        }

        FieldInfo fieldInfo = componentType.GetField(memberName, flags); // 동일 이름 필드 조회 결과입니다.
        if (fieldInfo != null && typeof(Transform).IsAssignableFrom(fieldInfo.FieldType))
        {
            fieldInfo.SetValue(targetComponent, value);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 대상 타입이 Cinemachine 네임스페이스/타입명 규칙을 만족하는지 판별합니다.
    /// </summary>
    private bool IsCinemachineType(Type type)
    {
        if (type == null)
        {
            return false;
        }

        string fullName = type.FullName ?? string.Empty; // Cinemachine 네임스페이스 판별에 사용할 전체 타입 이름입니다.
        return fullName.Contains("Cinemachine", StringComparison.Ordinal);
    }

    /// <summary>
    /// 사용자 지정 타입 이름 필터 조건을 충족하는지 판별합니다.
    /// </summary>
    private bool PassesTypeFilter(Type type)
    {
        if (type == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_componentTypeNameFilter))
        {
            return true;
        }

        string typeName = type.Name ?? string.Empty; // 필터 문자열 매칭에 사용할 간단 타입 이름입니다.
        return typeName.Contains(_componentTypeNameFilter, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// sceneLoaded 콜백을 등록해 씬 전환 시 재바인딩을 보장합니다.
    /// </summary>
    private void RegisterSceneLoadedHook()
    {
        if (_isSceneLoadedHookRegistered)
        {
            return;
        }

        SceneManager.sceneLoaded += HandleSceneLoaded;
        _isSceneLoadedHookRegistered = true;
    }

    /// <summary>
    /// sceneLoaded 콜백 등록을 해제합니다.
    /// </summary>
    private void UnregisterSceneLoadedHook()
    {
        if (!_isSceneLoadedHookRegistered)
        {
            return;
        }

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        _isSceneLoadedHookRegistered = false;
    }

    /// <summary>
    /// 네트워크 비활성(singleplayer) 환경에서 카메라 바인딩 폴백을 수행해도 되는지 판정합니다.
    /// </summary>
    private bool ShouldUseSinglePlayerFallbackBinding()
    {
        if (!_bindInSinglePlayerWithoutNetworkSpawn || IsSpawned)
        {
            return false;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            return false;
        }

        if (!TryResolveGameFlowController(out GameFlowController gameFlowController))
        {
            return false;
        }

        return gameFlowController.CurrentPlayMode == E_GamePlayMode.SinglePlayer;
    }

    /// <summary>
    /// 싱글플레이 모드 판별에 사용할 GameFlowController 참조를 해석합니다.
    /// </summary>
    private bool TryResolveGameFlowController(out GameFlowController gameFlowController)
    {
        if (_cachedGameFlowController == null)
        {
            _cachedGameFlowController = GameFlowController.Instance != null
                ? GameFlowController.Instance
                : FindAnyObjectByType<GameFlowController>();
        }

        gameFlowController = _cachedGameFlowController;
        return gameFlowController != null;
    }
}
