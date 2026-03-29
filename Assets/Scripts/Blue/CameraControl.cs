using UnityEngine;

public class CameraControl : MonoBehaviour
{
    [Header("移动参数")]
    // 最大移动速度
    public float keyboardspeed = 10f;
    // 加速加速度
    public float acceleration = 20f;
    // 减速减速度
    public float deceleration = 25f;
    private Vector3 currentVelocity;

    [Header("缩放参数")]
    // 滚轮缩放速度（每次滚轮输入对应的Z变化系数）
    public float zoomSpeed = 2f;
    // 滚轮输入缩放系数，值越小越不敏感。
    public float scrollSensitivity = 0.05f;
    // Z轴缩放下限（更远，通常是更小的负值）。
    public float minZoomDistance = -80f;
    // Z轴缩放上限（更近，通常接近0的负值）。
    public float maxZoomDistance = -2f;

    private void Awake()
    {
        Camera cam = GetComponent<Camera>();
        if (cam != null)
        {
            // 方案2：通过改变Z轴实现透视缩放。
            cam.orthographic = false;
        }
    }

    private void OnValidate()
    {
        if (minZoomDistance > maxZoomDistance)
        {
            float temp = minZoomDistance;
            minZoomDistance = maxZoomDistance;
            maxZoomDistance = temp;
        }

        zoomSpeed = Mathf.Max(0f, zoomSpeed);
        scrollSensitivity = Mathf.Max(0f, scrollSensitivity);
    }

    private void Update()
    {
        HandleZoomInput();

        // x
        float x = Input.GetAxisRaw("Horizontal");
        // y
        float y = Input.GetAxisRaw("Vertical");

        // up相机上方向
        Vector3 up = transform.up;
        up.z = 0f;
        up.Normalize();

        // right相机右方向
        Vector3 right = transform.right;
        right.z = 0f;
        right.Normalize();

    
        Vector3 inputDir = (right * x + up * y).normalized;
        // targetVelocity：期望达到的目标速度。
        Vector3 targetVelocity = inputDir * keyboardspeed;

        // rate，速度变化率
        float rate = inputDir.sqrMagnitude > 0f ? acceleration : deceleration;
        currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, rate * Time.deltaTime);

        transform.position += currentVelocity * Time.deltaTime;
    }

    private void HandleZoomInput()
    {
        // 滚轮有输入就缩放；停止输入就立即停在当前位置。
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) < 0.0001f)
        {
            return;
        }

        Vector3 position = transform.position;
        float minZ = Mathf.Min(minZoomDistance, maxZoomDistance);
        float maxZ = Mathf.Max(minZoomDistance, maxZoomDistance);
        float targetZ = Mathf.Clamp(
            position.z - scroll * zoomSpeed * scrollSensitivity,
            minZ,
            maxZ
        );
        transform.position = new Vector3(position.x, position.y, targetZ);
    }
}
