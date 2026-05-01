using System.Collections.Generic;
using UnityEngine;

public class EffectPool
{
    private readonly EffectDefinition _definition; // 풀 설정 기준 정의
    private readonly Transform _poolRoot; // 비활성 인스턴스 부모 루트
    private readonly Queue<EffectInstance> _inactiveQueue = new Queue<EffectInstance>(); // 사용 가능한 인스턴스 큐
    private readonly LinkedList<EffectInstance> _activeOrder = new LinkedList<EffectInstance>(); // 활성 순서 추적 목록
    private readonly Dictionary<EffectInstance, LinkedListNode<EffectInstance>> _activeNodeMap = new Dictionary<EffectInstance, LinkedListNode<EffectInstance>>(); // 빠른 삭제를 위한 노드 맵
    private int _createdCount; // 현재까지 생성한 총 인스턴스 수

    public EffectDefinition Definition => _definition;

    public EffectPool(EffectDefinition definition, Transform serviceRoot)
    {
        _definition = definition;

        GameObject rootObject = new GameObject($"Pool_{definition.EffectId}");
        _poolRoot = rootObject.transform;
        _poolRoot.SetParent(serviceRoot, false);

        Prewarm();
    }

    /// <summary>
    /// 초기 풀 사이즈만큼 인스턴스를 미리 생성한다.
    /// </summary>
    public void Prewarm()
    {
        for (int i = _createdCount; i < _definition.DefaultPoolSize; i++)
        {
            EffectInstance instance = CreateNewInstance();
            _inactiveQueue.Enqueue(instance);
        }
    }

    /// <summary>
    /// 풀에서 인스턴스를 획득한다. 부족 시 정책 적용 여부를 함께 반환한다.
    /// </summary>
    public EffectInstance AcquireOrNull(out bool usedFallback)
    {
        usedFallback = false;

        if (_inactiveQueue.Count > 0)
        {
            EffectInstance ready = _inactiveQueue.Dequeue();
            TrackActive(ready);
            return ready;
        }

        if (_createdCount < _definition.MaxPoolSize)
        {
            EffectInstance expanded = CreateNewInstance();
            TrackActive(expanded);
            return expanded;
        }

        usedFallback = true;

        if (_definition.FallbackPolicy == E_EffectFallbackPolicy.ReuseOldest)
        {
            EffectInstance oldest = GetOldestActive();
            if (oldest != null)
            {
                UntrackActive(oldest);
                TrackActive(oldest);
                return oldest;
            }
        }

        if (_definition.FallbackPolicy == E_EffectFallbackPolicy.InstantiateNew)
        {
            Debug.LogWarning($"[EffectPool] MaxPoolSize exceeded. Overflow effect instance will be created and managed by pool. id={_definition.EffectId}, prefab={_definition.Prefab.name}, max={_definition.MaxPoolSize}, newCount={_createdCount + 1}");
            EffectInstance overflow = CreateNewInstance();
            TrackActive(overflow);
            return overflow;
        }

        return null;
    }

    /// <summary>
    /// 사용 완료된 인스턴스를 풀로 반환한다.
    /// </summary>
    public void Release(EffectInstance instance)
    {
        if (instance == null)
        {
            return;
        }

        UntrackActive(instance);
        instance.transform.SetParent(_poolRoot, false);
        _inactiveQueue.Enqueue(instance);
    }

    /// <summary>
    /// 인스턴스를 실제로 생성한다.
    /// </summary>
    private EffectInstance CreateNewInstance()
    {
        GameObject instanceObject = Object.Instantiate(_definition.Prefab, _poolRoot);
        instanceObject.name = $"{_definition.EffectId}_Instance_{_createdCount}";
        EffectInstance instance = instanceObject.GetComponent<EffectInstance>();
        if (instance == null)
        {
            instance = instanceObject.AddComponent<EffectInstance>();
        }

        _createdCount++;
        instance.gameObject.SetActive(false);
        return instance;
    }

    /// <summary>
    /// 활성 순서 추적 목록에 인스턴스를 등록한다.
    /// </summary>
    private void TrackActive(EffectInstance instance)
    {
        if (_activeNodeMap.ContainsKey(instance))
        {
            return;
        }

        LinkedListNode<EffectInstance> node = _activeOrder.AddLast(instance);
        _activeNodeMap.Add(instance, node);
    }

    /// <summary>
    /// 활성 순서 추적 목록에서 인스턴스를 제거한다.
    /// </summary>
    private void UntrackActive(EffectInstance instance)
    {
        if (_activeNodeMap.TryGetValue(instance, out LinkedListNode<EffectInstance> node) == false)
        {
            return;
        }

        _activeOrder.Remove(node);
        _activeNodeMap.Remove(instance);
    }

    /// <summary>
    /// 가장 오래 활성 상태였던 인스턴스를 가져온다.
    /// </summary>
    private EffectInstance GetOldestActive()
    {
        if (_activeOrder.First == null)
        {
            return null;
        }

        return _activeOrder.First.Value;
    }
}
