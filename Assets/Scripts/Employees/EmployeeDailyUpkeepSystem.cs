using System.Collections.Generic;
using UnityEngine;

// 每日员工维护：
// 1) 每天结束为树增加固定 root
// 2) 结算每只鼠鼠果实消耗
// 3) 未吃到果实的鼠鼠进入罢工（次日分配到房间将无产出）
public class EmployeeDailyUpkeepSystem : MonoBehaviour
{
    [Header("依赖")]
    public ResourceManager resourceManager;
    public EmployeeRepository employeeRepository;
    public RoomProductionScheduler scheduler;

    [Header("每日结算")]
    public int rootPerDay = 150;

    private bool _bound;

    void Awake()
    {
        ResolveDependencies();
    }

    void OnEnable()
    {
        TryBindSchedulerEvent();
    }

    void Update()
    {
        if (!_bound)
        {
            TryBindSchedulerEvent();
        }

        if (resourceManager == null || employeeRepository == null)
        {
            ResolveDependencies();
        }
    }

    void OnDisable()
    {
        if (scheduler != null)
        {
            scheduler.OnDayEnded -= HandleDayEnded;
        }

        _bound = false;
    }

    private void ResolveDependencies()
    {
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

        if (scheduler == null)
        {
            scheduler = RoomProductionScheduler.Instance;
            if (scheduler == null)
            {
                scheduler = FindObjectOfType<RoomProductionScheduler>();
            }
        }
    }

    private void TryBindSchedulerEvent()
    {
        ResolveDependencies();

        if (scheduler == null)
        {
            return;
        }

        scheduler.OnDayEnded -= HandleDayEnded;
        scheduler.OnDayEnded += HandleDayEnded;
        _bound = true;
    }

    private void HandleDayEnded(int day)
    {
        if (resourceManager == null || employeeRepository == null)
        {
            ResolveDependencies();
        }

        if (resourceManager == null || employeeRepository == null)
        {
            return;
        }

        if (rootPerDay != 0)
        {
            resourceManager.Add(ResourceType.Root, rootPerDay);
        }

        IReadOnlyList<HREmployeeData> employees = employeeRepository.Employees;
        if (employees == null || employees.Count == 0)
        {
            return;
        }

        int fruitAvailable = resourceManager.Get(ResourceType.Fruit);
        int totalSpent = 0;
        int strikeCount = 0;

        for (int i = 0; i < employees.Count; i++)
        {
            HREmployeeData employee = employees[i];
            if (employee == null)
            {
                continue;
            }

            int cost = employee.GetDailyFruitCost();
            bool canFeed = fruitAvailable >= cost;

            if (canFeed)
            {
                fruitAvailable -= cost;
                totalSpent += cost;
                employee.RemoveTrait(HREmployeeTraitType.Strike);
            }
            else
            {
                employee.AddTrait(HREmployeeTraitType.Strike);
                strikeCount++;
            }
        }

        if (totalSpent > 0)
        {
            resourceManager.Add(ResourceType.Fruit, -totalSpent);
        }

        Debug.Log($"[EmployeeDailyUpkeepSystem] Day {day} 结算完成: +Root {rootPerDay}, 果实消耗 {totalSpent}, 罢工鼠鼠 {strikeCount}", this);
    }
}
