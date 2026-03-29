using System.Collections.Generic;
using UnityEngine;
using System;

public enum DayPhase
{
    Day = 0,
    Dusk = 1,
    Night = 2,
}

// 统一生产调度器：按固定时间片轮询所有运行中的房间，避免每个房间自己Update。
public class RoomProductionScheduler : MonoBehaviour
{
    private const float FixedTickInterval = 0.2f;

    public static RoomProductionScheduler Instance { get; private set; }

    [Header("调度间隔(秒)")]
    public float tickInterval = 0.2f;

    [Header("日循环")]
    public bool enableDayCycle = true;
    public float dayDurationSeconds = 150f;

    public int totalDay = 10;
    public int CurrentDayIndex { get; private set; } = 1;
    public float CurrentDayElapsedSeconds { get; private set; } = 0f;

    [Header("日相位阈值(0-1)")]
    [Range(0f, 1f)] public float duskStartNormalized = 0.65f;
    [Range(0f, 1f)] public float nightStartNormalized = 0.85f;
    public DayPhase CurrentDayPhase { get; private set; } = DayPhase.Day;

    [Header("时间流速")]
    [Range(0f, 2f)] public float initialSpeedMultiplier = 1f;
    public float CurrentSpeedMultiplier { get; private set; } = 1f;

    [Header("每日资源结算")]
    public bool applyDailySettlement = true;
    [Min(0)] public int treeNutrientPerDay = 150;
    public ResourceType nutrientResourceType = ResourceType.Root;
    [Min(0)] public int squirrelFoodCostPerDay = 10;
    public ResourceType foodResourceType = ResourceType.Fruit;
    public bool logDailySettlement = false;

    public event Action<int> OnDayEnded;
    public event Action<DayPhase> OnDayPhaseChanged;
    public event Action<float> OnSpeedChanged;

    private float _accumulated;
    private float _baseFixedDeltaTime;
    private ResourceManager _resourceManager;

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
        tickInterval = FixedTickInterval;
        _baseFixedDeltaTime = Time.fixedDeltaTime;
        SetSpeed(initialSpeedMultiplier);
        UpdateCurrentDayPhase();
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
        ApplyDailySettlement();

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
        UpdateCurrentDayPhase();

        while (CurrentDayElapsedSeconds >= dayDurationSeconds)
        {
            CurrentDayElapsedSeconds -= dayDurationSeconds;
            double now = Time.timeAsDouble;
            HandleDayEnded(now);
            CurrentDayIndex++;

            if (CurrentDayIndex > totalDay + 1)
            {
                CurrentDayIndex = totalDay + 1;
                CurrentDayElapsedSeconds = 0f;
                break;
            }

            UpdateCurrentDayPhase();
        }
    }

    // 便于UI显示“距离当天结束还剩多久”。
    public float GetRemainingDaySeconds()
    {
        if (!enableDayCycle || dayDurationSeconds <= 0f) return 0f;
        return Mathf.Max(0f, dayDurationSeconds - CurrentDayElapsedSeconds);
    }

    public float GetDayProgress01()
    {
        if (!enableDayCycle || dayDurationSeconds <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(CurrentDayElapsedSeconds / dayDurationSeconds);
    }

    public void SetPaused()
    {
        SetSpeed(0f);
    }

    public void SetNormalSpeed()
    {
        SetSpeed(1f);
    }

    public void SetDoubleSpeed()
    {
        SetSpeed(2f);
    }

    public void SetSpeed(float speedMultiplier)
    {
        float clamped = Mathf.Clamp(speedMultiplier, 0f, 2f);
        bool changed = !Mathf.Approximately(clamped, CurrentSpeedMultiplier);

        CurrentSpeedMultiplier = clamped;
        Time.timeScale = clamped;

        if (_baseFixedDeltaTime > 0f)
        {
            if (clamped <= 0f)
            {
                Time.fixedDeltaTime = _baseFixedDeltaTime;
            }
            else
            {
                Time.fixedDeltaTime = _baseFixedDeltaTime * clamped;
            }
        }

        if (changed)
        {
            OnSpeedChanged?.Invoke(CurrentSpeedMultiplier);
        }
    }

    private void UpdateCurrentDayPhase()
    {
        float progress = GetDayProgress01();
        float duskStart = Mathf.Clamp01(duskStartNormalized);
        float nightStart = Mathf.Clamp01(nightStartNormalized);
        if (nightStart < duskStart)
        {
            nightStart = duskStart;
        }

        DayPhase nextPhase;
        if (progress >= nightStart)
        {
            nextPhase = DayPhase.Night;
        }
        else if (progress >= duskStart)
        {
            nextPhase = DayPhase.Dusk;
        }
        else
        {
            nextPhase = DayPhase.Day;
        }

        if (nextPhase == CurrentDayPhase)
        {
            return;
        }

        CurrentDayPhase = nextPhase;
        OnDayPhaseChanged?.Invoke(CurrentDayPhase);
    }

    private void ApplyDailySettlement()
    {
        if (!applyDailySettlement)
        {
            return;
        }

        if (!TryEnsureResourceManager())
        {
            return;
        }

        int squirrelCount = Mathf.Max(0, _resourceManager.Get(ResourceType.Squirrel));
        int nutrientGain = Mathf.Max(0, treeNutrientPerDay);
        int foodCost = Mathf.Max(0, squirrelFoodCostPerDay) * squirrelCount;

        if (nutrientResourceType == foodResourceType)
        {
            int net = nutrientGain - foodCost;
            if (net != 0)
            {
                _resourceManager.Add(nutrientResourceType, net);
            }
        }
        else
        {
            if (nutrientGain > 0)
            {
                _resourceManager.Add(nutrientResourceType, nutrientGain);
            }

            if (foodCost > 0)
            {
                _resourceManager.Add(foodResourceType, -foodCost);
            }
        }

        if (logDailySettlement)
        {
            Debug.Log($"[RoomProductionScheduler] Day {CurrentDayIndex} settlement: +{nutrientGain} {nutrientResourceType}, -{foodCost} {foodResourceType} (squirrels={squirrelCount})", this);
        }
    }

    private bool TryEnsureResourceManager()
    {
        if (_resourceManager != null)
        {
            return true;
        }

        _resourceManager = ResourceManager.Instance;
        if (_resourceManager == null)
        {
            _resourceManager = FindObjectOfType<ResourceManager>();
        }

        return _resourceManager != null;
    }
}
