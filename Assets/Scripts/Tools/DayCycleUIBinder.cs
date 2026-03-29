using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 将日循环数据显示到UI：当前天数、当天剩余时间。
public class DayCycleUIBinder : MonoBehaviour
{
    [Header("依赖")]
    public RoomProductionScheduler scheduler;

    [Header("文本绑定")]
    public TMP_Text dayText;
    public TMP_Text remainingText;
    public TMP_Text phaseText;
    public TMP_Text speedText;

    [Header("速度按钮")]
    public Button pauseButton;
    public Button speed1xButton;
    public Button speed2xButton;

    [Header("显示格式")]
    public string dayPrefix = "Day";
    public string remainingPrefix = "Remaining";
    public string phasePrefix = "Phase";
    public string speedPrefix = "Speed";

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

        BindButtons();
        scheduler.OnDayEnded += HandleDayEnded;
        scheduler.OnDayPhaseChanged += HandleDayPhaseChanged;
        scheduler.OnSpeedChanged += HandleSpeedChanged;
        RefreshNow();
    }

    void OnDisable()
    {
        if (scheduler == null) return;
        UnbindButtons();
        scheduler.OnDayEnded -= HandleDayEnded;
        scheduler.OnDayPhaseChanged -= HandleDayPhaseChanged;
        scheduler.OnSpeedChanged -= HandleSpeedChanged;
    }

    void Update()
    {
        RefreshRemaining();
        RefreshSpeedAndButtons();
    }

    // 外部可手动调用，强制刷新全部UI。
    public void RefreshNow()
    {
        RefreshDayIndex();
        RefreshRemaining();
        RefreshPhaseText();
        RefreshSpeedAndButtons();
    }

    private void HandleDayEnded(int dayIndex)
    {
        RefreshNow();
    }

    private void HandleDayPhaseChanged(DayPhase phase)
    {
        RefreshPhaseText();
    }

    private void HandleSpeedChanged(float speed)
    {
        RefreshSpeedAndButtons();
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

    private void RefreshPhaseText()
    {
        if (phaseText == null || scheduler == null) return;

        string phaseName;
        switch (scheduler.CurrentDayPhase)
        {
            case DayPhase.Dusk:
                phaseName = "黄昏";
                break;
            case DayPhase.Night:
                phaseName = "夜晚";
                break;
            default:
                phaseName = "白天";
                break;
        }

        phaseText.text = string.IsNullOrEmpty(phasePrefix)
            ? phaseName
            : $"{phasePrefix}: {phaseName}";
    }

    private void RefreshSpeedAndButtons()
    {
        if (scheduler == null)
        {
            return;
        }

        float speed = scheduler.CurrentSpeedMultiplier;
        if (speedText != null)
        {
            string label;
            if (speed <= 0f)
            {
                label = "暂停";
            }
            else
            {
                label = "x" + Mathf.RoundToInt(speed);
            }

            speedText.text = string.IsNullOrEmpty(speedPrefix)
                ? label
                : $"{speedPrefix}: {label}";
        }

        if (pauseButton != null)
        {
            pauseButton.interactable = speed > 0f;
        }

        if (speed1xButton != null)
        {
            speed1xButton.interactable = !Mathf.Approximately(speed, 1f);
        }

        if (speed2xButton != null)
        {
            speed2xButton.interactable = !Mathf.Approximately(speed, 2f);
        }
    }

    private void BindButtons()
    {
        if (pauseButton != null)
        {
            pauseButton.onClick.RemoveListener(SetPause);
            pauseButton.onClick.AddListener(SetPause);
        }

        if (speed1xButton != null)
        {
            speed1xButton.onClick.RemoveListener(Set1x);
            speed1xButton.onClick.AddListener(Set1x);
        }

        if (speed2xButton != null)
        {
            speed2xButton.onClick.RemoveListener(Set2x);
            speed2xButton.onClick.AddListener(Set2x);
        }
    }

    private void UnbindButtons()
    {
        if (pauseButton != null)
        {
            pauseButton.onClick.RemoveListener(SetPause);
        }

        if (speed1xButton != null)
        {
            speed1xButton.onClick.RemoveListener(Set1x);
        }

        if (speed2xButton != null)
        {
            speed2xButton.onClick.RemoveListener(Set2x);
        }
    }

    private void SetPause()
    {
        if (scheduler == null) return;
        scheduler.SetPaused();
    }

    private void Set1x()
    {
        if (scheduler == null) return;
        scheduler.SetNormalSpeed();
    }

    private void Set2x()
    {
        if (scheduler == null) return;
        scheduler.SetDoubleSpeed();
    }
}
