using System;
using System.Collections.Generic;
using UnityEngine;

// 员工仓库：集中存放所有招募到的鼠鼠。
public class HREmployeeRepository : MonoBehaviour
{
    public static HREmployeeRepository Instance { get; private set; }

    [Header("默认鼠鼠")]
    public bool createStarterEmployeeOnEmpty = true;
    public string starterDisplayName = "初始鼠鼠";
    [Range(1, 10)] public int starterStamina = 3;
    [Range(1, 10)] public int starterIntelligence = 3;
    [Range(0, 10)] public int starterMagic = 3;
    public bool starterCanFarm = true;
    public bool starterCanCook = true;
    public bool starterCanPineconePlant = true;
    [SerializeField] private List<HREmployeeTraitType> starterTraits = new List<HREmployeeTraitType>();

    public event Action<HREmployeeData> OnEmployeeAdded;
    public event Action OnRepositoryChanged;

    [SerializeField] private List<HREmployeeData> employees = new List<HREmployeeData>();

    public int Count => employees.Count;
    public IReadOnlyList<HREmployeeData> Employees => employees;

    public static HREmployeeRepository EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        HREmployeeRepository existing = FindObjectOfType<HREmployeeRepository>();
        if (existing != null)
        {
            return existing;
        }

        GameObject go = new GameObject("HREmployeeRepository_Auto");
        return go.AddComponent<HREmployeeRepository>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        NormalizeEmployees();
        SeedStarterEmployeeIfNeeded();
    }

    public void Add(HREmployeeData employee)
    {
        if (employee == null) return;

        if (string.IsNullOrWhiteSpace(employee.id))
        {
            employee.id = Guid.NewGuid().ToString("N");
        }

        if (string.IsNullOrWhiteSpace(employee.displayName))
        {
            employee.displayName = "未命名鼠鼠";
        }

        employees.Add(employee);
        OnEmployeeAdded?.Invoke(employee);
        OnRepositoryChanged?.Invoke();
    }

    public bool TryGetById(string employeeId, out HREmployeeData employee)
    {
        employee = null;
        if (string.IsNullOrWhiteSpace(employeeId))
        {
            return false;
        }

        string id = employeeId.Trim();
        for (int i = 0; i < employees.Count; i++)
        {
            HREmployeeData e = employees[i];
            if (e == null || string.IsNullOrWhiteSpace(e.id))
            {
                continue;
            }

            if (string.Equals(e.id, id, StringComparison.Ordinal))
            {
                employee = e;
                return true;
            }
        }

        return false;
    }

    private void NormalizeEmployees()
    {
        for (int i = 0; i < employees.Count; i++)
        {
            HREmployeeData e = employees[i];
            if (e == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(e.id))
            {
                e.id = Guid.NewGuid().ToString("N");
            }

            if (string.IsNullOrWhiteSpace(e.displayName))
            {
                e.displayName = "未命名鼠鼠";
            }
        }
    }

    private void SeedStarterEmployeeIfNeeded()
    {
        if (!createStarterEmployeeOnEmpty)
        {
            return;
        }

        if (employees.Count > 0)
        {
            return;
        }

        HREmployeeData starter = new HREmployeeData
        {
            id = Guid.NewGuid().ToString("N"),
            displayName = string.IsNullOrWhiteSpace(starterDisplayName) ? "初始鼠鼠" : starterDisplayName.Trim(),
            stamina = Mathf.Clamp(starterStamina, 1, 10),
            intelligence = Mathf.Clamp(starterIntelligence, 1, 10),
            magic = Mathf.Clamp(starterMagic, 0, 10),
            canFarm = starterCanFarm,
            canCook = starterCanCook,
            canPineconePlant = starterCanPineconePlant,
            traits = starterTraits != null ? new List<HREmployeeTraitType>(starterTraits) : new List<HREmployeeTraitType>(),
        };

        Add(starter);
    }
}
