using UnityEngine;

/// <summary>
/// 캐릭터 단위의 VFX 재생/정지를 전담하는 컨트롤러입니다.
/// </summary>
[DisallowMultipleComponent]
public class CharacterVfxController : MonoBehaviour
{
    [Header("VFX Anchor Points")]
    [Tooltip("EyeEffect의 기준 위치 Transform입니다. 비어 있으면 현재 오브젝트 Transform을 사용합니다.")]
    [SerializeField] private Transform _eyeVfxPoint; // EyeEffect를 배치/활성 제어할 기준 Transform입니다.
    [Tooltip("WalkDust/JumpDust의 기준 위치 Transform입니다. 비어 있으면 현재 오브젝트 Transform을 사용합니다.")]
    [SerializeField] private Transform _footVfxPoint; // 발 먼지 계열 VFX를 배치/활성 제어할 기준 Transform입니다.
    [Tooltip("HitEffect의 기준 위치 Transform입니다. 비어 있으면 현재 오브젝트 Transform을 사용합니다.")]
    [SerializeField] private Transform _hitVfxPoint; // 피격 이펙트를 배치할 기준 Transform입니다.
    [Tooltip("SwordEffect의 기준 위치 Transform입니다. 비어 있으면 현재 오브젝트 Transform을 사용합니다.")]
    [SerializeField] private Transform _swordTipVfxPoint; // 검 궤적 이펙트를 배치/활성 제어할 기준 Transform입니다.

    [Header("Persistent VFX Objects")]
    [Tooltip("버프 지속형 EyeEffect 오브젝트입니다. On/Off 또는 ParticleSystem Play/Stop으로 제어합니다.")]
    [SerializeField] private GameObject _eyeEffectObject; // EyeEffect를 켜고 끌 대상 오브젝트 참조입니다.
    [Tooltip("지면 상태 지속형 WalkDust 오브젝트입니다. On/Off 또는 ParticleSystem Play/Stop으로 제어합니다.")]
    [SerializeField] private GameObject _walkDustObject; // WalkDust를 켜고 끌 대상 오브젝트 참조입니다.
    [Tooltip("공격 중 지속형 SwordEffect 오브젝트입니다. On/Off 또는 ParticleSystem Play/Stop으로 제어합니다.")]
    [SerializeField] private GameObject _swordEffectObject; // SwordEffect를 켜고 끌 대상 오브젝트 참조입니다.

    [Header("OneShot Effect Ids")]
    [Tooltip("점프 성공 시 1회 재생할 JumpDust EffectId입니다.")]
    [SerializeField] private E_EffectId _jumpDustEffectId = E_EffectId.JumpDust; // 점프 성공 시 단발 재생할 이펙트 ID입니다.
    [Tooltip("피격 성공 시 1회 재생할 HitEffect EffectId입니다.")]
    [SerializeField] private E_EffectId _hitEffectId = E_EffectId.HitEffect; // 피격 성공 시 단발 재생할 이펙트 ID입니다.

    [Header("Fallback")]
    [Tooltip("지속형 오브젝트 참조가 누락되면 EffectService Attach 방식 폴백을 사용할지 여부입니다.")]
    [SerializeField] private bool _allowPersistentEffectServiceFallback = true; // 지속형 오브젝트 누락 시 EffectService 폴백 허용 여부입니다.
    [Tooltip("지속형 EyeEffect 오브젝트 누락 시 Attach로 재생할 EffectId입니다.")]
    [SerializeField] private E_EffectId _eyeFallbackEffectId = E_EffectId.EyeEffect; // EyeEffect 오브젝트 누락 시 사용할 서비스 폴백 ID입니다.
    [Tooltip("지속형 WalkDust 오브젝트 누락 시 Attach로 재생할 EffectId입니다.")]
    [SerializeField] private E_EffectId _walkFallbackEffectId = E_EffectId.WalkDust; // WalkDust 오브젝트 누락 시 사용할 서비스 폴백 ID입니다.
    [Tooltip("지속형 SwordEffect 오브젝트 누락 시 Attach로 재생할 EffectId입니다.")]
    [SerializeField] private E_EffectId _swordFallbackEffectId = E_EffectId.SwordEffect; // SwordEffect 오브젝트 누락 시 사용할 서비스 폴백 ID입니다.

    private EffectHandle _eyeFallbackHandle; // EyeEffect 서비스 폴백으로 재생한 핸들입니다.
    private EffectHandle _walkFallbackHandle; // WalkDust 서비스 폴백으로 재생한 핸들입니다.
    private EffectHandle _swordFallbackHandle; // SwordEffect 서비스 폴백으로 재생한 핸들입니다.

    private bool _isEyeEffectActive; // 현재 EyeEffect 활성 상태 캐시입니다.
    private bool _isWalkDustActive; // 현재 WalkDust 활성 상태 캐시입니다.
    private bool _isSwordEffectActive; // 현재 SwordEffect 활성 상태 캐시입니다.

    /// <summary>
    /// 에디터 값 변경 시 필수 참조 누락을 점검합니다.
    /// </summary>
    private void OnValidate()
    {
        if (_eyeVfxPoint == null)
        {
            Debug.LogWarning($"[CharacterVfxController] Eye VFX Point가 비어 있습니다. object={name}", this);
        }

        if (_footVfxPoint == null)
        {
            Debug.LogWarning($"[CharacterVfxController] Foot VFX Point가 비어 있습니다. object={name}", this);
        }

        if (_hitVfxPoint == null)
        {
            Debug.LogWarning($"[CharacterVfxController] Hit VFX Point가 비어 있습니다. object={name}", this);
        }

        if (_swordTipVfxPoint == null)
        {
            Debug.LogWarning($"[CharacterVfxController] Sword Tip VFX Point가 비어 있습니다. object={name}", this);
        }
    }

    /// <summary>
    /// 런타임 시작 시 지속형 이펙트를 기본 비활성 상태로 정리합니다.
    /// </summary>
    private void Awake()
    {
        SetEyeEffectActive(false);
        SetWalkDustActive(false);
        StopSwordEffect();
    }

    /// <summary>
    /// 비활성화 시 지속형 이펙트를 정지해 잔상 상태를 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        StopHandle(ref _eyeFallbackHandle);
        StopHandle(ref _walkFallbackHandle);
        StopHandle(ref _swordFallbackHandle);
    }

    /// <summary>
    /// EyeEffect를 활성/비활성 상태로 전환합니다.
    /// </summary>
    public void SetEyeEffectActive(bool isActive)
    {
        _isEyeEffectActive = isActive;

        if (_eyeEffectObject != null)
        {
            AlignPersistentObject(_eyeEffectObject, _eyeVfxPoint);
            SetPersistentObjectActive(_eyeEffectObject, isActive);
            return;
        }

        Debug.LogWarning($"[CharacterVfxController] EyeEffect 오브젝트 참조가 누락되었습니다. object={name}", this);
        HandlePersistentFallback(ref _eyeFallbackHandle, _eyeFallbackEffectId, ResolveAnchor(_eyeVfxPoint), isActive);
    }

    /// <summary>
    /// WalkDust를 활성/비활성 상태로 전환합니다.
    /// </summary>
    public void SetWalkDustActive(bool isActive)
    {
        _isWalkDustActive = isActive;

        if (_walkDustObject != null)
        {
            AlignPersistentObject(_walkDustObject, _footVfxPoint);
            SetPersistentObjectActive(_walkDustObject, isActive);
            return;
        }

        Debug.LogWarning($"[CharacterVfxController] WalkDust 오브젝트 참조가 누락되었습니다. object={name}", this);
        HandlePersistentFallback(ref _walkFallbackHandle, _walkFallbackEffectId, ResolveAnchor(_footVfxPoint), isActive);
    }

    /// <summary>
    /// 점프 성공 시 JumpDust를 1회 재생합니다.
    /// </summary>
    public void PlayJumpDust()
    {
        PlayJumpDustAt(ResolveAnchor(_footVfxPoint).position);
    }

    /// <summary>
    /// 지정된 위치에서 JumpDust를 1회 재생합니다.
    /// </summary>
    public void PlayJumpDustAt(Vector3 worldPosition)
    {
        PlayJumpDustWorldOneShot(worldPosition);
    }

    /// <summary>
    /// 기본 Hit 위치 기준으로 HitEffect를 1회 재생합니다.
    /// </summary>
    public void PlayHitEffect()
    {
        PlayOneShot(_hitEffectId, ResolveAnchor(_hitVfxPoint).position);
    }

    /// <summary>
    /// 지정된 위치에서 HitEffect를 1회 재생합니다.
    /// </summary>
    public void PlayHitEffectAt(Vector3 worldPosition)
    {
        PlayOneShot(_hitEffectId, worldPosition);
    }

    /// <summary>
    /// SwordEffect를 공격 시작 상태로 활성화합니다.
    /// </summary>
    public void StartSwordEffect()
    {
        _isSwordEffectActive = true;

        if (_swordEffectObject != null)
        {
            AlignPersistentObject(_swordEffectObject, _swordTipVfxPoint);
            SetPersistentObjectActive(_swordEffectObject, true);
            return;
        }

        Debug.LogWarning($"[CharacterVfxController] SwordEffect 오브젝트 참조가 누락되었습니다. object={name}", this);
        HandlePersistentFallback(ref _swordFallbackHandle, _swordFallbackEffectId, ResolveAnchor(_swordTipVfxPoint), true);
    }

    /// <summary>
    /// SwordEffect를 공격 종료 상태로 비활성화합니다.
    /// </summary>
    public void StopSwordEffect()
    {
        _isSwordEffectActive = false;

        if (_swordEffectObject != null)
        {
            AlignPersistentObject(_swordEffectObject, _swordTipVfxPoint);
            SetPersistentObjectActive(_swordEffectObject, false);
            return;
        }

        Debug.LogWarning($"[CharacterVfxController] SwordEffect 오브젝트 참조가 누락되었습니다. object={name}", this);
        HandlePersistentFallback(ref _swordFallbackHandle, _swordFallbackEffectId, ResolveAnchor(_swordTipVfxPoint), false);
    }

    /// <summary>
    /// Eye VFX 기준 월드 좌표를 반환합니다.
    /// </summary>
    public Vector3 GetEyeVfxWorldPosition()
    {
        return ResolveAnchor(_eyeVfxPoint).position;
    }

    /// <summary>
    /// Foot VFX 기준 월드 좌표를 반환합니다.
    /// </summary>
    public Vector3 GetFootVfxWorldPosition()
    {
        return ResolveAnchor(_footVfxPoint).position;
    }

    /// <summary>
    /// Hit VFX 기준 월드 좌표를 반환합니다.
    /// </summary>
    public Vector3 GetHitVfxWorldPosition()
    {
        return ResolveAnchor(_hitVfxPoint).position;
    }

    /// <summary>
    /// Sword Tip VFX 기준 월드 좌표를 반환합니다.
    /// </summary>
    public Vector3 GetSwordTipVfxWorldPosition()
    {
        return ResolveAnchor(_swordTipVfxPoint).position;
    }

    /// <summary>
    /// 지속형 오브젝트를 앵커 Transform 기준 위치/회전으로 정렬합니다.
    /// </summary>
    private void AlignPersistentObject(GameObject effectObject, Transform anchor)
    {
        Transform resolvedAnchor = ResolveAnchor(anchor);
        if (effectObject.transform.parent != resolvedAnchor)
        {
            effectObject.transform.SetParent(resolvedAnchor, false);
        }

        effectObject.transform.localPosition = Vector3.zero;
        effectObject.transform.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// 지속형 오브젝트와 하위 ParticleSystem 재생 상태를 함께 전환합니다.
    /// </summary>
    private void SetPersistentObjectActive(GameObject effectObject, bool isActive)
    {
        if (effectObject == null)
        {
            Debug.LogWarning($"[CharacterVfxController] SetPersistentObjectActive 대상이 null입니다. object={name}", this);
            return;
        }

        ParticleSystem[] particleSystems = effectObject.GetComponentsInChildren<ParticleSystem>(true); // 지속형 오브젝트에 포함된 파티클 목록입니다.
        TrailRenderer[] trailRenderers = effectObject.GetComponentsInChildren<TrailRenderer>(true); // 지속형 오브젝트에 포함된 트레일 렌더러 목록입니다.

        if (isActive)
        {
            effectObject.SetActive(true);
            ResetTrailRenderers(trailRenderers);
        }

        for (int index = 0; index < particleSystems.Length; index++)
        {
            ParticleSystem particle = particleSystems[index]; // 지속형 오브젝트의 현재 재생 상태를 전환할 파티클 참조입니다.
            if (particle == null)
            {
                continue;
            }

            if (isActive)
            {
                particle.Play(true);
            }
            else
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        if (!isActive)
        {
            ResetTrailRenderers(trailRenderers);
            effectObject.SetActive(false);
        }
    }

    /// <summary>
    /// 지속형 오브젝트 누락 시 EffectService Attach 폴백으로 활성/비활성을 처리합니다.
    /// </summary>
    private void HandlePersistentFallback(ref EffectHandle handle, E_EffectId effectId, Transform anchor, bool isActive)
    {
        if (!_allowPersistentEffectServiceFallback)
        {
            Debug.LogWarning($"[CharacterVfxController] 지속형 EffectService 폴백이 비활성화되어 있습니다. effectId={effectId}, object={name}", this);
            return;
        }

        if (EffectService.Instance == null)
        {
            Debug.LogWarning($"[CharacterVfxController] EffectService가 없어 지속형 폴백을 처리하지 못했습니다. effectId={effectId}, object={name}", this);
            return;
        }

        if (isActive)
        {
            if (handle != null && handle.IsValid)
            {
                return;
            }

            EffectRequest request = new EffectRequest();
            request.EffectId = effectId;
            request.PlayMode = E_EffectPlayMode.Attach;
            request.AttachTarget = anchor;
            request.Owner = gameObject;
            request.LocalOffset = Vector3.zero;
            request.AutoReturnOverrideEnabled = true;
            request.AutoReturn = false;
            request.LifetimeOverride = 0f;
            request.IgnoreDuplicateGuard = false;
            request.FacingDirection = E_EffectFacingDirection.UsePrefab;

            handle = EffectService.Instance.Play(request);
            if (handle == null || !handle.IsValid)
            {
                Debug.LogWarning($"[CharacterVfxController] 지속형 폴백 핸들이 유효하지 않습니다. effectId={effectId}, object={name}", this);
            }

            return;
        }

        StopHandle(ref handle);
    }

    /// <summary>
    /// 단발성 이펙트를 EffectService로 재생합니다.
    /// </summary>
    private void PlayOneShot(E_EffectId effectId, Vector3 worldPosition)
    {
        if (effectId == E_EffectId.None)
        {
            Debug.LogWarning($"[CharacterVfxController] OneShot EffectId가 None이라 재생을 건너뜁니다. object={name}", this);
            return;
        }

        if (EffectService.Instance == null)
        {
            Debug.LogWarning($"[CharacterVfxController] EffectService가 없어 OneShot 이펙트를 재생하지 못했습니다. effectId={effectId}, object={name}", this);
            return;
        }

        EffectService.Instance.Play(effectId, worldPosition);
    }

    /// <summary>
    /// JumpDust를 캐릭터 부착 없이 월드 좌표 고정 1회성으로 재생합니다.
    /// </summary>
    private void PlayJumpDustWorldOneShot(Vector3 worldPosition)
    {
        if (_jumpDustEffectId == E_EffectId.None)
        {
            Debug.LogWarning($"[CharacterVfxController] JumpDust EffectId가 None이라 재생을 건너뜁니다. object={name}", this);
            return;
        }

        if (EffectService.Instance == null)
        {
            Debug.LogWarning($"[CharacterVfxController] EffectService가 없어 JumpDust를 재생하지 못했습니다. object={name}", this);
            return;
        }

        EffectRequest request = new EffectRequest();
        request.EffectId = _jumpDustEffectId;
        request.PlayMode = E_EffectPlayMode.Spawn;
        request.Position = worldPosition;
        request.LocalOffset = Vector3.zero;
        request.FollowTarget = null;
        request.AttachTarget = null;
        request.Owner = null;
        request.AutoReturnOverrideEnabled = true;
        request.AutoReturn = true;
        request.LifetimeOverride = 0f;
        request.IgnoreDuplicateGuard = false;
        request.FacingDirection = E_EffectFacingDirection.UsePrefab;

        EffectService.Instance.Play(request);
    }

    /// <summary>
    /// 핸들이 유효한 경우 정지 후 참조를 해제합니다.
    /// </summary>
    private void StopHandle(ref EffectHandle handle)
    {
        if (handle != null && handle.IsValid)
        {
            handle.Stop();
        }

        handle = null;
    }

    /// <summary>
    /// null 앵커를 현재 Transform으로 보정하고 경고를 남깁니다.
    /// </summary>
    private Transform ResolveAnchor(Transform anchor)
    {
        if (anchor != null)
        {
            return anchor;
        }

        Debug.LogWarning($"[CharacterVfxController] 앵커 Transform이 비어 있어 현재 Transform으로 폴백합니다. object={name}", this);
        return transform;
    }

    /// <summary>
    /// 트레일 렌더러의 기존 궤적을 지우고 방출 상태를 재초기화합니다.
    /// </summary>
    private void ResetTrailRenderers(TrailRenderer[] trailRenderers)
    {
        if (trailRenderers == null)
        {
            return;
        }

        for (int index = 0; index < trailRenderers.Length; index++)
        {
            TrailRenderer trailRenderer = trailRenderers[index]; // 초기화할 현재 TrailRenderer 참조입니다.
            if (trailRenderer == null)
            {
                continue;
            }

            trailRenderer.emitting = false;
            trailRenderer.Clear();
            trailRenderer.emitting = true;
        }
    }
}
