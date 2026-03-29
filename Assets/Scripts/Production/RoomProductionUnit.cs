using System;
using UnityEngine;
using System.Collections.Generic;

// 房间运行状态：运行、手动停运、资源不足停摆。
public enum RoomProductionState
{
    Running = 0,
    PausedManual = 1,
    PausedNoResource = 2,
}

public enum RoomEmployeeWorkType
{
    Generic = 0,
    Farm = 1,
    Cook = 2,
    PineconePlant = 3,
    Gym = 4,
    Library = 5,
    MagicRoom = 6,
    HR = 7,
}

public enum RoomEmployeeTraitRuleMode
{
    Required = 0,
    Preferred = 1,
    Forbidden = 2,
}

[Serializable]
public class RoomEmployeeTraitRule
{
    public HREmployeeTraitType trait = HREmployeeTraitType.InsectPhobia;
    public RoomEmployeeTraitRuleMode mode = RoomEmployeeTraitRuleMode.Preferred;
    [Min(0f)] public float scoreWeight = 1f;
}

// 单个房间的生产配置：周期时长、周期消耗、周期产出。
[Serializable]
public class RoomProductionPlan
{
    public string roomId;
    public List<ResourceAmount> constructionCosts = new List<ResourceAmount>();
    public int requiredSquirrels = 0;
    public RoomEmployeeWorkType workType = RoomEmployeeWorkType.Generic;
    public List<string> requiredEmployeeIds = new List<string>();
    public List<RoomEmployeeTraitRule> traitRules = new List<RoomEmployeeTraitRule>();
    public float cycleSeconds = 30f;
    public List<ResourceAmount> cycleCosts = new List<ResourceAmount>();
    public List<ResourceAmount> cycleOutputs = new List<ResourceAmount>();
}


// 单个房间实例的运行状态机：建造完成即运行，到点结算，资源不足停摆，支持手动停运/重启。
public class RoomProductionUnit : MonoBehaviour
{
    private const float ProductionCycleSeconds = 30f;

    [Header("依赖")]
    public ResourceManager resourceManager;
    public WorkforceManager workforceManager;

    [Header("生产配置")]
    public RoomProductionPlan plan = new RoomProductionPlan();

    [Header("生命周期")]
    public bool builtAtStart = false;

    [Header("调试")]
    public bool enableDebugLogs = true;

    public RoomProductionState State { get; private set; } = RoomProductionState.PausedManual;
    public bool IsBuilt { get; private set; }
    public double NextSettleTime { get; private set; }

    public event Action<RoomProductionUnit, RoomProductionState> OnStateChanged;
    public event Action<RoomProductionUnit> OnConstructionCompleted;
    public event Action<RoomProductionUnit> OnConstructionFailedByResource;
    public event Action<RoomProductionUnit> OnWorkerAllocationFailed;
    public event Action<RoomProductionUnit> OnSettled;
    public event Action<RoomProductionUnit> OnSettleFailedByResource; //因资源不够而停摆

    private bool _hasStarted;
    private float _activeOutputMultiplier = 1f;
    private float _pendingOutputMultiplier = 1f;

    void Awake()
    {
        if (resourceManager == null)
        {
            resourceManager = ResourceManager.Instance;
            if (resourceManager == null)
            {
                resourceManager = FindObjectOfType<ResourceManager>();
            }
        }

        if (workforceManager == null)
        {
            workforceManager = WorkforceManager.Instance;
            if (workforceManager == null)
            {
                workforceManager = FindObjectOfType<WorkforceManager>();
            }
        }
    }

    void OnEnable()
    {
        RoomProductionScheduler scheduler = RoomProductionScheduler.Instance;
        if (scheduler == null)
        {
            scheduler = FindObjectOfType<RoomProductionScheduler>();
        }

        if (scheduler == null)
        {
            GameObject schedulerObject = new GameObject("RoomProductionScheduler_Auto");
            scheduler = schedulerObject.AddComponent<RoomProductionScheduler>();
        }

        if (scheduler != null)
        {
            scheduler.Register(this);
        }

        if (_hasStarted)
        {
            TryAutoStartIfNeeded();
        }
    }

    void Start()
    {
        _hasStarted = true;
        TryAutoStartIfNeeded();
    }

    void OnDisable()
    {
        RoomProductionScheduler scheduler = RoomProductionScheduler.Instance;
        if (scheduler != null)
        {
            scheduler.Unregister(this);
        }

        ReleaseWorkers();
    }

    // 建造完成后调用：兼容旧接口，内部走可失败建造逻辑。
    public void CompleteConstructionAndStart()
    {
        TryCompleteConstructionAndStart();
    }

    public void ApplyPlan(RoomProductionPlan newPlan)
    {
        plan = ClonePlan(newPlan);
    }

    public float GetActiveOutputMultiplier()
    {
        return _activeOutputMultiplier;
    }

    public float GetPendingOutputMultiplier()
    {
        return _pendingOutputMultiplier;
    }

    // 配置后只会更新下一周期倍率，不影响当前正在进行的30秒。
    public void SetPendingCycleBonusFromEmployees(IReadOnlyList<HREmployeeData> employees)
    {
        _pendingOutputMultiplier = Mathf.Max(0f, CalculateOutputMultiplier(employees));
    }

    // V2规则桥接入口：直接写入下一周期倍率，不影响当前正在进行的30秒。
    public void SetPendingOutputMultiplierExternal(float multiplier)
    {
        _pendingOutputMultiplier = Mathf.Max(0f, multiplier);
    }

    // V2规则桥接入口：用于“配置后立刻生效”。仅在运行中房间应用到当前周期。
    public void ApplyCurrentOutputMultiplierExternal(float multiplier)
    {
        if (State != RoomProductionState.Running)
        {
            return;
        }

        _activeOutputMultiplier = Mathf.Max(0f, multiplier);
    }

    // 尝试完成建造并启动运行：
    // 1) 未建造时会先校验并扣除建造成本
    // 2) 已建造则直接进入运行
    public bool TryCompleteConstructionAndStart()
    {
        if (!IsBuilt && resourceManager == null)
        {
            Debug.LogWarning($"{name} 建造失败: 未找到 ResourceManager");
            return false;
        }

        if (!IsBuilt)
        {
            if (!resourceManager.CanAfford(plan.constructionCosts))
            {
                LogDebug("初始建设失败: 资源不足, costs=" + FormatResourceList(plan.constructionCosts));
                ChangeState(RoomProductionState.PausedNoResource);
                OnConstructionFailedByResource?.Invoke(this);
                return false;
            }

            if (!resourceManager.TrySpend(plan.constructionCosts))
            {
                LogDebug("初始建设失败: 扣除资源失败, costs=" + FormatResourceList(plan.constructionCosts));
                ChangeState(RoomProductionState.PausedNoResource);
                OnConstructionFailedByResource?.Invoke(this);
                return false;
            }

            IsBuilt = true;
            LogDebug("初始建设完成并扣费: costs=" + FormatResourceList(plan.constructionCosts));
            OnConstructionCompleted?.Invoke(this);
        }

        // 建设与运行解耦：允许先建造成功，待配置松鼠后再进入30秒生产周期。
        StartRunning();
        return IsBuilt;
    }

    // 手动停运：无论当前是否运行，都转到手动停运状态。
    public void PauseManual()
    {
        ChangeState(RoomProductionState.PausedManual);
    }

    // 手动重启：从停运状态恢复为运行，并重新开始一个完整周期。
    public void ResumeManual()
    {
        if (!IsBuilt) return;
        StartRunning();
    }

    // 仅用于UI判断：当前资源是否足够支付建造成本。
    public bool CanAffordConstruction()
    {
        if (resourceManager == null) return false;
        if (IsBuilt) return true;
        return resourceManager.CanAfford(plan.constructionCosts);
    }

    // 调度器调用：只在运行状态且到达结算点时尝试结算。
    public void Tick(double now)
    {
        if (!IsBuilt) return;
        if (State != RoomProductionState.Running) return;
        if (now < NextSettleTime) return;

        TrySettle(now);
    }

    // 返回距离下次结算的剩余秒数，UI可直接显示倒计时。
    public float GetRemainingSeconds(double now)
    {
        if (State != RoomProductionState.Running) return 0f;
        return Mathf.Max(0f, (float)(NextSettleTime - now));
    }

    // 每天结束时调用：清空“正在进行中”的周期进度并从整周期重新计时。
    public void ResetProgressForNewDay(double now)
    {
        if (!IsBuilt) return;
        if (State != RoomProductionState.Running) return;

        float cycle = ProductionCycleSeconds;
        NextSettleTime = now + cycle;
    }

    private void StartRunning()
    {
        if (resourceManager == null)
        {
            Debug.LogWarning($"{name} 启动失败: 未找到 ResourceManager");
            return;
        }

        if (!HasRequiredAssignments())
        {
            int required = Mathf.Max(0, plan.requiredSquirrels);
            int assigned = 0;
            RoomEmployeeAssignmentManager manager = RoomEmployeeAssignmentManager.Instance;
            if (manager != null)
            {
                assigned = manager.GetAssignedCount(this);
            }

            LogDebug("已建设，等待松鼠配置后启动: required=" + required + ", assigned=" + assigned);
            SyncPendingMultiplierFromAssignments();
            ChangeState(RoomProductionState.PausedManual);
            return;
        }

        if (!TryAcquireWorkers())
        {
            LogDebug("启动失败: 员工不足, requiredSquirrels=" + Mathf.Max(0, plan.requiredSquirrels));
            ChangeState(RoomProductionState.PausedNoResource);
            OnWorkerAllocationFailed?.Invoke(this);
            return;
        }

        float cycle = ProductionCycleSeconds;
        NextSettleTime = Time.timeAsDouble + cycle;
        _activeOutputMultiplier = 1f;
        SyncPendingMultiplierFromAssignments();
        LogDebug("开始运行: settleIn=" + cycle.ToString("0.0") + "s");
        ChangeState(RoomProductionState.Running);
    }

    private bool HasRequiredAssignments()
    {
        int required = Mathf.Max(0, plan.requiredSquirrels);
        if (required <= 0)
        {
            return true;
        }

        RoomEmployeeAssignmentManager manager = RoomEmployeeAssignmentManager.Instance;
        if (manager == null)
        {
            manager = FindObjectOfType<RoomEmployeeAssignmentManager>();
        }

        if (manager == null)
        {
            return false;
        }

        return manager.GetAssignedCount(this) >= required;
    }

    // 结算规则：到点时检查资源，足够则扣费并产出；不足则停摆。
    private void TrySettle(double now)
    {
        if (resourceManager == null)
        {
            ChangeState(RoomProductionState.PausedNoResource);
            return;
        }

        if (!resourceManager.CanAfford(plan.cycleCosts))
        {
            LogDebug("周期结算失败: 资源不足, costs=" + FormatResourceList(plan.cycleCosts));
            ChangeState(RoomProductionState.PausedNoResource);
            ReleaseWorkers();
            OnSettleFailedByResource?.Invoke(this);
            return;
        }

        resourceManager.TrySpend(plan.cycleCosts);

        List<ResourceAmount> baseCycleOutputs = CloneResourceList(plan.cycleOutputs);
        List<ResourceAmount> currentCycleOutputs = BuildOutputsWithMultiplier(plan.cycleOutputs, _activeOutputMultiplier);
        resourceManager.Add(currentCycleOutputs);

        LogDebug("周期结算成功: -" + FormatResourceList(plan.cycleCosts)
            + ", baseOut=" + FormatResourceList(baseCycleOutputs)
            + ", +" + FormatResourceList(currentCycleOutputs)
            + ", currentMul=" + _activeOutputMultiplier.ToString("0.###")
            + ", nextMul=" + _pendingOutputMultiplier.ToString("0.###")
            + ", calcDetail={" + BuildCycleSettleDebugDetail() + "}");

        float cycle = ProductionCycleSeconds;
        NextSettleTime = now + cycle;
        _activeOutputMultiplier = _pendingOutputMultiplier;

        OnSettled?.Invoke(this);
    }

    private void ChangeState(RoomProductionState next)
    {
        if (State == next) return;

        if (next != RoomProductionState.Running)
        {
            ReleaseWorkers();
        }

        State = next;
        LogDebug("状态切换 => " + State);
        OnStateChanged?.Invoke(this, State);
    }

    private bool TryAcquireWorkers()
    {
        int required = Mathf.Max(0, plan.requiredSquirrels);
        if (required == 0) return true;

        if (workforceManager == null)
        {
            Debug.LogWarning($"{name} 员工分配失败 未找到 WorkforceManager");
            return false;
        }

        return workforceManager.TryAllocate(this, required);
    }

    private void ReleaseWorkers()
    {
        if (workforceManager == null) return;
        workforceManager.Release(this);
    }

    private void TryAutoStartIfNeeded()
    {
        if (!builtAtStart || IsBuilt)
        {
            return;
        }

        bool ok = TryCompleteConstructionAndStart();
        if (!ok)
        {
            Debug.Log($"{name} 自动建造失败: 资源不足，进入停摆状态，等待后续手动重试。");
        }
    }

    private static RoomProductionPlan ClonePlan(RoomProductionPlan source)
    {
        RoomProductionPlan src = source ?? new RoomProductionPlan();

        return new RoomProductionPlan
        {
            roomId = src.roomId,
            constructionCosts = CloneResourceList(src.constructionCosts),
            requiredSquirrels = Mathf.Max(0, src.requiredSquirrels),
            workType = src.workType,
            requiredEmployeeIds = CloneStringList(src.requiredEmployeeIds),
            traitRules = CloneTraitRules(src.traitRules),
            cycleSeconds = Mathf.Max(0.1f, src.cycleSeconds),
            cycleCosts = CloneResourceList(src.cycleCosts),
            cycleOutputs = CloneResourceList(src.cycleOutputs),
        };
    }

    private static List<string> CloneStringList(List<string> source)
    {
        if (source == null)
        {
            return new List<string>();
        }

        List<string> result = new List<string>(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            result.Add(source[i]);
        }

        return result;
    }

    private static List<RoomEmployeeTraitRule> CloneTraitRules(List<RoomEmployeeTraitRule> source)
    {
        if (source == null)
        {
            return new List<RoomEmployeeTraitRule>();
        }

        List<RoomEmployeeTraitRule> result = new List<RoomEmployeeTraitRule>(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            RoomEmployeeTraitRule rule = source[i];
            if (rule == null)
            {
                continue;
            }

            result.Add(new RoomEmployeeTraitRule
            {
                trait = rule.trait,
                mode = rule.mode,
                scoreWeight = Mathf.Max(0f, rule.scoreWeight),
            });
        }

        return result;
    }

    private static List<ResourceAmount> CloneResourceList(List<ResourceAmount> source)
    {
        if (source == null)
        {
            return new List<ResourceAmount>();
        }

        List<ResourceAmount> result = new List<ResourceAmount>(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            result.Add(source[i]);
        }

        return result;
    }

    private void LogDebug(string message)
    {
        if (!enableDebugLogs)
        {
            return;
        }

        Debug.Log("[RoomProductionUnit] " + name + " | " + message, this);
    }

    private static string FormatResourceList(List<ResourceAmount> values)
    {
        if (values == null || values.Count == 0)
        {
            return "none";
        }

        List<string> parts = new List<string>(values.Count);
        for (int i = 0; i < values.Count; i++)
        {
            ResourceAmount value = values[i];
            parts.Add(value.type + ":" + value.amount);
        }

        return string.Join(", ", parts);
    }

    private string BuildCycleSettleDebugDetail()
    {
        RoomProductionModifierBreakdownV2 breakdown;
        if (!RoomProductionModifierBridgeV2.TryGetLatestBreakdown(this, out breakdown) || breakdown == null)
        {
            return "v2Breakdown=none";
        }

        string squirrelDetails = "none";
        if (breakdown.employeeRateDetails != null && breakdown.employeeRateDetails.Count > 0)
        {
            squirrelDetails = string.Join(" | ", breakdown.employeeRateDetails);
        }

        return "roomId=" + breakdown.roomId
            + ", mainStat=" + breakdown.mainStat
            + ", employees=" + breakdown.employeeCount
            + ", statRateSum=" + breakdown.statRateSum.ToString("0.###")
            + ", formula=max(0, 1+statRateSum)=" + breakdown.finalMultiplier.ToString("0.###")
            + ", squirrels=[" + squirrelDetails + "]";
    }

    private void SyncPendingMultiplierFromAssignments()
    {
        RoomEmployeeAssignmentManager manager = RoomEmployeeAssignmentManager.Instance;
        if (manager == null)
        {
            _pendingOutputMultiplier = 1f;
            return;
        }

        List<HREmployeeData> assigned = manager.GetAssignedEmployees(this);
        _pendingOutputMultiplier = Mathf.Max(0f, CalculateOutputMultiplier(assigned));
    }

    private float CalculateOutputMultiplier(IReadOnlyList<HREmployeeData> employees)
    {
        if (employees == null || employees.Count == 0)
        {
            return 1f;
        }

        float totalRate = 0f;
        for (int i = 0; i < employees.Count; i++)
        {
            HREmployeeData employee = employees[i];
            if (employee == null)
            {
                continue;
            }

            int stat = GetCoreStatForWorkType(employee);
            totalRate += HREmployeeData.GetProductionModifierRate(stat);
            totalRate += GetTraitRateBonus(employee);
        }

        float avgRate = totalRate / Mathf.Max(1, employees.Count);
        return Mathf.Max(0f, 1f + avgRate);
    }

    private int GetCoreStatForWorkType(HREmployeeData employee)
    {
        switch (plan.workType)
        {
            case RoomEmployeeWorkType.Farm:
            case RoomEmployeeWorkType.Gym:
                return employee.stamina;
            case RoomEmployeeWorkType.Cook:
            case RoomEmployeeWorkType.Library:
            case RoomEmployeeWorkType.HR:
                return employee.intelligence;
            case RoomEmployeeWorkType.MagicRoom:
                return employee.magic;
            case RoomEmployeeWorkType.PineconePlant:
                return Mathf.RoundToInt((employee.stamina + employee.intelligence) * 0.5f);
            default:
                return Mathf.RoundToInt((employee.stamina + employee.intelligence + employee.magic) / 3f);
        }
    }

    private float GetTraitRateBonus(HREmployeeData employee)
    {
        if (employee.traits == null || employee.traits.Count == 0)
        {
            return 0f;
        }

        float bonus = 0f;
        for (int i = 0; i < employee.traits.Count; i++)
        {
            HREmployeeTraitType trait = employee.traits[i];
            switch (trait)
            {
                case HREmployeeTraitType.GardeningExpert:
                    if (plan.workType == RoomEmployeeWorkType.Farm || plan.workType == RoomEmployeeWorkType.PineconePlant)
                    {
                        bonus += 0.03f;
                    }
                    break;
                case HREmployeeTraitType.SmartTalent:
                    if (plan.workType == RoomEmployeeWorkType.Cook || plan.workType == RoomEmployeeWorkType.Library || plan.workType == RoomEmployeeWorkType.HR)
                    {
                        bonus += 0.03f;
                    }
                    break;
                case HREmployeeTraitType.MagicalGirl:
                    if (plan.workType == RoomEmployeeWorkType.MagicRoom)
                    {
                        bonus += 0.03f;
                    }
                    break;
                case HREmployeeTraitType.LuckyMouse:
                    bonus += 0.02f;
                    break;
                case HREmployeeTraitType.LazySyndrome:
                    bonus -= 0.03f;
                    break;
                case HREmployeeTraitType.LearningDisability:
                    if (plan.workType == RoomEmployeeWorkType.Cook || plan.workType == RoomEmployeeWorkType.Library || plan.workType == RoomEmployeeWorkType.HR)
                    {
                        bonus -= 0.03f;
                    }
                    break;
                case HREmployeeTraitType.LowComprehension:
                    if (plan.workType == RoomEmployeeWorkType.MagicRoom)
                    {
                        bonus -= 0.03f;
                    }
                    break;
            }
        }

        return bonus;
    }

    private static List<ResourceAmount> BuildOutputsWithMultiplier(List<ResourceAmount> baseOutputs, float multiplier)
    {
        if (baseOutputs == null || baseOutputs.Count == 0)
        {
            return new List<ResourceAmount>();
        }

        float safeMultiplier = Mathf.Max(0f, multiplier);
        List<ResourceAmount> result = new List<ResourceAmount>(baseOutputs.Count);
        for (int i = 0; i < baseOutputs.Count; i++)
        {
            ResourceAmount output = baseOutputs[i];
            int amount = Mathf.Max(0, Mathf.RoundToInt(output.amount * safeMultiplier));
            result.Add(new ResourceAmount
            {
                type = output.type,
                amount = amount,
            });
        }

        return result;
    }
}
