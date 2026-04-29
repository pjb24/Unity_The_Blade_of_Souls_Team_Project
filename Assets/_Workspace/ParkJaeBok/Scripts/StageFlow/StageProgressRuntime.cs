using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어의 스테이지 진행도를 표현하는 레코드입니다.
/// </summary>
[Serializable]
public class StageProgressRecord
{
    [Tooltip("진행도를 기록할 대상 Stage ID입니다.")]
    [SerializeField] private string _stageId; // 진행 레코드가 가리키는 Stage ID입니다.

    [Tooltip("해당 Stage를 1회 이상 클리어했는지 여부입니다.")]
    [SerializeField] private bool _isCleared; // 최초 클리어 달성 여부입니다.

    [Tooltip("해당 Stage의 누적 클리어 횟수입니다.")]
    [Min(0)]
    [SerializeField] private int _clearCount; // 반복 플레이를 포함한 누적 클리어 횟수입니다.

    [Tooltip("마지막 클리어 시각입니다. UTC Unix 초 단위로 기록합니다.")]
    [SerializeField] private long _lastClearUnixTimeUtc; // 마지막 클리어 시각을 UTC UnixTime으로 저장합니다.

    public StageProgressRecord()
    {
    }

    /// <summary>
    /// Stage ID를 지정해 진행 레코드를 초기화합니다.
    /// </summary>
    public StageProgressRecord(string stageId)
    {
        _stageId = stageId;
    }

    public string StageId => _stageId;
    public bool IsCleared => _isCleared;
    public int ClearCount => _clearCount;
    public long LastClearUnixTimeUtc => _lastClearUnixTimeUtc;

    /// <summary>
    /// 클리어 결과를 진행도에 반영합니다.
    /// </summary>
    public void MarkCleared(long unixTimeUtc)
    {
        _isCleared = true;
        _clearCount++;
        _lastClearUnixTimeUtc = unixTimeUtc;
    }
}

/// <summary>
/// Stage 단위 체크포인트 진행 상태를 저장하는 레코드입니다.
/// </summary>
[Serializable]
public class CheckpointProgressRecord
{
    [Tooltip("체크포인트 진행 상태가 속한 Stage ID입니다.")]
    [SerializeField] private string _stageId; // 체크포인트 진행 상태가 속한 Stage ID입니다.

    [Tooltip("마지막으로 상호작용한 Checkpoint ID입니다.")]
    [SerializeField] private string _lastInteractedCheckpointId; // 현재 리스폰 지점으로 사용하는 마지막 상호작용 Checkpoint ID입니다.

    [Tooltip("활성화된 Checkpoint ID 목록입니다.")]
    [SerializeField] private List<string> _activatedCheckpointIds = new List<string>(); // Stage 안에서 활성화된 Checkpoint ID 목록입니다.

    public CheckpointProgressRecord()
    {
    }

    /// <summary>
    /// Stage ID를 기준으로 체크포인트 진행 레코드를 생성합니다.
    /// </summary>
    public CheckpointProgressRecord(string stageId)
    {
        _stageId = stageId;
    }

    public string StageId => _stageId;
    public string LastInteractedCheckpointId => _lastInteractedCheckpointId;
    public IReadOnlyList<string> ActivatedCheckpointIds => _activatedCheckpointIds;

    /// <summary>
    /// 체크포인트 진행 스냅샷을 레코드에 반영합니다.
    /// </summary>
    public void ApplySnapshot(string lastInteractedCheckpointId, IEnumerable<string> activatedCheckpointIds)
    {
        _lastInteractedCheckpointId = lastInteractedCheckpointId ?? string.Empty;
        _activatedCheckpointIds = new List<string>();

        if (activatedCheckpointIds == null)
        {
            return;
        }

        HashSet<string> dedupe = new HashSet<string>();
        foreach (string checkpointId in activatedCheckpointIds)
        {
            if (string.IsNullOrWhiteSpace(checkpointId) || !dedupe.Add(checkpointId))
            {
                continue;
            }

            _activatedCheckpointIds.Add(checkpointId);
        }
    }
}

/// <summary>
/// 플레이어의 스테이지 진행도를 런타임과 저장소 사이에서 관리하는 서비스입니다.
/// </summary>
public class StageProgressRuntime : MonoBehaviour
{
    [Serializable]
    public struct SnapshotData
    {
        public List<StageProgressRecord> Records; // 저장 복원에 사용할 스테이지 진행도 레코드 목록입니다.
        public List<CheckpointProgressRecord> CheckpointRecords; // 저장 복원에 사용할 체크포인트 진행도 레코드 목록입니다.
    }

    private static StageProgressRuntime _instance; // 전역 접근을 위한 StageProgressRuntime 단일 인스턴스입니다.

    [Tooltip("씬 전환 뒤에도 진행도 런타임 오브젝트를 유지할지 여부입니다.")]
    [SerializeField] private bool _dontDestroyOnLoad = true; // 씬 전환 중 진행도 데이터를 유지할지 여부입니다.

    [Tooltip("디버그용: 스테이지 진행 레코드 목록입니다.")]
    [SerializeField] private List<StageProgressRecord> _records = new List<StageProgressRecord>(); // 런타임에 유지되는 스테이지 진행 레코드 목록입니다.

    [Tooltip("디버그용: Stage 단위 체크포인트 진행 레코드 목록입니다.")]
    [SerializeField] private List<CheckpointProgressRecord> _checkpointRecords = new List<CheckpointProgressRecord>(); // 런타임에 유지되는 체크포인트 진행 레코드 목록입니다.

    private readonly Dictionary<string, StageProgressRecord> _indexByStageId = new Dictionary<string, StageProgressRecord>(); // stageId 기반 빠른 조회 인덱스입니다.
    private readonly Dictionary<string, CheckpointProgressRecord> _checkpointIndexByStageId = new Dictionary<string, CheckpointProgressRecord>(); // stageId 기반 체크포인트 진행 조회 인덱스입니다.
    private Action<string> _stageProgressChangedListeners; // StageProgress 변경 알림 리스너 체인입니다.

    public static StageProgressRuntime Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<StageProgressRuntime>();
                if (_instance == null)
                {
                    GameObject runtimeObject = new GameObject("--- Stage Progress Runtime ---"); // 진행도 런타임 자동 생성 오브젝트입니다.
                    _instance = runtimeObject.AddComponent<StageProgressRuntime>();
                }
            }

            return _instance;
        }
    }

    /// <summary>
    /// 새 인스턴스를 만들지 않고 현재 존재하는 StageProgressRuntime을 반환합니다.
    /// </summary>
    public static bool TryGetExistingInstance(out StageProgressRuntime runtime)
    {
        runtime = _instance != null ? _instance : FindAnyObjectByType<StageProgressRuntime>();
        return runtime != null;
    }

    /// <summary>
    /// StageProgress 변경 알림 리스너를 등록합니다.
    /// </summary>
    public void AddListener(Action<string> listener)
    {
        _stageProgressChangedListeners += listener;
    }

    /// <summary>
    /// StageProgress 변경 알림 리스너를 해제합니다.
    /// </summary>
    public void RemoveListener(Action<string> listener)
    {
        _stageProgressChangedListeners -= listener;
    }

    /// <summary>
    /// 인스턴스 중복을 방지하고 인덱스를 초기화합니다.
    /// </summary>
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[StageProgressRuntime] 중복 인스턴스를 제거합니다.", this);
            Destroy(gameObject);
            return;
        }

        _instance = this;

        if (_dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        RebuildIndex();
    }

    /// <summary>
    /// 특정 Stage가 클리어되었는지 반환합니다.
    /// </summary>
    public bool IsStageCleared(string stageId)
    {
        if (string.IsNullOrWhiteSpace(stageId))
        {
            return false;
        }

        return _indexByStageId.TryGetValue(stageId, out StageProgressRecord record) && record != null && record.IsCleared;
    }

    /// <summary>
    /// 특정 Stage의 누적 클리어 횟수를 반환합니다.
    /// </summary>
    public int GetClearCount(string stageId)
    {
        if (string.IsNullOrWhiteSpace(stageId))
        {
            return 0;
        }

        return _indexByStageId.TryGetValue(stageId, out StageProgressRecord record) && record != null ? record.ClearCount : 0;
    }

    /// <summary>
    /// 전체 Stage 누적 클리어 횟수를 반환합니다.
    /// </summary>
    public int GetTotalClearCount()
    {
        int total = 0;
        for (int i = 0; i < _records.Count; i++)
        {
            StageProgressRecord record = _records[i]; // 합산 중인 진행 레코드입니다.
            if (record == null)
            {
                continue;
            }

            total += Mathf.Max(0, record.ClearCount);
        }

        return total;
    }

    /// <summary>
    /// Stage 클리어 결과를 진행도에 반영합니다.
    /// </summary>
    public void MarkStageCleared(string stageId)
    {
        if (string.IsNullOrWhiteSpace(stageId))
        {
            return;
        }

        StageProgressRecord record = GetOrCreateRecord(stageId);
        record.MarkCleared(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        _stageProgressChangedListeners?.Invoke(stageId);
    }

    /// <summary>
    /// Stage ID에 해당하는 체크포인트 진행 레코드를 조회합니다.
    /// </summary>
    public bool TryGetCheckpointProgress(string stageId, out CheckpointProgressRecord record)
    {
        record = null;
        if (string.IsNullOrWhiteSpace(stageId))
        {
            Debug.LogWarning("[StageProgressRuntime] Stage ID가 비어 있어 체크포인트 진행 조회를 중단합니다.", this);
            return false;
        }

        return _checkpointIndexByStageId.TryGetValue(stageId, out record) && record != null;
    }

    /// <summary>
    /// Stage ID에 해당하는 체크포인트 진행 상태를 저장하고 변경 알림을 발생시킵니다.
    /// </summary>
    public void SetCheckpointProgress(string stageId, string lastInteractedCheckpointId, IEnumerable<string> activatedCheckpointIds)
    {
        if (string.IsNullOrWhiteSpace(stageId))
        {
            Debug.LogWarning("[StageProgressRuntime] Stage ID가 비어 있어 체크포인트 진행 저장을 중단합니다.", this);
            return;
        }

        CheckpointProgressRecord record = GetOrCreateCheckpointRecord(stageId);
        record.ApplySnapshot(lastInteractedCheckpointId, activatedCheckpointIds);
        _stageProgressChangedListeners?.Invoke(stageId);
    }

    /// <summary>
    /// 현재 진행도 상태를 저장 가능한 스냅샷으로 생성합니다.
    /// </summary>
    public SnapshotData CreateSnapshot()
    {
        return new SnapshotData
        {
            Records = new List<StageProgressRecord>(_records),
            CheckpointRecords = new List<CheckpointProgressRecord>(_checkpointRecords)
        };
    }

    /// <summary>
    /// 전달받은 스냅샷으로 진행도 상태를 복원합니다.
    /// </summary>
    public void ApplySnapshot(SnapshotData snapshot)
    {
        _records = snapshot.Records ?? new List<StageProgressRecord>();
        _checkpointRecords = snapshot.CheckpointRecords ?? new List<CheckpointProgressRecord>();
        RebuildIndex();
    }

    /// <summary>
    /// stageId에 해당하는 진행 레코드를 조회하거나 생성합니다.
    /// </summary>
    private StageProgressRecord GetOrCreateRecord(string stageId)
    {
        if (_indexByStageId.TryGetValue(stageId, out StageProgressRecord existing) && existing != null)
        {
            return existing;
        }

        StageProgressRecord created = new StageProgressRecord(stageId); // 새 진행 레코드입니다.
        _records.Add(created);
        _indexByStageId[stageId] = created;
        return created;
    }

    /// <summary>
    /// stageId에 해당하는 체크포인트 진행 레코드를 조회하거나 생성합니다.
    /// </summary>
    private CheckpointProgressRecord GetOrCreateCheckpointRecord(string stageId)
    {
        if (_checkpointIndexByStageId.TryGetValue(stageId, out CheckpointProgressRecord existing) && existing != null)
        {
            return existing;
        }

        CheckpointProgressRecord created = new CheckpointProgressRecord(stageId); // 새 체크포인트 진행 레코드입니다.
        _checkpointRecords.Add(created);
        _checkpointIndexByStageId[stageId] = created;
        return created;
    }

    /// <summary>
    /// 레코드 목록을 기준으로 stageId 인덱스를 재구성합니다.
    /// </summary>
    private void RebuildIndex()
    {
        _indexByStageId.Clear();
        _checkpointIndexByStageId.Clear();

        if (_records == null)
        {
            _records = new List<StageProgressRecord>();
        }

        for (int i = 0; i < _records.Count; i++)
        {
            StageProgressRecord record = _records[i]; // 진행 인덱스에 반영할 레코드입니다.
            if (record == null || string.IsNullOrWhiteSpace(record.StageId))
            {
                continue;
            }

            _indexByStageId[record.StageId] = record;
        }

        if (_checkpointRecords == null)
        {
            _checkpointRecords = new List<CheckpointProgressRecord>();
        }

        for (int i = 0; i < _checkpointRecords.Count; i++)
        {
            CheckpointProgressRecord record = _checkpointRecords[i]; // 체크포인트 인덱스에 반영할 레코드입니다.
            if (record == null || string.IsNullOrWhiteSpace(record.StageId))
            {
                continue;
            }

            _checkpointIndexByStageId[record.StageId] = record;
        }
    }
}
