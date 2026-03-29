using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 简易仓库与房间配鼠界面：支持手动分配、取消分配、查看当前/下周期倍率。
public class RoomEmployeeWarehouseUI : MonoBehaviour
{
    public static RoomEmployeeWarehouseUI Instance { get; private set; }

    [Header("依赖")]
    public RoomEmployeeAssignmentManager assignmentManager;

    [Header("显示")]
    public bool showDebugWarehouseButton = true;
    public string warehouseButtonText = "鼠鼠仓库";

    private Rect _windowRect = new Rect(20f, 80f, 700f, 560f);
    private Vector2 _assignedScroll;
    private Vector2 _poolScroll;
    private bool _isOpen;
    private RoomProductionUnit _activeRoom;
    private string _message = string.Empty;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (assignmentManager == null)
        {
            assignmentManager = RoomEmployeeAssignmentManager.EnsureInstance();
        }
    }

    public static RoomEmployeeWarehouseUI EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        RoomEmployeeWarehouseUI existing = FindObjectOfType<RoomEmployeeWarehouseUI>();
        if (existing != null)
        {
            return existing;
        }

        GameObject go = new GameObject("RoomEmployeeWarehouseUI_Auto");
        return go.AddComponent<RoomEmployeeWarehouseUI>();
    }

    public void OpenWarehouse()
    {
        _activeRoom = null;
        _isOpen = true;
        _message = string.Empty;
    }

    public void OpenRoomConfig(RoomProductionUnit room)
    {
        _activeRoom = room;
        _isOpen = true;
        _message = "配置完成后，将在下一次30秒结算生效。";
    }

    public void Close()
    {
        _isOpen = false;
    }

    void OnGUI()
    {
        if (showDebugWarehouseButton)
        {
            Rect buttonRect = new Rect(Screen.width - 120f, 12f, 100f, 34f);
            if (GUI.Button(buttonRect, warehouseButtonText))
            {
                OpenWarehouse();
            }
        }

        if (!_isOpen)
        {
            return;
        }

        _windowRect = GUILayout.Window(91173, _windowRect, DrawWindow, "鼠鼠仓库/配鼠");
    }

    private void DrawWindow(int id)
    {
        if (assignmentManager == null)
        {
            assignmentManager = RoomEmployeeAssignmentManager.EnsureInstance();
        }

        if (_activeRoom == null)
        {
            DrawWarehouseView();
        }
        else
        {
            DrawRoomConfigView();
        }

        if (!string.IsNullOrEmpty(_message))
        {
            GUILayout.Space(6f);
            GUILayout.Label(_message);
        }

        GUILayout.Space(8f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("只看仓库", GUILayout.Height(28f)))
        {
            OpenWarehouse();
        }

        if (GUILayout.Button("关闭", GUILayout.Height(28f)))
        {
            Close();
        }
        GUILayout.EndHorizontal();

        GUI.DragWindow(new Rect(0, 0, 10000, 24));
    }

    private void DrawWarehouseView()
    {
        GUILayout.Label("仓库列表（点击房间后可在此界面配置）");
        IReadOnlyList<HREmployeeData> all = assignmentManager.GetAllEmployees();
        if (all.Count == 0)
        {
            GUILayout.Label("仓库暂无鼠鼠。请先打开 HR 房间进行手动招募。");
            return;
        }

        _poolScroll = GUILayout.BeginScrollView(_poolScroll, GUILayout.Height(430f));
        for (int i = 0; i < all.Count; i++)
        {
            HREmployeeData e = all[i];
            if (e == null)
            {
                continue;
            }

            RoomProductionUnit owner = assignmentManager.GetEmployeeOwner(e.id);
            string ownerName = owner == null ? "空闲" : owner.name;
            GUILayout.BeginHorizontal("box");
            GUILayout.Label(BuildEmployeeSummary(e) + " | 状态: " + ownerName, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
    }

    private void DrawRoomConfigView()
    {
        if (_activeRoom == null)
        {
            GUILayout.Label("当前未选中房间");
            return;
        }

        int required = Mathf.Max(0, _activeRoom.plan.requiredSquirrels);
        int assignedCount = assignmentManager.GetAssignedCount(_activeRoom);
        float currentMul = _activeRoom.GetActiveOutputMultiplier();
        float nextMul = _activeRoom.GetPendingOutputMultiplier();

        GUILayout.Label("房间: " + _activeRoom.name);
        GUILayout.Label("需求: " + required + " | 已配置: " + assignedCount + " | 当前倍率: " + currentMul.ToString("0.###") + " | 下周期倍率: " + nextMul.ToString("0.###"));

        GUILayout.Space(6f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("清空该房间配置", GUILayout.Height(28f)))
        {
            assignmentManager.ClearRoom(_activeRoom);
            _message = "已清空，下一次30秒结算开始按新配置生效。";
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        GUILayout.Label("已配置鼠鼠");
        _assignedScroll = GUILayout.BeginScrollView(_assignedScroll, GUILayout.Height(150f));
        List<HREmployeeData> assigned = assignmentManager.GetAssignedEmployees(_activeRoom);
        if (assigned.Count == 0)
        {
            GUILayout.Label("暂无已配置鼠鼠");
        }
        else
        {
            for (int i = 0; i < assigned.Count; i++)
            {
                HREmployeeData e = assigned[i];
                if (e == null)
                {
                    continue;
                }

                GUILayout.BeginHorizontal("box");
                GUILayout.Label(BuildEmployeeSummary(e), GUILayout.ExpandWidth(true));
                if (GUILayout.Button("移除", GUILayout.Width(70f), GUILayout.Height(24f)))
                {
                    if (assignmentManager.TryUnassign(_activeRoom, e.id))
                    {
                        _message = "已移除，将在下一次30秒结算生效。";
                    }
                }
                GUILayout.EndHorizontal();
            }
        }
        GUILayout.EndScrollView();

        GUILayout.Space(8f);
        GUILayout.Label("可配置鼠鼠");
        _poolScroll = GUILayout.BeginScrollView(_poolScroll, GUILayout.Height(220f));
        IReadOnlyList<HREmployeeData> all = assignmentManager.GetAllEmployees();
        for (int i = 0; i < all.Count; i++)
        {
            HREmployeeData e = all[i];
            if (e == null)
            {
                continue;
            }

            if (!assignmentManager.CanAssignToRoom(_activeRoom, e, out string reason))
            {
                GUILayout.BeginHorizontal("box");
                GUILayout.Label(BuildEmployeeSummary(e) + " | 不可配置: " + reason, GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();
                continue;
            }

            RoomProductionUnit owner = assignmentManager.GetEmployeeOwner(e.id);
            bool alreadyHere = owner == _activeRoom;

            GUILayout.BeginHorizontal("box");
            GUILayout.Label(BuildEmployeeSummary(e), GUILayout.ExpandWidth(true));

            GUI.enabled = !alreadyHere;
            if (GUILayout.Button(alreadyHere ? "已在本房间" : "配置", GUILayout.Width(90f), GUILayout.Height(24f)))
            {
                if (assignmentManager.TryAssign(_activeRoom, e.id, out string failReason))
                {
                    _message = "配置成功，将在下一次30秒结算生效。";
                }
                else
                {
                    _message = failReason;
                }
            }
            GUI.enabled = true;

            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
    }

    private static string BuildEmployeeSummary(HREmployeeData e)
    {
        string traits = BuildTraits(e);
        return string.Format("{0} [{1}] 体:{2} 智:{3} 魔:{4} 词条:{5}", e.displayName, e.id, e.stamina, e.intelligence, e.magic, traits);
    }

    private static string BuildTraits(HREmployeeData e)
    {
        if (e == null || e.traits == null || e.traits.Count == 0)
        {
            return "无";
        }

        List<string> names = new List<string>(e.traits.Count);
        for (int i = 0; i < e.traits.Count; i++)
        {
            names.Add(e.traits[i].ToString());
        }

        return string.Join("/", names);
    }
}
