using System;
using System.Collections.Generic;
using UnityEngine;

//这个类用于存储每种数值的数量
[Serializable]
public struct ResourceAmount
{
    public ResourceType type;
    public int amount;
}

//用来获取当前数值数据
[Serializable]
public class ResourceSaveEntry
{
    public ResourceType type;
    public int current;
}

//用来存储数据
[Serializable]
public class ResourceSaveData
{
    public List<ResourceSaveEntry> entries = new List<ResourceSaveEntry>();
}

//数值数据类型
public enum ResourceType
{
    Energy = 0,
    Fruit = 1,
    Root = 2,
}

//数据当前的状态
[Serializable]
public class ResourceState
{
    public ResourceType type;
    public int current;
    public int min = 0;
    public int max = 2000;
}

public class ResourceManager : MonoBehaviour
{
    //使用实例同一管理资源，仅可私有设置，可读
    public static ResourceManager Instance { get; private set; }

    [Header("启动时是否跨场景保留")]
    public bool dontDestroyOnLoad = true;

    [Header("初始资源配置")]
    public List<ResourceState> initialStates = new List<ResourceState>
    {
        new ResourceState { type = ResourceType.Energy, current = 0, min = 0, max = 2000},
        new ResourceState { type = ResourceType.Root, current = 0, min = 0, max = 2000 },
        new ResourceState { type = ResourceType.Fruit, current = 0, min = 0, max = 2000 },
    };

    //使用事件来管理资源变化，通过订阅来改变数值
    public event Action<ResourceType, int, int> OnResourceChanged;
    public event Action OnResourcesInitialized;

    private readonly Dictionary<ResourceType, ResourceState> _states = new Dictionary<ResourceType, ResourceState>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (dontDestroyOnLoad) //保证加载场景时不会销毁原有资源
        {
            DontDestroyOnLoad(gameObject);
        }

        InitializeStates();
    }

    public int Get(ResourceType type)
    {
        if (!_states.TryGetValue(type, out ResourceState state)) return 0;
        return state.current;
    }

    public int GetMax(ResourceType type)
    {
        if (!_states.TryGetValue(type, out ResourceState state)) return 0;
        return state.max;
    }

    public bool Has(ResourceType type)
    {
        return _states.ContainsKey(type);
    }

    public void Set(ResourceType type, int value)
    {
        if (!_states.TryGetValue(type, out ResourceState state)) return;

        int clamped = Mathf.Clamp(value, state.min, state.max);
        if (clamped == state.current) return;

        int before = state.current;
        state.current = clamped;
        OnResourceChanged?.Invoke(type, before, state.current);
    }

    //添加delta值的数据到type里面
    public void Add(ResourceType type, int delta)
    {
        if (delta == 0) return;
        Set(type, Get(type) + delta);
    }

    public bool CanAfford(ResourceAmount cost)
    {
        if (cost.amount <= 0) return true;
        return Get(cost.type) >= cost.amount;
    }

    public bool CanAfford(List<ResourceAmount> costs)
    {
        if (costs == null) return true;

        for (int i = 0; i < costs.Count; i++)
        {
            if (!CanAfford(costs[i])) return false;
        }

        return true;
    }

    //花费cost数量的资源
    public bool TrySpend(ResourceAmount cost)
    {
        if (!CanAfford(cost)) return false;
        Add(cost.type, -cost.amount);
        return true;
    }

    public bool TrySpend(List<ResourceAmount> costs)
    {
        if (!CanAfford(costs)) return false;

        for (int i = 0; i < costs.Count; i++)
        {
            Add(costs[i].type, -costs[i].amount);
        }

        return true;
    }

    public void Add(List<ResourceAmount> gains)
    {
        if (gains == null) return;

        for (int i = 0; i < gains.Count; i++)
        {
            Add(gains[i].type, gains[i].amount);
        }
    }

    //存储数据键值对
    public ResourceSaveData BuildSaveData()
    {
        ResourceSaveData data = new ResourceSaveData();

        foreach (KeyValuePair<ResourceType, ResourceState> kv in _states)
        {
            data.entries.Add(new ResourceSaveEntry
            {
                type = kv.Key,
                current = kv.Value.current,
            });
        }

        return data;
    }

    //从已经保存的数据中取出，并赋值
    public void LoadFromSaveData(ResourceSaveData data)
    {
        if (data == null || data.entries == null) return;

        for (int i = 0; i < data.entries.Count; i++)
        {
            ResourceSaveEntry entry = data.entries[i];
            Set(entry.type, entry.current);
        }
    }

    //取出所有的value
    public Dictionary<ResourceType, int> SnapshotValues()
    {
        Dictionary<ResourceType, int> snapshot = new Dictionary<ResourceType, int>();
        foreach (KeyValuePair<ResourceType, ResourceState> kv in _states)
        {
            snapshot[kv.Key] = kv.Value.current;
        }
        return snapshot;
    }

    private void InitializeStates()
    {
        _states.Clear();

        for (int i = 0; i < initialStates.Count; i++)
        {
            ResourceState src = initialStates[i];
            if (src == null) continue;

            if (_states.ContainsKey(src.type))
            {
                Debug.LogWarning($"发现重复资源配置: {src.type}，后续项将被忽略。");
                continue;
            }

            //将所有数值赋值给运行中的states
            ResourceState runtime = new ResourceState
            {
                type = src.type,
                min = src.min,
                max = Mathf.Max(src.max, src.min),
                current = Mathf.Clamp(src.current, src.min, Mathf.Max(src.max, src.min)),
            };

            _states.Add(runtime.type, runtime);
        }

        OnResourcesInitialized?.Invoke();

        foreach (KeyValuePair<ResourceType, ResourceState> kv in _states)
        {
            OnResourceChanged?.Invoke(kv.Key, kv.Value.current, kv.Value.current);
        }
    }
}
