using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// Hit 시스템 수동 테스트를 위해 다양한 HitRequest를 전송하는 공격자 테스트 컴포넌트입니다. (2D 전용 탐색)
/// </summary>
public class HitTestAttacker : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private HitReceiver _targetReceiver; // 단일 타겟 모드에서 사용할 대상 HitReceiver 참조입니다.

    [Header("Multi Target")]
    [SerializeField] private bool _useMultiTargetPerHit = true; // 한 번의 Hit 요청을 여러 타겟에 동시에 전송할지 여부입니다.
    [SerializeField] private int _maxTargetsPerHit = 5; // 멀티 타겟 모드에서 한 번의 Hit에 허용할 최대 타겟 수입니다.

    [Header("Auto Target (2D)")]
    [SerializeField] private bool _useAutoFindTarget = true; // 공격 시 자동으로 타겟을 탐색할지 여부입니다.
    [SerializeField] private float _autoFindRadius = 5f; // 자동 탐색에 사용할 반경 값입니다.
    [SerializeField] private LayerMask _targetLayerMask = ~0; // 자동 탐색 시 대상 후보를 필터링할 레이어 마스크입니다.
    [SerializeField] private string _targetTag = "Enemy"; // 자동 탐색 시 대상 후보를 필터링할 태그 문자열입니다.
    [SerializeField] private bool _requireTargetTag = true; // 태그 일치 대상을 강제할지 여부입니다.
    [SerializeField] private bool _preferClosestTarget = true; // 단일 타겟 선택 시 가장 가까운 대상을 우선 선택할지 여부입니다.

    [Header("Input (New Input System)")]
    [SerializeField] private Key _normalHitKey = Key.Digit1; // 정상 피격 요청을 전송할 키입니다.
    [SerializeField] private Key _duplicateHitKey = Key.Digit2; // 마지막 HitId를 재사용해 중복 피격을 전송할 키입니다.
    [SerializeField] private Key _invalidHitIdKey = Key.Digit3; // 빈 HitId 요청을 전송할 키입니다.
    [SerializeField] private Key _zeroDamageKey = Key.Digit4; // 0 데미지 요청을 전송할 키입니다.
    [SerializeField] private Key _negativeDamageKey = Key.Digit5; // 음수 데미지 요청을 전송할 키입니다.
    [SerializeField] private Key _nanDamageKey = Key.Digit6; // NaN 데미지 요청을 전송할 키입니다.
    [SerializeField] private Key _infinityDamageKey = Key.Digit7; // Infinity 데미지 요청을 전송할 키입니다.
    [SerializeField] private Key _toggleInvincibleKey = Key.I; // 대상 무적 상태를 토글할 키입니다.
    [SerializeField] private Key _refreshTargetKey = Key.Tab; // 자동 탐색으로 타겟을 수동 갱신할 키입니다.

    [Header("Damage")]
    [SerializeField] private float _normalDamage = 10f; // 정상 피격 요청에 사용할 데미지 값입니다.

    private readonly List<HitReceiver> _cachedTargets = new List<HitReceiver>(); // 멀티 타겟 전송 시 사용할 탐색 결과 캐시 목록입니다.
    private string _lastHitId; // 중복 피격 테스트에 재사용할 마지막 HitId 문자열입니다.
    private bool _isInvincibleState; // 테스트용으로 관리하는 대상 무적 상태 값입니다.
    private bool _didLogMissingKeyboardWarning; // 키보드 입력 장치 미검출 경고 중복 출력을 방지하는 플래그입니다.

    /// <summary>
    /// 매 프레임 New Input System 키 입력을 확인해 테스트 케이스별 HitRequest를 전송합니다.
    /// </summary>
    private void Update()
    {
        if (IsPressedThisFrame(_refreshTargetKey))
        {
            RefreshTargetReceiver();
        }

        if (IsPressedThisFrame(_toggleInvincibleKey))
        {
            ToggleTargetInvincible();
        }

        if (IsPressedThisFrame(_normalHitKey))
        {
            SendNormalHit();
        }

        if (IsPressedThisFrame(_duplicateHitKey))
        {
            SendDuplicateHit();
        }

        if (IsPressedThisFrame(_invalidHitIdKey))
        {
            SendInvalidHitIdHit();
        }

        if (IsPressedThisFrame(_zeroDamageKey))
        {
            SendCustomHit(string.Empty, 0f, "ZeroDamage");
        }

        if (IsPressedThisFrame(_negativeDamageKey))
        {
            SendCustomHit(string.Empty, -5f, "NegativeDamage");
        }

        if (IsPressedThisFrame(_nanDamageKey))
        {
            SendCustomHit(string.Empty, float.NaN, "NaNDamage");
        }

        if (IsPressedThisFrame(_infinityDamageKey))
        {
            SendCustomHit(string.Empty, float.PositiveInfinity, "InfinityDamage");
        }
    }

    /// <summary>
    /// 지정한 키가 이번 프레임에 눌렸는지 New Input System으로 판정합니다.
    /// </summary>
    private bool IsPressedThisFrame(Key key)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            if (_didLogMissingKeyboardWarning == false)
            {
                Debug.LogWarning($"[HitTestAttacker] Keyboard device not found on {name}. New Input System input is unavailable.");
                _didLogMissingKeyboardWarning = true;
            }

            return false;
        }

        if (_didLogMissingKeyboardWarning)
        {
            _didLogMissingKeyboardWarning = false;
        }

        KeyControl keyControl = keyboard[key];
        if (keyControl == null)
        {
            Debug.LogWarning($"[HitTestAttacker] Invalid key binding({key}) on {name}.");
            return false;
        }

        return keyControl.wasPressedThisFrame;
    }

    /// <summary>
    /// 정상 피격 요청을 생성하여 전송합니다.
    /// </summary>
    private void SendNormalHit()
    {
        string newHitId = CreateNewHitId();
        SendCustomHit(newHitId, _normalDamage, "NormalHit");
    }

    /// <summary>
    /// 마지막 HitId를 재사용해 중복 피격 요청을 전송합니다.
    /// </summary>
    private void SendDuplicateHit()
    {
        if (string.IsNullOrWhiteSpace(_lastHitId))
        {
            Debug.LogWarning($"[HitTestAttacker] Duplicate test requested before any normal hit on {name}. Fallback to normal hit.");
            SendNormalHit();
            return;
        }

        SendCustomHit(_lastHitId, _normalDamage, "DuplicateHit");
    }

    /// <summary>
    /// 빈 HitId로 입력 검증 실패를 유도하는 요청을 전송합니다.
    /// </summary>
    private void SendInvalidHitIdHit()
    {
        SendCustomHit("", _normalDamage, "InvalidHitId");
    }

    /// <summary>
    /// 현재 선택된 타겟의 무적 상태를 토글합니다.
    /// </summary>
    private void ToggleTargetInvincible()
    {
        if (TryGetTargetReceiver(out HitReceiver receiver) == false)
        {
            return;
        }

        _isInvincibleState = !_isInvincibleState;
        receiver.SetInvincible(_isInvincibleState);
        Debug.Log($"[HitTestAttacker] Target invincible state changed: {_isInvincibleState}, Target={receiver.name}");
    }

    /// <summary>
    /// 전달받은 파라미터로 HitRequest를 구성하고 단일/멀티 타겟에 전송합니다.
    /// </summary>
    private void SendCustomHit(string hitId, float rawDamage, string statusTag)
    {
        string resolvedHitId = string.IsNullOrWhiteSpace(hitId) ? hitId : hitId.Trim();
        if (string.IsNullOrWhiteSpace(resolvedHitId) == false)
        {
            _lastHitId = resolvedHitId;
        }

        if (_useMultiTargetPerHit)
        {
            SendHitToMultipleTargets(resolvedHitId, rawDamage, statusTag);
            return;
        }

        if (TryGetTargetReceiver(out HitReceiver receiver) == false)
        {
            return;
        }

        SendHitToSingleTarget(receiver, resolvedHitId, rawDamage, statusTag);
    }

    /// <summary>
    /// 현재 조건에 맞는 여러 타겟에게 동일 HitId를 사용해 요청을 전송합니다.
    /// </summary>
    private void SendHitToMultipleTargets(string hitId, float rawDamage, string statusTag)
    {
        if (TryCollectTargetReceivers(_cachedTargets) == false)
        {
            Debug.LogWarning($"[HitTestAttacker] Multi-target hit skipped because no valid targets were found on {name}.");
            return;
        }

        for (int i = 0; i < _cachedTargets.Count; i++)
        {
            HitReceiver receiver = _cachedTargets[i];
            if (receiver == null)
            {
                Debug.LogWarning($"[HitTestAttacker] Null target in multi-target cache on {name}. Skipped.");
                continue;
            }

            SendHitToSingleTarget(receiver, hitId, rawDamage, statusTag);
        }
    }

    /// <summary>
    /// 지정한 단일 타겟에게 HitRequest를 생성해 전송합니다.
    /// </summary>
    private void SendHitToSingleTarget(HitReceiver receiver, string hitId, float rawDamage, string statusTag)
    {
        Vector3 attackerPosition = transform.position; // 공격자 위치 기반 피격 방향 계산에 사용하는 위치입니다.
        Vector3 targetPosition = receiver.transform.position; // 현재 타겟 위치 기반 피격 방향 계산에 사용하는 위치입니다.
        Vector2 hitDirection2D = ((Vector2)(targetPosition - attackerPosition)).normalized; // 2D 평면에서 계산한 피격 방향 벡터입니다.

        HitRequest request = new HitRequest(
            hitId: hitId,
            rawDamage: rawDamage,
            attacker: gameObject,
            hitPoint: targetPosition,
            hitDirection: new Vector3(hitDirection2D.x, hitDirection2D.y, 0f),
            statusTag: statusTag,
            requestTime: Time.time);

        HitResult result = receiver.ReceiveHit(request);
        LogHitResult(receiver, request, result);
    }

    /// <summary>
    /// 자동 탐색 규칙으로 대표 타겟을 갱신합니다.
    /// </summary>
    private void RefreshTargetReceiver()
    {
        if (_useAutoFindTarget == false)
        {
            Debug.LogWarning($"[HitTestAttacker] Auto target finding is disabled on {name}.");
            return;
        }

        if (TryFindTargetReceiver(out HitReceiver foundReceiver))
        {
            _targetReceiver = foundReceiver;
            Debug.Log($"[HitTestAttacker] Target refreshed: {_targetReceiver.name}");
            return;
        }

        Debug.LogWarning($"[HitTestAttacker] Could not find 2D target receiver around {name}. Keep previous target.");
    }

    /// <summary>
    /// 대상 HitReceiver 참조를 검증하고 필요 시 자동 탐색/동일 오브젝트 폴백을 수행합니다.
    /// </summary>
    private bool TryGetTargetReceiver(out HitReceiver receiver)
    {
        receiver = _targetReceiver;
        if (receiver != null)
        {
            return true;
        }

        if (_useAutoFindTarget && TryFindTargetReceiver(out receiver))
        {
            _targetReceiver = receiver;
            Debug.LogWarning($"[HitTestAttacker] _targetReceiver was null on {name}. Fallback to auto-found target {receiver.name}.");
            return true;
        }

        receiver = GetComponent<HitReceiver>();
        if (receiver != null)
        {
            _targetReceiver = receiver;
            Debug.LogWarning($"[HitTestAttacker] _targetReceiver was null on {name}. Fallback to same GameObject HitReceiver.");
            return true;
        }

        Debug.LogWarning($"[HitTestAttacker] Missing target receiver on {name}. Hit request was skipped.");
        return false;
    }

    /// <summary>
    /// 현재 조건에 맞는 타겟 목록을 수집합니다.
    /// </summary>
    private bool TryCollectTargetReceivers(List<HitReceiver> targets)
    {
        targets.Clear();

        int safeMaxTargets = Mathf.Max(1, _maxTargetsPerHit); // 잘못된 최대 타겟 수 설정을 보정한 안전 값입니다.
        if (safeMaxTargets != _maxTargetsPerHit)
        {
            Debug.LogWarning($"[HitTestAttacker] Invalid _maxTargetsPerHit({_maxTargetsPerHit}) on {name}. Fallback to {safeMaxTargets}.");
        }

        float safeRadius = Mathf.Max(0.1f, _autoFindRadius); // 잘못된 탐색 반경 설정을 보정한 안전 값입니다.
        if (!Mathf.Approximately(safeRadius, _autoFindRadius))
        {
            Debug.LogWarning($"[HitTestAttacker] Invalid _autoFindRadius({_autoFindRadius}) on {name}. Fallback to {safeRadius}.");
        }

        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, safeRadius, _targetLayerMask);
        if (colliders == null || colliders.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D currentCollider = colliders[i];
            if (currentCollider == null)
            {
                continue;
            }

            HitReceiver candidate = currentCollider.GetComponentInParent<HitReceiver>();
            if (IsValidTargetCandidate(candidate) == false)
            {
                continue;
            }

            if (targets.Contains(candidate))
            {
                continue;
            }

            targets.Add(candidate);
        }

        if (_preferClosestTarget)
        {
            targets.Sort(CompareDistanceAscending);
        }

        if (targets.Count > safeMaxTargets)
        {
            targets.RemoveRange(safeMaxTargets, targets.Count - safeMaxTargets);
        }

        if (targets.Count > 0)
        {
            _targetReceiver = targets[0];
        }

        return targets.Count > 0;
    }

    /// <summary>
    /// 단일 타겟 탐색을 위해 조건에 맞는 대표 타겟을 찾습니다.
    /// </summary>
    private bool TryFindTargetReceiver(out HitReceiver receiver)
    {
        receiver = null;

        if (TryCollectTargetReceivers(_cachedTargets) == false)
        {
            return false;
        }

        receiver = _cachedTargets[0];
        return receiver != null;
    }

    /// <summary>
    /// 후보 타겟이 유효한지 검증합니다.
    /// </summary>
    private bool IsValidTargetCandidate(HitReceiver candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (candidate.gameObject == gameObject)
        {
            return false;
        }

        if (_requireTargetTag && string.IsNullOrWhiteSpace(_targetTag) == false && candidate.CompareTag(_targetTag) == false)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 공격자 기준 거리 오름차순으로 타겟 정렬을 위한 비교를 수행합니다.
    /// </summary>
    private int CompareDistanceAscending(HitReceiver left, HitReceiver right)
    {
        float leftDistance = (left.transform.position - transform.position).sqrMagnitude;
        float rightDistance = (right.transform.position - transform.position).sqrMagnitude;
        return leftDistance.CompareTo(rightDistance);
    }

    /// <summary>
    /// 고유 HitId를 생성합니다.
    /// </summary>
    private string CreateNewHitId()
    {
        return $"{name}_hit_{Time.frameCount}_{Random.Range(1000, 9999)}";
    }

    /// <summary>
    /// 요청/결과 정보를 테스트 로그로 출력합니다.
    /// </summary>
    private void LogHitResult(HitReceiver receiver, in HitRequest request, in HitResult result)
    {
        Debug.Log(
            $"[HitTestAttacker] Target={receiver?.name ?? "None"}, HitId={request.HitId}, RawDamage={request.RawDamage}, StatusTag={request.StatusTag}, " +
            $"Accepted={result.IsAccepted}, Reason={result.RejectReason}, AppliedDamage={result.AppliedDamage}, " +
            $"Health={result.HealthBefore}->{result.HealthAfter}, Dead={result.IsDeadAfter}");
    }

#if UNITY_EDITOR
    /// <summary>
    /// Scene 뷰에서 자동 탐색 반경(2D)을 시각화합니다.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, _autoFindRadius));
    }
#endif
}
