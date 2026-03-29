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
    public EmployeeRepository employeeRepository;

    // 每个房间当前占用的员工数
    private readonly Dictionary<RoomProductionUnit, int> _allocations = new Dictionary<RoomProductionUnit, int>();
    private readonly Dictionary<RoomProductionUnit, List<HREmployeeData>> _assignedEmployees = new Dictionary<RoomProductionUnit, List<HREmployeeData>>();

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

        if (employeeRepository == null)
        {
            employeeRepository = EmployeeRepository.GetOrCreateInstance();
        }
    }

    public int GetTotalSquirrels()
    {
        if (employeeRepository != null)
        {
            return employeeRepository.Count;
        }

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

        CleanupStaleAssignments();

        if (_allocations.TryGetValue(unit, out int existing) && existing == required)
        {
            return true;
        }

        // 若该房间已有占用，先释放后重新申请，避免重复占用。
        if (_allocations.ContainsKey(unit))
        {
            _allocations.Remove(unit);
            _assignedEmployees.Remove(unit);
        }

        if (TryAllocateWithEmployees(unit, required))
        {
            return true;
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
        _assignedEmployees.Remove(unit);
    }

    public int GetAllocation(RoomProductionUnit unit)
    {
        if (unit == null) return 0;
        if (!_allocations.TryGetValue(unit, out int count)) return 0;
        return count;
    }

    public bool HasAssignedStrikingEmployee(RoomProductionUnit unit)
    {
        if (unit == null)
        {
            return false;
        }

        if (!_assignedEmployees.TryGetValue(unit, out List<HREmployeeData> assigned) || assigned == null)
        {
            return false;
        }

        for (int i = 0; i < assigned.Count; i++)
        {
            HREmployeeData employee = assigned[i];
            if (employee == null)
            {
                continue;
            }

            if (employee.HasTrait(HREmployeeTraitType.Strike))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryAllocateWithEmployees(RoomProductionUnit unit, int required)
    {
        if (employeeRepository == null)
        {
            return false;
        }

        IReadOnlyList<HREmployeeData> all = employeeRepository.Employees;
        if (all == null || all.Count == 0)
        {
            return false;
        }

        HashSet<HREmployeeData> occupied = new HashSet<HREmployeeData>();
        foreach (KeyValuePair<RoomProductionUnit, List<HREmployeeData>> kv in _assignedEmployees)
        {
            if (kv.Key == unit || kv.Value == null)
            {
                continue;
            }

            for (int i = 0; i < kv.Value.Count; i++)
            {
                if (kv.Value[i] != null)
                {
                    occupied.Add(kv.Value[i]);
                }
            }
        }

        List<HREmployeeData> selected = new List<HREmployeeData>(required);
        for (int i = 0; i < all.Count; i++)
        {
            HREmployeeData employee = all[i];
            if (employee == null || occupied.Contains(employee))
            {
                continue;
            }

            selected.Add(employee);
            if (selected.Count >= required)
            {
                break;
            }
        }

        if (selected.Count < required)
        {
            return false;
        }

        _allocations[unit] = required;
        _assignedEmployees[unit] = selected;
        return true;
    }

    private void CleanupStaleAssignments()
    {
        if (_assignedEmployees.Count == 0)
        {
            return;
        }

        HashSet<HREmployeeData> validEmployees = new HashSet<HREmployeeData>();
        if (employeeRepository != null)
        {
            IReadOnlyList<HREmployeeData> all = employeeRepository.Employees;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null)
                {
                    validEmployees.Add(all[i]);
                }
            }
        }

        List<RoomProductionUnit> removeUnits = null;
        foreach (KeyValuePair<RoomProductionUnit, List<HREmployeeData>> kv in _assignedEmployees)
        {
            if (kv.Key == null || kv.Value == null)
            {
                if (removeUnits == null) removeUnits = new List<RoomProductionUnit>();
                removeUnits.Add(kv.Key);
                continue;
            }

            kv.Value.RemoveAll(e => e == null || (validEmployees.Count > 0 && !validEmployees.Contains(e)));
            if (kv.Value.Count == 0)
            {
                if (removeUnits == null) removeUnits = new List<RoomProductionUnit>();
                removeUnits.Add(kv.Key);
            }
        }

        if (removeUnits != null)
        {
            for (int i = 0; i < removeUnits.Count; i++)
            {
                RoomProductionUnit unit = removeUnits[i];
                _assignedEmployees.Remove(unit);
                _allocations.Remove(unit);
            }
        }
    }
}
