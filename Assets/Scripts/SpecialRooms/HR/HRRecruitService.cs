using System;
using System.Collections.Generic;
using UnityEngine;

// HR 抽卡逻辑服务：只负责生成员工数据，不关心UI。
public static class HRRecruitService
{
    private const int MinStat = 1;
    private const int MaxStat = 10;

    private static readonly int[] BaseStatValues = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
    private static readonly int[] BaseStatWeights = { 12, 17, 30, 17, 11, 6, 3, 2, 1, 1 };

    private static readonly string[] NamePool =
    {
        "鼠鼠我鸭", "鼠滑羊", "鼠零八", "鼠繁琦", "鼠前", "鼠听君", "鼠月阳", "鼠喜阳", "鼠鸭亭", "鼠宇杰",
        "鼠韦成", "鼠敖", "鼠明辉", "鼠杰瑞", "鼠里绫华", "鼠音未来", "鼠天依", "鼠麻衣", "鼠莉希雅", "鼠蒙",
        "鼠柯克", "鼠条", "鼠片", "鼠塔", "鼠格", "鼠饼", "鼠重八", "鼠谷和人", "鼠大林", "鼠利奈绪",
        "鼠城明日奈", "鼠莱因", "鼠迪乌斯", "鼠琪希", "鼠丽丝", "鼠露菲", "鼠条悟", "鼠艺菡", "鼠曼巴", "鼠拉给木",
        "鼠鼠侠", "鼠川祥子", "鼠早爱音", "鼠松灯", "鼠理员", "鼠行者", "鼠标", "鼠崎爽世", "鼠名立希", "鼠宫妃那",
        "鼠瑟夫", "鼠斯拿", "鼠芹仁菜", "鼠方仗助", "鼠陈露", "鼠嘉豪", "鼠耽任", "鼠贝林", "鼠笑川", "鼠角洲",
    };

    public static HREmployeeData Recruit(int hrIntelligence, bool eliteHrBonus)
    {
        HREmployeeData e = new HREmployeeData
        {
            id = Guid.NewGuid().ToString("N"),
            displayName = NamePool[UnityEngine.Random.Range(0, NamePool.Length)],
            stamina = RollBaseStatByWeight(),
            intelligence = RollBaseStatByWeight(),
            magic = RollBaseStatByWeight(),
        };

        ApplyHrBonus(e, hrIntelligence);
        if (eliteHrBonus)
        {
            ApplyEliteHrBonus(e);
        }

        List<HREmployeeTraitType> rolledTraits = RollTraits();
        for (int i = 0; i < rolledTraits.Count; i++)
        {
            HREmployeeTraitType trait = rolledTraits[i];
            e.traits.Add(trait);
            ApplyTraitEffect(e, trait);
        }

        ClampStats(e);
        return e;
    }

    private static int RollBaseStatByWeight()
    {
        int totalWeight = 0;
        for (int i = 0; i < BaseStatWeights.Length; i++)
        {
            totalWeight += Mathf.Max(0, BaseStatWeights[i]);
        }

        if (totalWeight <= 0)
        {
            return 3;
        }

        int r = UnityEngine.Random.Range(1, totalWeight + 1);
        int cumulative = 0;

        for (int i = 0; i < BaseStatWeights.Length; i++)
        {
            cumulative += BaseStatWeights[i];
            if (r <= cumulative)
            {
                return BaseStatValues[i];
            }
        }

        return 3;
    }

    // HR加成：每个属性独立判定是否 +1。
    private static void ApplyHrBonus(HREmployeeData e, int hrIntelligence)
    {
        float chance = Mathf.Clamp01((hrIntelligence - 3) * 0.07f);

        // 高数值应稀有：8以上不再通过HR加成继续抬升。
        TryIncreaseStatWithSoftCap(ref e.stamina, chance, 7);
        TryIncreaseStatWithSoftCap(ref e.intelligence, chance, 7);
        TryIncreaseStatWithSoftCap(ref e.magic, chance, 7);
    }

    // 精英HR额外提高优秀员工概率：这里实现为随机属性 +1。
    private static void ApplyEliteHrBonus(HREmployeeData e)
    {
        float chance = 0.10f;
        if (UnityEngine.Random.value >= chance) return;

        int pick = UnityEngine.Random.Range(0, 3);
        if (pick == 0) TryIncreaseStatWithSoftCap(ref e.stamina, 1f, 7);
        else if (pick == 1) TryIncreaseStatWithSoftCap(ref e.intelligence, 1f, 7);
        else TryIncreaseStatWithSoftCap(ref e.magic, 1f, 7);
    }

    private static void TryIncreaseStatWithSoftCap(ref float stat, float chance, float softCap)
    {
        if (stat >= softCap)
        {
            return;
        }

        if (UnityEngine.Random.value < chance)
        {
            stat += 1;
        }
    }

    private static List<HREmployeeTraitType> RollTraits()
    {
        List<HREmployeeTraitType> pool = new List<HREmployeeTraitType>();
        foreach (HREmployeeTraitType t in Enum.GetValues(typeof(HREmployeeTraitType)))
        {
            if (!IsRollableTrait(t))
            {
                continue;
            }

            pool.Add(t);
        }

        List<HREmployeeTraitType> selected = new List<HREmployeeTraitType>();

        PickOne(pool, selected); // 必出1个
        if (UnityEngine.Random.value < 0.50f) PickOne(pool, selected);
        if (UnityEngine.Random.value < 0.25f) PickOne(pool, selected);

        return selected;
    }

    private static bool IsRollableTrait(HREmployeeTraitType trait)
    {
        if (trait == HREmployeeTraitType.Strike)
        {
            return false;
        }

        return true;
    }

    private static void PickOne(List<HREmployeeTraitType> pool, List<HREmployeeTraitType> selected)
    {
        if (pool.Count == 0) return;
        int idx = UnityEngine.Random.Range(0, pool.Count);
        HREmployeeTraitType t = pool[idx];
        pool.RemoveAt(idx);
        selected.Add(t);
    }

    private static void ApplyTraitEffect(HREmployeeData e, HREmployeeTraitType trait)
    {
        switch (trait)
        {
            case HREmployeeTraitType.InsectPhobia:
                e.canFarm = false;
                break;
            case HREmployeeTraitType.DarkCook:
                e.canCook = false;
                break;
            case HREmployeeTraitType.PineconeAllergy:
                e.canPineconePlant = false;
                break;
            case HREmployeeTraitType.Sickly:
                e.stamina -= 2;
                break;
            case HREmployeeTraitType.KneeInjury:
                e.stamina -= 3;
                break;
            case HREmployeeTraitType.SevereMyopia:
                e.intelligence -= 2;
                break;
            case HREmployeeTraitType.LazySyndrome:
                e.staminaGrowthMultiplier *= 0.7f;
                break;
            case HREmployeeTraitType.LearningDisability:
                e.intelligenceGrowthMultiplier *= 0.7f;
                break;
            case HREmployeeTraitType.Muggle:
                // 数值范围统一为 1~10，不再出现 0。
                e.magic = MinStat;
                e.magicLocked = true;
                break;
            case HREmployeeTraitType.LowComprehension:
                e.magicGrowthMultiplier *= 0.7f;
                break;
            case HREmployeeTraitType.BigAppetite:
                e.dailyFruitConsumptionDelta += 10;
                break;
            case HREmployeeTraitType.UltimateBigAppetite:
                e.dailyFruitConsumptionDelta += 20;
                break;
            case HREmployeeTraitType.GardeningExpert:
                e.stamina += 3;
                break;
            case HREmployeeTraitType.StrongBody:
                e.stamina += 2;
                break;
            case HREmployeeTraitType.SmartTalent:
                e.intelligence += 2;
                break;
            case HREmployeeTraitType.MagicalGirl:
                e.magic += 2;
                break;
            case HREmployeeTraitType.FitnessFan:
                e.gymSuccessMultiplier *= 1.3f;
                break;
            case HREmployeeTraitType.BookLover:
                e.librarySuccessMultiplier *= 1.3f;
                break;
            case HREmployeeTraitType.MagicLover:
                e.magicRoomSuccessMultiplier *= 1.3f;
                break;
            case HREmployeeTraitType.LuckyMouse:
                e.walnutCollectibleBonus += 0.05f;
                break;
            case HREmployeeTraitType.EliteHR:
                e.intelligence += 5;
                break;
            case HREmployeeTraitType.BirdStomach:
                e.dailyFruitConsumptionDelta -= 3;
                break;
        }
    }

    private static void ClampStats(HREmployeeData e)
    {
        e.stamina = Mathf.Clamp(e.stamina, MinStat, MaxStat);
        e.intelligence = Mathf.Clamp(e.intelligence, MinStat, MaxStat);

        if (e.magicLocked)
        {
            e.magic = MinStat;
        }
        else
        {
            e.magic = Mathf.Clamp(e.magic, MinStat, MaxStat);
        }
    }
}
