using System;
using System.Collections.Generic;
using UnityEngine;

// 员工仓库：集中存放所有招募到的鼠鼠。
public class HREmployeeRepository : MonoBehaviour
{
    public static HREmployeeRepository Instance { get; private set; }

    public event Action<HREmployeeData> OnEmployeeAdded;

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
    }
}
