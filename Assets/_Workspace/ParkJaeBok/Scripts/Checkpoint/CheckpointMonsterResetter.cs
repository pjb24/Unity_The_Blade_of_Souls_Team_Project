using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 현재 Stage에 배치된 Enemy/Monster를 초기 위치와 생존 상태로 되돌립니다.
/// </summary>
[DisallowMultipleComponent]
public class CheckpointMonsterResetter : MonoBehaviour
{
    private sealed class MonsterSnapshot
    {
        public GameObject Root; // 리셋 대상 루트 오브젝트입니다.
        public Transform Transform; // 초기 위치를 적용할 Transform입니다.
        public Vector3 Position; // 초기 월드 위치입니다.
        public Quaternion Rotation; // 초기 월드 회전입니다.
        public HealthComponent Health; // 기존 Health 시스템 참조입니다.
        public EnemyAIDeathController DeathController; // 기존 Enemy 사망 처리 컨트롤러 참조입니다.
        public EnemyMovementController MovementController; // 기존 Enemy 이동 컨트롤러 참조입니다.
    }

    [Tooltip("Start 시점에 씬의 Enemy를 자동 수집할지 여부입니다.")]
    [SerializeField] private bool _autoCollectOnStart = true; // 씬 내 몬스터를 자동으로 수집할지 여부입니다.

    [Tooltip("비활성화된 Enemy도 수집할지 여부입니다.")]
    [SerializeField] private bool _includeInactive = true; // 비활성 Enemy까지 리셋 대상으로 포함할지 여부입니다.

    private readonly List<MonsterSnapshot> _snapshots = new List<MonsterSnapshot>(); // 수집된 몬스터 초기 상태 목록입니다.

    /// <summary>
    /// 설정에 따라 Stage 몬스터 초기 상태를 수집합니다.
    /// </summary>
    private void Start()
    {
        if (_autoCollectOnStart)
        {
            CaptureStageMonsters();
        }
    }

    /// <summary>
    /// 현재 씬에 있는 Enemy/Monster 후보의 초기 상태를 수집합니다.
    /// </summary>
    public void CaptureStageMonsters()
    {
        _snapshots.Clear();
        EnemyHealthAdapter[] enemies = FindObjectsByType<EnemyHealthAdapter>(
            _includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyHealthAdapter enemy = enemies[i]; // 현재 수집 중인 Enemy 후보입니다.
            if (enemy == null)
            {
                continue;
            }

            GameObject root = enemy.GetComponentInParent<NetworkObject>() != null
                ? enemy.GetComponentInParent<NetworkObject>().gameObject
                : enemy.gameObject;

            _snapshots.Add(new MonsterSnapshot
            {
                Root = root,
                Transform = root.transform,
                Position = root.transform.position,
                Rotation = root.transform.rotation,
                Health = root.GetComponentInChildren<HealthComponent>(true),
                DeathController = root.GetComponentInChildren<EnemyAIDeathController>(true),
                MovementController = root.GetComponentInChildren<EnemyMovementController>(true)
            });
        }

        if (_snapshots.Count == 0)
        {
            Debug.LogWarning("[CheckpointMonsterResetter] Monster Reset 대상이 없습니다. EnemyHealthAdapter 배치를 확인하세요.", this);
        }
    }

    /// <summary>
    /// 수집된 몬스터를 초기 위치와 생존 상태로 되돌립니다.
    /// </summary>
    public void ResetStageMonsters(string reason)
    {
        if (_snapshots.Count == 0)
        {
            Debug.LogWarning($"[CheckpointMonsterResetter] Monster Reset 대상이 없어 리셋을 건너뜁니다. reason={reason}", this);
            return;
        }

        for (int i = 0; i < _snapshots.Count; i++)
        {
            MonsterSnapshot snapshot = _snapshots[i]; // 현재 리셋 중인 몬스터 초기 상태입니다.
            if (snapshot == null || snapshot.Root == null || snapshot.Transform == null)
            {
                Debug.LogWarning($"[CheckpointMonsterResetter] 유효하지 않은 Monster Snapshot을 건너뜁니다. reason={reason}", this);
                continue;
            }

            snapshot.Root.SetActive(true);
            snapshot.Transform.SetPositionAndRotation(snapshot.Position, snapshot.Rotation);
            snapshot.DeathController?.ResetRuntime();
            snapshot.MovementController?.StopMovement();
            snapshot.MovementController?.ForceSyncNow();

            if (snapshot.Health != null)
            {
                snapshot.Health.Revive(snapshot.Health.GetMaxHealth());
                snapshot.Health.SetCurrentHealth(snapshot.Health.GetMaxHealth());
            }
            else
            {
                Debug.LogWarning($"[CheckpointMonsterResetter] HealthComponent를 찾지 못한 몬스터가 있습니다. monster={snapshot.Root.name}, reason={reason}", this);
            }
        }
    }
}
