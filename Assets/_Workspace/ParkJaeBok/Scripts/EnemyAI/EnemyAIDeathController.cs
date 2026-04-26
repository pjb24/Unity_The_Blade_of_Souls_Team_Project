using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Enemy 사망 1회 진입, 사망 VFX, Die 애니메이션 대기, 최종 비활성화를 담당합니다.
/// </summary>
[DisallowMultipleComponent]
public class EnemyAIDeathController : NetworkBehaviour
{
    [Header("References")]
    [Tooltip("사망 연출 종료 이벤트를 받을 EnemyAnimationBridge 참조입니다. 비어 있으면 같은 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private EnemyAnimationBridge _animationBridge; // 사망 애니메이션 종료 이벤트를 수신할 브리지 참조입니다.
    [Tooltip("NGO 인스턴스에서 제거 복제 기준으로 사용할 NetworkObject 참조입니다. 비어 있으면 자동 탐색합니다.")]
    [SerializeField] private NetworkObject _networkObject; // 멀티플레이 제거 복제 기준으로 사용할 NetworkObject 참조입니다.

    [Header("Death VFX")]
    [Tooltip("사망 시 EffectService로 재생할 이펙트 ID입니다.")]
    [SerializeField] private E_EffectId _deathEffectId = E_EffectId.EnemyDeath; // 사망 시 재생할 이펙트 식별자입니다.
    [Tooltip("사망 VFX를 생성할 위치 기준 Transform입니다. 비어 있으면 본인 Transform을 사용합니다.")]
    [SerializeField] private Transform _deathVfxSpawnPoint; // 사망 VFX 스폰 위치 기준 Transform입니다.
    [Tooltip("사망 VFX를 강제로 정지하기까지의 지연 시간(초)입니다.")]
    [SerializeField] private float _vfxReleaseDelay = 1.2f; // 사망 VFX 강제 정지까지의 지연 시간입니다.

    [Header("Death Exit")]
    [Tooltip("Die 애니메이션 종료 이벤트를 우선 사용해 비활성화를 결정할지 여부입니다.")]
    [SerializeField] private bool _useDeathAnimationEventAsPrimaryExit = true; // Die 애니메이션 종료 이벤트를 비활성화 기준으로 사용할지 여부입니다.
    [Tooltip("Die 애니메이션 이벤트가 없을 때 fallback으로 대기할 최소 지연 시간(초)입니다.")]
    [SerializeField] private float _fallbackHideDelay = 1.2f; // Die 애니메이션 이벤트가 없을 때 fallback으로 대기할 지연 시간입니다.
    [Tooltip("true면 제거 시 GameObject를 Destroy하고, false면 SetActive(false)로 비활성화합니다.")]
    [SerializeField] private bool _destroyOnRemove = false; // 최종 제거 시 Destroy를 사용할지 여부입니다.
    [Tooltip("Die 애니메이션 이벤트가 없어서 fallback 지연으로 종료된 경우 경고 로그를 출력할지 여부입니다.")]
    [SerializeField] private bool _warnWhenDeathAnimationEventMissing = true; // Die 애니메이션 이벤트 누락 경고 출력 여부입니다.

    private bool _hasEnteredDeath; // Death 상태 1회 진입 보장 플래그입니다.
    private bool _isRemovalInProgress; // 제거 코루틴 진행 여부입니다.
    private bool _isRemoved; // 제거 완료 여부입니다.
    private bool _receivedDeathAnimationFinished; // Die 애니메이션 종료 이벤트 수신 여부입니다.
    private bool _hasWarnedDeathAnimationFallback; // Die 애니메이션 이벤트 누락 경고 중복 출력을 막는 플래그입니다.

    private EffectHandle _deathVfxHandle; // 현재 재생 중인 사망 VFX 핸들입니다.
    private Coroutine _removeRoutine; // 제거 시퀀스 코루틴 핸들입니다.

    /// <summary>
    /// Death 상태 1회 진입 여부를 반환합니다.
    /// </summary>
    public bool HasEnteredDeath => _hasEnteredDeath;

    /// <summary>
    /// 제거 진행 중 여부를 반환합니다.
    /// </summary>
    public bool IsRemovalInProgress => _isRemovalInProgress;

    /// <summary>
    /// 제거 완료 여부를 반환합니다.
    /// </summary>
    public bool IsRemoved => _isRemoved;

    /// <summary>
    /// 런타임 참조를 자동으로 보정합니다.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
    }

    /// <summary>
    /// 활성화 시 사망 애니메이션 종료 이벤트를 구독합니다.
    /// </summary>
    private void OnEnable()
    {
        ResolveReferences();
        SubscribeAnimationSignals();
    }

    /// <summary>
    /// 비활성화 시 제거 코루틴, VFX, 애니메이션 이벤트 구독을 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        UnsubscribeAnimationSignals();

        if (_removeRoutine != null)
        {
            StopCoroutine(_removeRoutine);
            _removeRoutine = null;
        }

        if (_deathVfxHandle != null && _deathVfxHandle.IsValid)
        {
            _deathVfxHandle.Stop();
        }
    }

    /// <summary>
    /// 인스펙터 값 변경 시 설정과 참조를 보정합니다.
    /// </summary>
    private void OnValidate()
    {
        if (_vfxReleaseDelay < 0f)
        {
            Debug.LogWarning($"[EnemyAIDeathController] Invalid _vfxReleaseDelay({_vfxReleaseDelay}) on {name}. Fallback to 0.");
            _vfxReleaseDelay = 0f;
        }

        if (_fallbackHideDelay < 0f)
        {
            Debug.LogWarning($"[EnemyAIDeathController] Invalid _fallbackHideDelay({_fallbackHideDelay}) on {name}. Fallback to 0.");
            _fallbackHideDelay = 0f;
        }

        if (_deathVfxSpawnPoint == null)
        {
            Debug.LogWarning($"[EnemyAIDeathController] _deathVfxSpawnPoint is null on {name}. Fallback to actor transform.");
        }

        ResolveReferences();
    }

    /// <summary>
    /// Death 진입을 1회만 수행하고 제거 시퀀스를 예약합니다.
    /// </summary>
    public bool TryEnterDeath()
    {
        if (_hasEnteredDeath)
        {
            return false;
        }

        _hasEnteredDeath = true;
        _receivedDeathAnimationFinished = false;
        _hasWarnedDeathAnimationFallback = false;
        PlayDeathVfxOnce();

        if (!_isRemovalInProgress)
        {
            _removeRoutine = StartCoroutine(RemoveSequenceRoutine());
        }

        return true;
    }

    /// <summary>
    /// 사망 런타임 상태를 초기화합니다.
    /// </summary>
    public void ResetRuntime()
    {
        _hasEnteredDeath = false;
        _isRemovalInProgress = false;
        _isRemoved = false;
        _receivedDeathAnimationFinished = false;
        _hasWarnedDeathAnimationFallback = false;

        if (_removeRoutine != null)
        {
            StopCoroutine(_removeRoutine);
            _removeRoutine = null;
        }

        if (_deathVfxHandle != null && _deathVfxHandle.IsValid)
        {
            _deathVfxHandle.Stop();
        }
    }

    /// <summary>
    /// 현재 Enemy 루트 오브젝트를 즉시 비활성화하거나 파괴합니다.
    /// </summary>
    public void HideGameObject()
    {
        if (_destroyOnRemove)
        {
            if (gameObject != null)
            {
                Destroy(gameObject);
            }

            return;
        }

        if (!gameObject.activeSelf)
        {
            return;
        }

        gameObject.SetActive(false);
    }

    /// <summary>
    /// 사망 애니메이션 종료 이벤트를 수신합니다.
    /// </summary>
    public void NotifyDeathAnimationFinished()
    {
        _receivedDeathAnimationFinished = true;
    }

    /// <summary>
    /// 런타임 참조를 자동으로 해석합니다.
    /// </summary>
    private void ResolveReferences()
    {
        if (_animationBridge == null)
        {
            _animationBridge = GetComponent<EnemyAnimationBridge>();
        }

        if (_networkObject == null)
        {
            _networkObject = GetComponent<NetworkObject>();
        }
    }

    /// <summary>
    /// 사망 애니메이션 종료 이벤트를 구독합니다.
    /// </summary>
    private void SubscribeAnimationSignals()
    {
        if (_animationBridge == null)
        {
            return;
        }

        _animationBridge.DeathAnimationFinished -= HandleDeathAnimationFinished;
        _animationBridge.DeathAnimationFinished += HandleDeathAnimationFinished;
    }

    /// <summary>
    /// 사망 애니메이션 종료 이벤트 구독을 해제합니다.
    /// </summary>
    private void UnsubscribeAnimationSignals()
    {
        if (_animationBridge == null)
        {
            return;
        }

        _animationBridge.DeathAnimationFinished -= HandleDeathAnimationFinished;
    }

    /// <summary>
    /// 사망 애니메이션 종료 이벤트를 런타임 플래그로 반영합니다.
    /// </summary>
    private void HandleDeathAnimationFinished()
    {
        NotifyDeathAnimationFinished();
    }

    /// <summary>
    /// 사망 VFX를 1회만 재생합니다.
    /// </summary>
    private void PlayDeathVfxOnce()
    {
        if (EffectService.Instance == null)
        {
            Debug.LogWarning($"[EnemyAIDeathController] EffectService is missing on death enter. target={name}");
            return;
        }

        Vector3 spawnPosition = _deathVfxSpawnPoint != null ? _deathVfxSpawnPoint.position : transform.position;
        _deathVfxHandle = EffectService.Instance.Play(_deathEffectId, spawnPosition);

        if (_deathVfxHandle == null || !_deathVfxHandle.IsValid)
        {
            Debug.LogWarning($"[EnemyAIDeathController] Death VFX handle is invalid. effectId={_deathEffectId}, target={name}");
        }
    }

    /// <summary>
    /// 사망 연출 종료 조건을 기다린 뒤 관찰자와 로컬 인스턴스 모두에 최종 제거를 반영합니다.
    /// </summary>
    private IEnumerator RemoveSequenceRoutine()
    {
        _isRemovalInProgress = true;

        float deathEnteredAt = Time.time; // 사망 연출 시작 시각입니다.
        while (!CanFinishDeathSequence(deathEnteredAt))
        {
            yield return null;
        }

        if (_deathVfxHandle != null && _deathVfxHandle.IsValid)
        {
            _deathVfxHandle.Stop();
        }

        _isRemoved = true;
        _isRemovalInProgress = false;
        _removeRoutine = null;

        if (EnemyNetworkAuthorityUtility.ShouldReplicateFromServer(_networkObject))
        {
            HideOnObserversRpc();
        }

        HideGameObject();
    }

    /// <summary>
    /// 사망 연출 종료 조건을 만족했는지 판정합니다.
    /// </summary>
    private bool CanFinishDeathSequence(float deathEnteredAt)
    {
        float requiredDelay = Mathf.Max(_vfxReleaseDelay, _fallbackHideDelay);
        bool fallbackDelayExpired = Time.time - deathEnteredAt >= requiredDelay;
        if (!_useDeathAnimationEventAsPrimaryExit)
        {
            return fallbackDelayExpired;
        }

        if (_receivedDeathAnimationFinished)
        {
            return true;
        }

        if (fallbackDelayExpired && _warnWhenDeathAnimationEventMissing && !_hasWarnedDeathAnimationFallback)
        {
            Debug.LogWarning($"[EnemyAIDeathController] Death finished by fallback delay on {name}. Add AnimationEvent_DeathFinished for precise sync.", this);
            _hasWarnedDeathAnimationFallback = true;
        }

        return fallbackDelayExpired;
    }

    /// <summary>
    /// 서버가 확정한 제거 결과를 관찰자 인스턴스에도 적용합니다.
    /// </summary>
    [Rpc(SendTo.NotServer)]
    private void HideOnObserversRpc()
    {
        HideGameObject();
    }
}
