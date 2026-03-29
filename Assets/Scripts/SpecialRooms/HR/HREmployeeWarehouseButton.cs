using UnityEngine;

// 可绑定到UI Button的 OnClick：打开鼠鼠仓库界面。
public class HREmployeeWarehouseButton : MonoBehaviour
{
    public void OpenWarehouse()
    {
        RoomEmployeeWarehouseUI ui = RoomEmployeeWarehouseUI.EnsureInstance();
        ui.OpenWarehouse();
    }
}
