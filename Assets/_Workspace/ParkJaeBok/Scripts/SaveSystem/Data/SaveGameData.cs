using System;
using UnityEngine;

/// <summary>
/// JSON file에 기록되는 로컬 플레이어 저장 데이터입니다.
/// </summary>
[Serializable]
public class SaveGameData
{
    public int Version; // 저장 데이터 스키마 버전입니다.
    public OptionSaveData Options; // 로컬 플레이어 옵션 저장값입니다.
    public StageProgressRuntime.SnapshotData StageProgress; // 로컬 플레이어 스테이지 진행 저장값입니다.
    public StageSession.SnapshotData StageSession; // 이어하기에 필요한 마지막 스테이지/체크포인트 문맥입니다.
    public string LastPlayedSceneName; // 이어하기 폴백에 사용할 마지막 로컬 진행 씬 이름입니다.
    public long LastSavedUnixTimeUtc; // 마지막 저장 시각입니다.
    public bool HasProgress; // 새 게임 경고와 Continue 표시 기준이 되는 진행 데이터 존재 여부입니다.
}

/// <summary>
/// 게임 실행 중 사용하는 저장 런타임 데이터입니다.
/// </summary>
[Serializable]
public class SaveRuntimeData
{
    public OptionSaveData Options; // 현재 세션에서 사용하는 옵션 런타임 값입니다.
    public StageProgressRuntime.SnapshotData StageProgress; // 현재 세션에서 사용하는 스테이지 진행 런타임 값입니다.
    public StageSession.SnapshotData StageSession; // 현재 세션에서 사용하는 이어하기 런타임 문맥입니다.
    public string LastPlayedSceneName; // 현재 세션에서 마지막으로 기록한 진행 씬 이름입니다.
    public bool HasProgress; // 현재 세션에 저장 가능한 진행 데이터가 있는지 여부입니다.

    /// <summary>
    /// 기본값으로 런타임 저장 데이터를 초기화합니다.
    /// </summary>
    public void InitializeFromDefaults(OptionSaveData defaultOptions)
    {
        Options = defaultOptions ?? new OptionSaveData();
        StageProgress = new StageProgressRuntime.SnapshotData();
        StageSession = new StageSession.SnapshotData();
        LastPlayedSceneName = string.Empty;
        HasProgress = false;
    }

    /// <summary>
    /// 저장 파일에서 읽은 값 중 필요한 값만 현재 런타임 데이터에 덮어씁니다.
    /// </summary>
    public void ApplyLoadedData(SaveGameData saveData)
    {
        if (saveData == null)
        {
            Debug.LogWarning("[SaveRuntimeData] Loaded save data is null. Runtime save data was kept.");
            return;
        }

        if (saveData.Options != null)
        {
            Options = saveData.Options;
        }

        if (saveData.StageProgress.Records != null)
        {
            StageProgress = saveData.StageProgress;
        }

        StageSession = saveData.StageSession;
        LastPlayedSceneName = saveData.LastPlayedSceneName ?? string.Empty;
        HasProgress = saveData.HasProgress;
    }

    /// <summary>
    /// 현재 런타임 저장 데이터를 JSON 직렬화 가능한 저장 데이터로 변환합니다.
    /// </summary>
    public SaveGameData ToSaveData(int version)
    {
        return new SaveGameData
        {
            Version = version,
            Options = Options,
            StageProgress = StageProgress,
            StageSession = StageSession,
            LastPlayedSceneName = LastPlayedSceneName,
            LastSavedUnixTimeUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            HasProgress = HasProgress
        };
    }
}
