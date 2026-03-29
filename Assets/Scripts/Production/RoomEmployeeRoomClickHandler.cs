using UnityEngine;

// 挂在已放置房间对象上：左键点击房间打开配鼠界面。
public class RoomEmployeeRoomClickHandler : MonoBehaviour
{
    public RoomProductionUnit roomUnit;

    void Awake()
    {
        if (roomUnit == null)
        {
            roomUnit = GetComponent<RoomProductionUnit>();
        }

        if (roomUnit == null)
        {
            roomUnit = GetComponentInChildren<RoomProductionUnit>(true);
        }
    }

    void OnMouseDown()
    {
        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        if (roomUnit == null)
        {
            return;
        }

        RoomEmployeeWarehouseUI ui = RoomEmployeeWarehouseUI.EnsureInstance();
        ui.OpenRoomConfig(roomUnit);
    }
}
