using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "VFX/Effect Catalog", fileName = "EffectCatalog")]
public class EffectCatalog : ScriptableObject
{
    [SerializeField]
    private List<EffectDefinition> _definitions = new List<EffectDefinition>(); // 인스펙터에서 관리하는 이펙트 정의 목록

    private readonly Dictionary<E_EffectId, EffectDefinition> _cache = new Dictionary<E_EffectId, EffectDefinition>(); // O(1) 조회용 런타임 캐시
    private bool _isInitialized; // 캐시 초기화 완료 여부

    /// <summary>
    /// 카탈로그 캐시를 구성한다.
    /// </summary>
    public void Initialize()
    {
        _cache.Clear();

        for (int i = 0; i < _definitions.Count; i++)
        {
            EffectDefinition definition = _definitions[i];
            if (definition == null)
            {
                Debug.LogWarning($"[EffectCatalog] Index={i}에 null 정의가 있습니다.", this);
                continue;
            }

            if (_cache.ContainsKey(definition.EffectId))
            {
                Debug.LogWarning($"[EffectCatalog] 중복 EffectId가 감지되었습니다. id={definition.EffectId}", this);
                continue;
            }

            _cache.Add(definition.EffectId, definition);
        }

        _isInitialized = true;
    }

    /// <summary>
    /// EffectId에 대응하는 정의를 조회한다.
    /// </summary>
    public bool TryGetDefinition(E_EffectId effectId, out EffectDefinition definition)
    {
        if (_isInitialized == false)
        {
            Initialize();
        }

        return _cache.TryGetValue(effectId, out definition);
    }
}
