using System;
using UnityEngine;

/// <summary>
/// Buff 상태 Source of Truth로서 토글/게이지/네트워크 확정 및 하위 모듈 동기화를 담당하는 컨트롤러입니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerBuffGauge))]
public class PlayerBuffController : MonoBehaviour, IAttackExecutionListener
{
    [Header("Dependencies")]
    [Tooltip("Buff 정책을 제공하는 설정 ScriptableObject입니다.")]
    [SerializeField] private BuffConfigSO _buffConfig; // Buff 정책/수치 설정 참조입니다.

    [Tooltip("Player 루트 NetworkObject와 연결된 네트워크 릴레이 참조입니다. 비어 있으면 부모/자식에서 자동 탐색합니다.")]
    [SerializeField] private PlayerBuffNetworkRelay _networkRelay; // Buff 네트워크 권한/복제 중계를 담당하는 릴레이 참조입니다.

    [Tooltip("게이지 값을 관리할 PlayerBuffGauge 참조입니다. 비어 있으면 자동 탐색합니다.")]
    [SerializeField] private PlayerBuffGauge _buffGauge; // Buff 게이지 상태를 보관/통지하는 컴포넌트 참조입니다.

    [Tooltip("Buff 상태에 따른 스탯 적용/복구를 담당할 컴포넌트 참조입니다.")]
    [SerializeField] private PlayerBuffStatModifier _statModifier; // Buff 스탯 보정을 담당하는 컴포넌트 참조입니다.

    [Tooltip("Buff 상태에 따른 VFX 표시를 담당할 컴포넌트 참조입니다.")]
    [SerializeField] private PlayerBuffVisualController _visualController; // Buff VFX 표시를 담당하는 컴포넌트 참조입니다.

    [Tooltip("Buff 상태에 따른 SFX 재생을 담당할 컴포넌트 참조입니다.")]
    [SerializeField] private PlayerBuffAudioController _audioController; // Buff SFX 재생을 담당하는 컴포넌트 참조입니다.

    [Tooltip("공격 성공 결과를 수신할 AttackExecutor 참조입니다. 비어 있으면 자동 탐색합니다.")]
    [SerializeField] private AttackExecutor _attackExecutor; // 공격 성공 시 게이지 충전을 받기 위한 AttackExecutor 참조입니다.

    [Header("Runtime")]
    [Tooltip("디버그용: 현재 Buff 활성 여부입니다.")]
    [SerializeField] private bool _isBuffActive; // 현재 Buff 활성 여부입니다.

    [Tooltip("디버그용: 런타임에서 계산된 Buff 시작 최소 게이지입니다.")]
    [SerializeField] private float _runtimeMinBuffStartGauge; // 런타임 보정 후 Buff 시작 최소 게이지 값입니다.

    [Tooltip("디버그용: 게이지 기반 Buff 시작 차단 경고를 이미 출력했는지 여부입니다.")]
    [SerializeField] private bool _didLogGaugeStartBlockedWarning; // 게이지 설정 불가 경고 중복 출력을 방지하는 플래그입니다.

    /// <summary>
    /// Buff 활성 상태가 변경될 때 구독자에게 전달되는 이벤트입니다.
    /// </summary>
    public event Action<bool> BuffActiveStateChanged;

    /// <summary>
    /// 현재 Buff 활성 여부를 반환합니다.
    /// </summary>
    public bool IsBuffActive => _isBuffActive;

    /// <summary>
    /// 현재 게이지 값을 반환합니다.
    /// </summary>
    public float CurrentGauge => _buffGauge != null ? _buffGauge.CurrentGauge : 0f;

    /// <summary>
    /// 최대 게이지 값을 반환합니다.
    /// </summary>
    public float MaxGauge => _buffGauge != null ? _buffGauge.MaxGauge : 0f;

    /// <summary>
    /// 현재 게이지 정규화 값을 반환합니다.
    /// </summary>
    public float GaugeNormalized => _buffGauge != null ? _buffGauge.NormalizedGauge : 0f;

    /// <summary>
    /// 현재 설정이 Buff 게이지 사용 모드인지 여부를 반환합니다.
    /// </summary>
    public bool IsUsingBuffGauge => _buffConfig != null && _buffConfig.UseBuffGauge;

    /// <summary>
    /// Buff 시작 가능 판정에 사용하는 최소 게이지 값을 반환합니다.
    /// </summary>
    public float MinBuffStartGauge => _buffConfig != null ? _buffConfig.GetRuntimeMinBuffStartGauge() : 0f;

    /// <summary>
    /// 현재 로컬 인스턴스가 Buff 입력을 구동할 수 있는지 반환합니다.
    /// </summary>
    public bool CanDriveLocalInput()
    {
        ResolveDependencies();

        if (_networkRelay == null || !_networkRelay.HasNetworkSession())
        {
            return true;
        }

        return _networkRelay.CanDriveOwnerInput();
    }

    /// <summary>
    /// 초기 참조 보정 및 게이지 초기화를 수행합니다.
    /// </summary>
    private void Awake()
    {
        ResolveDependencies();
        BindRelayEvents(true);
        InitializeGauge();
    }

    /// <summary>
    /// 활성화 시 공격 실행 결과 리스너를 구독합니다.
    /// </summary>
    private void OnEnable()
    {
        ResolveDependencies();
        BindRelayEvents(true);
        BindAttackListener(true);
    }

    /// <summary>
    /// 비활성화 시 구독한 리스너를 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        BindAttackListener(false);
        BindRelayEvents(false);
    }

    /// <summary>
    /// 권한 경로에서 Buff 상태를 갱신하고 게이지 소모를 처리합니다.
    /// </summary>
    private void Update()
    {
        if (!CanProcessAuthorityLogic())
        {
            return;
        }

        if (!_isBuffActive)
        {
            return;
        }

        if (_buffConfig == null || !_buffConfig.UseBuffGauge)
        {
            return;
        }

        float gaugeDrainPerSecond = Mathf.Max(0f, _buffConfig.GaugeDrainPerSecond); // 현재 설정에서 적용할 초당 게이지 감소량입니다.
        if (gaugeDrainPerSecond > 0f)
        {
            _buffGauge.ConsumeGauge(gaugeDrainPerSecond * Time.deltaTime);
            PushGaugeToReplicationIfNeeded();
        }

        if (_buffConfig.EndBuffWhenGaugeEmpty && _buffGauge.IsGaugeEmpty())
        {
            SetBuffActive(false, true, "GaugeEmptyAutoEnd");
        }
    }

    /// <summary>
    /// 로컬 입력 경로에서 Buff 토글을 요청합니다.
    /// </summary>
    public bool RequestToggleBuffFromInput()
    {
        ResolveDependencies();

        if (_networkRelay == null || !_networkRelay.HasNetworkSession())
        {
            return RequestToggleBuffOnAuthority();
        }

        if (!_networkRelay.CanDriveOwnerInput())
        {
            Debug.LogWarning($"[PlayerBuffController] Input toggle denied because caller is not owner. object={name}", this);
            return false;
        }

        if (_networkRelay.ShouldUseServerRpcRoute())
        {
            _networkRelay.RequestToggleBuffServerRpc();
            return true;
        }

        return RequestToggleBuffOnAuthority();
    }

    /// <summary>
    /// 네트워크 릴레이가 서버 권한에서 위임한 Buff 토글 요청을 처리합니다.
    /// </summary>
    public void HandleRelayServerToggleRequest()
    {
        RequestToggleBuffOnAuthority();
    }

    /// <summary>
    /// 공격 실행 결과를 수신해 Hit 성공 시 Buff 게이지를 증가시킵니다.
    /// </summary>
    public void OnAttackExecuted(in AttackExecutionReport report)
    {
        if (!CanProcessAuthorityLogic())
        {
            return;
        }

        if (_buffConfig == null || !_buffConfig.UseBuffGauge)
        {
            return;
        }

        if (!report.Result.IsAccepted)
        {
            return;
        }

        float gainAmount = Mathf.Max(0f, _buffConfig.GaugeGainOnSuccessfulHit); // 공격 성공 시 증가시킬 게이지 양입니다.
        if (gainAmount <= 0f)
        {
            return;
        }

        _buffGauge.AddGauge(gainAmount);
        PushGaugeToReplicationIfNeeded();
    }

    /// <summary>
    /// 서버/오프라인 권한 경로에서 Buff 토글 요청을 처리합니다.
    /// </summary>
    private bool RequestToggleBuffOnAuthority()
    {
        if (!CanProcessAuthorityLogic())
        {
            Debug.LogWarning($"[PlayerBuffController] RequestToggleBuff denied because caller is not authority. object={name}", this);
            return false;
        }

        if (_isBuffActive)
        {
            SetBuffActive(false, true, "ManualToggleOff");
            return true;
        }

        if (!CanStartBuff())
        {
            return false;
        }

        SetBuffActive(true, true, "ManualToggleOn");
        return true;
    }

    /// <summary>
    /// Buff 시작 가능 여부를 판정합니다.
    /// </summary>
    private bool CanStartBuff()
    {
        if (_buffConfig == null)
        {
            Debug.LogWarning($"[PlayerBuffController] BuffConfig is missing. Buff start denied. object={name}", this);
            return false;
        }

        if (!_buffConfig.UseBuffGauge)
        {
            return true;
        }

        if (_buffConfig.MaxBuffGauge <= 0f)
        {
            if (!_didLogGaugeStartBlockedWarning)
            {
                Debug.LogWarning($"[PlayerBuffController] UseBuffGauge=true but MaxBuffGauge <= 0. Buff start denied. object={name}", this);
                _didLogGaugeStartBlockedWarning = true;
            }

            return false;
        }

        _runtimeMinBuffStartGauge = _buffConfig.GetRuntimeMinBuffStartGauge();
        if (_buffGauge.CurrentGauge < _runtimeMinBuffStartGauge)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Buff 활성 상태를 적용하고 하위 시스템을 동기화합니다.
    /// </summary>
    private void SetBuffActive(bool isActive, bool playAudio, string reason)
    {
        if (_isBuffActive == isActive)
        {
            return;
        }

        _isBuffActive = isActive;
        BuffActiveStateChanged?.Invoke(_isBuffActive);

        if (_statModifier != null)
        {
            _statModifier.SetBuffApplied(isActive);
        }

        if (_visualController != null)
        {
            _visualController.SetBuffVisualActive(isActive);
        }

        if (playAudio && _audioController != null)
        {
            if (isActive)
            {
                _audioController.PlayBuffStartSfx();
            }
            else
            {
                _audioController.PlayBuffEndSfx();
            }
        }

        PushAuthorityStateToReplication();
    }

    /// <summary>
    /// 복제 상태를 로컬 표시/게이지 값에 반영합니다.
    /// </summary>
    private void ApplyReplicatedState(bool isBuffActive, float replicatedGauge, bool playAudio, string reason)
    {
        if (_buffGauge != null)
        {
            _buffGauge.SetCurrentGauge(replicatedGauge);
            _buffGauge.NotifyGaugeChanged();
        }

        if (_isBuffActive != isBuffActive)
        {
            _isBuffActive = isBuffActive;
            BuffActiveStateChanged?.Invoke(_isBuffActive);

            if (_statModifier != null)
            {
                _statModifier.SetBuffApplied(isBuffActive);
            }

            if (_visualController != null)
            {
                _visualController.SetBuffVisualActive(isBuffActive);
            }

            if (playAudio && _audioController != null)
            {
                if (isBuffActive)
                {
                    _audioController.PlayBuffStartSfx();
                }
                else
                {
                    _audioController.PlayBuffEndSfx();
                }
            }
        }
    }

    /// <summary>
    /// 권한 상태를 네트워크 복제 변수로 반영합니다.
    /// </summary>
    private void PushAuthorityStateToReplication()
    {
        if (_networkRelay == null)
        {
            return;
        }

        _networkRelay.PublishAuthorityState(_isBuffActive, _buffGauge != null ? _buffGauge.CurrentGauge : 0f);
    }

    /// <summary>
    /// 게이지 값만 복제 변수에 반영합니다.
    /// </summary>
    private void PushGaugeToReplicationIfNeeded()
    {
        if (_networkRelay == null)
        {
            return;
        }

        float currentGauge = _buffGauge != null ? _buffGauge.CurrentGauge : 0f; // 현재 게이지 스냅샷 값입니다.
        _networkRelay.PublishAuthorityGauge(currentGauge);
    }

    /// <summary>
    /// 네트워크/오프라인 환경에서 권한 로직 처리 가능 여부를 판정합니다.
    /// </summary>
    private bool CanProcessAuthorityLogic()
    {
        if (_networkRelay == null)
        {
            return true;
        }

        return _networkRelay.HasServerAuthority();
    }

    /// <summary>
    /// 초기 게이지 값을 설정합니다.
    /// </summary>
    private void InitializeGauge()
    {
        ResolveDependencies();

        if (_buffGauge == null)
        {
            return;
        }

        float maxGauge = _buffConfig != null ? Mathf.Max(0f, _buffConfig.MaxBuffGauge) : 0f; // 설정에서 읽은 최대 게이지 값입니다.
        float initialGauge = _buffConfig != null ? _buffConfig.GetRuntimeInitialGauge() : 0f; // 설정에서 보정한 초기 시작 게이지 값입니다.

        _runtimeMinBuffStartGauge = _buffConfig != null ? _buffConfig.GetRuntimeMinBuffStartGauge() : 0f;
        _buffGauge.Initialize(maxGauge, initialGauge);

        if (_networkRelay != null && _networkRelay.HasNetworkSession() && !_networkRelay.HasServerAuthority())
        {
            ApplyReplicatedState(_networkRelay.ReplicatedBuffActive, _networkRelay.ReplicatedGauge, false, "InitializeGaugeFromRelay");
        }
    }

    /// <summary>
    /// AttackExecutor 리스너 등록/해제를 처리합니다.
    /// </summary>
    private void BindAttackListener(bool bind)
    {
        if (_attackExecutor == null)
        {
            return;
        }

        if (bind)
        {
            _attackExecutor.AddListener(this);
            return;
        }

        _attackExecutor.RemoveListener(this);
    }

    /// <summary>
    /// 네트워크 릴레이의 복제 이벤트 등록/해제를 처리합니다.
    /// </summary>
    private void BindRelayEvents(bool bind)
    {
        if (_networkRelay == null)
        {
            return;
        }

        if (bind)
        {
            _networkRelay.ReplicatedBuffActiveChanged -= HandleReplicatedBuffActiveChanged;
            _networkRelay.ReplicatedBuffActiveChanged += HandleReplicatedBuffActiveChanged;

            _networkRelay.ReplicatedGaugeChanged -= HandleReplicatedGaugeChanged;
            _networkRelay.ReplicatedGaugeChanged += HandleReplicatedGaugeChanged;
            return;
        }

        _networkRelay.ReplicatedBuffActiveChanged -= HandleReplicatedBuffActiveChanged;
        _networkRelay.ReplicatedGaugeChanged -= HandleReplicatedGaugeChanged;
    }

    /// <summary>
    /// 직렬화 참조가 비어 있으면 런타임에서 자동 보정합니다.
    /// </summary>
    private void ResolveDependencies()
    {
        if (_networkRelay == null)
        {
            _networkRelay = ResolveInPlayerHierarchy<PlayerBuffNetworkRelay>();
        }

        if (_buffGauge == null)
        {
            _buffGauge = ResolveInBuffHierarchy<PlayerBuffGauge>();
        }

        if (_statModifier == null)
        {
            _statModifier = ResolveInBuffHierarchy<PlayerBuffStatModifier>();
        }

        if (_visualController == null)
        {
            _visualController = ResolveInBuffHierarchy<PlayerBuffVisualController>();
        }

        if (_audioController == null)
        {
            _audioController = ResolveInBuffHierarchy<PlayerBuffAudioController>();
        }

        if (_attackExecutor == null)
        {
            _attackExecutor = ResolveInPlayerHierarchy<AttackExecutor>();
        }
    }

    /// <summary>
    /// Buff 하위 모듈 오브젝트 기준으로 동일/자식/부모 순서로 컴포넌트를 해석합니다.
    /// </summary>
    private T ResolveInBuffHierarchy<T>() where T : Component
    {
        T resolved = GetComponent<T>();
        if (resolved != null)
        {
            return resolved;
        }

        resolved = GetComponentInChildren<T>(true);
        if (resolved != null)
        {
            return resolved;
        }

        resolved = GetComponentInParent<T>();
        return resolved;
    }

    /// <summary>
    /// 플레이어 루트 기준으로 부모/자식을 포함해 컴포넌트를 해석합니다.
    /// </summary>
    private T ResolveInPlayerHierarchy<T>() where T : Component
    {
        T resolved = GetComponent<T>();
        if (resolved != null)
        {
            return resolved;
        }

        resolved = GetComponentInParent<T>();
        if (resolved != null)
        {
            return resolved;
        }

        resolved = GetComponentInChildren<T>(true);
        return resolved;
    }

    /// <summary>
    /// 복제된 Buff 활성 상태 변경을 처리합니다.
    /// </summary>
    private void HandleReplicatedBuffActiveChanged(bool previousValue, bool currentValue)
    {
        if (CanProcessAuthorityLogic())
        {
            return;
        }

        ApplyReplicatedState(currentValue, _networkRelay != null ? _networkRelay.ReplicatedGauge : 0f, true, "ReplicatedBuffActiveChanged");
    }

    /// <summary>
    /// 복제된 게이지 값 변경을 처리합니다.
    /// </summary>
    private void HandleReplicatedGaugeChanged(float previousValue, float currentValue)
    {
        if (CanProcessAuthorityLogic())
        {
            return;
        }

        ApplyReplicatedState(_networkRelay != null && _networkRelay.ReplicatedBuffActive, currentValue, false, "ReplicatedGaugeChanged");
    }
}
