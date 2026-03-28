using System.Collections.Generic;
using UnityEngine;

// 松鼠员工占用池：
// - 总松鼠数量来自 ResourceManager(ResourceType.Squirrel)
// - 运行中的房间向这里申请/释放员工占用
public class WorkforceManager : MonoBehaviour
{
    public static WorkforceManager Instance { get; private set; }

    [Header("依赖")]
    public ResourceManager resourceManager;

    // 每个房间当前占用的员工数
    private readonly Dictionary<RoomProductionUnit, int> _allocations = new Dictionary<RoomProductionUnit, int>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (resourceManager == null)
        {
            resourceManager = ResourceManager.Instance;
            if (resourceManager == null)
            {
                resourceManager = FindObjectOfType<ResourceManager>();
            }
        }
    }

    public int GetTotalSquirrels()
    {
        if (resourceManager == null) return 0;
        return resourceManager.Get(ResourceType.Squirrel);
    }

    public int GetAllocatedSquirrels()
    {
        int total = 0;
        foreach (KeyValuePair<RoomProductionUnit, int> kv in _allocations)
        {
            total += Mathf.Max(0, kv.Value);
        }
        return total;
    }

    public int GetAvailableSquirrels()
    {
        return Mathf.Max(0, GetTotalSquirrels() - GetAllocatedSquirrels());
    }

    public bool TryAllocate(RoomProductionUnit unit, int required)
    {
        if (unit == null) return false;
        if (required <= 0) return true;

        if (_allocations.TryGetValue(unit, out int existing) && existing == required)
        {
            return true;
        }

        // 若该房间已有占用，先释放后重新申请，避免重复占用。
        if (_allocations.ContainsKey(unit))
        {
            _allocations.Remove(unit);
        }

        if (GetAvailableSquirrels() < required)
        {
            return false;
        }

        _allocations[unit] = required;
        return true;
    }

    public void Release(RoomProductionUnit unit)
    {
        if (unit == null) return;
        _allocations.Remove(unit);
    }

    public int GetAllocation(RoomProductionUnit unit)
    {
        if (unit == null) return 0;
        if (!_allocations.TryGetValue(unit, out int count)) return 0;
        return count;
    }
}
