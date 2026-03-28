using System.Collections.Generic;
using UnityEngine;

public class PlacementManager : MonoBehaviour
{
    [Header("依赖")]
    public GridManager gridManager;

    [Header("可放置房间定义")]
    public List<PlaceableRoom> availableRooms = new List<PlaceableRoom>();

    [Header("可选: 从Grid阶段自动构建可放置对象")]
    public bool autoBuildRoomsFromStageOnAwake = false;
    public int sourceStageIndex = 0;
    public bool clearRoomsBeforeBuild = true;
    public bool disableSourceStageObjects = true;

    [Header("调试交互")]
    public bool enableDebugInput = false;
    public Camera mainCamera;
    public LayerMask placementRaycastMask = ~0;

    private int selectedRoomIndex = 0;
    private readonly Dictionary<Vector2Int, PlacedRoomData> originToPlacedRoom = new Dictionary<Vector2Int, PlacedRoomData>();

    private class PlacedRoomData
    {
        public PlaceableRoom room;
        public Vector2Int origin;
        public List<Vector2Int> occupiedPositions = new List<Vector2Int>();
        public GameObject instance;
    }

    void Awake()
    {
        if (gridManager == null)
        {
            gridManager = FindObjectOfType<GridManager>();
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (autoBuildRoomsFromStageOnAwake)
        {
            BuildRoomsFromStage(sourceStageIndex);
        }
    }

    void Update()
    {
        if (!enableDebugInput) return;
        HandleDebugInput();
    }

    public bool CanPlaceObject(PlaceableRoom obj, Vector2Int origin)
    {
        if (gridManager == null || obj == null || obj.occupiedCells == null) return false;

        foreach (Vector2Int offset in obj.occupiedCells)
        {
            Vector2Int targetPos = origin + offset;
            if (!gridManager.CanOccupyCell(targetPos)) return false;
        }

        return true;
    }

    public bool TryPlaceObject(PlaceableRoom room, Vector2Int origin)
    {
        if (!CanPlaceObject(room, origin)) return false;
        PlaceObject(room, origin);
        return true;
    }

    public void PlaceObject(PlaceableRoom obj, Vector2Int origin)
    {
        if (gridManager == null || obj == null)
        {
            Debug.LogWarning("PlaceObject 失败: gridManager 或 obj 为空");
            return;
        }

        if (originToPlacedRoom.ContainsKey(origin))
        {
            Debug.LogWarning($"PlaceObject 失败: 原点 {origin} 已有已放置对象");
            return;
        }

        if (!CanPlaceObject(obj, origin))
        {
            Debug.LogWarning($"PlaceObject 失败: {obj.roomName} 无法放在 {origin}");
            return;
        }

        var data = new PlacedRoomData
        {
            room = obj,
            origin = origin
        };

        foreach (Vector2Int offset in obj.occupiedCells)
        {
            Vector2Int targetPos = origin + offset;
            gridManager.SetCellOccupied(targetPos, true, obj);
            data.occupiedPositions.Add(targetPos);
        }

        if (obj.roomPrefab != null)
        {
            Vector3 spawnPosition = gridManager.GridToWorldPosition(origin);
            spawnPosition.z = gridManager.placementZ;
            data.instance = Instantiate(obj.roomPrefab, spawnPosition, Quaternion.identity, transform);
            data.instance.name = string.IsNullOrEmpty(obj.roomName) ? obj.roomPrefab.name : obj.roomName;
        }

        originToPlacedRoom[origin] = data;
    }

    public bool TryRemoveObject(Vector2Int origin)
    {
        if (!originToPlacedRoom.ContainsKey(origin)) return false;
        RemoveObject(origin);
        return true;
    }

    public void RemoveObject(Vector2Int origin)
    {
        if (!originToPlacedRoom.TryGetValue(origin, out PlacedRoomData data))
        {
            Debug.LogWarning($"RemoveObject 失败: 原点 {origin} 没有已放置对象");
            return;
        }

        foreach (Vector2Int pos in data.occupiedPositions)
        {
            gridManager.SetCellOccupied(pos, false, null);
        }

        if (data.instance != null)
        {
            Destroy(data.instance);
        }

        originToPlacedRoom.Remove(origin);
    }

    public void SelectRoomByIndex(int index)
    {
        if (availableRooms == null || availableRooms.Count == 0) return;
        if (index < 0 || index >= availableRooms.Count) return;
        selectedRoomIndex = index;
    }

    [ContextMenu("Build Rooms From Source Stage")]
    public void BuildRoomsFromSourceStage()
    {
        BuildRoomsFromStage(sourceStageIndex);
    }

    public void BuildRoomsFromStage(int stageIndex)
    {
        if (gridManager == null)
        {
            Debug.LogWarning("BuildRoomsFromStage 失败: gridManager 为空");
            return;
        }

        if (gridManager.stages == null || gridManager.stages.Count == 0)
        {
            Debug.LogWarning("BuildRoomsFromStage 失败: gridManager.stages 为空");
            return;
        }

        if (stageIndex < 0 || stageIndex >= gridManager.stages.Count)
        {
            Debug.LogWarning($"BuildRoomsFromStage 失败: stageIndex 越界({stageIndex})");
            return;
        }

        if (clearRoomsBeforeBuild)
        {
            availableRooms.Clear();
            selectedRoomIndex = 0;
        }

        GridStage stage = gridManager.stages[stageIndex];
        int created = 0;

        for (int i = 0; i < stage.cellObjects.Count; i++)
        {
            GameObject source = stage.cellObjects[i];
            if (source == null) continue;

            PlaceableRoom room = new PlaceableRoom
            {
                roomName = $"Stage{stageIndex}_{source.name}_{i}",
                roomPrefab = source,
                occupiedCells = new List<Vector2Int> { Vector2Int.zero }
            };

            availableRooms.Add(room);
            created++;

            if (disableSourceStageObjects)
            {
                source.SetActive(false);
            }
        }

        Debug.Log($"已从 Stage {stageIndex} 构建可放置对象: {created} 个");
    }

    private void HandleDebugInput()
    {
        if (gridManager == null) return;

        if (Input.GetKeyDown(KeyCode.PageUp))
        {
            gridManager.UnlockNextStage();
        }

        if (availableRooms == null || availableRooms.Count == 0) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) selectedRoomIndex = 0;
        if (Input.GetKeyDown(KeyCode.Alpha2) && availableRooms.Count > 1) selectedRoomIndex = 1;
        if (Input.GetKeyDown(KeyCode.Alpha3) && availableRooms.Count > 2) selectedRoomIndex = 2;
        if (Input.GetKeyDown(KeyCode.Alpha4) && availableRooms.Count > 3) selectedRoomIndex = 3;

        if (!TryGetGridPositionFromMouse(out Vector2Int origin)) return;

        if (Input.GetMouseButtonDown(0))
        {
            TryPlaceObject(availableRooms[selectedRoomIndex], origin);
        }

        if (Input.GetMouseButtonDown(1))
        {
            TryRemoveObject(origin);
        }
    }

    private bool TryGetGridPositionFromMouse(out Vector2Int gridPosition)
    {
        gridPosition = default;
        if (mainCamera == null || gridManager == null) return false;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, placementRaycastMask))
        {
            return false;
        }

        gridPosition = gridManager.WorldToGridPosition(hit.point);
        return true;
    }
}
