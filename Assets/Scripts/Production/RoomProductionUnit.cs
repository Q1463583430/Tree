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

// 单个房间的生产配置：周期时长、周期消耗、周期产出。
[Serializable]
public class RoomProductionPlan
{
    public string roomId;
    public List<ResourceAmount> constructionCosts = new List<ResourceAmount>();
    public int requiredSquirrels = 0;
    public float cycleSeconds = 30f;
    public List<ResourceAmount> cycleCosts = new List<ResourceAmount>();
    public List<ResourceAmount> cycleOutputs = new List<ResourceAmount>();
}


// 单个房间实例的运行状态机：建造完成即运行，到点结算，资源不足停摆，支持手动停运/重启。
public class RoomProductionUnit : MonoBehaviour
{
    [Header("生产周期")]
    [Min(0.1f)]
    public float ProductionCycleSeconds = 30f;

    [Header("依赖")]
    public ResourceManager resourceManager;
    public WorkforceManager workforceManager;

    [Header("生产配置")]
    public RoomProductionPlan plan = new RoomProductionPlan();

    [Header("生命周期")]
    public bool builtAtStart = true;

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

    // 尝试完成建造并启动运行：
    // 1) 未建造时会先校验并扣除建造成本
    // 2) 已建造则直接进入运行
    public bool TryCompleteConstructionAndStart()
    {
        if (resourceManager == null)
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

            resourceManager.TrySpend(plan.constructionCosts);
            IsBuilt = true;
            LogDebug("初始建设完成并扣费: costs=" + FormatResourceList(plan.constructionCosts));
            OnConstructionCompleted?.Invoke(this);
        }

        StartRunning();
        return true;
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

    // 返回当前周期进度(0~1)，用于进度条显示。
    public float GetProgress01(double now)
    {
        if (!IsBuilt) return 0f;
        if (State != RoomProductionState.Running) return 0f;

        float cycle = GetCycleSeconds();
        double cycleStart = NextSettleTime - cycle;
        double elapsed = now - cycleStart;
        return Mathf.Clamp01((float)(elapsed / cycle));
    }

    public float GetCycleSeconds()
    {
        return Mathf.Max(0.1f, ProductionCycleSeconds);
    }

    // 每天结束时调用：清空“正在进行中”的周期进度并从整周期重新计时。
    public void ResetProgressForNewDay(double now)
    {
        if (!IsBuilt) return;
        if (State != RoomProductionState.Running) return;

        float cycle = GetCycleSeconds();
        NextSettleTime = now + cycle;
    }

    private void StartRunning()
    {
        if (resourceManager == null)
        {
            Debug.LogWarning($"{name} 启动失败: 未找到 ResourceManager");
            return;
        }

        if (!TryAcquireWorkers())
        {
            LogDebug("启动失败: 员工不足, requiredSquirrels=" + Mathf.Max(0, plan.requiredSquirrels));
            ChangeState(RoomProductionState.PausedNoResource);
            OnWorkerAllocationFailed?.Invoke(this);
            return;
        }

        float cycle = GetCycleSeconds();
        NextSettleTime = Time.timeAsDouble + cycle;
        LogDebug("开始运行: settleIn=" + cycle.ToString("0.0") + "s");
        ChangeState(RoomProductionState.Running);
    }

    // 结算规则：到点时检查资源，足够则扣费并产出；不足则停摆。
    private void TrySettle(double now)
    {
        if (resourceManager == null)
        {
            ChangeState(RoomProductionState.PausedNoResource);
            return;
        }

        if (workforceManager != null && workforceManager.HasAssignedStrikingEmployee(this))
        {
            LogDebug("周期结算跳过: 分配到罢工员工，本周期无产出");
            float strikeCycle = GetCycleSeconds();
            NextSettleTime = now + strikeCycle;
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
        resourceManager.Add(plan.cycleOutputs);

        LogDebug("周期结算成功: -" + FormatResourceList(plan.cycleCosts) + ", +" + FormatResourceList(plan.cycleOutputs));

        float cycle = GetCycleSeconds();
        NextSettleTime = now + cycle;

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
            Debug.LogWarning($"{name} 员工分配失败: 未找到 WorkforceManager");
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
            cycleSeconds = Mathf.Max(0.1f, src.cycleSeconds),
            cycleCosts = CloneResourceList(src.cycleCosts),
            cycleOutputs = CloneResourceList(src.cycleOutputs),
        };
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
}
