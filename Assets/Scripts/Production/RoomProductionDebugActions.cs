using UnityEngine;

// 调试入口：可在Inspector按钮或UI按钮上直接调用。
public class RoomProductionDebugActions : MonoBehaviour
{
    public RoomProductionUnit target;

    public void BuildAndStart()
    {
        if (target == null) return;
        target.CompleteConstructionAndStart();
    }

    public void PauseManual()
    {
        if (target == null) return;
        target.PauseManual();
    }

    public void ResumeManual()
    {
        if (target == null) return;
        target.ResumeManual();
    }
}
