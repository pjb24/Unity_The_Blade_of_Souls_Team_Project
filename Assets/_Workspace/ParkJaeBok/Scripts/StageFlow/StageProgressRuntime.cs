using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어의 스테이지 진행도를 표현하는 레코드입니다.
/// </summary>
[Serializable]
public class StageProgressRecord
{
    [Tooltip("진행도를 기록할 대상 스테이지 ID입니다.")]
    [SerializeField] private string _stageId; // 진행 레코드가 가리키는 스테이지 ID입니다.

    [Tooltip("해당 스테이지를 1회 이상 클리어했는지 여부입니다.")]
    [SerializeField] private bool _isCleared; // 최초 클리어 달성 여부입니다.

    [Tooltip("해당 스테이지 누적 클리어 횟수입니다.")]
    [Min(0)]
    [SerializeField] private int _clearCount; // 반복 플레이를 포함한 누적 클리어 횟수입니다.

    [Tooltip("마지막 클리어 시각(UnixTime UTC, 초 단위)입니다.")]
    [SerializeField] private long _lastClearUnixTimeUtc; // 마지막 클리어 시각을 저장하는 UTC UnixTime 값입니다.

    /// <summary>
    /// 직렬화/역직렬화용 기본 생성자입니다.
    /// </summary>
    public StageProgressRecord()
    {
    }

    /// <summary>
    /// 스테이지 ID를 지정해 진행 레코드를 초기화합니다.
    /// </summary>
    public StageProgressRecord(string stageId)
    {
        _stageId = stageId;
    }

    /// <summary>
    /// 레코드가 가리키는 스테이지 ID를 반환합니다.
    /// </summary>
    public string StageId => _stageId;

    /// <summary>
    /// 스테이지 클리어 여부를 반환합니다.
    /// </summary>
    public bool IsCleared => _isCleared;

    /// <summary>
    /// 누적 클리어 횟수를 반환합니다.
    /// </summary>
    public int ClearCount => _clearCount;

    /// <summary>
    /// 마지막 클리어 시각(UnixTime UTC)을 반환합니다.
    /// </summary>
    public long LastClearUnixTimeUtc => _lastClearUnixTimeUtc;

    /// <summary>
    /// 클리어 결과를 반영해 레코드를 갱신합니다.
    /// </summary>
    public void MarkCleared(long unixTimeUtc)
    {
        _isCleared = true;
        _clearCount++;
        _lastClearUnixTimeUtc = unixTimeUtc;
    }
}

/// <summary>
/// 플레이어의 스테이지 진행도를 런타임에서 관리하는 서비스입니다.
/// </summary>
public class StageProgressRuntime : MonoBehaviour
{
    /// <summary>
    /// StageProgressRuntime 스냅샷 데이터입니다.
    /// </summary>
    [Serializable]
    public struct SnapshotData
    {
        public List<StageProgressRecord> Records; // 저장/복원에 사용할 스테이지 진행도 레코드 목록입니다.
    }

    private static StageProgressRuntime _instance; // 전역 접근을 위한 StageProgressRuntime 싱글톤 인스턴스입니다.

    [Tooltip("씬 전환 후에도 진행도 런타임 오브젝트를 유지할지 여부입니다.")]
    [SerializeField] private bool _dontDestroyOnLoad = true; // 씬 전환 후 진행도 데이터 유지 여부입니다.

    [Tooltip("디버그 확인용 스테이지 진행 레코드 목록입니다.")]
    [SerializeField] private List<StageProgressRecord> _records = new List<StageProgressRecord>(); // 런타임에서 유지하는 스테이지 진행 레코드 목록입니다.

    private readonly Dictionary<string, StageProgressRecord> _indexByStageId = new Dictionary<string, StageProgressRecord>(); // stageId 기반 빠른 조회를 위한 인덱스입니다.

    /// <summary>
    /// 진행도 변경 시 호출되는 이벤트입니다.
    /// </summary>
    private Action<string> _stageProgressChangedListeners; // StageProgress 변경 알림 리스너 체인입니다.

    /// <summary>
    /// 전역 StageProgressRuntime 인스턴스를 반환합니다.
    /// </summary>
    public static StageProgressRuntime Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<StageProgressRuntime>();
                if (_instance == null)
                {
                    GameObject runtimeObject = new GameObject("--- Stage Progress Runtime ---"); // 런타임 자동 생성용 진행도 오브젝트입니다.
                    _instance = runtimeObject.AddComponent<StageProgressRuntime>();
                }
            }

            return _instance;
        }
    }

    /// <summary>
    /// 새 인스턴스를 생성하지 않고 현재 존재하는 StageProgressRuntime을 반환합니다.
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
    /// 특정 스테이지의 클리어 여부를 반환합니다.
    /// </summary>
    public bool IsStageCleared(string stageId)
    {
        if (string.IsNullOrWhiteSpace(stageId))
        {
            return false;
        }

        if (_indexByStageId.TryGetValue(stageId, out StageProgressRecord record) == false || record == null)
        {
            return false;
        }

        return record.IsCleared;
    }

    /// <summary>
    /// 특정 스테이지의 누적 클리어 횟수를 반환합니다.
    /// </summary>
    public int GetClearCount(string stageId)
    {
        if (string.IsNullOrWhiteSpace(stageId))
        {
            return 0;
        }

        if (_indexByStageId.TryGetValue(stageId, out StageProgressRecord record) == false || record == null)
        {
            return 0;
        }

        return record.ClearCount;
    }

    /// <summary>
    /// 전체 스테이지 누적 클리어 횟수를 반환합니다.
    /// </summary>
    public int GetTotalClearCount()
    {
        int total = 0;

        for (int i = 0; i < _records.Count; i++)
        {
            StageProgressRecord record = _records[i]; // 합산 중인 진행도 레코드입니다.
            if (record == null)
            {
                continue;
            }

            total += Mathf.Max(0, record.ClearCount);
        }

        return total;
    }

    /// <summary>
    /// 스테이지 클리어 결과를 진행도에 반영합니다.
    /// </summary>
    public void MarkStageCleared(string stageId)
    {
        if (string.IsNullOrWhiteSpace(stageId))
        {
            return;
        }

        StageProgressRecord record = GetOrCreateRecord(stageId);
        long unixTimeUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); // 클리어 반영 시각을 기록할 UTC UnixTime 값입니다.
        record.MarkCleared(unixTimeUtc);

        _stageProgressChangedListeners?.Invoke(stageId);
    }

    /// <summary>
    /// 현재 진행도 상태를 스냅샷으로 생성합니다.
    /// </summary>
    public SnapshotData CreateSnapshot()
    {
        return new SnapshotData
        {
            Records = new List<StageProgressRecord>(_records)
        };
    }

    /// <summary>
    /// 전달받은 스냅샷으로 진행도 상태를 복원합니다.
    /// </summary>
    public void ApplySnapshot(SnapshotData snapshot)
    {
        _records = snapshot.Records ?? new List<StageProgressRecord>();
        RebuildIndex();
    }

    /// <summary>
    /// stageId에 해당하는 진행도 레코드를 조회하거나 생성합니다.
    /// </summary>
    private StageProgressRecord GetOrCreateRecord(string stageId)
    {
        if (_indexByStageId.TryGetValue(stageId, out StageProgressRecord existing) && existing != null)
        {
            return existing;
        }

        StageProgressRecord created = new StageProgressRecord(stageId); // 신규 생성한 진행도 레코드입니다.
        _records.Add(created);
        _indexByStageId[stageId] = created;
        return created;
    }

    /// <summary>
    /// 레코드 목록을 기준으로 stageId 인덱스를 재구성합니다.
    /// </summary>
    private void RebuildIndex()
    {
        _indexByStageId.Clear();

        for (int i = 0; i < _records.Count; i++)
        {
            StageProgressRecord record = _records[i]; // 인덱스에 반영할 진행도 레코드입니다.
            if (record == null || string.IsNullOrWhiteSpace(record.StageId))
            {
                continue;
            }

            _indexByStageId[record.StageId] = record;
        }
    }
}
