using System;
using System.Collections.Generic;
using UnityEngine;

// 员工仓库：集中存放所有招募到的鼠鼠。
public class EmployeeRepository : MonoBehaviour
{
    public static EmployeeRepository Instance { get; private set; }

    public static EmployeeRepository GetOrCreateInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        EmployeeRepository found = FindObjectOfType<EmployeeRepository>();
        if (found != null)
        {
            Instance = found;
            return found;
        }

        GameObject go = new GameObject("EmployeeRepository_Auto");
        return go.AddComponent<EmployeeRepository>();
    }

    public event Action<HREmployeeData> OnEmployeeAdded;
    public event Action<HREmployeeData> OnEmployeeRemoved;

    [Header("全局设置")]
    public bool dontDestroyOnLoad = true;

    [Header("容量")]
    [Min(0)]
    public int baseCapacity = 50;

    [SerializeField] private List<HREmployeeData> employees = new List<HREmployeeData>();
    private readonly Dictionary<UnityEngine.Object, int> _capacityBonuses = new Dictionary<UnityEngine.Object, int>();

    public int Count => employees.Count;
    public IReadOnlyList<HREmployeeData> Employees => employees;
    public int Capacity => Mathf.Max(0, baseCapacity + GetCapacityBonusTotal());
    public int RemainingCapacity => Mathf.Max(0, Capacity - Count);
    public bool IsFull => Count >= Capacity;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Add(HREmployeeData employee)
    {
        TryAdd(employee);
    }

    public bool TryAdd(HREmployeeData employee)
    {
        if (employee == null) return false;
        if (IsFull) return false;

        employees.Add(employee);
        OnEmployeeAdded?.Invoke(employee);
        return true;
    }

    public bool Remove(HREmployeeData employee)
    {
        if (employee == null) return false;

        bool removed = employees.Remove(employee);
        if (!removed) return false;

        OnEmployeeRemoved?.Invoke(employee);
        return true;
    }

    public void RegisterCapacityBonus(UnityEngine.Object source, int bonus)
    {
        if (source == null) return;
        if (bonus == 0)
        {
            _capacityBonuses.Remove(source);
            return;
        }

        _capacityBonuses[source] = bonus;
    }

    public void UnregisterCapacityBonus(UnityEngine.Object source)
    {
        if (source == null) return;
        _capacityBonuses.Remove(source);
    }

    private int GetCapacityBonusTotal()
    {
        if (_capacityBonuses.Count == 0)
        {
            return 0;
        }

        int total = 0;
        List<UnityEngine.Object> staleKeys = null;

        foreach (KeyValuePair<UnityEngine.Object, int> kv in _capacityBonuses)
        {
            if (kv.Key == null)
            {
                if (staleKeys == null)
                {
                    staleKeys = new List<UnityEngine.Object>();
                }

                staleKeys.Add(kv.Key);
                continue;
            }

            total += kv.Value;
        }

        if (staleKeys != null)
        {
            for (int i = 0; i < staleKeys.Count; i++)
            {
                _capacityBonuses.Remove(staleKeys[i]);
            }
        }

        return total;
    }
}
