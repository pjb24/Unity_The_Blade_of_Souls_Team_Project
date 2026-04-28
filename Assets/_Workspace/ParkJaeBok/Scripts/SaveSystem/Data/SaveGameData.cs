using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이 데이터 저장에 사용할 고정 슬롯입니다.
/// </summary>
public enum E_SaveSlot
{
    Slot1 = 1,
    Slot2 = 2,
    Slot3 = 3
}

/// <summary>
/// 슬롯과 무관하게 하나만 존재하는 글로벌 옵션 저장 파일의 JSON 루트 데이터입니다.
/// </summary>
[Serializable]
public class GlobalOptionsSaveData
{
    [Tooltip("글로벌 옵션 저장 데이터의 스키마 버전입니다.")]
    public int Version; // 글로벌 옵션 저장 파일 검증에 사용하는 스키마 버전입니다.

    [Tooltip("모든 슬롯이 공유하는 로컬 글로벌 옵션 데이터입니다.")]
    public OptionSaveData Options; // 슬롯과 분리되어 저장되는 로컬 옵션 데이터입니다.

    [Tooltip("마지막 저장 시각입니다. UTC Unix 초 단위로 기록됩니다.")]
    public long LastSavedUnixTimeUtc; // 디버그와 저장 파일 확인에 사용할 마지막 저장 시각입니다.
}

/// <summary>
/// 슬롯별 플레이 저장 파일의 JSON 루트 데이터입니다.
/// </summary>
[Serializable]
public class SlotPlaySaveData
{
    [Tooltip("슬롯 플레이 저장 데이터의 스키마 버전입니다.")]
    public int Version; // 슬롯 플레이 저장 파일 검증에 사용하는 스키마 버전입니다.

    [Tooltip("이 플레이 데이터가 속한 슬롯 번호입니다.")]
    public int SlotIndex; // 저장 파일과 슬롯 요청이 일치하는지 검증하는 슬롯 번호입니다.

    [Tooltip("이 슬롯에 저장된 스테이지 클리어와 진행 기록입니다.")]
    public StageProgressRuntime.SnapshotData StageProgress; // 슬롯별 스테이지 진행 상태입니다.

    [Tooltip("이 슬롯에 저장된 스테이지 세션과 이어하기 정보입니다.")]
    public StageSession.SnapshotData StageSession; // 슬롯별 현재 스테이지, 체크포인트, 복귀 지점 정보입니다.

    [Tooltip("이어하기에 사용할 마지막 플레이 씬 이름입니다.")]
    public string LastPlayedSceneName; // 이어하기 대상 씬을 결정할 때 사용하는 마지막 플레이 씬 이름입니다.

    [Tooltip("마지막 저장 시각입니다. UTC Unix 초 단위로 기록됩니다.")]
    public long LastSavedUnixTimeUtc; // 디버그와 저장 파일 확인에 사용할 마지막 저장 시각입니다.

    [Tooltip("이 슬롯에 이어하기 가능한 진행 데이터가 있는지 여부입니다.")]
    public bool HasProgress; // 타이틀 UI와 Continue 가능 여부 판단에 사용하는 진행 데이터 존재 여부입니다.

    [Tooltip("이 슬롯에 저장할 보유 재화 값입니다.")]
    public int Currency; // 슬롯마다 달라지는 보유 재화 값입니다.

    [Tooltip("이 슬롯에서 획득한 아이템 ID 목록입니다.")]
    public List<string> OwnedItemIds; // 슬롯마다 달라지는 획득 아이템 식별자 목록입니다.

    [Tooltip("이 슬롯에서 해금된 콘텐츠 ID 목록입니다.")]
    public List<string> UnlockedIds; // 슬롯마다 달라지는 해금 상태 식별자 목록입니다.

    [Tooltip("이 슬롯의 누적 플레이 시간입니다. 초 단위입니다.")]
    public float PlayTimeSeconds; // 슬롯마다 달라지는 누적 플레이 시간입니다.
}

/// <summary>
/// 저장소 리스너에게 전달하기 위한 현재 상태 스냅샷입니다.
/// </summary>
[Serializable]
public class SaveGameData
{
    public int Version; // 리스너에게 전달하는 스냅샷의 스키마 버전입니다.
    public OptionSaveData Options; // 현재 글로벌 옵션 스냅샷입니다.
    public StageProgressRuntime.SnapshotData StageProgress; // 현재 선택된 슬롯의 진행도 스냅샷입니다.
    public StageSession.SnapshotData StageSession; // 현재 선택된 슬롯의 세션 스냅샷입니다.
    public string LastPlayedSceneName; // 현재 선택된 슬롯의 마지막 플레이 씬 이름입니다.
    public long LastSavedUnixTimeUtc; // 스냅샷 생성 시각입니다. UTC Unix 초 단위입니다.
    public bool HasProgress; // 현재 선택된 슬롯에 진행 데이터가 있는지 여부입니다.
    public int CurrentSlotIndex; // 현재 선택된 슬롯 번호입니다.
}

/// <summary>
/// Unity 런타임 시스템과 JSON 저장 데이터 객체 사이를 연결하는 메모리 상태입니다.
/// </summary>
[Serializable]
public class SaveRuntimeData
{
    public OptionSaveData Options; // 현재 로컬 글로벌 옵션 상태입니다.
    public StageProgressRuntime.SnapshotData StageProgress; // 현재 선택된 슬롯의 스테이지 진행 상태입니다.
    public StageSession.SnapshotData StageSession; // 현재 선택된 슬롯의 스테이지 세션 상태입니다.
    public string LastPlayedSceneName; // 현재 선택된 슬롯의 마지막 플레이 씬 이름입니다.
    public bool HasProgress; // 현재 선택된 슬롯에 저장 가능한 진행 데이터가 있는지 여부입니다.
    public int Currency; // 현재 선택된 슬롯의 보유 재화 값입니다.
    public List<string> OwnedItemIds; // 현재 선택된 슬롯의 획득 아이템 ID 목록입니다.
    public List<string> UnlockedIds; // 현재 선택된 슬롯의 해금 콘텐츠 ID 목록입니다.
    public float PlayTimeSeconds; // 현재 선택된 슬롯의 누적 플레이 시간입니다.

    /// <summary>
    /// 옵션 기본값과 비어 있는 플레이 데이터로 런타임 저장 상태를 초기화합니다.
    /// </summary>
    public void InitializeFromDefaults(OptionSaveData defaultOptions)
    {
        Options = defaultOptions ?? new OptionSaveData();
        ApplyPlayData(CreateDefaultSlotData(1, 1));
    }

    /// <summary>
    /// 글로벌 옵션 저장 데이터만 런타임 상태에 반영합니다.
    /// </summary>
    public void ApplyGlobalOptions(GlobalOptionsSaveData saveData)
    {
        if (saveData == null || saveData.Options == null)
        {
            Debug.LogWarning("[SaveRuntimeData] 글로벌 옵션 저장 데이터가 유효하지 않아 기존 런타임 옵션을 유지합니다.");
            return;
        }

        Options = saveData.Options;
    }

    /// <summary>
    /// 슬롯 플레이 저장 데이터만 런타임 상태에 반영합니다.
    /// </summary>
    public void ApplyPlayData(SlotPlaySaveData saveData)
    {
        if (saveData == null)
        {
            Debug.LogWarning("[SaveRuntimeData] 슬롯 플레이 저장 데이터가 null이라 기존 런타임 플레이 데이터를 유지합니다.");
            return;
        }

        StageProgress = saveData.StageProgress;
        if (StageProgress.Records == null)
        {
            StageProgress.Records = new List<StageProgressRecord>();
        }

        StageSession = saveData.StageSession;
        LastPlayedSceneName = saveData.LastPlayedSceneName ?? string.Empty;
        HasProgress = saveData.HasProgress;
        Currency = Mathf.Max(0, saveData.Currency);
        OwnedItemIds = saveData.OwnedItemIds ?? new List<string>();
        UnlockedIds = saveData.UnlockedIds ?? new List<string>();
        PlayTimeSeconds = Mathf.Max(0f, saveData.PlayTimeSeconds);
    }

    /// <summary>
    /// 현재 런타임 옵션 상태를 글로벌 옵션 저장 DTO로 변환합니다.
    /// </summary>
    public GlobalOptionsSaveData ToGlobalOptionsData(int version)
    {
        return new GlobalOptionsSaveData
        {
            Version = version,
            Options = Options,
            LastSavedUnixTimeUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }

    /// <summary>
    /// 현재 런타임 플레이 상태를 지정 슬롯의 저장 DTO로 변환합니다.
    /// </summary>
    public SlotPlaySaveData ToSlotPlayData(int version, E_SaveSlot slot)
    {
        return new SlotPlaySaveData
        {
            Version = version,
            SlotIndex = (int)slot,
            StageProgress = StageProgress,
            StageSession = StageSession,
            LastPlayedSceneName = LastPlayedSceneName,
            LastSavedUnixTimeUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            HasProgress = HasProgress,
            Currency = Currency,
            OwnedItemIds = OwnedItemIds ?? new List<string>(),
            UnlockedIds = UnlockedIds ?? new List<string>(),
            PlayTimeSeconds = PlayTimeSeconds
        };
    }

    /// <summary>
    /// 현재 런타임 상태를 외부 리스너에게 전달할 스냅샷으로 변환합니다.
    /// </summary>
    public SaveGameData ToSaveData(int version, E_SaveSlot currentSlot)
    {
        return new SaveGameData
        {
            Version = version,
            Options = Options,
            StageProgress = StageProgress,
            StageSession = StageSession,
            LastPlayedSceneName = LastPlayedSceneName,
            LastSavedUnixTimeUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            HasProgress = HasProgress,
            CurrentSlotIndex = (int)currentSlot
        };
    }

    /// <summary>
    /// 파일에 쓰지 않고 지정 슬롯의 기본 플레이 데이터를 생성합니다.
    /// </summary>
    public static SlotPlaySaveData CreateDefaultSlotData(int version, int slotIndex)
    {
        return new SlotPlaySaveData
        {
            Version = version,
            SlotIndex = slotIndex,
            StageProgress = new StageProgressRuntime.SnapshotData
            {
                Records = new List<StageProgressRecord>()
            },
            StageSession = new StageSession.SnapshotData(),
            LastPlayedSceneName = string.Empty,
            LastSavedUnixTimeUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            HasProgress = false,
            Currency = 0,
            OwnedItemIds = new List<string>(),
            UnlockedIds = new List<string>(),
            PlayTimeSeconds = 0f
        };
    }
}
