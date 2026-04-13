using System;
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

    [Tooltip("비어 있지 않으면 컴포넌트 타입 이름에 이 문자열이 포함된 Cinemachine 컴포넌트만 바인딩 대상으로 사용합니다.")]
    [SerializeField] private string _componentTypeNameFilter; // 카메라 컴포넌트 탐색 시 타입 이름 필터링에 사용할 문자열입니다.

    [Header("Debug")]
    [Tooltip("카메라 바인딩 상세 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _verboseLogging; // 카메라 바인딩 성공/실패 상세 로그 출력 여부를 제어하는 플래그입니다.

    private bool _isSceneLoadedHookRegistered; // sceneLoaded 콜백 등록 상태를 추적하는 런타임 플래그입니다.

    /// <summary>
    /// 네트워크 스폰 이후 로컬 소유자일 때만 카메라 타깃 바인딩을 수행합니다.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            return;
        }

        BindCameraToOwnerTarget();

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
        UnregisterSceneLoadedHook();
    }

    /// <summary>
    /// 씬 로드 후 로컬 소유자 기준으로 카메라 타깃 재바인딩을 수행합니다.
    /// </summary>
    private void HandleSceneLoaded(Scene _, LoadSceneMode __)
    {
        if (!IsOwner)
        {
            return;
        }

        BindCameraToOwnerTarget();
    }

    /// <summary>
    /// 현재 씬의 Cinemachine 관련 컴포넌트를 탐색해 로컬 플레이어 타깃으로 바인딩합니다.
    /// </summary>
    private void BindCameraToOwnerTarget()
    {
        Transform target = _cameraTarget != null ? _cameraTarget : transform; // 카메라 추적 대상으로 사용할 최종 Transform 참조입니다.
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None); // 씬에서 탐색한 MonoBehaviour 후보 목록입니다.

        int boundComponentCount = 0; // 이번 호출에서 실제 타깃 바인딩이 적용된 컴포넌트 수입니다.
        for (int index = 0; index < behaviours.Length; index++)
        {
            MonoBehaviour behaviour = behaviours[index]; // 현재 검사 중인 MonoBehaviour 후보입니다.
            if (behaviour == null)
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
                boundComponentCount++;
            }
        }

        if (_verboseLogging)
        {
            Debug.Log($"[PlayerCameraBinder] Bound components={boundComponentCount}, owner={OwnerClientId}, target={target.name}", this);
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
}
