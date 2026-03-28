using System.Collections.Generic;
using UnityEngine;

// 统一生产调度器：按固定时间片轮询所有运行中的房间，避免每个房间自己Update。
public class RoomProductionScheduler : MonoBehaviour
{
    public static RoomProductionScheduler Instance { get; private set; }

    [Header("调度间隔(秒)")]
    public float tickInterval = 0.2f;

    private float _accumulated;

    //房间的生产单元哈希表
    private readonly HashSet<RoomProductionUnit> _units = new HashSet<RoomProductionUnit>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Update()
    {
        _accumulated += Time.deltaTime;
        if (_accumulated < tickInterval) return;
        _accumulated = 0f;

        double now = Time.timeAsDouble;
        foreach (RoomProductionUnit unit in _units)
        {
            if (unit == null) continue;
            unit.Tick(now);
        }
    }

    //将房间单元加入哈希表
    public void Register(RoomProductionUnit unit)
    {
        if (unit == null) return;
        _units.Add(unit);
    }

    //取消登记
    public void Unregister(RoomProductionUnit unit)
    {
        if (unit == null) return;
        _units.Remove(unit);
    }
}
