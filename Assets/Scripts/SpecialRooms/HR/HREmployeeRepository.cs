using System;
using System.Collections.Generic;
using UnityEngine;

// 员工仓库：集中存放所有招募到的鼠鼠。
public class HREmployeeRepository : MonoBehaviour
{
    public static HREmployeeRepository Instance { get; private set; }

    public static HREmployeeRepository GetOrCreateInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        HREmployeeRepository found = FindObjectOfType<HREmployeeRepository>();
        if (found != null)
        {
            Instance = found;
            return found;
        }

        GameObject go = new GameObject("HREmployeeRepository_Auto");
        return go.AddComponent<HREmployeeRepository>();
    }

    public event Action<HREmployeeData> OnEmployeeAdded; //添加员工事件
    public event Action<HREmployeeData> OnEmployeeRemoved; //移除员工事件

    [Header("全局设置")]
    public bool dontDestroyOnLoad = true; //加载场景不变

    [Header("容量")]
    [Min(0)]
    public int baseCapacity = 50; //基础容量

    [SerializeField] private List<HREmployeeData> employees = new List<HREmployeeData>();
    private readonly Dictionary<UnityEngine.Object, int> _capacityBonuses = new Dictionary<UnityEngine.Object, int>();

    public int Count => employees.Count;
    public IReadOnlyList<HREmployeeData> Employees => employees;
    public int Capacity => Mathf.Max(0, baseCapacity + GetCapacityBonusTotal());
    public int RemainingCapacity => Mathf.Max(0, Capacity - Count);
    public bool IsFull => Count >= Capacity; //员工是否满员

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

    public bool TryAdd(HREmployeeData employee) //添加员工
    {
        if (employee == null) return false;
        if (IsFull) return false;

        employees.Add(employee);
        OnEmployeeAdded?.Invoke(employee);
        return true;
    }

    public bool Remove(HREmployeeData employee) //移除员工
    {
        if (employee == null) return false;

        bool removed = employees.Remove(employee);
        if (!removed) return false;

        OnEmployeeRemoved?.Invoke(employee);
        return true;
    }

    public void RegisterCapacityBonus(UnityEngine.Object source, int bonus) //获取到添加员工上限的功能
    {
        if (source == null) return;
        if (bonus == 0)
        {
            _capacityBonuses.Remove(source);
            return;
        }

        _capacityBonuses[source] = bonus;
    }

    public void UnregisterCapacityBonus(UnityEngine.Object source) //取消员工上限增加
    {
        if (source == null) return;
        _capacityBonuses.Remove(source);
    }

    private int GetCapacityBonusTotal() //获取到所有的员工上限增加的能力
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
