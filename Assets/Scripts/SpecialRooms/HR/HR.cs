using UnityEngine;

public class HR : MonoBehaviour
{
    [Header("招募参数")]
    public int recruitFruitCost = 50;
    public int hrIntelligence = 3;
    public bool eliteHrBonus = false;
    public bool syncSquirrelResourceOnRecruit = true;
    public bool openOnMouseDown = false;

    [Header("依赖")]
    public ResourceManager resourceManager;
    public HREmployeeRepository employeeRepository;
    public HRRecruitPanel recruitPanel;

    void Awake()
    {
        if (resourceManager == null)
        {
            resourceManager = FindObjectOfType<ResourceManager>();
        }

        if (employeeRepository == null)
        {
            employeeRepository = FindObjectOfType<HREmployeeRepository>();
        }

        TryAutoBindRecruitPanel();
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

        if (employeeRepository.IsFull)
        {
            failReason = $"员工已满（上限 {employeeRepository.Capacity}）";
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
        if (!employeeRepository.TryAdd(employee))
        {
            resourceManager.Add(ResourceType.Fruit, recruitFruitCost);
            employee = null;
            failReason = $"员工已满（上限 {employeeRepository.Capacity}）";
            return false;
        }

        if (syncSquirrelResourceOnRecruit)
        {
            resourceManager.Add(ResourceType.Squirrel, 1);
        }

        return true;
    }
}
