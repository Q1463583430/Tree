using System.Collections.Generic;
using UnityEngine;
using System;

// 统一生产调度器：按固定时间片轮询所有运行中的房间，避免每个房间自己Update。
public class RoomProductionScheduler : MonoBehaviour
{
    public static RoomProductionScheduler Instance { get; private set; }

    [Header("调度间隔(秒)")]
    public float tickInterval = 0.2f;

    [Header("日循环")]
    public bool enableDayCycle = true;
    public float dayDurationSeconds = 150f;

    public int totalDay = 10;
    public int CurrentDayIndex { get; private set; } = 1;
    public float CurrentDayElapsedSeconds { get; private set; } = 0f;

    public event Action<int> OnDayEnded;

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
        UpdateDayCycle(Time.deltaTime);

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

    // 每天结束时重置运行中的房间进度，让它们从新的一天重新计时。
    private void HandleDayEnded(double now)
    {
        foreach (RoomProductionUnit unit in _units)
        {
            if (unit == null) continue;
            unit.ResetProgressForNewDay(now);
        }

        OnDayEnded?.Invoke(CurrentDayIndex);
    }

    private void UpdateDayCycle(float deltaTime)
    {
        if (CurrentDayIndex == totalDay + 1) return;
        if (!enableDayCycle) return;
        if (dayDurationSeconds <= 0f) return;
        if (deltaTime <= 0f) return;

        CurrentDayElapsedSeconds += deltaTime;

        while (CurrentDayElapsedSeconds >= dayDurationSeconds)
        {
            CurrentDayElapsedSeconds -= dayDurationSeconds;
            double now = Time.timeAsDouble;
            HandleDayEnded(now);
            CurrentDayIndex++;
        }
    }

    // 便于UI显示“距离当天结束还剩多久”。
    public float GetRemainingDaySeconds()
    {
        if (!enableDayCycle || dayDurationSeconds <= 0f) return 0f;
        return Mathf.Max(0f, dayDurationSeconds - CurrentDayElapsedSeconds);
    }
}
