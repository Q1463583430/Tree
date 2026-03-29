using UnityEngine;

public class HR : MonoBehaviour
{
    [Header("招募参数")]
    public int recruitFruitCost = 50;
    public int hrIntelligence = 3;
    public bool eliteHrBonus = false;
    public bool syncSquirrelResourceOnRecruit = true;
    public bool openOnMouseDown = false;

    [Header("自动产鼠")]
    public bool autoGenerateEmployee = false;
    [Min(1f)] public float generateIntervalSeconds = 30f;
    public bool requireManualPlacementToStartGenerate = true;

    [Header("依赖")]
    public ResourceManager resourceManager;
    public EmployeeRepository employeeRepository;
    public HRRecruitPanel recruitPanel;

    [SerializeField] private bool hasRecruitedOnce;
    [SerializeField] private string lastRecruitedEmployeeId;

    private float _nextGenerateAt;
    private bool _canGenerate;
    private RoomProductionUnit _roomProductionUnit;

    public bool HasRecruitedOnce => hasRecruitedOnce;
    public string LastRecruitedEmployeeId => lastRecruitedEmployeeId;

    void Awake()
    {
        // 当前版本需求：仅允许手动招募，禁用自动产鼠。
        autoGenerateEmployee = false;

        if (resourceManager == null)
        {
            resourceManager = FindObjectOfType<ResourceManager>();
        }

        if (employeeRepository == null)
        {
            employeeRepository = EmployeeRepository.GetOrCreateInstance();
        }

        if (hasRecruitedOnce && string.IsNullOrWhiteSpace(lastRecruitedEmployeeId) && employeeRepository != null && employeeRepository.Count > 0)
        {
            HREmployeeData fallback = employeeRepository.Employees[employeeRepository.Count - 1];
            if (fallback != null)
            {
                lastRecruitedEmployeeId = fallback.id;
            }
        }

        if (_roomProductionUnit == null)
        {
            _roomProductionUnit = GetComponent<RoomProductionUnit>();
            if (_roomProductionUnit == null)
            {
                _roomProductionUnit = GetComponentInChildren<RoomProductionUnit>(true);
            }
        }

        if (_roomProductionUnit != null)
        {
            _roomProductionUnit.plan.requiredSquirrels = Mathf.Max(1, _roomProductionUnit.plan.requiredSquirrels);
            _roomProductionUnit.plan.workType = RoomEmployeeWorkType.HR;
        }

        TryAutoBindRecruitPanel();

        float interval = Mathf.Max(1f, generateIntervalSeconds);
        _nextGenerateAt = Time.time + interval;
        _canGenerate = !requireManualPlacementToStartGenerate;
    }

    void Update()
    {
        if (!autoGenerateEmployee)
        {
            return;
        }

        if (!_canGenerate)
        {
            return;
        }

        if (Time.time < _nextGenerateAt)
        {
            return;
        }

        float interval = Mathf.Max(1f, generateIntervalSeconds);

        // 最多补发5次，避免长卡顿后在同一帧生成过多。
        int safety = 0;
        while (Time.time >= _nextGenerateAt && safety < 5)
        {
            TryGenerateEmployee(out _);
            _nextGenerateAt += interval;
            safety++;
        }

        if (Time.time >= _nextGenerateAt)
        {
            _nextGenerateAt = Time.time + interval;
        }
    }

    void TryAutoBindRecruitPanel()
    {
        if (recruitPanel != null) return;

        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        foreach (Canvas canvas in canvases)
        {
            if (!canvas.gameObject.scene.IsValid() || !canvas.gameObject.scene.isLoaded)
            {
                continue;
            }

            HRRecruitPanel panel = canvas.GetComponentInChildren<HRRecruitPanel>(true);
            if (panel != null && panel.gameObject.scene.IsValid() && panel.gameObject.scene.isLoaded)
            {
                recruitPanel = panel;
                return;
            }
        }

        HRRecruitPanel[] allPanels = FindObjectsOfType<HRRecruitPanel>(true);
        foreach (HRRecruitPanel panel in allPanels)
        {
            if (panel.gameObject.scene.IsValid() && panel.gameObject.scene.isLoaded)
            {
                recruitPanel = panel;
                return;
            }
        }
    }

    // 可给房间点击事件、UI按钮、或交互系统调用。
    public void OpenRecruitPanel()
    {
        if (recruitPanel == null)
        {
            TryAutoBindRecruitPanel();

            if (recruitPanel == null)
            {
                Debug.LogWarning("[HR] 自动查找 HRRecruitPanel 失败，请检查 Canvas 下是否存在该组件。", this);
                return;
            }
        }

        recruitPanel.hrRoom = this;
        recruitPanel.OpenPanel();
    }

    void OnMouseDown()
    {
        if (!openOnMouseDown) return;
        OpenRecruitPanel();
    }

    public bool TryRecruit(out HREmployeeData employee, out string failReason)
    {
        employee = null;
        failReason = string.Empty;

        if (resourceManager == null)
        {
            failReason = "未找到 ResourceManager";
            return false;
        }

        if (employeeRepository == null)
        {
            employeeRepository = EmployeeRepository.GetOrCreateInstance();
            if (employeeRepository == null)
            {
                failReason = "未找到 EmployeeRepository";
                return false;
            }
        }

        if (!EnsureRoomReadyForRecruit(out failReason))
        {
            return false;
        }

        if (!TrySpendRecruitCost(out failReason))
        {
            return false;
        }

        employee = HRRecruitService.Recruit(hrIntelligence, eliteHrBonus);
        employeeRepository.Add(employee);
        hasRecruitedOnce = true;
        lastRecruitedEmployeeId = employee.id;

        if (syncSquirrelResourceOnRecruit)
        {
            resourceManager.Add(ResourceType.Squirrel, 1);
        }

        return true;
    }

    public bool TryReroll(out HREmployeeData employee, out string failReason)
    {
        employee = null;
        failReason = string.Empty;

        if (resourceManager == null)
        {
            failReason = "未找到 ResourceManager";
            return false;
        }

        if (employeeRepository == null)
        {
            employeeRepository = EmployeeRepository.GetOrCreateInstance();
            if (employeeRepository == null)
            {
                failReason = "未找到 EmployeeRepository";
                return false;
            }
        }

        if (!EnsureRoomReadyForRecruit(out failReason))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(lastRecruitedEmployeeId))
        {
            failReason = "没有可重新招募的上一只鼠鼠，请先招募一次。";
            return false;
        }

        string previousId = lastRecruitedEmployeeId.Trim();
        if (!employeeRepository.TryGetById(previousId, out HREmployeeData previousEmployee) || previousEmployee == null)
        {
            failReason = "上一只鼠鼠已不存在，无法重新招募。";
            return false;
        }

        if (!TrySpendRecruitCost(out failReason))
        {
            return false;
        }

        RoomEmployeeAssignmentManager assignmentManager = RoomEmployeeAssignmentManager.Instance;
        if (assignmentManager != null)
        {
            RoomProductionUnit owner = assignmentManager.GetEmployeeOwner(previousId);
            if (owner != null)
            {
                assignmentManager.TryUnassign(owner, previousId);
            }
        }

        if (!employeeRepository.RemoveById(previousId, out _))
        {
            resourceManager.Add(ResourceType.Fruit, recruitFruitCost);
            failReason = "删除上一只鼠鼠失败，已返还本次果实消耗。";
            return false;
        }

        employee = HRRecruitService.Recruit(hrIntelligence, eliteHrBonus);
        employeeRepository.Add(employee);
        hasRecruitedOnce = true;
        lastRecruitedEmployeeId = employee.id;
        return true;
    }

    private bool TrySpendRecruitCost(out string failReason)
    {
        failReason = string.Empty;

        if (resourceManager == null)
        {
            failReason = "未找到 ResourceManager";
            return false;
        }

        ResourceAmount cost = new ResourceAmount
        {
            type = ResourceType.Fruit,
            amount = recruitFruitCost,
        };

        if (resourceManager.TrySpend(cost))
        {
            return true;
        }

        float current = resourceManager.Get(ResourceType.Fruit);
        failReason = $"果实不足（需要 {recruitFruitCost}，当前 {current:0}）";
        return false;
    }

    private bool EnsureRoomReadyForRecruit(out string reason)
    {
        reason = string.Empty;

        if (_roomProductionUnit == null)
        {
            _roomProductionUnit = GetComponent<RoomProductionUnit>();
            if (_roomProductionUnit == null)
            {
                _roomProductionUnit = GetComponentInChildren<RoomProductionUnit>(true);
            }
        }

        if (_roomProductionUnit == null)
        {
            reason = "未找到 HR 房间生产单元";
            return false;
        }

        _roomProductionUnit.plan.requiredSquirrels = Mathf.Max(1, _roomProductionUnit.plan.requiredSquirrels);

        if (_roomProductionUnit.State != RoomProductionState.Running)
        {
            _roomProductionUnit.ResumeManual();
        }

        if (_roomProductionUnit.State == RoomProductionState.Running)
        {
            return true;
        }

        RoomEmployeeAssignmentManager manager = RoomEmployeeAssignmentManager.Instance;
        int assigned = manager != null ? manager.GetAssignedCount(_roomProductionUnit) : 0;
        int required = Mathf.Max(1, _roomProductionUnit.plan.requiredSquirrels);
        reason = $"HR房间未启动（已分配 {assigned}/{required}）。请先分配松鼠员工。";
        return false;
    }

    public bool TryGenerateEmployee(out HREmployeeData employee)
    {
        employee = null;

        if (employeeRepository == null)
        {
            employeeRepository = EmployeeRepository.GetOrCreateInstance();
        }

        if (employeeRepository == null)
        {
            return false;
        }

        if (resourceManager == null)
        {
            resourceManager = ResourceManager.Instance;
            if (resourceManager == null)
            {
                resourceManager = FindObjectOfType<ResourceManager>();
            }
        }

        employee = HRRecruitService.Recruit(hrIntelligence, eliteHrBonus);
        if (!employeeRepository.TryAdd(employee))
        {
            resourceManager.Add(ResourceType.Fruit, recruitFruitCost);
            employee = null;
            Debug.LogWarning($"[HR] 自动产鼠失败：员工已满（上限 {employeeRepository.Capacity}）", this);
            return false;
        }

        if (syncSquirrelResourceOnRecruit && resourceManager != null)
        {
            resourceManager.Add(ResourceType.Squirrel, 1);
        }

        return true;
    }

    // 由房间放置流程调用：只有手动放置并成功建造后，HR 才开始自动产鼠。
    public void NotifyRoomPlacedAndReady()
    {
        _canGenerate = true;
        _nextGenerateAt = Time.time + Mathf.Max(1f, generateIntervalSeconds);
    }

    public RoomProductionUnit GetRoomProductionUnit()
    {
        if (_roomProductionUnit == null)
        {
            _roomProductionUnit = GetComponent<RoomProductionUnit>();
            if (_roomProductionUnit == null)
            {
                _roomProductionUnit = GetComponentInChildren<RoomProductionUnit>(true);
            }
        }

        return _roomProductionUnit;
    }
}
