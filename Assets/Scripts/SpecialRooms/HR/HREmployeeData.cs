using System;
using System.Collections.Generic;
using UnityEngine;

// 所有可挂载的特殊词条。
public enum HREmployeeTraitType
{
    InsectPhobia = 1,
    DarkCook = 2,
    PineconeAllergy = 3,
    Sickly = 4,
    KneeInjury = 5,
    SevereMyopia = 6,
    LazySyndrome = 7,
    LearningDisability = 8,
    Muggle = 9,
    LowComprehension = 10,
    BigAppetite = 11,
    UltimateBigAppetite = 12,
    GardeningExpert = 13,
    StrongBody = 14,
    SmartTalent = 15,
    MagicalGirl = 16,
    FitnessFan = 17,
    BookLover = 18,
    MagicLover = 19,
    LuckyMouse = 20,
    EliteHR = 21,
    BirdStomach = 22,
}

[Serializable]
public class HREmployeeData
{
    public string id;
    public string displayName;

    public int stamina;
    public int intelligence;
    public int magic;

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

    // 你的规则：属性决定生产加成。
    public static float GetProductionModifierRate(int value)
    {
        if (value <= 1) return -0.10f;
        if (value == 2) return -0.05f;
        if (value == 3) return 0f;
        return (value - 3) * 0.01f;
    }
}
