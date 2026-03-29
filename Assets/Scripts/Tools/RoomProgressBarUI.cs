using UnityEngine;
using UnityEngine.UI;

// 房间进度条UI：读取 RoomProductionUnit 的周期进度并刷新填充与状态颜色。
public class RoomProgressBarUI : MonoBehaviour
{
    [Header("绑定")]
    public RoomProductionUnit roomUnit;
    public Image progressFill;

    [Header("显示")]
    public bool hideWhenNotRunning = false;

    [Header("颜色")]
    public Color runningColor = new Color(0.25f, 0.8f, 0.35f, 1f);
    public Color pausedManualColor = new Color(0.65f, 0.65f, 0.65f, 1f);
    public Color pausedNoResourceColor = new Color(0.95f, 0.45f, 0.25f, 1f);

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
        if (progressFill == null)
        {
            return;
        }

        if (roomUnit == null)
        {
            progressFill.fillAmount = 0f;
            progressFill.enabled = !hideWhenNotRunning;
            return;
        }

        bool isRunning = roomUnit.IsBuilt && roomUnit.State == RoomProductionState.Running;
        progressFill.enabled = isRunning || !hideWhenNotRunning;

        if (!progressFill.enabled)
        {
            return;
        }

        progressFill.fillAmount = roomUnit.GetProgress01(Time.timeAsDouble);
        progressFill.color = GetStateColor(roomUnit.State);
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
