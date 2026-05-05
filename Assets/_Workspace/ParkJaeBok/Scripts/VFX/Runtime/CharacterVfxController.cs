using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 캐릭터 단위의 VFX 재생/정지를 담당하는 컨트롤러입니다.
/// </summary>
[DisallowMultipleComponent]
public class CharacterVfxController : MonoBehaviour
{
    [System.Serializable]
    private struct AttackActionParticleMap
    {
        public string Name;

        [Tooltip("이 파티클 시스템을 재생할 공격 액션 타입입니다.")]
        public E_ActionType ActionType; // 파티클 시스템을 재생할 공격 액션 타입입니다.

        [Tooltip("해당 액션에서 Play할 파티클 시스템입니다. 비어 있으면 Auto Resolve Particle Name으로 자동 탐색합니다.")]
        public ParticleSystem ParticleSystem; // 액션 실행 시 직접 Play할 파티클 시스템 참조입니다.

        [Tooltip("ParticleSystem 참조가 비어 있을 때 자식 계층에서 자동 탐색할 파티클 시스템 이름입니다.")]
        public string AutoResolveParticleName; // 직접 참조가 없을 때 사용할 자식 파티클 자동 탐색 이름입니다.

        [Tooltip("파티클 시스템을 찾지 못했을 때 EffectService로 재생할 폴백 EffectId입니다.")]
        public E_EffectId FallbackEffectId; // 파티클 시스템 누락 시 사용할 일회성 폴백 이펙트 ID입니다.

        [Tooltip("이 공격 이펙트 전용 기준 위치 Transform입니다. 비어 있으면 공통 Attack VFX Point를 사용합니다.")]
        public Transform AnchorOverride; // 액션별 공격 이펙트 위치 기준으로 사용할 전용 앵커 참조입니다.

        [Tooltip("기준 위치 Transform에서 추가로 적용할 로컬 오프셋입니다.")]
        public Vector3 LocalOffset; // 액션별 공격 이펙트 세부 위치를 조정할 로컬 오프셋 값입니다.
    }

    private struct AttackParticleTransformState
    {
        public Vector3 LocalRotationEuler; // 공격 파티클 시스템의 기본 로컬 회전 복원용 캐시입니다.
        public Vector3 LocalScale; // 공격 파티클 시스템의 기본 로컬 스케일 복원용 캐시입니다.
    }

    private struct AnchorTransformState
    {
        public Vector3 LocalPosition; // 앵커의 우측 기준 기본 로컬 위치 캐시입니다.
        public Quaternion LocalRotation; // 앵커의 기본 로컬 회전 캐시입니다.
    }

    private struct PersistentObjectTransformState
    {
        public Vector3 LocalOffset; // 지속형 이펙트 오브젝트의 앵커 기준 기본 로컬 위치 오프셋 캐시입니다.
        public Vector3 LocalRotationEuler; // 지속형 이펙트 오브젝트의 앵커 기준 기본 로컬 회전 캐시입니다.
        public Vector3 LocalScale; // 지속형 이펙트 오브젝트의 기본 로컬 스케일 복원용 캐시입니다.
    }

    [Header("VFX Anchor Points")]
    [Tooltip("EyeEffect의 기준 위치 Transform입니다. 비어 있으면 현재 오브젝트 Transform을 사용합니다.")]
    [SerializeField] private Transform _eyeVfxPoint; // EyeEffect를 배치/재생할 기준 Transform입니다.
    [Tooltip("WalkDust/JumpDust의 기준 위치 Transform입니다. 비어 있으면 현재 오브젝트 Transform을 사용합니다.")]
    [SerializeField] private Transform _footVfxPoint; // 발 계열 VFX를 배치/재생할 기준 Transform입니다.
    [Tooltip("HitEffect의 기준 위치 Transform입니다. 비어 있으면 현재 오브젝트 Transform을 사용합니다.")]
    [SerializeField] private Transform _hitVfxPoint; // 피격 이펙트를 배치할 기준 Transform입니다.
    [Tooltip("공격 이펙트의 공통 기준 위치 Transform입니다. 비어 있으면 현재 오브젝트 Transform을 사용합니다.")]
    [SerializeField] private Transform _swordTipVfxPoint; // 공격 이펙트를 배치/재생할 공통 기준 Transform입니다.

    [Tooltip("EyeEffect가 Eye Anchor 기준에서 추가로 사용할 로컬 위치 오프셋입니다. 우측 기준으로 설정하며 좌측을 볼 때 X만 반전됩니다.")]
    [SerializeField] private Vector3 _eyeEffectLocalOffset = Vector3.zero; // EyeEffect를 Eye Anchor 기준으로 디자이너가 미세 조정할 로컬 위치 오프셋입니다.

    [Header("Facing")]
    [Tooltip("공격 이펙트의 좌우 방향을 우선 판정할 PlayerMovement 참조입니다. 비어 있으면 같은 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private PlayerMovement _playerMovement; // 공격 이펙트 좌우 방향 판정에 사용할 PlayerMovement 참조입니다.
    [Tooltip("PlayerMovement가 없을 때 localScale.x 부호로 좌우를 판정할 시각 루트 Transform입니다.")]
    [SerializeField] private Transform _facingVisualTarget; // 방향 판정 폴백으로 사용할 시각 루트 Transform 참조입니다.
    [Tooltip("PlayerMovement와 시각 루트가 모두 없을 때 flipX로 좌우를 판정할 SpriteRenderer입니다.")]
    [SerializeField] private SpriteRenderer _facingSpriteRenderer; // 마지막 방향 판정 폴백으로 사용할 SpriteRenderer 참조입니다.

    [Header("Persistent VFX Objects")]
    [Tooltip("버프 지속형 EyeEffect 오브젝트입니다. On/Off 또는 ParticleSystem Play/Stop으로 제어합니다.")]
    [SerializeField] private GameObject _eyeEffectObject; // EyeEffect를 켜고 끌 대상 오브젝트 참조입니다.
    [Tooltip("지면 상태 지속형 WalkDust 오브젝트입니다. On/Off 또는 ParticleSystem Play/Stop으로 제어합니다.")]
    [SerializeField] private GameObject _walkDustObject; // WalkDust를 켜고 끌 대상 오브젝트 참조입니다.

    [Header("Attack Effect Particle Systems")]
    [Tooltip("공격 액션별로 Play할 파티클 시스템 매핑입니다. 각 액션마다 서로 다른 기준 위치와 오프셋을 줄 수 있습니다.")]
    [SerializeField]
    private AttackActionParticleMap[] _attackActionParticleMaps =
    {
        new AttackActionParticleMap
        {
            Name = "AttackCombo1",
            ActionType = E_ActionType.AttackCombo1,
            ParticleSystem = null,
            AutoResolveParticleName = "AttackCombo1",
            FallbackEffectId = E_EffectId.AttackCombo1,
            AnchorOverride = null,
            LocalOffset = Vector3.zero
        },
        new AttackActionParticleMap
        {
            Name = "AttackCombo2",
            ActionType = E_ActionType.AttackCombo2,
            ParticleSystem = null,
            AutoResolveParticleName = "AttackCombo2",
            FallbackEffectId = E_EffectId.AttackCombo2,
            AnchorOverride = null,
            LocalOffset = Vector3.zero
        }
    }; // 공격 액션별 파티클 시스템, 폴백 ID, 위치 기준을 묶은 매핑입니다.

    [Header("OneShot Effect Ids")]
    [Tooltip("점프 성공 시 1회 재생할 JumpDust EffectId입니다.")]
    [SerializeField] private E_EffectId _jumpDustEffectId = E_EffectId.JumpDust; // 점프 성공 시 1회 재생할 이펙트 ID입니다.
    [Tooltip("피격 성공 시 1회 재생할 HitEffect EffectId입니다.")]
    [SerializeField] private E_EffectId _hitEffectId = E_EffectId.HitEffect; // 피격 성공 시 1회 재생할 이펙트 ID입니다.

    [Header("OneShot Pool Fallback Prefabs")]
    [Tooltip("JumpDust EffectId를 EffectService에서 재생하지 못할 때 LocalObjectPoolManager로 대체할 Jump VFX Prefab입니다.")]
    [SerializeField] private GameObject _jumpDustPoolPrefab; // JumpDust EffectService 실패 시 로컬 풀에서 대체할 Prefab입니다.
    [Tooltip("JumpDust Pool Prefab을 사용할 때 자동 반환까지 기다릴 시간(초)입니다.")]
    [Min(0.01f)]
    [SerializeField] private float _jumpDustPoolFallbackLifetime = 1.5f; // Jump VFX Pool Prefab 권장 생존 시간입니다.

    [Header("Fallback")]
    [Tooltip("지속형 오브젝트 참조가 누락되면 EffectService Attach 방식 폴백을 사용할지 여부입니다.")]
    [SerializeField] private bool _allowPersistentEffectServiceFallback = true; // 지속형 오브젝트 누락 시 EffectService 폴백 허용 여부입니다.
    [Tooltip("지속형 EyeEffect 오브젝트 누락 시 Attach로 재생할 EffectId입니다.")]
    [SerializeField] private E_EffectId _eyeFallbackEffectId = E_EffectId.EyeEffect; // EyeEffect 오브젝트 누락 시 사용할 폴백 ID입니다.
    [Tooltip("지속형 WalkDust 오브젝트 누락 시 Attach로 재생할 EffectId입니다.")]
    [SerializeField] private E_EffectId _walkFallbackEffectId = E_EffectId.WalkDust; // WalkDust 오브젝트 누락 시 사용할 폴백 ID입니다.

    private EffectHandle _eyeFallbackHandle; // EyeEffect 서비스 폴백으로 재생한 핸들입니다.
    private EffectHandle _walkFallbackHandle; // WalkDust 서비스 폴백으로 재생한 핸들입니다.
    private readonly Dictionary<E_ActionType, AttackActionParticleMap> _attackActionParticleMapLookup = new Dictionary<E_ActionType, AttackActionParticleMap>(); // 액션 타입으로 공격 파티클 매핑을 빠르게 조회하기 위한 캐시입니다.
    private readonly Dictionary<int, AttackParticleTransformState> _attackParticleTransformStates = new Dictionary<int, AttackParticleTransformState>(); // 공격 파티클 기본 회전/스케일을 복원하기 위한 캐시입니다.
    private readonly Dictionary<int, AnchorTransformState> _anchorTransformStates = new Dictionary<int, AnchorTransformState>(); // 우측 기준 앵커 배치를 방향 반전에 재사용하기 위한 캐시입니다.

    private bool _isEyeEffectActive; // Eye Effect가 활성 상태인지 추적해 방향 전환 시 위치를 재정렬하기 위한 캐시입니다.
    private bool _didWarnFacingVisualFallback; // 시각 루트 방향 폴백 경고 출력 여부입니다.
    private bool _didWarnFacingSpriteFallback; // SpriteRenderer 방향 폴백 경고 출력 여부입니다.
    private bool _didWarnFacingDefaultFallback; // 기본 오른쪽 방향 폴백 경고 출력 여부입니다.

    private readonly Dictionary<int, PersistentObjectTransformState> _persistentObjectTransformStates = new Dictionary<int, PersistentObjectTransformState>(); // 지속형 이펙트 오브젝트의 기본 위치/회전/스케일을 방향 반전에 재사용하기 위한 캐시입니다.

    /// <summary>
    /// 에디터 값이 변경될 때 핵심 참조 누락 경고를 확인합니다.
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
            Debug.LogWarning($"[CharacterVfxController] Attack VFX Point가 비어 있습니다. object={name}", this);
        }
    }

    /// <summary>
    /// 우측 기준 로컬 위치를 현재 바라보는 방향에 맞춰 X축만 반전한 값으로 변환합니다.
    /// </summary>
    private Vector3 ResolveMirroredLocalPositionByFacing(Vector3 rightFacingLocalPosition, Transform attachParent, bool isFacingRight)
    {
        rightFacingLocalPosition.x = Mathf.Abs(rightFacingLocalPosition.x) * ResolveRequiredLocalDirectionSign(attachParent, isFacingRight);
        return rightFacingLocalPosition;
    }

    /// <summary>
    /// 우측 기준 로컬 회전을 현재 바라보는 방향에 맞춰 Y축 180도 보정한 값으로 변환합니다.
    /// </summary>
    private Vector3 ResolveMirroredLocalRotationEulerByFacing(Vector3 rightFacingLocalRotationEuler, Transform attachParent, bool isFacingRight)
    {
        if (ResolveRequiredLocalDirectionSign(attachParent, isFacingRight) < 0f)
        {
            rightFacingLocalRotationEuler.y = Mathf.Repeat(rightFacingLocalRotationEuler.y + 180f, 360f);
        }

        return rightFacingLocalRotationEuler;
    }

    /// <summary>
    /// 시작 시 참조를 보정하고 지속형 이펙트를 기본 비활성 상태로 정리합니다.
    /// </summary>
    private void Awake()
    {
        TryResolveFacingReferences();
        ConfigureEyeEffectSimulationSpace();
        RebuildAttackActionParticleMapLookup();
        SetEyeEffectActive(false);
        SetWalkDustActive(false);
    }

    /// <summary>
    /// 비활성화 시 지속형 폴백 핸들을 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        StopHandle(ref _eyeFallbackHandle);
        StopHandle(ref _walkFallbackHandle);
    }

    /// <summary>
    /// 지속형 Eye Effect가 활성 중이면 현재 바라보는 방향에 맞춰 위치를 재정렬합니다.
    /// </summary>
    private void LateUpdate()
    {
        if (!_isEyeEffectActive || _eyeEffectObject == null)
        {
            return;
        }

        AlignPersistentObject(_eyeEffectObject, _eyeVfxPoint, mirrorAnchorByFacing: true);
    }

    /// <summary>
    /// EyeEffect를 활성/비활성 상태로 전환합니다.
    /// </summary>
    public void SetEyeEffectActive(bool isActive)
    {
        _isEyeEffectActive = isActive;

        if (_eyeEffectObject != null)
        {
            ConfigureEyeEffectSimulationSpace();
            AlignPersistentObject(_eyeEffectObject, _eyeVfxPoint, mirrorAnchorByFacing: true);
            SetPersistentObjectActive(_eyeEffectObject, isActive);
            return;
        }

        Debug.LogWarning($"[CharacterVfxController] EyeEffect 오브젝트 참조가 누락되었습니다. object={name}", this);
        HandlePersistentFallback(ref _eyeFallbackHandle, _eyeFallbackEffectId, _eyeVfxPoint, isActive, mirrorAnchorByFacing: true);
    }

    /// <summary>
    /// WalkDust를 활성/비활성 상태로 전환합니다.
    /// </summary>
    public void SetWalkDustActive(bool isActive)
    {
        if (_walkDustObject != null)
        {
            AlignPersistentObject(_walkDustObject, _footVfxPoint, mirrorAnchorByFacing: false);
            SetPersistentObjectActive(_walkDustObject, isActive);
            return;
        }

        Debug.LogWarning($"[CharacterVfxController] WalkDust 오브젝트 참조가 누락되었습니다. object={name}", this);
        HandlePersistentFallback(ref _walkFallbackHandle, _walkFallbackEffectId, _footVfxPoint, isActive, mirrorAnchorByFacing: false);
    }

    /// <summary>
    /// EyeEffect가 활성 중에도 캐릭터를 따라오도록 하위 파티클을 로컬 시뮬레이션으로 고정합니다.
    /// </summary>
    private void ConfigureEyeEffectSimulationSpace()
    {
        if (_eyeEffectObject == null)
        {
            return;
        }

        ParticleSystem[] particleSystems = _eyeEffectObject.GetComponentsInChildren<ParticleSystem>(true); // EyeEffect 오브젝트에 포함된 파티클 시스템 목록입니다.
        for (int index = 0; index < particleSystems.Length; index++)
        {
            ParticleSystem particleSystem = particleSystems[index]; // 시뮬레이션 공간을 보정할 현재 EyeEffect 파티클 시스템입니다.
            if (particleSystem == null)
            {
                continue;
            }

            ParticleSystem.MainModule mainModule = particleSystem.main;
            if (mainModule.simulationSpace == ParticleSystemSimulationSpace.Local)
            {
                continue;
            }

            mainModule.simulationSpace = ParticleSystemSimulationSpace.Local;
        }
    }

    /// <summary>
    /// 점프 성공 시 JumpDust를 1회 재생합니다.
    /// </summary>
    public void PlayJumpDust()
    {
        PlayJumpDustAt(GetFootVfxWorldPosition());
    }

    /// <summary>
    /// 지정한 위치에서 JumpDust를 1회 재생합니다.
    /// </summary>
    public void PlayJumpDustAt(Vector3 worldPosition)
    {
        PlayJumpDustWorldOneShotPooled(worldPosition);
    }

    /// <summary>
    /// 기본 Hit 위치 기준으로 HitEffect를 1회 재생합니다.
    /// </summary>
    public void PlayHitEffect()
    {
        PlayOneShot(_hitEffectId, GetHitVfxWorldPosition());
    }

    /// <summary>
    /// 지정한 위치에서 HitEffect를 1회 재생합니다.
    /// </summary>
    public void PlayHitEffectAt(Vector3 worldPosition)
    {
        PlayOneShot(_hitEffectId, worldPosition);
    }

    /// <summary>
    /// 현재 캐릭터 방향 기준으로 지정한 공격 액션의 파티클 시스템을 1회 재생합니다.
    /// </summary>
    public void PlayAttackEffect(E_ActionType actionType)
    {
        PlayAttackEffect(actionType, ResolveFacingDirection());
    }

    /// <summary>
    /// 전달받은 방향 기준으로 지정한 공격 액션의 파티클 시스템을 1회 재생합니다.
    /// </summary>
    public void PlayAttackEffect(E_ActionType actionType, bool isFacingRight)
    {
        if (!TryGetAttackActionParticleMap(actionType, out AttackActionParticleMap particleMap))
        {
            Debug.LogWarning($"[CharacterVfxController] 공격 이펙트 매핑이 없습니다. actionType={actionType}, object={name}", this);
            return;
        }

        ParticleSystem attackParticleSystem = ResolveAttackParticleSystem(particleMap);
        if (attackParticleSystem != null)
        {
            PlayMappedAttackParticleSystem(attackParticleSystem, particleMap, isFacingRight);
            return;
        }

        if (particleMap.FallbackEffectId == E_EffectId.None)
        {
            Debug.LogWarning($"[CharacterVfxController] 공격 파티클과 폴백 EffectId가 모두 없습니다. actionType={actionType}, object={name}", this);
            return;
        }

        PlayOneShot(
            particleMap.FallbackEffectId,
            ResolveAttackEffectWorldPosition(particleMap, isFacingRight),
            isFacingRight ? E_EffectFacingDirection.Right : E_EffectFacingDirection.Left);
    }

    /// <summary>
    /// Eye VFX 기준 월드 좌표를 반환합니다.
    /// </summary>
    public Vector3 GetEyeVfxWorldPosition()
    {
        return ResolveAnchorWorldPosition(_eyeVfxPoint, mirrorAnchorByFacing: true);
    }

    /// <summary>
    /// Foot VFX 기준 월드 좌표를 반환합니다.
    /// </summary>
    public Vector3 GetFootVfxWorldPosition()
    {
        return ResolveAnchorWorldPosition(_footVfxPoint, mirrorAnchorByFacing: false);
    }

    /// <summary>
    /// Hit VFX 기준 월드 좌표를 반환합니다.
    /// </summary>
    public Vector3 GetHitVfxWorldPosition()
    {
        return ResolveAnchorWorldPosition(_hitVfxPoint, mirrorAnchorByFacing: false);
    }

    /// <summary>
    /// Attack VFX 공통 기준 월드 좌표를 반환합니다.
    /// </summary>
    public Vector3 GetSwordTipVfxWorldPosition()
    {
        return ResolveAnchorWorldPosition(_swordTipVfxPoint, mirrorAnchorByFacing: true);
    }

    /// <summary>
    /// 지속형 오브젝트를 기준 Transform 위치/회전으로 정렬합니다.
    /// </summary>
    private void AlignPersistentObject(GameObject effectObject, Transform anchor, bool mirrorAnchorByFacing)
    {
        Transform resolvedAnchor = ResolveAnchor(anchor);
        Transform attachParent = ResolveAttachTarget(resolvedAnchor, mirrorAnchorByFacing);
        if (effectObject.transform.parent != attachParent)
        {
            effectObject.transform.SetParent(attachParent, false);
        }

        if (mirrorAnchorByFacing)
        {
            CapturePersistentObjectTransformStateIfNeeded(effectObject, resolvedAnchor);
            if (_persistentObjectTransformStates.TryGetValue(effectObject.GetInstanceID(), out PersistentObjectTransformState transformState))
            {
                bool isFacingRight = ResolveFacingDirection();
                effectObject.transform.localPosition = ResolvePersistentObjectLocalPosition(effectObject, resolvedAnchor, attachParent, transformState, isFacingRight);
                effectObject.transform.localRotation = ResolvePersistentObjectLocalRotation(effectObject, resolvedAnchor, attachParent, transformState, isFacingRight);
                effectObject.transform.localScale = transformState.LocalScale;
                return;
            }

            effectObject.transform.position = ResolveAnchorWorldPosition(anchor, true);
            effectObject.transform.rotation = ResolveAnchorWorldRotation(anchor, true);
            return;
        }

        effectObject.transform.position = resolvedAnchor.position;
        effectObject.transform.rotation = resolvedAnchor.rotation;
    }

    /// <summary>
    /// 지속형 오브젝트와 하위 ParticleSystem 재생 상태를 전환합니다.
    /// </summary>
    private void SetPersistentObjectActive(GameObject effectObject, bool isActive)
    {
        if (effectObject == null)
        {
            Debug.LogWarning($"[CharacterVfxController] SetPersistentObjectActive 대상이 null입니다. object={name}", this);
            return;
        }

        ParticleSystem[] particleSystems = effectObject.GetComponentsInChildren<ParticleSystem>(true); // 지속형 오브젝트에 포함된 파티클 시스템 목록입니다.
        TrailRenderer[] trailRenderers = effectObject.GetComponentsInChildren<TrailRenderer>(true); // 지속형 오브젝트에 포함된 트레일 렌더러 목록입니다.

        if (isActive)
        {
            effectObject.SetActive(true);
            ResetTrailRenderers(trailRenderers);
        }

        for (int index = 0; index < particleSystems.Length; index++)
        {
            ParticleSystem particle = particleSystems[index]; // 활성/비활성 상태를 적용할 현재 파티클 시스템입니다.
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
    /// 공격 액션 파티클 매핑 조회용 캐시를 구성하고 누락된 파티클 시스템 자동 탐색을 시도합니다.
    /// </summary>
    private void RebuildAttackActionParticleMapLookup()
    {
        _attackActionParticleMapLookup.Clear();

        if (_attackActionParticleMaps == null)
        {
            return;
        }

        for (int index = 0; index < _attackActionParticleMaps.Length; index++)
        {
            AttackActionParticleMap particleMap = _attackActionParticleMaps[index]; // 현재 액션 타입에 대응하는 공격 파티클 매핑입니다.
            if (particleMap.ParticleSystem == null && string.IsNullOrWhiteSpace(particleMap.AutoResolveParticleName) == false)
            {
                particleMap.ParticleSystem = FindChildParticleSystemByName(particleMap.AutoResolveParticleName);
                _attackActionParticleMaps[index] = particleMap;
            }

            _attackActionParticleMapLookup[particleMap.ActionType] = particleMap;
        }
    }

    /// <summary>
    /// 지정한 공격 액션 타입에 연결된 파티클 매핑을 조회합니다.
    /// </summary>
    private bool TryGetAttackActionParticleMap(E_ActionType actionType, out AttackActionParticleMap particleMap)
    {
        if (_attackActionParticleMapLookup.Count == 0)
        {
            RebuildAttackActionParticleMapLookup();
        }

        return _attackActionParticleMapLookup.TryGetValue(actionType, out particleMap);
    }

    /// <summary>
    /// 매핑 데이터에 연결된 공격 파티클 시스템을 해석합니다.
    /// </summary>
    private ParticleSystem ResolveAttackParticleSystem(AttackActionParticleMap particleMap)
    {
        if (particleMap.ParticleSystem != null)
        {
            return particleMap.ParticleSystem;
        }

        if (string.IsNullOrWhiteSpace(particleMap.AutoResolveParticleName))
        {
            return null;
        }

        return FindChildParticleSystemByName(particleMap.AutoResolveParticleName);
    }

    /// <summary>
    /// 자식 계층에서 지정한 이름과 일치하는 파티클 시스템을 탐색합니다.
    /// </summary>
    private ParticleSystem FindChildParticleSystemByName(string particleSystemName)
    {
        if (string.IsNullOrWhiteSpace(particleSystemName))
        {
            return null;
        }

        ParticleSystem[] particleSystems = GetComponentsInChildren<ParticleSystem>(true); // 자동 탐색 대상이 되는 모든 자식 파티클 시스템입니다.
        for (int index = 0; index < particleSystems.Length; index++)
        {
            ParticleSystem particleSystem = particleSystems[index]; // 이름 비교 중인 현재 파티클 시스템입니다.
            if (particleSystem == null)
            {
                continue;
            }

            if (string.Equals(particleSystem.name, particleSystemName, System.StringComparison.Ordinal))
            {
                return particleSystem;
            }
        }

        return null;
    }

    /// <summary>
    /// 지정한 공격 파티클 시스템을 1회 재생하고 현재 방향과 위치가 반영되도록 정렬합니다.
    /// </summary>
    private void PlayMappedAttackParticleSystem(ParticleSystem attackParticleSystem, AttackActionParticleMap particleMap, bool isFacingRight)
    {
        if (attackParticleSystem == null)
        {
            Debug.LogWarning($"[CharacterVfxController] 공격 파티클 시스템 참조가 null입니다. object={name}", this);
            return;
        }

        CaptureAttackParticleTransformStateIfNeeded(attackParticleSystem);
        ApplyAttackParticleTransform(attackParticleSystem, particleMap, isFacingRight);

        TrailRenderer[] trailRenderers = attackParticleSystem.GetComponentsInChildren<TrailRenderer>(true); // 공격 파티클 재생 전에 초기화할 트레일 렌더러 목록입니다.
        ResetTrailRenderers(trailRenderers);

        attackParticleSystem.gameObject.SetActive(true);
        attackParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        attackParticleSystem.Play(true);
    }

    /// <summary>
    /// 공격 파티클 시스템의 기본 회전/스케일 데이터를 최초 한 번 캐시합니다.
    /// </summary>
    private void CaptureAttackParticleTransformStateIfNeeded(ParticleSystem attackParticleSystem)
    {
        int particleInstanceId = attackParticleSystem.GetInstanceID();
        if (_attackParticleTransformStates.ContainsKey(particleInstanceId))
        {
            return;
        }

        Transform particleTransform = attackParticleSystem.transform;
        AttackParticleTransformState transformState = new AttackParticleTransformState();
        transformState.LocalRotationEuler = particleTransform.localRotation.eulerAngles;
        transformState.LocalScale = particleTransform.localScale;
        _attackParticleTransformStates.Add(particleInstanceId, transformState);
    }

    /// <summary>
    /// 공격 파티클 시스템을 지정된 앵커와 오프셋으로 정렬하고 방향을 보정합니다.
    /// </summary>
    private void ApplyAttackParticleTransform(ParticleSystem attackParticleSystem, AttackActionParticleMap particleMap, bool isFacingRight)
    {
        int particleInstanceId = attackParticleSystem.GetInstanceID();
        if (_attackParticleTransformStates.TryGetValue(particleInstanceId, out AttackParticleTransformState transformState) == false)
        {
            return;
        }

        Transform particleTransform = attackParticleSystem.transform;
        Transform resolvedAnchor = ResolveAttackEffectAnchor(particleMap);
        Transform attachParent = ResolveAttachTarget(resolvedAnchor, mirrorAnchorByFacing: true);
        if (particleTransform.parent != attachParent)
        {
            particleTransform.SetParent(attachParent, false);
        }

        Vector3 anchorLocalPosition = ResolveAttachLocalOffset(resolvedAnchor, mirrorAnchorByFacing: true);
        Vector3 effectLocalOffset = ResolveAttackEffectLocalPosition(particleMap, isFacingRight);
        particleTransform.localPosition = anchorLocalPosition + effectLocalOffset;
        Quaternion anchorWorldRotation = ResolveAnchorWorldRotation(resolvedAnchor, mirrorAnchorByFacing: true);
        Quaternion anchorLocalRotation = attachParent == null
            ? anchorWorldRotation
            : Quaternion.Inverse(attachParent.rotation) * anchorWorldRotation;

        Vector3 particleLocalRotationEuler = transformState.LocalRotationEuler;
        bool shouldFlipByRotation = ResolveRequiredLocalDirectionSign(attachParent, isFacingRight) < 0f;
        if (shouldFlipByRotation)
        {
            particleLocalRotationEuler.y = Mathf.Repeat(particleLocalRotationEuler.y + 180f, 360f);
        }

        particleTransform.localRotation = anchorLocalRotation * Quaternion.Euler(particleLocalRotationEuler);

        particleTransform.localScale = transformState.LocalScale;
    }

    /// <summary>
    /// 액션별 공격 이펙트 기준 위치 Transform을 해석합니다.
    /// </summary>
    private Transform ResolveAttackEffectAnchor(AttackActionParticleMap particleMap)
    {
        if (particleMap.AnchorOverride != null)
        {
            return particleMap.AnchorOverride;
        }

        return ResolveAnchor(_swordTipVfxPoint);
    }

    /// <summary>
    /// 액션별 공격 이펙트 로컬 위치를 바라보는 방향에 맞춰 계산합니다.
    /// </summary>
    private Vector3 ResolveAttackEffectLocalPosition(AttackActionParticleMap particleMap, bool isFacingRight)
    {
        Vector3 localOffset = particleMap.LocalOffset;
        localOffset.x = Mathf.Abs(localOffset.x) * (isFacingRight ? 1f : -1f);
        return localOffset;
    }

    /// <summary>
    /// 액션별 공격 이펙트 월드 위치를 계산합니다.
    /// </summary>
    private Vector3 ResolveAttackEffectWorldPosition(AttackActionParticleMap particleMap, bool isFacingRight)
    {
        Transform resolvedAnchor = ResolveAttackEffectAnchor(particleMap);
        Vector3 localPosition = ResolveAttackEffectLocalPosition(particleMap, isFacingRight);
        return ResolveAnchorWorldPosition(resolvedAnchor, true) + (ResolveAnchorWorldRotation(resolvedAnchor, true) * localPosition);
    }

    /// <summary>
    /// 지속형 오브젝트 누락 시 EffectService Attach 폴백으로 활성/비활성을 처리합니다.
    /// </summary>
    private void HandlePersistentFallback(ref EffectHandle handle, E_EffectId effectId, Transform anchor, bool isActive, bool mirrorAnchorByFacing)
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
            request.AttachTarget = ResolveAttachTarget(anchor, mirrorAnchorByFacing);
            request.Owner = gameObject;
            request.LocalOffset = ResolveAttachLocalOffset(anchor, mirrorAnchorByFacing);
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
    /// 일회성 이펙트를 기본 방향으로 EffectService에 재생 요청합니다.
    /// </summary>
    private void PlayOneShot(E_EffectId effectId, Vector3 worldPosition)
    {
        PlayOneShot(effectId, worldPosition, E_EffectFacingDirection.UsePrefab);
    }

    /// <summary>
    /// 일회성 이펙트를 지정한 방향과 함께 EffectService에 재생 요청합니다.
    /// </summary>
    private void PlayOneShot(E_EffectId effectId, Vector3 worldPosition, E_EffectFacingDirection facingDirection)
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

        EffectRequest request = EffectRequest.CreateSimple(effectId, worldPosition);
        request.FacingDirection = facingDirection;
        EffectService.Instance.Play(request);
    }

    /// <summary>
    /// JumpDust를 EffectService Pool로 재생하고 실패하면 LocalObjectPoolManager Prefab Pool로 대체합니다.
    /// </summary>
    private void PlayJumpDustWorldOneShotPooled(Vector3 worldPosition)
    {
        if (_jumpDustEffectId == E_EffectId.None)
        {
            Debug.LogWarning($"[CharacterVfxController] JumpDust EffectId is None. Pool Prefab fallback will be used. object={name}", this);
            PlayJumpDustPoolPrefab(worldPosition, "EffectIdNone");
            return;
        }

        if (EffectService.Instance == null)
        {
            Debug.LogWarning($"[CharacterVfxController] EffectService missing. JumpDust Pool Prefab fallback will be used. object={name}", this);
            PlayJumpDustPoolPrefab(worldPosition, "EffectServiceMissing");
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

        EffectHandle handle = EffectService.Instance.Play(request);
        if (handle == null || !handle.IsValid)
        {
            Debug.LogWarning($"[CharacterVfxController] JumpDust EffectService handle is invalid. Pool Prefab fallback will be used. effectId={_jumpDustEffectId}, object={name}", this);
            PlayJumpDustPoolPrefab(worldPosition, "EffectServiceHandleInvalid");
        }
    }

    /// <summary>
    /// JumpDust EffectService 재생 실패 시 LocalObjectPoolManager로 Jump VFX Prefab을 대체 재생합니다.
    /// </summary>
    private void PlayJumpDustPoolPrefab(Vector3 worldPosition, string reason)
    {
        if (_jumpDustPoolPrefab == null)
        {
            Debug.LogWarning($"[CharacterVfxController] JumpDust Pool Prefab is missing. reason={reason}, object={name}", this);
            return;
        }

        LocalObjectPoolManager poolManager = LocalObjectPoolManager.Instance; // 로컬 전용 Jump VFX를 대체 재생할 Pool 관리자입니다.
        if (poolManager == null)
        {
            Debug.LogWarning($"[CharacterVfxController] LocalObjectPoolManager is missing for JumpDust. prefab={_jumpDustPoolPrefab.name}, object={name}", this);
            return;
        }

        GameObject spawnedObject = poolManager.Spawn(_jumpDustPoolPrefab, worldPosition, Quaternion.identity, null, gameObject, _jumpDustPoolFallbackLifetime);
        if (spawnedObject == null)
        {
            Debug.LogWarning($"[CharacterVfxController] JumpDust Pool Prefab spawn failed. prefab={_jumpDustPoolPrefab.name}, reason={reason}, object={name}", this);
        }
    }

    /// <summary>
    /// 핸들이 유효한 경우 정지하고 참조를 해제합니다.
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
    /// null 앵커를 현재 Transform으로 보정하고 경고를 출력합니다.
    /// </summary>
    private Transform ResolveAnchor(Transform anchor)
    {
        if (anchor != null)
        {
            return anchor;
        }

        Debug.LogWarning($"[CharacterVfxController] 앵커 Transform이 비어 있어 현재 Transform으로 대체합니다. object={name}", this);
        return transform;
    }

    /// <summary>
    /// 우측 기준으로 저장된 앵커의 기본 로컬 배치를 최초 한 번 캐시합니다.
    /// </summary>
    private void CaptureAnchorTransformStateIfNeeded(Transform anchor)
    {
        if (anchor == null)
        {
            return;
        }

        int anchorInstanceId = anchor.GetInstanceID();
        if (_anchorTransformStates.ContainsKey(anchorInstanceId))
        {
            return;
        }

        AnchorTransformState anchorTransformState = new AnchorTransformState();
        anchorTransformState.LocalPosition = anchor.localPosition;
        anchorTransformState.LocalRotation = anchor.localRotation;
        _anchorTransformStates.Add(anchorInstanceId, anchorTransformState);
    }

    /// <summary>
    /// 지속형 이펙트 오브젝트의 우측 기준 기본 로컬 오프셋과 회전을 캐시합니다.
    /// </summary>
    private void CapturePersistentObjectTransformStateIfNeeded(GameObject effectObject, Transform anchor)
    {
        if (effectObject == null)
        {
            return;
        }

        int effectObjectInstanceId = effectObject.GetInstanceID();
        if (_persistentObjectTransformStates.ContainsKey(effectObjectInstanceId))
        {
            return;
        }

        Transform resolvedAnchor = ResolveAnchor(anchor);
        Transform attachTarget = ResolveAttachTarget(resolvedAnchor, true);
        Transform effectTransform = effectObject.transform;
        Vector3 anchorLocalPosition = ResolveAttachLocalOffset(resolvedAnchor, true);
        Quaternion anchorLocalRotation = ResolveAnchorLocalRotation(resolvedAnchor, true, attachTarget);

        PersistentObjectTransformState transformState = new PersistentObjectTransformState
        {
            LocalOffset = effectTransform.localPosition - anchorLocalPosition,
            LocalRotationEuler = (Quaternion.Inverse(anchorLocalRotation) * effectTransform.localRotation).eulerAngles,
            LocalScale = effectTransform.localScale
        };

        _persistentObjectTransformStates.Add(effectObjectInstanceId, transformState);
    }

    /// <summary>
    /// 지속형 이펙트 오브젝트의 로컬 위치를 앵커 기준으로 계산합니다.
    /// </summary>
    private Vector3 ResolvePersistentObjectLocalPosition(GameObject effectObject, Transform anchor, Transform attachParent, PersistentObjectTransformState transformState, bool isFacingRight)
    {
        Vector3 anchorLocalPosition = ResolveAttachLocalOffset(anchor, true);
        if (effectObject == _eyeEffectObject)
        {
            return anchorLocalPosition + ResolveMirroredLocalPositionByFacing(_eyeEffectLocalOffset, attachParent, isFacingRight);
        }

        Vector3 effectLocalOffset = ResolveMirroredLocalPositionByFacing(transformState.LocalOffset, attachParent, isFacingRight);
        return anchorLocalPosition + effectLocalOffset;
    }

    /// <summary>
    /// 지속형 이펙트 오브젝트의 로컬 회전을 계산합니다.
    /// </summary>
    private Quaternion ResolvePersistentObjectLocalRotation(GameObject effectObject, Transform anchor, Transform attachParent, PersistentObjectTransformState transformState, bool isFacingRight)
    {
        Vector3 mirroredLocalRotationEuler = ResolveMirroredLocalRotationEulerByFacing(transformState.LocalRotationEuler, attachParent, isFacingRight);
        if (effectObject == _eyeEffectObject)
        {
            return Quaternion.Euler(mirroredLocalRotationEuler);
        }

        return ResolveAnchorLocalRotation(anchor, true, attachParent) * Quaternion.Euler(mirroredLocalRotationEuler);
    }

    /// <summary>
    /// 앵커를 직접 붙이지 않고도 Attach/배치 계산에 사용할 부모 Transform을 반환합니다.
    /// </summary>
    private Transform ResolveAttachTarget(Transform anchor, bool mirrorAnchorByFacing)
    {
        Transform resolvedAnchor = ResolveAnchor(anchor);
        if (!mirrorAnchorByFacing || resolvedAnchor.parent == null)
        {
            return resolvedAnchor;
        }

        return resolvedAnchor.parent;
    }

    /// <summary>
    /// 우측 기준 앵커의 로컬 위치를 현재 좌우 방향에 맞춰 보정합니다.
    /// </summary>
    private Vector3 ResolveAttachLocalOffset(Transform anchor, bool mirrorAnchorByFacing)
    {
        Transform resolvedAnchor = ResolveAnchor(anchor);
        if (!mirrorAnchorByFacing)
        {
            return Vector3.zero;
        }

        bool isFacingRight = ResolveFacingDirection();
        Vector3 localPosition = resolvedAnchor.localPosition;
        localPosition.x = Mathf.Abs(localPosition.x) * ResolveRequiredLocalDirectionSign(ResolveAttachTarget(resolvedAnchor, true), isFacingRight);
        return localPosition;
    }

    /// <summary>
    /// 우측 기준 앵커의 월드 위치를 현재 좌우 방향에 맞춰 계산합니다.
    /// </summary>
    private Vector3 ResolveAnchorWorldPosition(Transform anchor, bool mirrorAnchorByFacing)
    {
        Transform resolvedAnchor = ResolveAnchor(anchor);
        if (!mirrorAnchorByFacing)
        {
            return resolvedAnchor.position;
        }

        Transform attachTarget = ResolveAttachTarget(resolvedAnchor, true);
        Vector3 localOffset = ResolveAttachLocalOffset(resolvedAnchor, true);
        return attachTarget.TransformPoint(localOffset);
    }

    /// <summary>
    /// 우측 기준 앵커의 월드 회전을 현재 좌우 방향에 맞춰 계산합니다.
    /// </summary>
    /// <summary>
    /// 앵커의 로컬 회전을 현재 부모 기준 회전값으로 환산합니다.
    /// </summary>
    private Quaternion ResolveAnchorLocalRotation(Transform anchor, bool mirrorAnchorByFacing, Transform attachParent)
    {
        Quaternion anchorWorldRotation = ResolveAnchorWorldRotation(anchor, mirrorAnchorByFacing);
        if (attachParent == null)
        {
            return anchorWorldRotation;
        }

        return Quaternion.Inverse(attachParent.rotation) * anchorWorldRotation;
    }

    /// <summary>
    /// 현재 부모 월드 방향과 목표 바라보는 방향을 조합해 필요한 로컬 좌우 부호를 계산합니다.
    /// </summary>
    private float ResolveRequiredLocalDirectionSign(Transform attachParent, bool isFacingRight)
    {
        float parentWorldDirectionSign = 1f;
        if (attachParent != null && attachParent.lossyScale.x < 0f)
        {
            parentWorldDirectionSign = -1f;
        }

        float desiredWorldDirectionSign = isFacingRight ? 1f : -1f;
        return desiredWorldDirectionSign * parentWorldDirectionSign;
    }

    private Quaternion ResolveAnchorWorldRotation(Transform anchor, bool mirrorAnchorByFacing)
    {
        Transform resolvedAnchor = ResolveAnchor(anchor);
        if (!mirrorAnchorByFacing)
        {
            return resolvedAnchor.rotation;
        }

        Transform attachTarget = ResolveAttachTarget(resolvedAnchor, true);
        return attachTarget.rotation * resolvedAnchor.localRotation;
    }

    /// <summary>
    /// PlayerMovement를 우선 사용하고 없으면 시각 루트 또는 SpriteRenderer로 현재 좌우 방향을 판정합니다.
    /// </summary>
    private bool ResolveFacingDirection()
    {
        if (TryResolvePlayerMovement())
        {
            return _playerMovement.IsFacingRight;
        }

        TryResolveFacingReferences();

        if (_facingVisualTarget != null)
        {
            WarnFacingFallbackOnce(ref _didWarnFacingVisualFallback, "Visual Target localScale.x");
            return _facingVisualTarget.localScale.x >= 0f;
        }

        if (_facingSpriteRenderer != null)
        {
            WarnFacingFallbackOnce(ref _didWarnFacingSpriteFallback, "SpriteRenderer.flipX");
            return _facingSpriteRenderer.flipX == false;
        }

        WarnFacingFallbackOnce(ref _didWarnFacingDefaultFallback, "default right direction");
        return true;
    }

    /// <summary>
    /// 공격 이펙트 방향 판정에 필요한 참조를 자동 보정합니다.
    /// </summary>
    private void TryResolveFacingReferences()
    {
        if (_facingSpriteRenderer == null)
        {
            _facingSpriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }
    }

    /// <summary>
    /// PlayerMovement 참조가 비어 있을 때 같은 오브젝트에서 자동 보정합니다.
    /// </summary>
    private bool TryResolvePlayerMovement()
    {
        if (_playerMovement == null)
        {
            _playerMovement = GetComponent<PlayerMovement>();
        }

        return _playerMovement != null;
    }

    /// <summary>
    /// 방향 판정 폴백 경고를 각 경로마다 한 번만 출력합니다.
    /// </summary>
    private void WarnFacingFallbackOnce(ref bool didWarn, string fallbackSource)
    {
        if (didWarn)
        {
            return;
        }

        didWarn = true;
        Debug.LogWarning($"[CharacterVfxController] PlayerMovement 방향을 찾지 못해 공격 이펙트가 폴백 방향을 사용합니다. source={fallbackSource}, object={name}", this);
    }

    /// <summary>
    /// 트레일 렌더러의 기존 궤적을 지우고 방출 상태를 초기화합니다.
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
