using System;
using System.Collections.Generic;
using UnityEngine;

// 所有可挂载的特殊词条。
public enum HREmployeeTraitType
{
    InsectPhobia = 1,          // 昆虫恐惧症
    DarkCook = 2,              // 黑暗料理者
    PineconeAllergy = 3,       // 松果过敏
    Sickly = 4,                // 体弱多病
    KneeInjury = 5,            // 膝盖中了一箭
    SevereMyopia = 6,          // 重度近视
    LazySyndrome = 7,          // 懒癌
    LearningDisability = 8,    // 学习困难症
    Muggle = 9,                // 麻瓜
    LowComprehension = 10,     // 低悟性
    BigAppetite = 11,          // 大胃袋
    UltimateBigAppetite = 12,  // 究极大胃袋
    GardeningExpert = 13,      // 园艺高手
    StrongBody = 14,           // 身强体壮
    SmartTalent = 15,          // 天资聪颖
    MagicalGirl = 16,          // 马猴烧酒
    FitnessFan = 17,           // 健美爱好者
    BookLover = 18,            // 酷爱阅读者
    MagicLover = 19,           // 喜爱魔法
    LuckyMouse = 20,           // 幸运鼠
    EliteHR = 21,              // 精英HR
    BirdStomach = 22,          // 小鸟胃
    Strike = 23,               // 罢工
}

[Serializable]
public class HREmployeeData
{
    public string id;
    public string displayName;

    public float stamina;
    public float intelligence;
    public float magic;

    public bool magicLocked;

    // 岗位限制
    public bool canFarm = true;
    public bool canCook = true;
    public bool canPineconePlant = true;

    // 成长/生产相关修正
    public float staminaGrowthMultiplier = 1f;
    public float intelligenceGrowthMultiplier = 1f;
    public float magicGrowthMultiplier = 1f;

    public float gymSuccessMultiplier = 1f;
    public float librarySuccessMultiplier = 1f;
    public float magicRoomSuccessMultiplier = 1f;

    public int dailyFruitConsumptionDelta = 0;
    public float walnutCollectibleBonus = 0f;
    public float eliteHrBonusChance = 0f;

    public List<HREmployeeTraitType> traits = new List<HREmployeeTraitType>();

    public int GetDailyFruitCost()
    {
        return Mathf.Max(0, 10 + dailyFruitConsumptionDelta);
    }

    public bool HasTrait(HREmployeeTraitType trait)
    {
        return traits != null && traits.Contains(trait);
    }

    public void AddTrait(HREmployeeTraitType trait)
    {
        if (traits == null)
        {
            traits = new List<HREmployeeTraitType>();
        }

        if (!traits.Contains(trait))
        {
            traits.Add(trait);
        }
    }

    public void RemoveTrait(HREmployeeTraitType trait)
    {
        if (traits == null)
        {
            return;
        }

        traits.Remove(trait);
    }

    // 你的规则：属性决定生产加成。
    public static float GetProductionModifierRate(int value)
    {
        if (value <= 1) return -0.10f;
        if (value == 2) return -0.05f;
        if (value == 3) return 0f;
        return (value - 3) * 0.01f;
    }
}
