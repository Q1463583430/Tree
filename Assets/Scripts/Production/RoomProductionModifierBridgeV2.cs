using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-80)]
public class RoomProductionModifierBridgeV2 : MonoBehaviour
{
    private const string DefaultSettingsResourcePath = "ProductionModifierV2Settings";

    public static RoomProductionModifierBridgeV2 Instance { get; private set; }

    [Header("V2 配置")]
    public ProductionModifierV2Settings settings;
    public string settingsResourcePath = DefaultSettingsResourcePath;

    [Header("刷新")]
    [Min(0.1f)] public float periodicRefreshSeconds = 0.5f;

    [Header("生命周期")]
    public bool keepAliveAcrossScenes = true;

    private RoomEmployeeAssignmentManager _assignmentManager;
    private readonly Dictionary<RoomProductionUnit, float> _lastAppliedByRoom = new Dictionary<RoomProductionUnit, float>();
    private readonly Dictionary<RoomProductionUnit, RoomProductionModifierBreakdownV2> _lastBreakdownByRoom = new Dictionary<RoomProductionUnit, RoomProductionModifierBreakdownV2>();
    private float _nextRefreshTime;

    public static bool TryGetLatestBreakdown(RoomProductionUnit room, out RoomProductionModifierBreakdownV2 breakdown)
    {
        breakdown = null;
        if (room == null || Instance == null)
        {
            return false;
        }

        if (!Instance._lastBreakdownByRoom.TryGetValue(room, out breakdown))
        {
            return false;
        }

        return breakdown != null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateOnLoad()
    {
        if (FindObjectOfType<RoomProductionModifierBridgeV2>() != null)
        {
            return;
        }

        GameObject go = new GameObject("RoomProductionModifierBridgeV2_Auto");
        go.AddComponent<RoomProductionModifierBridgeV2>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (keepAliveAcrossScenes)
        {
            DontDestroyOnLoad(gameObject);
        }

        EnsureSettings();
        EnsureAssignmentManager();
    }

    void OnEnable()
    {
        EnsureAssignmentManager();
        SubscribeAssignmentEvents();
        RefreshAllRooms();
    }

    void OnDisable()
    {
        UnsubscribeAssignmentEvents();
    }

    void Update()
    {
        if (Time.unscaledTime < _nextRefreshTime)
        {
            return;
        }

        _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, periodicRefreshSeconds);
        RefreshAllRooms();
    }

    private void HandleRoomAssignmentsChanged(RoomProductionUnit room)
    {
        RefreshRoom(room);
    }

    private void EnsureSettings()
    {
        if (settings != null)
        {
            return;
        }

        string path = string.IsNullOrWhiteSpace(settingsResourcePath)
            ? DefaultSettingsResourcePath
            : settingsResourcePath.Trim();

        settings = Resources.Load<ProductionModifierV2Settings>(path);
        if (settings == null)
        {
            // 未创建资产时使用运行时默认配置：按roomId配置缺失时回退AverageAll。
            settings = ScriptableObject.CreateInstance<ProductionModifierV2Settings>();
            settings.useModifierV2 = true;
            settings.defaultMainStat = RoomMainStatSelector.AverageAll;
        }
    }

    private void EnsureAssignmentManager()
    {
        if (_assignmentManager != null)
        {
            return;
        }

        _assignmentManager = RoomEmployeeAssignmentManager.Instance;
        if (_assignmentManager == null)
        {
            _assignmentManager = FindObjectOfType<RoomEmployeeAssignmentManager>();
        }

        if (_assignmentManager == null)
        {
            _assignmentManager = RoomEmployeeAssignmentManager.EnsureInstance();
        }
    }

    private void SubscribeAssignmentEvents()
    {
        if (_assignmentManager == null)
        {
            return;
        }

        _assignmentManager.OnRoomAssignmentsChanged -= HandleRoomAssignmentsChanged;
        _assignmentManager.OnRoomAssignmentsChanged += HandleRoomAssignmentsChanged;
    }

    private void UnsubscribeAssignmentEvents()
    {
        if (_assignmentManager == null)
        {
            return;
        }

        _assignmentManager.OnRoomAssignmentsChanged -= HandleRoomAssignmentsChanged;
    }

    private void RefreshAllRooms()
    {
        EnsureSettings();
        EnsureAssignmentManager();

        if (settings == null || !settings.useModifierV2)
        {
            return;
        }

        RoomProductionUnit[] rooms = FindObjectsOfType<RoomProductionUnit>();
        for (int i = 0; i < rooms.Length; i++)
        {
            RefreshRoom(rooms[i]);
        }

        CleanupMissingRooms();
    }

    private void RefreshRoom(RoomProductionUnit room)
    {
        if (room == null)
        {
            return;
        }

        EnsureSettings();
        EnsureAssignmentManager();

        if (settings == null || !settings.useModifierV2)
        {
            return;
        }

        List<HREmployeeData> employees = _assignmentManager != null
            ? _assignmentManager.GetAssignedEmployees(room)
            : new List<HREmployeeData>();

        float nextMultiplier = RoomProductionModifierEngineV2.CalculateMultiplier(room.plan, employees, settings, out RoomProductionModifierBreakdownV2 breakdown);
        _lastBreakdownByRoom[room] = breakdown;

        float lastApplied;
        if (_lastAppliedByRoom.TryGetValue(room, out lastApplied) && Mathf.Abs(lastApplied - nextMultiplier) < 0.0001f)
        {
            return;
        }

        room.SetPendingOutputMultiplierExternal(nextMultiplier);
        room.ApplyCurrentOutputMultiplierExternal(nextMultiplier);
        _lastAppliedByRoom[room] = nextMultiplier;

        if (settings.enableVerboseLog)
        {
            Debug.Log("[RoomProductionModifierBridgeV2] room=" + room.name
                + ", roomId=" + breakdown.roomId
                + ", mainStat=" + breakdown.mainStat
                + ", employees=" + breakdown.employeeCount
                + ", statRateSum=" + breakdown.statRateSum.ToString("0.###")
                + ", final=" + breakdown.finalMultiplier.ToString("0.###"), room);
        }
    }

    private void CleanupMissingRooms()
    {
        if (_lastAppliedByRoom.Count == 0)
        {
            _lastBreakdownByRoom.Clear();
            return;
        }

        List<RoomProductionUnit> toRemove = null;
        foreach (KeyValuePair<RoomProductionUnit, float> kv in _lastAppliedByRoom)
        {
            if (kv.Key != null)
            {
                continue;
            }

            if (toRemove == null)
            {
                toRemove = new List<RoomProductionUnit>();
            }

            toRemove.Add(kv.Key);
        }

        if (toRemove == null)
        {
            return;
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            _lastAppliedByRoom.Remove(toRemove[i]);
            _lastBreakdownByRoom.Remove(toRemove[i]);
        }
    }
}
