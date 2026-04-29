using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 체크포인트 상호작용과 사망 복귀 시 공통 회복 처리를 수행합니다.
/// </summary>
[DisallowMultipleComponent]
public class CheckpointRecoveryProcessor : MonoBehaviour
{
    [Header("Recovery")]
    [Tooltip("체크포인트 상호작용 직후 조작을 차단할 시간입니다.")]
    [Min(0f)]
    [SerializeField] private float _interactionMovementBlockSeconds = 0.75f; // 체크포인트 상호작용 후 이동 잠금 유지 시간입니다.

    [Tooltip("사망 복귀 직후 조작을 차단할 시간입니다.")]
    [Min(0f)]
    [SerializeField] private float _respawnMovementBlockSeconds = 1.25f; // 사망 복귀 후 이동 잠금 유지 시간입니다.

    [Tooltip("체크포인트 상호작용 직후 무적을 부여할 시간입니다.")]
    [Min(0f)]
    [SerializeField] private float _interactionInvincibleSeconds = 1f; // 체크포인트 상호작용 후 무적 유지 시간입니다.

    [Tooltip("사망 복귀 직후 무적을 부여할 시간입니다.")]
    [Min(0f)]
    [SerializeField] private float _respawnInvincibleSeconds = 2f; // 사망 복귀 후 무적 유지 시간입니다.

    [Tooltip("회복 처리 시 Rigidbody2D 속도를 초기화할지 여부입니다.")]
    [SerializeField] private bool _clearRigidbodyVelocity = true; // 복귀/회복 시 물리 속도를 초기화할지 여부입니다.

    [Tooltip("회복 처리 시 InputManager gameplay 입력도 함께 차단할지 여부입니다.")]
    [SerializeField] private bool _blockGameplayInput = false; // 전역 입력 차단을 사용할지 여부입니다.

    [Header("Monster Reset")]
    [Tooltip("몬스터 리셋을 담당할 컴포넌트입니다. 비어 있으면 씬에서 자동 탐색합니다.")]
    [SerializeField] private CheckpointMonsterResetter _monsterResetter; // 현재 Stage 몬스터 리셋 담당 컴포넌트입니다.

    /// <summary>
    /// 체크포인트 직접 상호작용 회복 처리를 수행합니다.
    /// </summary>
    public void ProcessCheckpointInteraction(GameObject playerObject, bool resetMonsters)
    {
        ProcessRecovery(playerObject, _interactionMovementBlockSeconds, _interactionInvincibleSeconds, resetMonsters, "CheckpointInteraction");
    }

    /// <summary>
    /// 사망 후 체크포인트 복귀 회복 처리를 수행합니다.
    /// </summary>
    public void ProcessDeathRespawn(GameObject playerObject, bool resetMonsters)
    {
        ProcessRecovery(playerObject, _respawnMovementBlockSeconds, _respawnInvincibleSeconds, resetMonsters, "DeathRespawn");
    }

    /// <summary>
    /// 플레이어 회복, 조작 잠금, 무적, 몬스터 리셋 처리를 실행합니다.
    /// </summary>
    private void ProcessRecovery(GameObject playerObject, float movementBlockSeconds, float invincibleSeconds, bool resetMonsters, string reason)
    {
        if (playerObject == null)
        {
            Debug.LogWarning($"[CheckpointRecoveryProcessor] Player가 null이라 회복 처리를 중단합니다. reason={reason}", this);
            return;
        }

        RestoreHealth(playerObject, reason);
        RestoreBuffGauge(playerObject, reason);
        ApplyTemporaryMovementBlock(playerObject, movementBlockSeconds, reason);
        ApplyTemporaryInvincibility(playerObject, invincibleSeconds, reason);
        ClearVelocity(playerObject);

        if (resetMonsters)
        {
            ResolveMonsterResetter();
            if (_monsterResetter != null)
            {
                _monsterResetter.ResetStageMonsters(reason);
            }
            else
            {
                Debug.LogWarning($"[CheckpointRecoveryProcessor] Monster Reset 대상 또는 시스템을 찾지 못했습니다. reason={reason}", this);
            }
        }
    }

    /// <summary>
    /// HealthComponent를 찾아 최대 체력으로 회복합니다.
    /// </summary>
    private void RestoreHealth(GameObject playerObject, string reason)
    {
        HealthComponent health = playerObject.GetComponentInChildren<HealthComponent>(true);
        if (health == null)
        {
            health = playerObject.GetComponentInParent<HealthComponent>();
        }

        if (health == null)
        {
            Debug.LogWarning($"[CheckpointRecoveryProcessor] HealthComponent를 찾지 못했습니다. player={playerObject.name}, reason={reason}", this);
            return;
        }

        float maxHealth = health.GetMaxHealth(); // 회복 목표 최대 체력입니다.
        if (health.IsDead)
        {
            health.Revive(Mathf.Max(0.01f, maxHealth));
            return;
        }

        health.ApplyHeal(new HealContext(maxHealth, playerObject, $"Checkpoint.{reason}", false));
        health.SetCurrentHealth(maxHealth);
    }

    /// <summary>
    /// PlayerBuffGauge를 찾아 최대 게이지로 회복합니다.
    /// </summary>
    private void RestoreBuffGauge(GameObject playerObject, string reason)
    {
        PlayerBuffGauge buffGauge = playerObject.GetComponentInChildren<PlayerBuffGauge>(true);
        if (buffGauge == null)
        {
            buffGauge = playerObject.GetComponentInParent<PlayerBuffGauge>();
        }

        if (buffGauge == null)
        {
            Debug.LogWarning($"[CheckpointRecoveryProcessor] PlayerBuffGauge를 찾지 못했습니다. player={playerObject.name}, reason={reason}", this);
            return;
        }

        buffGauge.SetCurrentGauge(buffGauge.MaxGauge);
        buffGauge.NotifyGaugeChanged();
    }

    /// <summary>
    /// PlayerMovement 잠금을 일정 시간 적용합니다.
    /// </summary>
    private void ApplyTemporaryMovementBlock(GameObject playerObject, float duration, string reason)
    {
        PlayerMovement movement = playerObject.GetComponentInChildren<PlayerMovement>(true);
        if (movement == null)
        {
            movement = playerObject.GetComponentInParent<PlayerMovement>();
        }

        if (movement == null)
        {
            Debug.LogWarning($"[CheckpointRecoveryProcessor] PlayerMovement를 찾지 못해 조작 Block을 적용하지 못했습니다. player={playerObject.name}, reason={reason}", this);
            return;
        }

        StartCoroutine(MovementBlockRoutine(movement, Mathf.Max(0f, duration)));
    }

    /// <summary>
    /// HitReceiver 무적을 일정 시간 적용합니다.
    /// </summary>
    private void ApplyTemporaryInvincibility(GameObject playerObject, float duration, string reason)
    {
        HitReceiver hitReceiver = playerObject.GetComponentInChildren<HitReceiver>(true);
        if (hitReceiver == null)
        {
            hitReceiver = playerObject.GetComponentInParent<HitReceiver>();
        }

        if (hitReceiver == null)
        {
            Debug.LogWarning($"[CheckpointRecoveryProcessor] HitReceiver를 찾지 못해 무적을 적용하지 못했습니다. player={playerObject.name}, reason={reason}", this);
            return;
        }

        StartCoroutine(InvincibilityRoutine(hitReceiver, Mathf.Max(0f, duration)));
    }

    /// <summary>
    /// Rigidbody2D 속도를 초기화합니다.
    /// </summary>
    private void ClearVelocity(GameObject playerObject)
    {
        if (!_clearRigidbodyVelocity)
        {
            return;
        }

        Rigidbody2D body = playerObject.GetComponentInChildren<Rigidbody2D>(true);
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }
    }

    /// <summary>
    /// 이동 잠금 코루틴을 수행합니다.
    /// </summary>
    private IEnumerator MovementBlockRoutine(PlayerMovement movement, float duration)
    {
        object inputBlocker = this; // InputManager 전역 차단 소유자입니다.
        movement.AddMovementLock(E_MovementLockReason.Cutscene, true);
        if (_blockGameplayInput)
        {
            InputManager.AddGameplayInputBlocker(inputBlocker);
        }

        yield return new WaitForSeconds(duration);

        if (movement != null)
        {
            movement.RemoveMovementLock(E_MovementLockReason.Cutscene);
        }

        if (_blockGameplayInput)
        {
            InputManager.RemoveGameplayInputBlocker(inputBlocker);
        }
    }

    /// <summary>
    /// 무적 코루틴을 수행합니다.
    /// </summary>
    private IEnumerator InvincibilityRoutine(HitReceiver hitReceiver, float duration)
    {
        hitReceiver.SetInvincible(true);
        yield return new WaitForSeconds(duration);

        if (hitReceiver != null)
        {
            hitReceiver.SetInvincible(false);
        }
    }

    /// <summary>
    /// 씬에서 몬스터 리셋터를 자동 탐색합니다.
    /// </summary>
    private void ResolveMonsterResetter()
    {
        if (_monsterResetter == null)
        {
            _monsterResetter = FindAnyObjectByType<CheckpointMonsterResetter>();
        }
    }
}
