using TMPro;
using UnityEngine;

// 将日循环数据显示到UI：当前天数、当天剩余时间。
public class DayCycleUIBinder : MonoBehaviour
{
    [Header("依赖")]
    public RoomProductionScheduler scheduler;

    [Header("文本绑定")]
    public TMP_Text dayText;
    public TMP_Text remainingText;

    [Header("显示格式")]
    public string dayPrefix = "Day";
    public string remainingPrefix = "Remaining";

    void Awake()
    {
        if (scheduler == null)
        {
            scheduler = RoomProductionScheduler.Instance;
            if (scheduler == null)
            {
                scheduler = FindObjectOfType<RoomProductionScheduler>();
            }
        }
    }

    void OnEnable()
    {
        if (scheduler == null) return;
        scheduler.OnDayEnded += HandleDayEnded;
        RefreshNow();
    }

    void OnDisable()
    {
        if (scheduler == null) return;
        scheduler.OnDayEnded -= HandleDayEnded;
    }

    void Update()
    {
        RefreshRemaining();
    }

    // 外部可手动调用，强制刷新全部UI。
    public void RefreshNow()
    {
        RefreshDayIndex();
        RefreshRemaining();
    }

    private void HandleDayEnded(int dayIndex)
    {
        RefreshNow();
    }

    private void RefreshDayIndex()
    {
        if (dayText == null || scheduler == null) return;
        dayText.text = string.IsNullOrEmpty(dayPrefix)
            ? scheduler.CurrentDayIndex.ToString()
            : $"{dayPrefix}: {scheduler.CurrentDayIndex}";
    }

    private void RefreshRemaining()
    {
        if (remainingText == null || scheduler == null) return;

        float remainSeconds = scheduler.GetRemainingDaySeconds();
        int total = Mathf.CeilToInt(remainSeconds);
        int mm = total / 60;
        int ss = total % 60;
        string timeText = $"{mm:00}:{ss:00}";

        remainingText.text = string.IsNullOrEmpty(remainingPrefix)
            ? timeText
            : $"{remainingPrefix}: {timeText}";
    }
}
