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
    public HREmployeeRepository employeeRepository;
    public HRRecruitPanel recruitPanel;

    private float _nextGenerateAt;
    private bool _canGenerate;

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
            employeeRepository = FindObjectOfType<HREmployeeRepository>();
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
            failReason = "未找到 HREmployeeRepository";
            return false;
        }

        ResourceAmount cost = new ResourceAmount
        {
            type = ResourceType.Fruit,
            amount = recruitFruitCost,
        };

        if (!resourceManager.TrySpend(cost))
        {
            float current = resourceManager.Get(ResourceType.Fruit);
            failReason = $"果实不足（需要 {recruitFruitCost}，当前 {current:0}）";
            return false;
        }

        employee = HRRecruitService.Recruit(hrIntelligence, eliteHrBonus);
        employeeRepository.Add(employee);

        if (syncSquirrelResourceOnRecruit)
        {
            resourceManager.Add(ResourceType.Squirrel, 1);
        }

        return true;
    }

    public bool TryGenerateEmployee(out HREmployeeData employee)
    {
        employee = null;

        if (employeeRepository == null)
        {
            employeeRepository = HREmployeeRepository.Instance;
            if (employeeRepository == null)
            {
                employeeRepository = FindObjectOfType<HREmployeeRepository>();
            }
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
        employeeRepository.Add(employee);

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
}
