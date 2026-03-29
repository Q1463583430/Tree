using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class BluePrint : MonoBehaviour
{
    public bool isMagic = true;

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

    [Header("房间列表按钮范围")]
    [Min(1f)] public float roomButtonMinWidth = 180f;
    [Min(1f)] public float roomButtonMinHeight = 96f;
    [Min(0f)] public float roomButtonExtraHitY = 18f;
    [Min(1f)] public float pageNavButtonMinHeight = 50f;
    [Min(0f)] public float pageNavButtonExtraHitY = 14f;

    [Header("房间数据接入")]
    public Roommanager roomDataManager;
    public bool autoApplyProductionData = true;

    [Header("网格放置参数")]
    public GridManager gridManager;
    public Camera placementCamera;
    public bool alignRoomPrefabToFootprintCenter = true;
    public float roomZOffset = -0.01f;
    public Color previewValidColor = new Color(0.3f, 1f, 0.4f, 0.65f);
    public Color previewInvalidColor = new Color(1f, 0.3f, 0.3f, 0.65f);

    [Header("房间运行UI")]
    public RoomProgressBarUI roomProgressUiPrefab;
    public Vector3 roomProgressUiLocalOffset = new Vector3(0f, 1.6f, 0f);

    private const int RoomsPerPage = 4;
    private const float ForcedRoomZ = -0.01f;
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

        if (roomDataManager == null)
        {
            roomDataManager = FindObjectOfType<Roommanager>();
        }

        // Force all preview/placed room objects to a fixed z depth.
        roomZOffset = ForcedRoomZ;

        EnsureCanvasGroupReady();

        EnsureRoomButtonRootHorizontalLayout();

        if (magicBuildingButton != null)
        {
            // 若 Inspector 已绑定 onClick，则不再重复添加运行时监听，避免一次点击触发两个切换函数。
            if (magicBuildingButton.onClick.GetPersistentEventCount() == 0)
            {
                magicBuildingButton.onClick.AddListener(ToggleMagicBuildingPanel);
            }
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

        ConfigurePageNavButtonHitArea(prevPageButton);
        ConfigurePageNavButtonHitArea(nextPageButton);

        if (magicBuildingPanel != null)
        {
            magicBuildingPanel.SetActive(!hidePanelOnStart);
            isMagic = magicBuildingPanel.activeSelf;
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
            ConfigureRoomButtonHitArea(item);
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

        if (IsPointerOverUi())
        {
            if (previewInstance != null)
            {
                previewInstance.SetActive(false);
            }

            // 鼠标在 UI 上抬起时，忽略本次放置输入，避免点按钮时直接建到地图。
            if (Input.GetMouseButtonUp(0))
            {
                previewIsValid = false;
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

                // 若失败原因为资源不足，则直接销毁预览体并恢复 UI
                if (!string.IsNullOrEmpty(previewInvalidReason) && previewInvalidReason.Contains("资源不足"))
                {
                    DestroyPreview();
                    isHoldingPlaceButton = false;
                    SetCanvasVisible(true);
                }
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

    private static bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
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

    private void ConfigureRoomButtonHitArea(RoomButtonItem item)
    {
        if (item == null)
        {
            return;
        }

        RectTransform itemRect = item.GetComponent<RectTransform>();
        if (itemRect != null)
        {
            itemRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(roomButtonMinWidth, itemRect.rect.width));
            itemRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(roomButtonMinHeight, itemRect.rect.height));
        }

        LayoutElement layout = item.GetComponent<LayoutElement>();
        if (layout == null)
        {
            layout = item.gameObject.AddComponent<LayoutElement>();
        }

        layout.minWidth = roomButtonMinWidth;
        layout.preferredWidth = roomButtonMinWidth;
        layout.minHeight = roomButtonMinHeight;
        layout.preferredHeight = roomButtonMinHeight;

        Button button = item.button != null ? item.button : item.GetComponent<Button>();
        if (button == null)
        {
            return;
        }

        RectTransform buttonRect = button.GetComponent<RectTransform>();
        if (buttonRect != null)
        {
            buttonRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(roomButtonMinHeight, buttonRect.rect.height));
        }

        Graphic graphic = button.targetGraphic;
        if (graphic != null)
        {
            float y = Mathf.Max(0f, roomButtonExtraHitY);
            graphic.raycastPadding = new Vector4(0f, y, 0f, y);
        }
    }

    private void ConfigurePageNavButtonHitArea(Button button)
    {
        if (button == null)
        {
            return;
        }

        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(pageNavButtonMinHeight, rect.rect.height));
        }

        Graphic graphic = button.targetGraphic;
        if (graphic != null)
        {
            float y = Mathf.Max(0f, pageNavButtonExtraHitY);
            graphic.raycastPadding = new Vector4(0f, y, 0f, y);
        }
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
        previewInstance.SetActive(false);

        DisableRuntimeComponentsForPreview(previewInstance);

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

    private static void DisableRuntimeComponentsForPreview(GameObject previewRoot)
    {
        if (previewRoot == null)
        {
            return;
        }

        MonoBehaviour[] behaviours = previewRoot.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour != null)
            {
                behaviour.enabled = false;
            }
        }
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

        List<Vector2Int> offsets = GetOccupiedOffsets(selectedRoom);
        if (offsets.Count == 0)
        {
            previewInvalidReason = "房间占格为空";
            return false;
        }

        if (!CheckPlacementCells(previewOriginCell, offsets, out previewInvalidReason))
        {
            return false;
        }

        PlaceableRoom placedRoom = new PlaceableRoom
        {
            roomName = selectedRoom.roomName,
            category = ToRoomCategory(currentModule),
            description = selectedRoom.description,
            occupiedCells = new List<Vector2Int>(),
            roomPrefab = selectedRoom.blockPrefab
        };

        for (int i = 0; i < offsets.Count; i++)
        {
            Vector2Int absolute = previewOriginCell + offsets[i];
            placedRoom.occupiedCells.Add(absolute);
            gridManager.SetCellOccupied(absolute, true, placedRoom);
        }

        Vector3 placeWorldPosition = GetRoomPlacementWorldPosition(selectedRoom, previewOriginCell);
        GameObject placedObject = Instantiate(selectedRoom.blockPrefab, placeWorldPosition, Quaternion.identity);

        RoomProductionUnit productionUnit = placedObject.GetComponentInChildren<RoomProductionUnit>(true);
        bool hasInspectorPlan = HasConfiguredProductionPlan(productionUnit != null ? productionUnit.plan : null);
        bool planBound = BindRoomDataToPlacedObject(selectedRoom, placedObject);

        if (productionUnit != null)
        {
            EnsureRoomEmployeeClickHandler(placedObject, productionUnit);

            if (autoApplyProductionData && !planBound && !hasInspectorPlan)
            {
                for (int i = 0; i < placedRoom.occupiedCells.Count; i++)
                {
                    gridManager.SetCellOccupied(placedRoom.occupiedCells[i], false, null);
                }

                Destroy(placedObject);
                previewInvalidReason = "房间未配置可用生产计划(数据表与Prefab计划均缺失)";
                return false;
            }

            if (autoApplyProductionData && !planBound && hasInspectorPlan)
            {
                Debug.LogWarning($"{name} 房间[{selectedRoom.roomName}]未命中 Roommanager 配置，已回退使用 Prefab Inspector 中的生产计划。", this);
            }

            NormalizeHrProductionPlanForManualRecruit(placedObject, productionUnit);

            bool started = productionUnit.TryCompleteConstructionAndStart();
            if (!started)
            {
                for (int i = 0; i < placedRoom.occupiedCells.Count; i++)
                {
                    gridManager.SetCellOccupied(placedRoom.occupiedCells[i], false, null);
                }

                Destroy(placedObject);
                previewInvalidReason = GetPlacementFailureReason(productionUnit);
                return false;
            }

            AttachRoomProgressUi(placedObject, productionUnit);

            ActivatePlacedHrGeneration(placedObject);
        }
        else
        {
            if (!TrySpendConstructionCostWithoutProductionUnit(selectedRoom, out previewInvalidReason))
            {
                for (int i = 0; i < placedRoom.occupiedCells.Count; i++)
                {
                    gridManager.SetCellOccupied(placedRoom.occupiedCells[i], false, null);
                }

                Destroy(placedObject);
                return false;
            }
        }

        placedRoomObjects[placedRoom] = placedObject;
        return true;
    }

    private void AttachRoomProgressUi(GameObject placedObject, RoomProductionUnit productionUnit)
    {
        if (placedObject == null || productionUnit == null || roomProgressUiPrefab == null)
        {
            return;
        }

        RoomProgressBarUI existingUi = placedObject.GetComponentInChildren<RoomProgressBarUI>(true);
        if (existingUi != null)
        {
            existingUi.roomUnit = productionUnit;
            return;
        }

        RoomProgressBarUI ui = Instantiate(roomProgressUiPrefab, placedObject.transform);
        ui.transform.localPosition = roomProgressUiLocalOffset;
        ui.transform.localRotation = Quaternion.identity;
        ui.roomUnit = productionUnit;
    }

    private Vector3 GetRoomPlacementWorldPosition(RoomDefinition room, Vector2Int originCell)
    {
        Vector3 originWorld = gridManager.GridToWorldPosition(originCell);
        if (!alignRoomPrefabToFootprintCenter || room == null || gridManager == null)
        {
            return WithForcedRoomZ(originWorld + new Vector3(0f, 0f, roomZOffset));
        }

        List<Vector2Int> offsets = GetOccupiedOffsets(room);
        if (offsets.Count <= 0)
        {
            return WithForcedRoomZ(originWorld + new Vector3(0f, 0f, roomZOffset));
        }

        int minX = offsets[0].x;
        int maxX = offsets[0].x;
        int minY = offsets[0].y;
        int maxY = offsets[0].y;

        for (int i = 1; i < offsets.Count; i++)
        {
            Vector2Int offset = offsets[i];
            minX = Mathf.Min(minX, offset.x);
            maxX = Mathf.Max(maxX, offset.x);
            minY = Mathf.Min(minY, offset.y);
            maxY = Mathf.Max(maxY, offset.y);
        }

        float centerOffsetX = (minX + maxX) * 0.5f;
        float centerOffsetY = (minY + maxY) * 0.5f;

        Vector3 stepX = GetGridAxisStepWorld(originCell, Vector2Int.right, new Vector3(gridManager.gridSize, 0f, 0f));
        Vector3 stepY = GetGridAxisStepWorld(originCell, Vector2Int.up, new Vector3(0f, gridManager.gridSize, 0f));

        return WithForcedRoomZ(originWorld + stepX * centerOffsetX + stepY * centerOffsetY + new Vector3(0f, 0f, roomZOffset));
    }

    private static Vector3 WithForcedRoomZ(Vector3 position)
    {
        position.z = ForcedRoomZ;
        return position;
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

        List<Vector2Int> offsets = GetOccupiedOffsets(room);
        if (offsets.Count == 0)
        {
            reason = "房间占格为空";
            return false;
        }

        return CheckPlacementCells(originCell, offsets, out reason);
    }

    private bool CheckPlacementCells(Vector2Int originCell, List<Vector2Int> offsets, out string reason)
    {
        if (gridManager == null)
        {
            reason = "网格管理器为空";
            return false;
        }

        if (offsets == null || offsets.Count == 0)
        {
            reason = "房间占格为空";
            return false;
        }

        int roomMinX = int.MaxValue;
        int roomMinY = int.MaxValue;
        int roomMaxX = int.MinValue;
        int roomMaxY = int.MinValue;

        for (int i = 0; i < offsets.Count; i++)
        {
            Vector2Int p = originCell + offsets[i];
            if (p.x < roomMinX) roomMinX = p.x;
            if (p.x > roomMaxX) roomMaxX = p.x;
            if (p.y < roomMinY) roomMinY = p.y;
            if (p.y > roomMaxY) roomMaxY = p.y;
        }

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

        for (int i = 0; i < offsets.Count; i++)
        {
            Vector2Int pos = originCell + offsets[i];

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

        reason = string.Empty;
        return true;
    }

    private static List<Vector2Int> GetOccupiedOffsets(RoomDefinition room)
    {
        if (room == null)
        {
            return new List<Vector2Int>();
        }

        int width = Mathf.Max(1, room.sizeX);
        int height = Mathf.Max(1, room.sizeY);
        List<Vector2Int> offsets = new List<Vector2Int>(width * height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                offsets.Add(new Vector2Int(x, y));
            }
        }

        return offsets;
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

    private bool BindRoomDataToPlacedObject(RoomDefinition room, GameObject placedObject)
    {
        if (!autoApplyProductionData || room == null || placedObject == null)
        {
            return true;
        }

        if (roomDataManager == null)
        {
            roomDataManager = FindObjectOfType<Roommanager>();
            if (roomDataManager == null)
            {
                return false;
            }
        }

        RoomProductionUnit productionUnit = placedObject.GetComponentInChildren<RoomProductionUnit>(true);
        if (productionUnit == null)
        {
            return true;
        }

        string dataKey = room.GetRoomDataKey();
        if (string.IsNullOrWhiteSpace(dataKey))
        {
            Debug.LogWarning($"{name} 房间[{room.roomName}]未配置数据键，无法注入生产数据。", this);
            return false;
        }

        if (!roomDataManager.TryApplyProductionPlan(dataKey, productionUnit))
        {
            Debug.LogWarning($"{name} 找不到房间数据[{dataKey}]，请检查 Roommanager.roomProfiles。", this);
            return false;
        }

        return true;
    }

    private static void EnsureRoomEmployeeClickHandler(GameObject placedObject, RoomProductionUnit productionUnit)
    {
        if (placedObject == null || productionUnit == null)
        {
            return;
        }

        RoomEmployeeRoomClickHandler clickHandler = placedObject.GetComponent<RoomEmployeeRoomClickHandler>();
        if (clickHandler == null)
        {
            clickHandler = placedObject.AddComponent<RoomEmployeeRoomClickHandler>();
        }

        clickHandler.roomUnit = productionUnit;
    }

    private static void ActivatePlacedHrGeneration(GameObject placedObject)
    {
        if (placedObject == null)
        {
            return;
        }

        HR[] hrs = placedObject.GetComponentsInChildren<HR>(true);
        for (int i = 0; i < hrs.Length; i++)
        {
            HR hr = hrs[i];
            if (hr != null)
            {
                hr.NotifyRoomPlacedAndReady();
            }
        }
    }

    private static void NormalizeHrProductionPlanForManualRecruit(GameObject placedObject, RoomProductionUnit productionUnit)
    {
        if (placedObject == null || productionUnit == null)
        {
            return;
        }

        HR hr = placedObject.GetComponentInChildren<HR>(true);
        if (hr == null)
        {
            return;
        }

        // HR 当前版本走手动招募：房间放置后需先分配松鼠才能启动。
        productionUnit.plan.requiredSquirrels = Mathf.Max(0, productionUnit.plan.requiredSquirrels);
        productionUnit.plan.workType = RoomEmployeeWorkType.HR;

        if (productionUnit.plan.cycleCosts == null)
        {
            productionUnit.plan.cycleCosts = new List<ResourceAmount>();
        }
        else
        {
            productionUnit.plan.cycleCosts.Clear();
        }

        if (productionUnit.plan.cycleOutputs == null)
        {
            productionUnit.plan.cycleOutputs = new List<ResourceAmount>();
        }
        else
        {
            productionUnit.plan.cycleOutputs.Clear();
        }
    }

    private string GetPlacementFailureReason(RoomProductionUnit productionUnit)
    {
        if (productionUnit == null)
        {
            return "房间启动失败，请检查房间配置。";
        }

        if (!productionUnit.CanAffordConstruction())
        {
            return "资源不足，无法完成初始建设";
        }

        int required = Mathf.Max(0, productionUnit.plan.requiredSquirrels);
        if (required > 0)
        {
            WorkforceManager workforce = productionUnit.workforceManager;
            if (workforce == null)
            {
                workforce = WorkforceManager.Instance;
            }

            if (workforce == null)
            {
                workforce = FindObjectOfType<WorkforceManager>();
            }

            int available = workforce != null ? workforce.GetAvailableSquirrels() : 0;
            return $"员工不足，无法启动房间（需要 {required}，可用 {available}）";
        }

        return "房间启动失败，请检查房间配置与依赖引用。";
    }

    private bool TrySpendConstructionCostWithoutProductionUnit(RoomDefinition room, out string reason)
    {
        reason = string.Empty;

        if (room == null)
        {
            reason = "房间数据为空";
            return false;
        }

        if (roomDataManager == null)
        {
            roomDataManager = FindObjectOfType<Roommanager>();
        }

        if (roomDataManager == null)
        {
            // 没有生产单元且没有数据表时，允许仅放置，不做资源变动。
            return true;
        }

        string dataKey = room.GetRoomDataKey();
        if (string.IsNullOrWhiteSpace(dataKey))
        {
            return true;
        }

        if (!roomDataManager.TryGetProfile(dataKey, out RoomDataProfile profile) || profile == null)
        {
            return true;
        }

        ResourceManager resourceManager = ResourceManager.Instance;
        if (resourceManager == null)
        {
            resourceManager = FindObjectOfType<ResourceManager>();
        }

        if (resourceManager == null)
        {
            reason = "未找到资源管理器";
            return false;
        }

        if (profile.constructionCosts == null || profile.constructionCosts.Count == 0)
        {
            return true;
        }

        if (!resourceManager.CanAfford(profile.constructionCosts))
        {
            reason = "资源不足，无法完成初始建设";
            return false;
        }

        if (!resourceManager.TrySpend(profile.constructionCosts))
        {
            reason = "扣除建造资源失败";
            return false;
        }

        return true;
    }

    private static bool HasConfiguredProductionPlan(RoomProductionPlan plan)
    {
        if (plan == null)
        {
            return false;
        }

        if (plan.requiredSquirrels > 0)
        {
            return true;
        }

        if (plan.constructionCosts != null && plan.constructionCosts.Count > 0)
        {
            return true;
        }

        if (plan.cycleCosts != null && plan.cycleCosts.Count > 0)
        {
            return true;
        }

        if (plan.cycleOutputs != null && plan.cycleOutputs.Count > 0)
        {
            return true;
        }

        return false;
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

    public void MagicButton()
    {
        if (magicBuildingPanel == null)
        {
            return;
        }

        bool isOpenNext = !magicBuildingPanel.activeSelf;
        magicBuildingPanel.SetActive(isOpenNext);
        isMagic = isOpenNext;
    }
}
