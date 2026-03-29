using System.Collections.Generic;
using UnityEngine;

public class RoomProductionModifierBreakdownV2
{
    public string roomId;
    public RoomMainStatSelector mainStat;
    public float statRateSum;
    public float finalMultiplier = 1f;
    public int employeeCount;
    public readonly List<string> employeeRateDetails = new List<string>();
}

public static class RoomProductionModifierEngineV2
{
    public static float CalculateMultiplier(
        RoomProductionPlan plan,
        IReadOnlyList<HREmployeeData> employees,
        ProductionModifierV2Settings settings,
        out RoomProductionModifierBreakdownV2 breakdown)
    {
        breakdown = new RoomProductionModifierBreakdownV2();
        breakdown.employeeCount = employees != null ? employees.Count : 0;

        if (plan == null || employees == null || employees.Count == 0)
        {
            return 1f;
        }

        string roomId = string.IsNullOrWhiteSpace(plan.roomId) ? string.Empty : plan.roomId.Trim();
        RoomMainStatSelector selector = settings != null
            ? settings.ResolveMainStat(roomId)
            : RoomMainStatSelector.AverageAll;

        float statRateSum = 0f;
        for (int i = 0; i < employees.Count; i++)
        {
            HREmployeeData employee = employees[i];
            if (employee == null)
            {
                continue;
            }

            int statValue = ResolveMainStatValue(employee, selector, plan.workType);
            float statRate = HREmployeeData.GetProductionModifierRate(statValue);
            statRateSum += statRate;

            string employeeId = string.IsNullOrWhiteSpace(employee.id) ? "unknown" : employee.id;
            string employeeName = string.IsNullOrWhiteSpace(employee.displayName) ? employeeId : employee.displayName;
            breakdown.employeeRateDetails.Add(employeeName + "[" + employeeId + "] "
                + selector + "=" + statValue
                + " => rate=" + statRate.ToString("0.###"));
        }

        float finalMultiplier = Mathf.Max(0f, 1f + statRateSum);

        breakdown.roomId = roomId;
        breakdown.mainStat = selector;
        breakdown.statRateSum = statRateSum;
        breakdown.finalMultiplier = finalMultiplier;

        return finalMultiplier;
    }

    private static int ResolveMainStatValue(HREmployeeData employee, RoomMainStatSelector selector, RoomEmployeeWorkType workType)
    {
        switch (selector)
        {
            case RoomMainStatSelector.Stamina:
                return employee.stamina;
            case RoomMainStatSelector.Intelligence:
                return employee.intelligence;
            case RoomMainStatSelector.Magic:
                return employee.magic;
            case RoomMainStatSelector.AverageStaminaIntelligence:
                return Mathf.RoundToInt((employee.stamina + employee.intelligence) * 0.5f);
            case RoomMainStatSelector.AverageAll:
                return Mathf.RoundToInt((employee.stamina + employee.intelligence + employee.magic) / 3f);
            default:
                return Mathf.RoundToInt((employee.stamina + employee.intelligence + employee.magic) / 3f);
        }
    }
}
