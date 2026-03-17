using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SoundDatabase", menuName = "Audio/Sound Database")]
public class SoundDatabase : ScriptableObject
{
    [SerializeField]
    private List<SoundEntry> _entries = new List<SoundEntry>(); // 모든 사운드 메타데이터를 보관하는 리스트

    private Dictionary<E_SoundId, SoundEntry> _entryById; // 런타임 조회 성능 향상을 위한 캐시 딕셔너리

    public IReadOnlyList<SoundEntry> Entries => _entries;

    /// <summary>
    /// SoundId 기반 조회용 캐시를 초기화한다.
    /// </summary>
    public void InitializeCache()
    {
        _entryById = new Dictionary<E_SoundId, SoundEntry>(_entries.Count);

        for (int i = 0; i < _entries.Count; i++)
        {
            SoundEntry entry = _entries[i];
            if (entry == null)
            {
                Debug.LogWarning($"[SoundDatabase] Null Entry 발견: index={i}", this);
                continue;
            }

            if (_entryById.ContainsKey(entry.SoundId))
            {
                Debug.LogWarning($"[SoundDatabase] 중복 SoundId 캐시 제외: {entry.SoundId}", this);
                continue;
            }

            _entryById.Add(entry.SoundId, entry);
        }
    }

    /// <summary>
    /// ID에 해당하는 SoundEntry를 반환한다.
    /// </summary>
    public bool TryGetEntry(E_SoundId soundId, out SoundEntry entry)
    {
        if (_entryById == null)
        {
            InitializeCache();
        }

        return _entryById.TryGetValue(soundId, out entry);
    }

    /// <summary>
    /// 에디터에서 데이터 이상 여부를 검증하고 자동 교정한다.
    /// </summary>
    private void OnValidate()
    {
        HashSet<E_SoundId> uniqueIdSet = new HashSet<E_SoundId>();

        for (int i = 0; i < _entries.Count; i++)
        {
            SoundEntry entry = _entries[i];
            if (entry == null)
            {
                Debug.LogWarning($"[SoundDatabase] Entry가 null입니다. index={i}", this);
                continue;
            }

            if (entry.SoundId == E_SoundId.None)
            {
                Debug.LogWarning($"[SoundDatabase] SoundId None이 설정된 Entry가 있습니다. index={i}", this);
            }

            if (entry.Clip == null)
            {
                Debug.LogWarning($"[SoundDatabase] Clip이 비어있는 Entry가 있습니다. id={entry.SoundId}, index={i}", this);
            }

            if (uniqueIdSet.Contains(entry.SoundId))
            {
                Debug.LogWarning($"[SoundDatabase] 중복 SoundId가 있습니다. id={entry.SoundId}, index={i}", this);
            }
            else
            {
                uniqueIdSet.Add(entry.SoundId);
            }

            if (entry.RandomPitchOffsetMin > entry.RandomPitchOffsetMax)
            {
                float correctedMin = entry.RandomPitchOffsetMax;
                float correctedMax = entry.RandomPitchOffsetMin;
                entry.SetRandomPitchOffsets(correctedMin, correctedMax);
                Debug.LogWarning(
                    $"[SoundDatabase] RandomPitch Min/Max가 역전되어 자동 보정했습니다. id={entry.SoundId}, min={correctedMin}, max={correctedMax}",
                    this);
            }

            if (entry.BasePitch < 0.1f || entry.BasePitch > 3f)
            {
                float correctedBasePitch = Mathf.Clamp(entry.BasePitch, 0.1f, 3f);
                entry.SetBasePitch(correctedBasePitch);
                Debug.LogWarning(
                    $"[SoundDatabase] BasePitch 범위가 비정상이라 자동 보정했습니다. id={entry.SoundId}, corrected={correctedBasePitch}",
                    this);
            }

            float finalMinPitch = entry.BasePitch + entry.RandomPitchOffsetMin;
            float finalMaxPitch = entry.BasePitch + entry.RandomPitchOffsetMax;
            if (finalMinPitch <= 0f || finalMaxPitch <= 0f)
            {
                Debug.LogWarning(
                    $"[SoundDatabase] 최종 피치가 0 이하가 될 수 있습니다. id={entry.SoundId}, minFinal={finalMinPitch}, maxFinal={finalMaxPitch}",
                    this);
            }
        }

        InitializeCache();
    }
}
