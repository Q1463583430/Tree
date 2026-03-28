using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class BluePrint : MonoBehaviour
{
    [Header("主按钮与主面板")]
    public Button magicBuildingButton;
    public GameObject magicBuildingPanel;
    public CanvasGroup magicBuildingCanvasGroup;
    public bool hidePanelOnStart = false;
    public bool togglePanelByButton = false;

    [Header("四个模块按钮")]
    public Button productionButton;
    public Button convertButton;
    public Button growthButton;
    public Button otherButton;

    [Header("房间列表（每页4个）")]
    public Transform roomButtonRoot;
    public RoomButtonItem roomButtonPrefab;
    public Button prevPageButton;
    public Button nextPageButton;
    public TMP_Text pageText;
    public TMP_Text roomDescriptionText;
    public BuildingRoomCatalog roomCatalog;

    [Header("网格放置参数")]
    public GridManager gridManager;
    public Camera placementCamera;
    public bool alignRoomPrefabToFootprintCenter = true;
    public float roomZOffset = -2f;
    public Color previewValidColor = new Color(0.3f, 1f, 0.4f, 0.65f);
    public Color previewInvalidColor = new Color(1f, 0.3f, 0.3f, 0.65f);

    private const int RoomsPerPage = 4;
    private readonly List<RoomButtonItem> spawnedButtons = new List<RoomButtonItem>();
    private readonly List<Renderer> previewRenderers = new List<Renderer>();
    private readonly Dictionary<PlaceableRoom, GameObject> placedRoomObjects = new Dictionary<PlaceableRoom, GameObject>();

    private BuildingModuleType currentModule = BuildingModuleType.Production;
    private int currentPage;
    private RoomDefinition hoveredRoom;
    private RoomDefinition selectedRoom;
    private bool isHoldingPlaceButton;
    private GameObject previewInstance;
    private Vector2Int previewOriginCell;
    private bool previewIsValid;
    private string previewInvalidReason = string.Empty;

    private void Awake()
    {
        if (gridManager == null)
        {
            gridManager = FindObjectOfType<GridManager>();
        }

        if (placementCamera == null)
        {
            placementCamera = Camera.main;
        }

        EnsureCanvasGroupReady();

        EnsureRoomButtonRootHorizontalLayout();

        if (magicBuildingButton != null)
        {
            magicBuildingButton.onClick.AddListener(ToggleMagicBuildingPanel);
        }

        if (productionButton != null)
        {
            productionButton.onClick.AddListener(() => ShowModule(BuildingModuleType.Production));
        }

        if (convertButton != null)
        {
            convertButton.onClick.AddListener(() => ShowModule(BuildingModuleType.Convert));
        }

        if (growthButton != null)
        {
            growthButton.onClick.AddListener(() => ShowModule(BuildingModuleType.Growth));
        }

        if (otherButton != null)
        {
            otherButton.onClick.AddListener(() => ShowModule(BuildingModuleType.Other));
        }

        if (prevPageButton != null)
        {
            prevPageButton.onClick.AddListener(PrevPage);
        }

        if (nextPageButton != null)
        {
            nextPageButton.onClick.AddListener(NextPage);
        }

        if (magicBuildingPanel != null)
        {
            magicBuildingPanel.SetActive(!hidePanelOnStart);
        }

        EnsureCatalogReady();
        if (magicBuildingPanel == null || magicBuildingPanel.activeSelf)
        {
            ShowModule(BuildingModuleType.Production);
        }
    }

    private void Update()
    {
        HandlePlacementHoldAndRelease();
        HandleRightClickRemove();
    }

    private void OnDestroy()
    {
        DestroyPreview();
    }

    private void ToggleMagicBuildingPanel()
    {
        if (magicBuildingPanel == null)
        {
            return;
        }

        bool isOpen;
        if (togglePanelByButton)
        {
            isOpen = !magicBuildingPanel.activeSelf;
        }
        else
        {
            isOpen = true;
        }

        magicBuildingPanel.SetActive(isOpen);

        if (isOpen)
        {
            SetCanvasVisible(true);

            if (selectedRoom == null)
            {
                ShowModule(BuildingModuleType.Production);
            }
            else
            {
                RefreshRoomPage();
            }
        }
        else
        {
            CancelCurrentPlacement();
        }
    }

    private void ShowModule(BuildingModuleType module)
    {
        currentModule = module;
        currentPage = 0;

        CancelCurrentPlacement();
        RefreshRoomPage();
    }

    private void ClearRoomButtons()
    {
        for (int i = 0; i < spawnedButtons.Count; i++)
        {
            if (spawnedButtons[i] != null)
            {
                Destroy(spawnedButtons[i].gameObject);
            }
        }

        spawnedButtons.Clear();
    }

    private void PrevPage()
    {
        int maxPage = GetMaxPageIndex();
        if (maxPage <= 0)
        {
            currentPage = 0;
            RefreshRoomPage();
            return;
        }

        currentPage = currentPage <= 0 ? maxPage : currentPage - 1;
        RefreshRoomPage();
    }

    private void NextPage()
    {
        int maxPage = GetMaxPageIndex();
        if (maxPage <= 0)
        {
            currentPage = 0;
            RefreshRoomPage();
            return;
        }

        currentPage = currentPage >= maxPage ? 0 : currentPage + 1;
        RefreshRoomPage();
    }

    private void RefreshRoomPage()
    {
        if (roomButtonRoot == null || roomButtonPrefab == null)
        {
            return;
        }

        EnsureCatalogReady();
        ClearRoomButtons();

        List<RoomDefinition> rooms = GetCurrentModuleRooms();
        int maxPage = GetMaxPageIndex();
        currentPage = Mathf.Clamp(currentPage, 0, maxPage);

        int start = currentPage * RoomsPerPage;
        int end = Mathf.Min(start + RoomsPerPage, rooms.Count);

        for (int i = start; i < end; i++)
        {
            RoomDefinition room = rooms[i];
            if (room == null)
            {
                continue;
            }

            RoomButtonItem item = Instantiate(roomButtonPrefab, roomButtonRoot);
            item.Init(room, this);
            spawnedButtons.Add(item);
        }

        UpdatePageUI();
    }

    private int GetMaxPageIndex()
    {
        List<RoomDefinition> rooms = GetCurrentModuleRooms();
        if (rooms.Count <= 0)
        {
            return 0;
        }

        return (rooms.Count - 1) / RoomsPerPage;
    }

    private void UpdatePageUI()
    {
        List<RoomDefinition> rooms = GetCurrentModuleRooms();
        int maxPage = GetMaxPageIndex();
        currentPage = Mathf.Clamp(currentPage, 0, maxPage);

        if (pageText != null)
        {
            pageText.text = rooms.Count <= 0 ? "0/0" : string.Format("{0}/{1}", currentPage + 1, maxPage + 1);
        }

        bool hasMultiplePages = maxPage > 0;

        if (prevPageButton != null)
        {
            prevPageButton.interactable = hasMultiplePages;
        }

        if (nextPageButton != null)
        {
            nextPageButton.interactable = hasMultiplePages;
        }
    }

    public void OnRoomHovered(RoomDefinition room)
    {
        hoveredRoom = room;
        ShowRoomHoverDescription(room);
    }

    public void OnRoomHoverExit(RoomDefinition room)
    {
        if (hoveredRoom != room)
        {
            return;
        }

        hoveredRoom = null;
        ShowRoomHoverDescription(null);
    }

    public void OnRoomClicked(RoomDefinition room)
    {
        selectedRoom = room;
        RebuildPreviewObject();
    }

    public void OnRoomPointerDown(RoomDefinition room)
    {
        if (room == null)
        {
            return;
        }

        selectedRoom = room;
        RebuildPreviewObject();

        isHoldingPlaceButton = true;
        SetCanvasVisible(false);
        UpdatePreviewFromMouse();
    }

    private void RefreshRoomButtonLabels()
    {
        for (int i = 0; i < spawnedButtons.Count; i++)
        {
            if (spawnedButtons[i] != null)
            {
                spawnedButtons[i].RefreshLabel();
            }
        }
    }

    private void HandlePlacementHoldAndRelease()
    {
        if (selectedRoom == null || gridManager == null)
        {
            isHoldingPlaceButton = false;
            if (previewInstance != null)
            {
                previewInstance.SetActive(false);
            }
            return;
        }

        if (!isHoldingPlaceButton)
        {
            if (previewInstance != null)
            {
                previewInstance.SetActive(false);
            }

            return;
        }

        bool hasPreview = UpdatePreviewFromMouse();

        if (Input.GetMouseButtonUp(0))
        {
            bool placed = hasPreview && TryPlaceSelectedRoomAtPreview();

            if (placed)
            {
                isHoldingPlaceButton = false;
                SetCanvasVisible(true);

                if (previewInstance != null)
                {
                    previewInstance.SetActive(false);
                }
            }
            else
            {
                Debug.LogWarning("放置失败: " + previewInvalidReason);
            }

            return;
        }

        if (!hasPreview)
        {
            return;
        }

        if (Input.GetMouseButton(0))
        {
            UpdatePreviewFromMouse();
        }
    }

    private void HandleRightClickRemove()
    {
        if (gridManager == null)
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (Input.GetMouseButtonDown(1))
        {
            if (!TryGetGridFromMouse(out Vector2Int gridPos))
            {
                return;
            }

            if (!gridManager.TryGetCell(gridPos, out GridCell cell) || !cell.isOccupied || cell.placeableRoom == null)
            {
                return;
            }

            RemovePlacedRoom(cell.placeableRoom);
        }
    }

    private void EnsureRoomButtonRootHorizontalLayout()
    {
        if (roomButtonRoot == null)
        {
            return;
        }

        GridLayoutGroup gridLayout = roomButtonRoot.GetComponent<GridLayoutGroup>();
        if (gridLayout != null)
        {
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            gridLayout.constraintCount = 1;
            return;
        }

        HorizontalLayoutGroup horizontalLayout = roomButtonRoot.GetComponent<HorizontalLayoutGroup>();
        if (horizontalLayout == null)
        {
            horizontalLayout = roomButtonRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
        }

        horizontalLayout.childAlignment = TextAnchor.MiddleLeft;
        horizontalLayout.spacing = 12f;
        horizontalLayout.childControlWidth = false;
        horizontalLayout.childControlHeight = true;
        horizontalLayout.childForceExpandWidth = false;
        horizontalLayout.childForceExpandHeight = false;
    }

    private void EnsureCanvasGroupReady()
    {
        if (magicBuildingPanel == null)
        {
            return;
        }

        if (magicBuildingCanvasGroup == null)
        {
            magicBuildingCanvasGroup = magicBuildingPanel.GetComponent<CanvasGroup>();
        }

        if (magicBuildingCanvasGroup == null)
        {
            magicBuildingCanvasGroup = magicBuildingPanel.AddComponent<CanvasGroup>();
        }
    }

    private void SetCanvasVisible(bool visible)
    {
        if (magicBuildingPanel == null)
        {
            return;
        }

        EnsureCanvasGroupReady();

        if (magicBuildingCanvasGroup == null)
        {
            return;
        }

        magicBuildingCanvasGroup.alpha = visible ? 1f : 0f;
        magicBuildingCanvasGroup.interactable = visible;
        magicBuildingCanvasGroup.blocksRaycasts = visible;
    }

    private void ShowRoomHoverDescription(RoomDefinition room)
    {
        if (roomDescriptionText == null)
        {
            return;
        }

        if (room == null)
        {
            roomDescriptionText.text = string.Empty;
            return;
        }

        roomDescriptionText.text = room.description;
    }

    private List<RoomDefinition> GetCurrentModuleRooms()
    {
        if (roomCatalog == null)
        {
            return new List<RoomDefinition>();
        }

        return roomCatalog.GetRooms(currentModule);
    }

    private void EnsureCatalogReady()
    {
        if (roomCatalog != null)
        {
            roomCatalog.ValidateRooms();
        }
    }

    private void CancelCurrentPlacement()
    {
        isHoldingPlaceButton = false;
        SetCanvasVisible(true);
        hoveredRoom = null;
        ShowRoomHoverDescription(null);
        selectedRoom = null;
        DestroyPreview();
    }

    private void RebuildPreviewObject()
    {
        DestroyPreview();

        if (selectedRoom == null || selectedRoom.blockPrefab == null)
        {
            return;
        }

        previewInstance = Instantiate(selectedRoom.blockPrefab);
        previewInstance.name = selectedRoom.blockPrefab.name + "_Preview";

        Collider[] colliders = previewInstance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        previewRenderers.Clear();
        previewInstance.GetComponentsInChildren(true, previewRenderers);
        ApplyPreviewColor(previewInvalidColor);
        previewInstance.SetActive(false);
    }

    private void DestroyPreview()
    {
        if (previewInstance != null)
        {
            Destroy(previewInstance);
            previewInstance = null;
        }

        previewRenderers.Clear();
        previewIsValid = false;
    }

    private bool UpdatePreviewFromMouse()
    {
        if (selectedRoom == null || selectedRoom.blockPrefab == null || gridManager == null)
        {
            return false;
        }

        if (previewInstance == null)
        {
            RebuildPreviewObject();
        }

        if (previewInstance == null)
        {
            return false;
        }

        if (!TryGetGridFromMouse(out Vector2Int originCell))
        {
            previewInstance.SetActive(false);
            previewIsValid = false;
            previewInvalidReason = "鼠标未命中网格平面";
            return false;
        }

        previewOriginCell = originCell;
        previewInstance.SetActive(true);
        previewInstance.transform.position = GetRoomPlacementWorldPosition(selectedRoom, previewOriginCell);
        previewInstance.transform.rotation = Quaternion.identity;

        previewIsValid = CanPlaceRoomAt(selectedRoom, previewOriginCell, out previewInvalidReason);
        ApplyPreviewColor(previewIsValid ? previewValidColor : previewInvalidColor);
        return true;
    }

    private bool TryPlaceSelectedRoomAtPreview()
    {
        if (selectedRoom == null || selectedRoom.blockPrefab == null || gridManager == null)
        {
            return false;
        }

        if (!previewIsValid)
        {
            return false;
        }

        GetFootprintSize(selectedRoom, out int width, out int height);
        if (width <= 0 || height <= 0)
        {
            previewInvalidReason = "房间占格为空";
            return false;
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2Int absolute = new Vector2Int(previewOriginCell.x + x, previewOriginCell.y + y);
                if (!gridManager.TryGetCell(absolute, out GridCell checkCell))
                {
                    previewInvalidReason = "禁用格子(0): " + absolute;
                    return false;
                }

                if (!checkCell.isUnlocked)
                {
                    previewInvalidReason = "格子未解锁: " + absolute;
                    return false;
                }

                if (checkCell.isOccupied)
                {
                    previewInvalidReason = "格子已占用: " + absolute;
                    return false;
                }
            }
        }

        PlaceableRoom placedRoom = new PlaceableRoom
        {
            roomName = selectedRoom.roomName,
            category = ToRoomCategory(currentModule),
            description = selectedRoom.description,
            occupiedCells = new List<Vector2Int>(),
            roomPrefab = selectedRoom.blockPrefab
        };

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2Int absolute = new Vector2Int(previewOriginCell.x + x, previewOriginCell.y + y);
                placedRoom.occupiedCells.Add(absolute);
                gridManager.SetCellOccupied(absolute, true, placedRoom);
            }
        }

        Vector3 placeWorldPosition = GetRoomPlacementWorldPosition(selectedRoom, previewOriginCell);
        GameObject placedObject = Instantiate(selectedRoom.blockPrefab, placeWorldPosition, Quaternion.identity);
        placedRoomObjects[placedRoom] = placedObject;
        return true;
    }

    private Vector3 GetRoomPlacementWorldPosition(RoomDefinition room, Vector2Int originCell)
    {
        Vector3 originWorld = gridManager.GridToWorldPosition(originCell);
        if (!alignRoomPrefabToFootprintCenter || room == null || gridManager == null)
        {
            return originWorld + new Vector3(0f, 0f, roomZOffset);
        }

        GetFootprintSize(room, out int width, out int height);
        if (width <= 0 || height <= 0)
        {
            return originWorld + new Vector3(0f, 0f, roomZOffset);
        }

        float centerOffsetX = (width - 1) * 0.5f;
        float centerOffsetY = (height - 1) * 0.5f;

        Vector3 stepX = GetGridAxisStepWorld(originCell, Vector2Int.right, new Vector3(gridManager.gridSize, 0f, 0f));
        Vector3 stepY = GetGridAxisStepWorld(originCell, Vector2Int.up, new Vector3(0f, gridManager.gridSize, 0f));

        return originWorld + stepX * centerOffsetX + stepY * centerOffsetY + new Vector3(0f, 0f, roomZOffset);
    }

    private Vector3 GetGridAxisStepWorld(Vector2Int originCell, Vector2Int axis, Vector3 fallbackStep)
    {
        Vector2Int forward = originCell + axis;
        if (gridManager.HasCell(forward))
        {
            return gridManager.GridToWorldPosition(forward) - gridManager.GridToWorldPosition(originCell);
        }

        Vector2Int backward = originCell - axis;
        if (gridManager.HasCell(backward))
        {
            return gridManager.GridToWorldPosition(originCell) - gridManager.GridToWorldPosition(backward);
        }

        return fallbackStep;
    }

    private bool CanPlaceRoomAt(RoomDefinition room, Vector2Int originCell, out string reason)
    {
        if (room == null || gridManager == null)
        {
            reason = "房间或网格管理器为空";
            return false;
        }

        GetFootprintSize(room, out int width, out int height);
        if (width <= 0 || height <= 0)
        {
            reason = "房间占格为空";
            return false;
        }

        int roomMinX = originCell.x;
        int roomMinY = originCell.y;
        int roomMaxX = originCell.x + width - 1;
        int roomMaxY = originCell.y + height - 1;

        if (!gridManager.TryGetGridBounds(out Vector2Int boundsMin, out Vector2Int boundsMax))
        {
            reason = "网格未初始化(无可用边界)";
            return false;
        }

        if (roomMinX < boundsMin.x || roomMaxX > boundsMax.x || roomMinY < boundsMin.y || roomMaxY > boundsMax.y)
        {
            reason = "越界: start=(" + roomMinX + "," + roomMinY + "), end=(" + roomMaxX + "," + roomMaxY + "), bounds=(" + boundsMin.x + "," + boundsMin.y + ")-(" + boundsMax.x + "," + boundsMax.y + ")";
            return false;
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2Int pos = new Vector2Int(originCell.x + x, originCell.y + y);

                if (!gridManager.TryGetCell(pos, out GridCell cell))
                {
                    if (!gridManager.HasAnyRegisteredCells())
                    {
                        reason = "网格未初始化(请先生成并注册格子): " + pos;
                    }
                    else
                    {
                        reason = "禁用格子(0): " + pos;
                    }
                    return false;
                }

                if (!cell.isUnlocked)
                {
                    reason = "格子未解锁: " + pos;
                    return false;
                }

                if (cell.isOccupied)
                {
                    reason = "格子已占用: " + pos;
                    return false;
                }
            }
        }

        reason = string.Empty;
        return true;
    }

    private static void GetFootprintSize(RoomDefinition room, out int width, out int height)
    {
        if (room == null)
        {
            width = 0;
            height = 0;
            return;
        }

        // 明确使用 sizeX * sizeY 作为 XY 平面的占格范围。
        width = Mathf.Max(1, room.sizeX);
        height = Mathf.Max(1, room.sizeY);
    }

    private bool TryGetGridFromMouse(out Vector2Int gridPos)
    {
        gridPos = Vector2Int.zero;

        if (gridManager == null)
        {
            return false;
        }

        Camera cam = placementCamera != null ? placementCamera : Camera.main;
        if (cam == null)
        {
            return false;
        }

        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, gridManager.placementZ));
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!plane.Raycast(ray, out float distance))
        {
            return false;
        }

        Vector3 world = ray.GetPoint(distance);
        gridPos = gridManager.WorldToGridPosition(world);
        return true;
    }

    private void ApplyPreviewColor(Color color)
    {
        for (int i = 0; i < previewRenderers.Count; i++)
        {
            Renderer renderer = previewRenderers[i];
            if (renderer == null || renderer.sharedMaterial == null)
            {
                continue;
            }

            if (!renderer.sharedMaterial.HasProperty("_Color"))
            {
                continue;
            }

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }
    }

    private RoomCategory ToRoomCategory(BuildingModuleType module)
    {
        switch (module)
        {
            case BuildingModuleType.Production:
                return RoomCategory.Production;
            case BuildingModuleType.Convert:
                return RoomCategory.Conversion;
            case BuildingModuleType.Growth:
                return RoomCategory.Growth;
            default:
                return RoomCategory.Other;
        }
    }

    private void RemovePlacedRoom(PlaceableRoom room)
    {
        if (room == null)
        {
            return;
        }

        if (room.occupiedCells != null)
        {
            for (int i = 0; i < room.occupiedCells.Count; i++)
            {
                gridManager.SetCellOccupied(room.occupiedCells[i], false, null);
            }
        }

        if (placedRoomObjects.TryGetValue(room, out GameObject placedObject))
        {
            if (placedObject != null)
            {
                Destroy(placedObject);
            }

            placedRoomObjects.Remove(room);
        }
    }
}
