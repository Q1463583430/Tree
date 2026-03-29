using System;
using System.Collections.Generic;
using UnityEngine;

public enum HRTrainingRoomType
{
    Gym601 = 601,
    Library602 = 602,
    MagicHouse603 = 603,
}

public enum HRTrainingQuality
{
    Strong = 0,
    Normal = 1,
    Weak = 2,
    Specialized = 3,
}

public enum HRAttributeType
{
    None = 0,
    Stamina = 1,
    Intelligence = 2,
    Magic = 3,
}

[Serializable]
public class HRTrainingOption
{
    public HRAttributeType attribute = HRAttributeType.None;
    public float delta = 0f;
    [Range(0f, 1f)] public float chance = 0f;
}

[Serializable]
public class HRTrainingProfile
{
    public HRTrainingQuality quality = HRTrainingQuality.Normal;
    public List<HRTrainingOption> options = new List<HRTrainingOption>();
}

[Serializable]
public class HRTraitQualityRule
{
    public HREmployeeTraitType trait;
    public HRTrainingQuality quality = HRTrainingQuality.Normal;
}

public struct HRTrainingResult
{
    public bool changed;
    public HRTrainingQuality quality;
    public HRAttributeType attribute;
    public float delta;
}

// 特殊养成房：不走生产周期，手动触发一次训练并按概率提升员工属性。
public class HRAttributeTrainingRoom : MonoBehaviour
{
    [Header("房间")]
    public HRTrainingRoomType roomType = HRTrainingRoomType.Gym601;
    public HRTrainingQuality defaultQuality = HRTrainingQuality.Normal;

    [Header("依赖")]
    public HREmployeeRepository repository;

    [Header("每日结算")]
    public bool settleOnDayEnded = true;
    public bool requireBuiltRoomToSettle = true;

    [Header("词条覆盖")]
    public bool useTraitQualityOverride = true;
    public List<HRTraitQualityRule> traitQualityRules = new List<HRTraitQualityRule>();

    [Header("概率配置")]
    public List<HRTrainingProfile> profiles = new List<HRTrainingProfile>();

    private RoomProductionUnit _roomUnit;
    private RoomProductionScheduler _scheduler;

    void Reset()
    {
        BuildDefaultProfiles();
        BuildDefaultTraitRules();
    }

    void Awake()
    {
        _roomUnit = GetComponent<RoomProductionUnit>();

        if (repository == null)
        {
            repository = HREmployeeRepository.GetOrCreateInstance();
        }

        if (profiles == null || profiles.Count == 0)
        {
            BuildDefaultProfiles();
        }

        if (traitQualityRules == null || traitQualityRules.Count == 0)
        {
            BuildDefaultTraitRules();
        }
    }

    void OnEnable()
    {
        BindDayEndEvent();
    }

    void Update()
    {
        if (settleOnDayEnded && _scheduler == null)
        {
            BindDayEndEvent();
        }

        if (repository == null)
        {
            repository = HREmployeeRepository.GetOrCreateInstance();
        }
    }

    void OnDisable()
    {
        UnbindDayEndEvent();
    }

    [ContextMenu("Train All Employees Once")]
    public void TrainAllEmployeesOnce()
    {
        if (repository == null)
        {
            Debug.LogWarning("[HRAttributeTrainingRoom] 未找到 HREmployeeRepository", this);
            return;
        }

        int changedCount = 0;
        IReadOnlyList<HREmployeeData> all = repository.Employees;
        for (int i = 0; i < all.Count; i++)
        {
            if (TrainEmployee(all[i], out HRTrainingResult result) && result.changed)
            {
                changedCount++;
            }
        }

        Debug.Log($"[HRAttributeTrainingRoom] {name} 训练完成，生效 {changedCount}/{all.Count}", this);
    }

    private void HandleDayEnded(int _)
    {
        if (!settleOnDayEnded)
        {
            return;
        }

        if (requireBuiltRoomToSettle && _roomUnit != null && !_roomUnit.IsBuilt)
        {
            return;
        }

        TrainAllEmployeesOnce();
    }

    private void BindDayEndEvent()
    {
        if (!settleOnDayEnded)
        {
            return;
        }

        if (_scheduler == null)
        {
            _scheduler = RoomProductionScheduler.Instance;
            if (_scheduler == null)
            {
                _scheduler = FindObjectOfType<RoomProductionScheduler>();
            }
        }

        if (_scheduler != null)
        {
            _scheduler.OnDayEnded -= HandleDayEnded;
            _scheduler.OnDayEnded += HandleDayEnded;
        }
    }

    private void UnbindDayEndEvent()
    {
        if (_scheduler != null)
        {
            _scheduler.OnDayEnded -= HandleDayEnded;
        }
    }

    public bool TrainEmployee(HREmployeeData employee, out HRTrainingResult result)
    {
        result = new HRTrainingResult
        {
            changed = false,
            quality = ResolveQuality(employee),
            attribute = HRAttributeType.None,
            delta = 0f,
        };

        if (employee == null)
        {
            return false;
        }

        if (!TryGetProfile(result.quality, out HRTrainingProfile profile))
        {
            return false;
        }

        float successMultiplier = GetRoomSuccessMultiplier(employee);
        HRTrainingOption rolled = RollOption(profile, successMultiplier);
        if (rolled == null || rolled.attribute == HRAttributeType.None || rolled.delta <= 0f)
        {
            return true;
        }

        if (!ApplyDelta(employee, rolled.attribute, rolled.delta))
        {
            return true;
        }

        result.changed = true;
        result.attribute = rolled.attribute;
        result.delta = rolled.delta;
        return true;
    }

    private HRTrainingQuality ResolveQuality(HREmployeeData employee)
    {
        if (!useTraitQualityOverride || employee == null || employee.traits == null || employee.traits.Count == 0)
        {
            return defaultQuality;
        }

        for (int i = 0; i < traitQualityRules.Count; i++)
        {
            HRTraitQualityRule rule = traitQualityRules[i];
            if (employee.traits.Contains(rule.trait))
            {
                return rule.quality;
            }
        }

        return defaultQuality;
    }

    private bool TryGetProfile(HRTrainingQuality quality, out HRTrainingProfile profile)
    {
        for (int i = 0; i < profiles.Count; i++)
        {
            if (profiles[i] != null && profiles[i].quality == quality)
            {
                profile = profiles[i];
                return true;
            }
        }

        profile = null;
        return false;
    }

    private float GetRoomSuccessMultiplier(HREmployeeData employee)
    {
        if (employee == null)
        {
            return 1f;
        }

        switch (roomType)
        {
            case HRTrainingRoomType.Gym601:
                return Mathf.Max(0f, employee.gymSuccessMultiplier);
            case HRTrainingRoomType.Library602:
                return Mathf.Max(0f, employee.librarySuccessMultiplier);
            case HRTrainingRoomType.MagicHouse603:
                return Mathf.Max(0f, employee.magicRoomSuccessMultiplier);
            default:
                return 1f;
        }
    }

    private static HRTrainingOption RollOption(HRTrainingProfile profile, float successMultiplier)
    {
        if (profile == null || profile.options == null || profile.options.Count == 0)
        {
            return null;
        }

        List<HRTrainingOption> validOptions = new List<HRTrainingOption>();
        List<float> scaledChances = new List<float>();
        float total = 0f;

        for (int i = 0; i < profile.options.Count; i++)
        {
            HRTrainingOption option = profile.options[i];
            if (option == null || option.attribute == HRAttributeType.None || option.delta <= 0f || option.chance <= 0f)
            {
                continue;
            }

            float scaled = option.chance * successMultiplier;
            if (scaled <= 0f)
            {
                continue;
            }

            validOptions.Add(option);
            scaledChances.Add(scaled);
            total += scaled;
        }

        if (validOptions.Count == 0)
        {
            return null;
        }

        if (total > 1f)
        {
            float k = 1f / total;
            total = 0f;
            for (int i = 0; i < scaledChances.Count; i++)
            {
                float normalized = scaledChances[i] * k;
                scaledChances[i] = normalized;
                total += normalized;
            }
        }

        float r = UnityEngine.Random.value;
        float cumulative = 0f;

        for (int i = 0; i < validOptions.Count; i++)
        {
            cumulative += scaledChances[i];
            if (r <= cumulative)
            {
                return validOptions[i];
            }
        }

        return null;
    }

    private static bool ApplyDelta(HREmployeeData employee, HRAttributeType attribute, float delta)
    {
        if (employee == null || delta <= 0f)
        {
            return false;
        }

        switch (attribute)
        {
            case HRAttributeType.Stamina:
                employee.stamina = Mathf.Clamp(employee.stamina + delta, 1f, 10f);
                return true;

            case HRAttributeType.Intelligence:
                employee.intelligence = Mathf.Clamp(employee.intelligence + delta, 1f, 10f);
                return true;

            case HRAttributeType.Magic:
                if (employee.magicLocked)
                {
                    return false;
                }

                employee.magic = Mathf.Clamp(employee.magic + delta, 1f, 10f);
                return true;

            default:
                return false;
        }
    }

    private void BuildDefaultTraitRules()
    {
        traitQualityRules = new List<HRTraitQualityRule>();

        switch (roomType)
        {
            case HRTrainingRoomType.Gym601:
                traitQualityRules.Add(new HRTraitQualityRule { trait = HREmployeeTraitType.FitnessFan, quality = HRTrainingQuality.Strong });
                traitQualityRules.Add(new HRTraitQualityRule { trait = HREmployeeTraitType.Sickly, quality = HRTrainingQuality.Weak });
                traitQualityRules.Add(new HRTraitQualityRule { trait = HREmployeeTraitType.KneeInjury, quality = HRTrainingQuality.Weak });
                break;

            case HRTrainingRoomType.Library602:
                traitQualityRules.Add(new HRTraitQualityRule { trait = HREmployeeTraitType.BookLover, quality = HRTrainingQuality.Strong });
                traitQualityRules.Add(new HRTraitQualityRule { trait = HREmployeeTraitType.SmartTalent, quality = HRTrainingQuality.Specialized });
                traitQualityRules.Add(new HRTraitQualityRule { trait = HREmployeeTraitType.LearningDisability, quality = HRTrainingQuality.Weak });
                break;

            case HRTrainingRoomType.MagicHouse603:
                traitQualityRules.Add(new HRTraitQualityRule { trait = HREmployeeTraitType.MagicLover, quality = HRTrainingQuality.Strong });
                traitQualityRules.Add(new HRTraitQualityRule { trait = HREmployeeTraitType.LowComprehension, quality = HRTrainingQuality.Weak });
                traitQualityRules.Add(new HRTraitQualityRule { trait = HREmployeeTraitType.Muggle, quality = HRTrainingQuality.Specialized });
                break;
        }
    }

    private void BuildDefaultProfiles()
    {
        profiles = new List<HRTrainingProfile>
        {
            new HRTrainingProfile { quality = HRTrainingQuality.Strong, options = BuildDefaultOptions(roomType, HRTrainingQuality.Strong) },
            new HRTrainingProfile { quality = HRTrainingQuality.Normal, options = BuildDefaultOptions(roomType, HRTrainingQuality.Normal) },
            new HRTrainingProfile { quality = HRTrainingQuality.Weak, options = BuildDefaultOptions(roomType, HRTrainingQuality.Weak) },
            new HRTrainingProfile { quality = HRTrainingQuality.Specialized, options = BuildDefaultOptions(roomType, HRTrainingQuality.Specialized) },
        };
    }

    private static List<HRTrainingOption> BuildDefaultOptions(HRTrainingRoomType room, HRTrainingQuality quality)
    {
        List<HRTrainingOption> options = new List<HRTrainingOption>();

        switch (room)
        {
            case HRTrainingRoomType.Gym601:
                switch (quality)
                {
                    case HRTrainingQuality.Strong:
                        options.Add(new HRTrainingOption { attribute = HRAttributeType.Stamina, delta = 2f, chance = 0.10f });
                        options.Add(new HRTrainingOption { attribute = HRAttributeType.Stamina, delta = 1f, chance = 0.50f });
                        break;
                    case HRTrainingQuality.Normal:
                        options.Add(new HRTrainingOption { attribute = HRAttributeType.Stamina, delta = 2f, chance = 0.05f });
                        options.Add(new HRTrainingOption { attribute = HRAttributeType.Stamina, delta = 1f, chance = 0.25f });
                        break;
                    case HRTrainingQuality.Weak:
                        options.Add(new HRTrainingOption { attribute = HRAttributeType.Stamina, delta = 2f, chance = 0.025f });
                        options.Add(new HRTrainingOption { attribute = HRAttributeType.Stamina, delta = 1f, chance = 0.125f });
                        break;
                }
                break;

            case HRTrainingRoomType.Library602:
                switch (quality)
                {
                    case HRTrainingQuality.Strong:
                        options.Add(new HRTrainingOption { attribute = HRAttributeType.Intelligence, delta = 2f, chance = 0.02f });
                        options.Add(new HRTrainingOption { attribute = HRAttributeType.Intelligence, delta = 1f, chance = 0.08f });
                        options.Add(new HRTrainingOption { attribute = HRAttributeType.Magic, delta = 2f, chance = 0.08f });
                        options.Add(new HRTrainingOption { attribute = HRAttributeType.Magic, delta = 1f, chance = 0.32f });
                        break;
                    case HRTrainingQuality.Normal:
                        options.Add(new HRTrainingOption { attribute = HRAttributeType.Intelligence, delta = 2f, chance = 0.01f });
                        options.Add(new HRTrainingOption { attribute = HRAttributeType.Intelligence, delta = 1f, chance = 0.04f });
                        options.Add(new HRTrainingOption { attribute = HRAttributeType.Magic, delta = 2f, chance = 0.04f });
                        options.Add(new HRTrainingOption { attribute = HRAttributeType.Magic, delta = 1f, chance = 0.16f });
                        break;
                    case HRTrainingQuality.Weak:
                        options.Add(new HRTrainingOption { attribute = HRAttributeType.Intelligence, delta = 2f, chance = 0.005f });
                        options.Add(new HRTrainingOption { attribute = HRAttributeType.Intelligence, delta = 1f, chance = 0.02f });
                        options.Add(new HRTrainingOption { attribute = HRAttributeType.Magic, delta = 2f, chance = 0.04f });
                        options.Add(new HRTrainingOption { attribute = HRAttributeType.Magic, delta = 1f, chance = 0.16f });
                        break;
                    case HRTrainingQuality.Specialized:
                        options.Add(new HRTrainingOption { attribute = HRAttributeType.Intelligence, delta = 2f, chance = 0.01f });
                        options.Add(new HRTrainingOption { attribute = HRAttributeType.Intelligence, delta = 1f, chance = 0.04f });
                        break;
                }
                break;

            case HRTrainingRoomType.MagicHouse603:
                switch (quality)
                {
                    case HRTrainingQuality.Strong:
                        options.Add(new HRTrainingOption { attribute = HRAttributeType.Magic, delta = 2f, chance = 0.10f });
                        options.Add(new HRTrainingOption { attribute = HRAttributeType.Magic, delta = 1f, chance = 0.50f });
                        break;
                    case HRTrainingQuality.Normal:
                        options.Add(new HRTrainingOption { attribute = HRAttributeType.Magic, delta = 2f, chance = 0.05f });
                        options.Add(new HRTrainingOption { attribute = HRAttributeType.Magic, delta = 1f, chance = 0.25f });
                        break;
                    case HRTrainingQuality.Weak:
                        options.Add(new HRTrainingOption { attribute = HRAttributeType.Magic, delta = 2f, chance = 0.025f });
                        options.Add(new HRTrainingOption { attribute = HRAttributeType.Magic, delta = 1f, chance = 0.125f });
                        break;
                }
                break;
        }

        return options;
    }
}