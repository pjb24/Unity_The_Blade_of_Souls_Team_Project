using System.Collections;
using UnityEngine;

/// <summary>
/// Enemy의 사망 1회 진입 보장, 사망 VFX 1회 재생, 제거 예약/실행을 담당하는 컨트롤러입니다.
/// </summary>
[DisallowMultipleComponent]
public class EnemyAIDeathController : MonoBehaviour
{
    [Header("Death VFX")]
    [Tooltip("사망 시 EffectService로 재생할 이펙트 ID입니다.")]
    [SerializeField] private E_EffectId _deathEffectId = E_EffectId.EnemyDeath; // 사망 시 재생할 이펙트 식별자입니다.
    [Tooltip("사망 VFX를 생성할 위치 기준 Transform입니다. 비어 있으면 본인 Transform을 사용합니다.")]
    [SerializeField] private Transform _deathVfxSpawnPoint; // 사망 VFX 스폰 위치 기준 Transform입니다.
    [Tooltip("사망 VFX를 강제로 정지(풀 반환)하기 전까지의 지연 시간(초)입니다.")]
    [SerializeField] private float _vfxReleaseDelay = 1.2f; // 사망 VFX 릴리즈 지연 시간입니다.

    [Header("Despawn")]
    [Tooltip("본체 제거까지 대기할 시간(초)입니다.")]
    [SerializeField] private float _bodyRemoveDelay = 1.2f; // 본체 제거 지연 시간입니다.
    [Tooltip("true면 제거 시 GameObject를 Destroy하고, false면 SetActive(false)로 디스폰합니다.")]
    [SerializeField] private bool _destroyOnRemove = false; // 제거 시 파괴/비활성화 처리 모드입니다.

    private bool _hasEnteredDeath; // Death 상태 1회 진입 보장 플래그입니다.
    private bool _isRemovalInProgress; // 제거 코루틴 진행 중 여부입니다.
    private bool _isRemoved; // 제거 완료 여부입니다.

    private EffectHandle _deathVfxHandle; // 재생 중인 사망 VFX 핸들입니다.
    private Coroutine _removeRoutine; // 제거 지연 코루틴 핸들입니다.

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
    /// 에디터 값 변경 시 사망/제거 설정 제약을 보정합니다.
    /// </summary>
    private void OnValidate()
    {
        if (_vfxReleaseDelay < 0f)
        {
            Debug.LogWarning($"[EnemyAIDeathController] Invalid _vfxReleaseDelay({_vfxReleaseDelay}) on {name}. Fallback to 0.");
            _vfxReleaseDelay = 0f;
        }

        if (_bodyRemoveDelay < 0f)
        {
            Debug.LogWarning($"[EnemyAIDeathController] Invalid _bodyRemoveDelay({_bodyRemoveDelay}) on {name}. Fallback to 0.");
            _bodyRemoveDelay = 0f;
        }

        if (_deathVfxSpawnPoint == null)
        {
            Debug.LogWarning($"[EnemyAIDeathController] _deathVfxSpawnPoint is null on {name}. Fallback to actor transform.");
        }
    }

    /// <summary>
    /// 비활성화 시 진행 중인 코루틴을 정리하고 핸들을 안전하게 중지합니다.
    /// </summary>
    private void OnDisable()
    {
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
    /// Death 진입을 1회만 수행하고 제거 시퀀스를 예약합니다.
    /// </summary>
    public bool TryEnterDeath()
    {
        if (_hasEnteredDeath)
        {
            return false;
        }

        _hasEnteredDeath = true;
        PlayDeathVfxOnce();

        if (!_isRemovalInProgress)
        {
            _removeRoutine = StartCoroutine(RemoveSequenceRoutine());
        }

        return true;
    }

    /// <summary>
    /// 런타임 사망/제거 상태를 초기화합니다.
    /// </summary>
    public void ResetRuntime()
    {
        _hasEnteredDeath = false;
        _isRemovalInProgress = false;
        _isRemoved = false;

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
    /// 사망 VFX를 정확히 1회 재생합니다.
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
    /// VFX 릴리즈와 본체 제거를 지연 순서대로 처리합니다.
    /// </summary>
    private IEnumerator RemoveSequenceRoutine()
    {
        _isRemovalInProgress = true;

        if (_vfxReleaseDelay > 0f)
        {
            yield return new WaitForSeconds(_vfxReleaseDelay);
        }

        if (_deathVfxHandle != null && _deathVfxHandle.IsValid)
        {
            _deathVfxHandle.Stop();
        }

        float remainingBodyDelay = Mathf.Max(0f, _bodyRemoveDelay - _vfxReleaseDelay);
        if (remainingBodyDelay > 0f)
        {
            yield return new WaitForSeconds(remainingBodyDelay);
        }

        _isRemoved = true;
        _isRemovalInProgress = false;
        _removeRoutine = null;

        if (_destroyOnRemove)
        {
            Destroy(gameObject);
            yield break;
        }

        gameObject.SetActive(false);
    }
}
