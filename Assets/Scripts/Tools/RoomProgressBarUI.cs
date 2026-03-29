using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 房间进度条UI：读取 RoomProductionUnit 的周期进度并刷新填充与状态颜色。
public class RoomProgressBarUI : MonoBehaviour
{
    [Header("绑定")]
    public RoomProductionUnit roomUnit;
    public Image progressFill;
    public Image statusBackground;
    public TMP_Text statusText;
    public TMP_Text timerText;

    [Header("显示")]
    public bool hideWhenNotRunning = false;
    public bool faceMainCamera = true;

    [Header("颜色")]
    public Color runningColor = new Color(0.25f, 0.8f, 0.35f, 1f);
    public Color pausedManualColor = new Color(0.65f, 0.65f, 0.65f, 1f);
    public Color pausedNoResourceColor = new Color(0.95f, 0.45f, 0.25f, 1f);

    [Header("文案")]
    public string runningLabel = "运行中";
    public string pausedManualLabel = "待机";
    public string pausedNoResourceLabel = "资源不足";
    public string notBuiltLabel = "未建造";

    void Reset()
    {
        if (roomUnit == null)
        {
            roomUnit = GetComponentInParent<RoomProductionUnit>();
        }

        if (progressFill == null)
        {
            progressFill = GetComponent<Image>();
        }
    }

    void Awake()
    {
        if (roomUnit == null)
        {
            roomUnit = GetComponentInParent<RoomProductionUnit>();
        }
    }

    void Update()
    {
        if (faceMainCamera && Camera.main != null)
        {
            transform.forward = Camera.main.transform.forward;
        }

        if (progressFill == null)
        {
            return;
        }

        if (roomUnit == null)
        {
            progressFill.fillAmount = 0f;
            progressFill.enabled = !hideWhenNotRunning;
            SetStatus(notBuiltLabel, pausedManualColor);
            SetTimerText("--");
            return;
        }

        bool isRunning = roomUnit.IsBuilt && roomUnit.State == RoomProductionState.Running;
        progressFill.enabled = isRunning || !hideWhenNotRunning;

        if (!progressFill.enabled)
        {
            UpdateStatusAndTimer(isRunning);
            return;
        }

        progressFill.fillAmount = roomUnit.GetProgress01(Time.timeAsDouble);
        Color stateColor = GetStateColor(roomUnit.State);
        progressFill.color = stateColor;
        UpdateStatusAndTimer(isRunning);
    }

    private void UpdateStatusAndTimer(bool isRunning)
    {
        if (roomUnit == null)
        {
            SetStatus(notBuiltLabel, pausedManualColor);
            SetTimerText("--");
            return;
        }

        if (!roomUnit.IsBuilt)
        {
            SetStatus(notBuiltLabel, pausedManualColor);
            SetTimerText("--");
            return;
        }

        string label = GetStateLabel(roomUnit.State);
        Color color = GetStateColor(roomUnit.State);
        SetStatus(label, color);

        if (isRunning)
        {
            float remain = roomUnit.GetRemainingSeconds(Time.timeAsDouble);
            SetTimerText(Mathf.CeilToInt(remain) + "s");
        }
        else
        {
            SetTimerText("--");
        }
    }

    private void SetStatus(string text, Color color)
    {
        if (statusText != null)
        {
            statusText.text = text;
        }

        if (statusBackground != null)
        {
            statusBackground.color = color;
        }
    }

    private void SetTimerText(string text)
    {
        if (timerText != null)
        {
            timerText.text = text;
        }
    }

    private string GetStateLabel(RoomProductionState state)
    {
        switch (state)
        {
            case RoomProductionState.Running:
                return runningLabel;
            case RoomProductionState.PausedNoResource:
                return pausedNoResourceLabel;
            case RoomProductionState.PausedManual:
            default:
                return pausedManualLabel;
        }
    }

    private Color GetStateColor(RoomProductionState state)
    {
        switch (state)
        {
            case RoomProductionState.Running:
                return runningColor;
            case RoomProductionState.PausedNoResource:
                return pausedNoResourceColor;
            case RoomProductionState.PausedManual:
            default:
                return pausedManualColor;
        }
    }
}
