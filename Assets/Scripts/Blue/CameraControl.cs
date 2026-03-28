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
    // 滚轮缩放速度
    public float zoomSpeed = 10f;
    // 滚轮输入缩放系数，值越小越不敏感。
    public float scrollSensitivity = 0.20f;
    // 相机到焦点的最近距离。
    public float minZoomDistance = 5f;
    // 相机到焦点的最远距离。
    public float maxZoomDistance = 80f;
    // 作为选点参考的地面高度
    public float focusPlaneY = 0f;

    private Camera cachedCamera;

    private void Awake()
    {
        cachedCamera = GetComponent<Camera>();
        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
        }
    }

    private void Update()
    {
        HandleZoomInput();

        // x
        float x = Input.GetAxisRaw("Horizontal");
        //z
        float z = Input.GetAxisRaw("Vertical");

        // forward相机前方向
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        // right相机右方向
        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();

    
        Vector3 inputDir = (right * x + forward * z).normalized;
        // targetVelocity：期望达到的目标速度。
        Vector3 targetVelocity = inputDir * keyboardspeed;

        // rate，速度变化率
        float rate = inputDir.sqrMagnitude > 0f ? acceleration : deceleration;
        currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, rate * Time.deltaTime);

        transform.position += currentVelocity * Time.deltaTime;
    }

    private void HandleZoomInput()
    {
        // 直接以鼠标当前位置作为缩放焦点
        float scroll = Input.GetAxis("Mouse ScrollWheel") * scrollSensitivity;
        if (Mathf.Abs(scroll) < 0.0001f)
        {
            return;
        }

        if (!TryGetMouseWorldPoint(out Vector3 zoomFocusPoint))
        {
            return;
        }

        ZoomTowardFocus(zoomFocusPoint, scroll);
    }

    private bool TryGetMouseWorldPoint(out Vector3 worldPoint)
    {
        if (cachedCamera == null)
        {
            worldPoint = Vector3.zero;
            return false;
        }

        Ray ray = cachedCamera.ScreenPointToRay(Input.mousePosition);
        // 用固定高度平面承接鼠标射线，得到稳定的世界焦点。
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, focusPlaneY, 0f));
        if (groundPlane.Raycast(ray, out float enter))
        {
            worldPoint = ray.GetPoint(enter);
            return true;
        }

        worldPoint = Vector3.zero;
        return false;
    }

    private void ZoomTowardFocus(Vector3 focusPoint, float scrollValue)
    {
        Vector3 toFocus = focusPoint - transform.position;
        float currentDistance = toFocus.magnitude;
        if (currentDistance < 0.0001f)
        {
            return;
        }

        // 通过限制焦点距离，防止穿地或拉得过远。
        float targetDistance = Mathf.Clamp(
            currentDistance - scrollValue * zoomSpeed,
            minZoomDistance,
            maxZoomDistance
        );

        float moveDistance = currentDistance - targetDistance;
        transform.position += toFocus.normalized * moveDistance;
    }
}
