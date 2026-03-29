using System;
using System.Collections.Generic;
using UnityEngine;

// 手动配置房间鼠鼠：从仓库分配到房间，并将生产加成延后到下个周期生效。
public class RoomEmployeeAssignmentManager : MonoBehaviour
{
    public static RoomEmployeeAssignmentManager Instance { get; private set; }

    [Header("依赖")]
    public HREmployeeRepository employeeRepository;

    private readonly Dictionary<RoomProductionUnit, List<string>> _assignedIdsByRoom = new Dictionary<RoomProductionUnit, List<string>>();
    private readonly Dictionary<string, RoomProductionUnit> _roomByEmployeeId = new Dictionary<string, RoomProductionUnit>(StringComparer.Ordinal);

    public event Action<RoomProductionUnit> OnRoomAssignmentsChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureRepository();
    }

    void Update()
    {
        CleanupDestroyedRooms();
    }

    public static RoomEmployeeAssignmentManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        RoomEmployeeAssignmentManager existing = FindObjectOfType<RoomEmployeeAssignmentManager>();
        if (existing != null)
        {
            return existing;
        }

        GameObject go = new GameObject("RoomEmployeeAssignmentManager_Auto");
        return go.AddComponent<RoomEmployeeAssignmentManager>();
    }

    public IReadOnlyList<HREmployeeData> GetAllEmployees()
    {
        if (!EnsureRepository())
        {
            return Array.Empty<HREmployeeData>();
        }

        return employeeRepository.Employees;
    }

    public List<HREmployeeData> GetAssignedEmployees(RoomProductionUnit room)
    {
        List<HREmployeeData> result = new List<HREmployeeData>();
        if (room == null)
        {
            return result;
        }

        if (!EnsureRepository())
        {
            return result;
        }

        if (!_assignedIdsByRoom.TryGetValue(room, out List<string> ids) || ids == null)
        {
            return result;
        }

        for (int i = 0; i < ids.Count; i++)
        {
            if (employeeRepository.TryGetById(ids[i], out HREmployeeData employee))
            {
                result.Add(employee);
            }
        }

        return result;
    }

    public IReadOnlyList<string> GetAssignedEmployeeIds(RoomProductionUnit room)
    {
        if (room == null)
        {
            return Array.Empty<string>();
        }

        if (!_assignedIdsByRoom.TryGetValue(room, out List<string> ids) || ids == null)
        {
            return Array.Empty<string>();
        }

        return ids;
    }

    public int GetAssignedCount(RoomProductionUnit room)
    {
        if (room == null)
        {
            return 0;
        }

        if (!_assignedIdsByRoom.TryGetValue(room, out List<string> ids) || ids == null)
        {
            return 0;
        }

        return ids.Count;
    }

    public RoomProductionUnit GetEmployeeOwner(string employeeId)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
        {
            return null;
        }

        _roomByEmployeeId.TryGetValue(employeeId.Trim(), out RoomProductionUnit room);
        return room;
    }

    public bool TryAssign(RoomProductionUnit room, string employeeId, out string failReason)
    {
        failReason = string.Empty;

        if (room == null)
        {
            failReason = "房间为空";
            return false;
        }

        if (!EnsureRepository())
        {
            failReason = "未找到鼠鼠仓库";
            return false;
        }

        string id = string.IsNullOrWhiteSpace(employeeId) ? string.Empty : employeeId.Trim();
        if (string.IsNullOrEmpty(id))
        {
            failReason = "员工ID为空";
            return false;
        }

        if (!employeeRepository.TryGetById(id, out HREmployeeData employee) || employee == null)
        {
            failReason = "仓库中不存在该鼠鼠";
            return false;
        }

        if (_roomByEmployeeId.TryGetValue(id, out RoomProductionUnit owner) && owner != null && owner != room)
        {
            failReason = "该鼠鼠已分配到其他房间";
            return false;
        }

        if (!_assignedIdsByRoom.TryGetValue(room, out List<string> assignedIds) || assignedIds == null)
        {
            assignedIds = new List<string>();
            _assignedIdsByRoom[room] = assignedIds;
        }

        if (assignedIds.Contains(id))
        {
            failReason = "该鼠鼠已在当前房间";
            return false;
        }

        int required = Mathf.Max(0, room.plan.requiredSquirrels);
        if (required > 0 && assignedIds.Count >= required)
        {
            failReason = "该房间已达到所需鼠鼠数量";
            return false;
        }

        if (!CheckPlanRules(room.plan, employee, out failReason))
        {
            return false;
        }

        assignedIds.Add(id);
        _roomByEmployeeId[id] = room;

        ApplyPendingBonus(room);
        OnRoomAssignmentsChanged?.Invoke(room);
        return true;
    }

    public bool TryUnassign(RoomProductionUnit room, string employeeId)
    {
        if (room == null || string.IsNullOrWhiteSpace(employeeId))
        {
            return false;
        }

        string id = employeeId.Trim();
        if (!_assignedIdsByRoom.TryGetValue(room, out List<string> assignedIds) || assignedIds == null)
        {
            return false;
        }

        bool removed = assignedIds.Remove(id);
        if (!removed)
        {
            return false;
        }

        _roomByEmployeeId.Remove(id);

        if (assignedIds.Count == 0)
        {
            _assignedIdsByRoom.Remove(room);
        }

        ApplyPendingBonus(room);
        OnRoomAssignmentsChanged?.Invoke(room);
        return true;
    }

    public void ClearRoom(RoomProductionUnit room)
    {
        if (room == null)
        {
            return;
        }

        if (!_assignedIdsByRoom.TryGetValue(room, out List<string> assignedIds) || assignedIds == null)
        {
            room.SetPendingCycleBonusFromEmployees(new List<HREmployeeData>());
            return;
        }

        for (int i = 0; i < assignedIds.Count; i++)
        {
            _roomByEmployeeId.Remove(assignedIds[i]);
        }

        _assignedIdsByRoom.Remove(room);
        room.SetPendingCycleBonusFromEmployees(new List<HREmployeeData>());
        OnRoomAssignmentsChanged?.Invoke(room);
    }

    public bool CanAssignToRoom(RoomProductionUnit room, HREmployeeData employee, out string reason)
    {
        reason = string.Empty;

        if (room == null)
        {
            reason = "房间为空";
            return false;
        }

        if (employee == null || string.IsNullOrWhiteSpace(employee.id))
        {
            reason = "鼠鼠数据无效";
            return false;
        }

        if (_roomByEmployeeId.TryGetValue(employee.id, out RoomProductionUnit owner) && owner != null && owner != room)
        {
            reason = "已分配到其他房间";
            return false;
        }

        int required = Mathf.Max(0, room.plan.requiredSquirrels);
        int assignedCount = GetAssignedCount(room);
        if (required > 0 && assignedCount >= required)
        {
            if (_assignedIdsByRoom.TryGetValue(room, out List<string> ids) && ids != null && ids.Contains(employee.id))
            {
                return true;
            }

            reason = "房间名额已满";
            return false;
        }

        return CheckPlanRules(room.plan, employee, out reason);
    }

    private bool CheckPlanRules(RoomProductionPlan plan, HREmployeeData employee, out string reason)
    {
        reason = string.Empty;
        if (plan == null)
        {
            return true;
        }

        if (plan.requiredEmployeeIds != null && plan.requiredEmployeeIds.Count > 0)
        {
            bool allowed = false;
            for (int i = 0; i < plan.requiredEmployeeIds.Count; i++)
            {
                string id = plan.requiredEmployeeIds[i];
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                if (string.Equals(id.Trim(), employee.id, StringComparison.Ordinal))
                {
                    allowed = true;
                    break;
                }
            }

            if (!allowed)
            {
                reason = "该房间要求指定鼠鼠，当前鼠鼠不在 required 列表";
                return false;
            }
        }

        if (!CanWorkByRoomType(employee, plan.workType))
        {
            reason = "该鼠鼠不满足房间岗位限制";
            return false;
        }

        if (plan.traitRules == null || plan.traitRules.Count == 0)
        {
            return true;
        }

        for (int i = 0; i < plan.traitRules.Count; i++)
        {
            RoomEmployeeTraitRule rule = plan.traitRules[i];
            if (rule == null)
            {
                continue;
            }

            bool hasTrait = HasTrait(employee, rule.trait);
            if (rule.mode == RoomEmployeeTraitRuleMode.Required && !hasTrait)
            {
                reason = "不满足房间特殊词条 Required 条件";
                return false;
            }

            if (rule.mode == RoomEmployeeTraitRuleMode.Forbidden && hasTrait)
            {
                reason = "命中房间特殊词条 Forbidden 限制";
                return false;
            }
        }

        return true;
    }

    private void ApplyPendingBonus(RoomProductionUnit room)
    {
        if (room == null)
        {
            return;
        }

        List<HREmployeeData> assigned = GetAssignedEmployees(room);
        room.SetPendingCycleBonusFromEmployees(assigned);
    }

    private bool EnsureRepository()
    {
        if (employeeRepository != null)
        {
            return true;
        }

        employeeRepository = HREmployeeRepository.Instance;
        if (employeeRepository == null)
        {
            employeeRepository = FindObjectOfType<HREmployeeRepository>();
        }

        return employeeRepository != null;
    }

    private void CleanupDestroyedRooms()
    {
        if (_assignedIdsByRoom.Count == 0)
        {
            return;
        }

        List<RoomProductionUnit> toRemove = null;
        foreach (KeyValuePair<RoomProductionUnit, List<string>> kv in _assignedIdsByRoom)
        {
            if (kv.Key != null)
            {
                continue;
            }

            if (toRemove == null)
            {
                toRemove = new List<RoomProductionUnit>();
            }

            toRemove.Add(kv.Key);
        }

        if (toRemove == null)
        {
            return;
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            RemoveRoomInternal(toRemove[i]);
        }
    }

    private void RemoveRoomInternal(RoomProductionUnit room)
    {
        if (!_assignedIdsByRoom.TryGetValue(room, out List<string> ids) || ids == null)
        {
            _assignedIdsByRoom.Remove(room);
            return;
        }

        for (int i = 0; i < ids.Count; i++)
        {
            _roomByEmployeeId.Remove(ids[i]);
        }

        _assignedIdsByRoom.Remove(room);
    }

    private static bool CanWorkByRoomType(HREmployeeData employee, RoomEmployeeWorkType workType)
    {
        switch (workType)
        {
            case RoomEmployeeWorkType.Farm:
                return employee.canFarm;
            case RoomEmployeeWorkType.Cook:
                return employee.canCook;
            case RoomEmployeeWorkType.PineconePlant:
                return employee.canPineconePlant;
            default:
                return true;
        }
    }

    private static bool HasTrait(HREmployeeData employee, HREmployeeTraitType trait)
    {
        if (employee == null || employee.traits == null)
        {
            return false;
        }

        for (int i = 0; i < employee.traits.Count; i++)
        {
            if (employee.traits[i] == trait)
            {
                return true;
            }
        }

        return false;
    }
}
