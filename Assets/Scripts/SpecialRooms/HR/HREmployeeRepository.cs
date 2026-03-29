using System;
using System.Collections.Generic;
using UnityEngine;

// 员工仓库：集中存放所有招募到的鼠鼠。
public class HREmployeeRepository : MonoBehaviour
{
    public static HREmployeeRepository Instance { get; private set; }

    public event Action<HREmployeeData> OnEmployeeAdded;
    public event Action OnRepositoryChanged;

    [SerializeField] private List<HREmployeeData> employees = new List<HREmployeeData>();

    public int Count => employees.Count;
    public IReadOnlyList<HREmployeeData> Employees => employees;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void Add(HREmployeeData employee)
    {
        if (employee == null) return;
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
}
