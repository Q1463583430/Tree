using UnityEngine;
using UnityEngine.UI;

// 通过三层背景透明度混合，实现白天/黄昏/夜晚平滑切换。
public class DayPhaseBackgroundBlender : MonoBehaviour
{
    [Header("依赖")]
    public RoomProductionScheduler scheduler;

    [Header("三层背景(同位置叠放)")]
    public Graphic dayBackground;
    public Graphic duskBackground;
    public Graphic nightBackground;

    [Header("过渡参数")]
    [Range(0.01f, 0.3f)] public float transitionWindow = 0.08f;
    [Min(0f)] public float alphaLerpSpeed = 8f;
    public bool useUnscaledDeltaTime = true;

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
        RefreshVisual(true);
    }

    void Update()
    {
        RefreshVisual(false);
    }

    public void RefreshVisual(bool instant)
    {
        if (scheduler == null)
        {
            return;
        }

        float progress = scheduler.GetDayProgress01();
        float duskStart = Mathf.Clamp01(scheduler.duskStartNormalized);
        float nightStart = Mathf.Clamp01(scheduler.nightStartNormalized);
        if (nightStart < duskStart)
        {
            nightStart = duskStart;
        }

        float blend = Mathf.Clamp(transitionWindow, 0.01f, 0.3f);
        float dayToDusk = Mathf.InverseLerp(duskStart - blend, duskStart + blend, progress);
        float duskToNight = Mathf.InverseLerp(nightStart - blend, nightStart + blend, progress);

        float dayAlpha = 1f - Mathf.Clamp01(dayToDusk);
        float nightAlpha = Mathf.Clamp01(duskToNight);
        float duskAlpha = Mathf.Clamp01(dayToDusk) * (1f - nightAlpha);

        float delta = instant ? 1f : (useUnscaledDeltaTime ? Time.unscaledDeltaTime : Time.deltaTime);
        float lerpFactor = instant ? 1f : Mathf.Clamp01(alphaLerpSpeed * Mathf.Max(0f, delta));

        ApplyAlpha(dayBackground, dayAlpha, lerpFactor, instant);
        ApplyAlpha(duskBackground, duskAlpha, lerpFactor, instant);
        ApplyAlpha(nightBackground, nightAlpha, lerpFactor, instant);
    }

    private static void ApplyAlpha(Graphic graphic, float target, float lerpFactor, bool instant)
    {
        if (graphic == null)
        {
            return;
        }

        Color color = graphic.color;
        if (instant)
        {
            color.a = target;
        }
        else
        {
            color.a = Mathf.Lerp(color.a, target, lerpFactor);
        }

        graphic.color = color;
    }
}