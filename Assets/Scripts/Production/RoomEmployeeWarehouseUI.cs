using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class EmployeePortraitBinding
{
    public string employeeId;
    public Sprite sprite;
}

[Serializable]
public class TraitPortraitBinding
{
    public HREmployeeTraitType trait;
    public Sprite sprite;
}

// UGUI 版仓库与房间配鼠界面，按四象限面板组织。
public class RoomEmployeeWarehouseUI : MonoBehaviour
{
    public static RoomEmployeeWarehouseUI Instance { get; private set; }

    [Header("依赖")]
    public RoomEmployeeAssignmentManager assignmentManager;
    public EmployeeRepository employeeRepository;

    [Header("WarehousePanel (Root)")]
    public GameObject warehousePanelRoot;
    public Button closeButton;

    [Header("TopLeft_WarehouseList")]
    public Transform slotContainer;
    public Button prevButton;
    public Button nextButton;
    public TMP_Text pageText;
    public RoomEmployeeSlotUI warehouseSlotPrefab;

    [Header("TopRight_EmployeeDetail")]
    public TMP_Text nameText;
    public TMP_Text statTexts;
    public TMP_Text traitText;
    public TMP_Text descriptionText;

    [Header("BottomLeft_WorkingEmployees")]
    public Transform slotContainerWorking;
    public RoomEmployeeSlotUI workingSlotPrefab;

    [Header("BottomRight_RoomInfo")]
    public TMP_Text roomNameText;
    public TMP_Text requirementText;
    public TMP_Text multiplierText;
    public Button clearButton;

    [Header("可选")]
    public TMP_Text messageText;
    public Sprite defaultEmployeePortrait;
    public List<EmployeePortraitBinding> portraitBindingsById = new List<EmployeePortraitBinding>();
    public List<TraitPortraitBinding> portraitBindingsByTrait = new List<TraitPortraitBinding>();

    [Header("头像预设映射（与HR系统相同的15张图）")]
    public Sprite portraitFitnessFan;         // 健美爱好者
    public Sprite portraitDarkCook;           // 厨师鼠鼠(黑暗料理者)
    public Sprite portraitDebuff;             // 各种debuff
    public Sprite portraitBigAppetite;        // 大胃袋
    public Sprite portraitSmartTalent;        // 天资聪颖
    public Sprite portraitLuckyMouse;         // 幸运鼠
    public Sprite portraitNormal;             // 普通鼠鼠
    public Sprite portraitEliteHR;            // 精英HR
    public Sprite portraitStrongBody;         // 身强体壮
    public Sprite portraitBookLover;          // 酷爱阅读者
    public Sprite portraitMagicalGirl;        // 马猴烧酒
    public Sprite portraitSevereMyopia;       // 高度近视
    public List<Sprite> reusableUntitledPortraits = new List<Sprite>(); // 未标题图，可重复使用

    [Min(1)] public int slotsPerPage = 5;
    public bool openOnStart;

    private readonly List<RoomEmployeeSlotUI> _spawnedWarehouseSlots = new List<RoomEmployeeSlotUI>();
    private readonly List<RoomEmployeeSlotUI> _spawnedWorkingSlots = new List<RoomEmployeeSlotUI>();
    private readonly List<HREmployeeData> _cachedWarehouseCandidates = new List<HREmployeeData>();
    private readonly List<HREmployeeData> _manualWorkingEmployees = new List<HREmployeeData>();

    private RoomProductionUnit _activeRoom;
    private HREmployeeData _hoveredEmployee;
    private RoomEmployeeAssignmentManager _boundAssignmentManager;
    private EmployeeRepository _boundRepository;
    private int _currentWarehousePage;
    private bool _uiBound;
    private bool _bindingWarningShown;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        TryAutoBindByHierarchyName();
        BindUiButtons();
        EnsureRepository();
        EnsureAssignmentManager();
    }

    void Start()
    {
        ReconnectDataEvents();
        SetPanelVisible(openOnStart);
        RefreshAllPanels();
    }

    void OnDestroy()
    {
        UnbindUiButtons();
        DisconnectDataEvents();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static RoomEmployeeWarehouseUI EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        RoomEmployeeWarehouseUI existing = FindObjectOfType<RoomEmployeeWarehouseUI>(true);
        if (existing != null)
        {
            return existing;
        }

        GameObject go = new GameObject("RoomEmployeeWarehouseUI_Auto");
        RoomEmployeeWarehouseUI ui = go.AddComponent<RoomEmployeeWarehouseUI>();
        Debug.LogWarning("[RoomEmployeeWarehouseUI] 场景中未找到实例，已自动创建空对象。请按约定层级绑定 UGUI 引用。", ui);
        return ui;
    }

    public void OpenWarehouse()
    {
        _activeRoom = null;
        _hoveredEmployee = null;
        _currentWarehousePage = 0;
        CleanupManualWorkingEmployees();
        SetMessage(string.Empty);
        SetPanelVisible(true);
        RefreshAllPanels();
    }

    public void OpenRoomConfig(RoomProductionUnit room)
    {
        _activeRoom = room;
        _hoveredEmployee = null;
        _currentWarehousePage = 0;
        _manualWorkingEmployees.Clear();
        SetMessage("配置完成后，将在下一次30秒结算生效。");
        SetPanelVisible(true);
        RefreshAllPanels();
    }

    public void Close()
    {
        _hoveredEmployee = null;
        SetPanelVisible(false);
        RefreshEmployeeDetailPanel();
    }

    public void OnClickClosePanel()
    {
        Debug.Log("点击了关闭按钮");
        Close();
    }

    public void NotifyWarehouseDataChanged()
    {
        CleanupManualWorkingEmployees();
        RefreshAllPanels();
    }

    public void OnClickPrevPage()
    {
        _currentWarehousePage = Mathf.Max(0, _currentWarehousePage - 1);
        RefreshWarehousePanel();
    }

    public void OnClickNextPage()
    {
        int totalPages = GetWarehouseTotalPages();
        if (totalPages <= 0)
        {
            _currentWarehousePage = 0;
        }
        else
        {
            _currentWarehousePage = Mathf.Min(totalPages - 1, _currentWarehousePage + 1);
        }

        RefreshWarehousePanel();
    }

    public void OnClickClearRoom()
    {
        RoomEmployeeAssignmentManager manager = EnsureAssignmentManager();
        if (manager == null || _activeRoom == null)
        {
            return;
        }

        manager.ClearRoom(_activeRoom);
        SetMessage("已清空当前房间配置。下一次30秒结算开始生效。");
        RefreshAllPanels();
    }

    private RoomEmployeeAssignmentManager EnsureAssignmentManager()
    {
        if (assignmentManager != null)
        {
            EmployeeRepository repo = EnsureRepository();
            if (repo != null && assignmentManager.employeeRepository != repo)
            {
                assignmentManager.employeeRepository = repo;
            }

            return assignmentManager;
        }

        assignmentManager = RoomEmployeeAssignmentManager.Instance;
        if (assignmentManager == null)
        {
            assignmentManager = FindObjectOfType<RoomEmployeeAssignmentManager>();
        }

        if (assignmentManager == null)
        {
            assignmentManager = RoomEmployeeAssignmentManager.EnsureInstance();
        }

        EmployeeRepository repository = EnsureRepository();
        if (assignmentManager != null && repository != null && assignmentManager.employeeRepository != repository)
        {
            assignmentManager.employeeRepository = repository;
        }

        return assignmentManager;
    }

    private EmployeeRepository EnsureRepository()
    {
        if (employeeRepository != null)
        {
            return employeeRepository;
        }

        employeeRepository = EmployeeRepository.Instance;
        if (employeeRepository == null)
        {
            employeeRepository = FindObjectOfType<EmployeeRepository>();
        }

        if (employeeRepository == null)
        {
            employeeRepository = EmployeeRepository.GetOrCreateInstance();
        }

        return employeeRepository;
    }

    private void ReconnectDataEvents()
    {
        RoomEmployeeAssignmentManager manager = EnsureAssignmentManager();
        if (_boundAssignmentManager != manager)
        {
            if (_boundAssignmentManager != null)
            {
                _boundAssignmentManager.OnRoomAssignmentsChanged -= OnRoomAssignmentsChanged;
            }

            _boundAssignmentManager = manager;
            if (_boundAssignmentManager != null)
            {
                _boundAssignmentManager.OnRoomAssignmentsChanged += OnRoomAssignmentsChanged;
            }
        }

        EmployeeRepository repository = EnsureRepository();
        if (repository == null && manager != null)
        {
            repository = manager.employeeRepository;
        }

        if (_boundRepository != repository)
        {
            if (_boundRepository != null)
            {
                _boundRepository.OnEmployeeAdded -= OnRepositoryEmployeeChanged;
                _boundRepository.OnEmployeeRemoved -= OnRepositoryEmployeeChanged;
            }

            _boundRepository = repository;
            if (_boundRepository != null)
            {
                _boundRepository.OnEmployeeAdded += OnRepositoryEmployeeChanged;
                _boundRepository.OnEmployeeRemoved += OnRepositoryEmployeeChanged;
            }
        }
    }

    private void DisconnectDataEvents()
    {
        if (_boundAssignmentManager != null)
        {
            _boundAssignmentManager.OnRoomAssignmentsChanged -= OnRoomAssignmentsChanged;
            _boundAssignmentManager = null;
        }

        if (_boundRepository != null)
        {
            _boundRepository.OnEmployeeAdded -= OnRepositoryEmployeeChanged;
            _boundRepository.OnEmployeeRemoved -= OnRepositoryEmployeeChanged;
            _boundRepository = null;
        }
    }

    private void BindUiButtons()
    {
        if (prevButton != null)
        {
            prevButton.onClick.RemoveListener(OnClickPrevPage);
            prevButton.onClick.AddListener(OnClickPrevPage);
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(OnClickNextPage);
            nextButton.onClick.AddListener(OnClickNextPage);
        }

        if (clearButton != null)
        {
            clearButton.onClick.RemoveListener(OnClickClearRoom);
            clearButton.onClick.AddListener(OnClickClearRoom);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }

        _uiBound = true;
    }

    private void UnbindUiButtons()
    {
        if (!_uiBound)
        {
            return;
        }

        if (prevButton != null)
        {
            prevButton.onClick.RemoveListener(OnClickPrevPage);
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(OnClickNextPage);
        }

        if (clearButton != null)
        {
            clearButton.onClick.RemoveListener(OnClickClearRoom);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }

        _uiBound = false;
    }

    private void OnRoomAssignmentsChanged(RoomProductionUnit _)
    {
        RefreshAllPanels();
    }

    private void OnRepositoryEmployeeChanged(HREmployeeData employee)
    {
        if (_hoveredEmployee != null && employee != null && string.Equals(_hoveredEmployee.id, employee.id, StringComparison.Ordinal))
        {
            _hoveredEmployee = null;
        }

        CleanupManualWorkingEmployees();

        RefreshAllPanels();
    }

    private void RefreshAllPanels()
    {
        ReconnectDataEvents();
        RefreshWarehousePanel();
        RefreshWorkingPanel();
        RefreshRoomInfoPanel();
        RefreshEmployeeDetailPanel();
    }

    private void RefreshWarehousePanel()
    {
        if (!EnsureRequiredBindings())
        {
            return;
        }

        ClearSpawnedSlots(_spawnedWarehouseSlots);

        BuildWarehouseCandidates(_cachedWarehouseCandidates);

        int totalPages = GetWarehouseTotalPages();
        if (totalPages <= 0)
        {
            _currentWarehousePage = 0;
        }
        else
        {
            _currentWarehousePage = Mathf.Clamp(_currentWarehousePage, 0, totalPages - 1);
        }

        if (pageText != null)
        {
            pageText.text = totalPages <= 0 ? "0/0" : (_currentWarehousePage + 1) + "/" + totalPages;
        }

        if (prevButton != null)
        {
            prevButton.interactable = totalPages > 1 && _currentWarehousePage > 0;
        }

        if (nextButton != null)
        {
            nextButton.interactable = totalPages > 1 && _currentWarehousePage < totalPages - 1;
        }

        if (_cachedWarehouseCandidates.Count == 0)
        {
            return;
        }

        RoomEmployeeAssignmentManager manager = EnsureAssignmentManager();
        if (manager == null)
        {
            return;
        }

        int start = _currentWarehousePage * Mathf.Max(1, slotsPerPage);
        int end = Mathf.Min(start + Mathf.Max(1, slotsPerPage), _cachedWarehouseCandidates.Count);

        for (int i = start; i < end; i++)
        {
            HREmployeeData employee = _cachedWarehouseCandidates[i];
            if (employee == null)
            {
                continue;
            }

            RoomProductionUnit owner = manager.GetEmployeeOwner(employee.id);
            bool inCurrentRoom = _activeRoom != null && owner == _activeRoom;
            bool hasSelectedRoom = _activeRoom != null;

            string subText;
            string actionText;
            bool interactable;

            if (!hasSelectedRoom)
            {
                string ownerName = owner == null ? "空闲" : owner.name;
                subText = "状态: " + ownerName;
                actionText = "添加";
                interactable = true;
            }
            else if (inCurrentRoom)
            {
                subText = "状态: 当前房间工作中";
                actionText = "已在房间";
                interactable = false;
            }
            else
            {
                bool canAssign = manager.CanAssignToRoom(_activeRoom, employee, out string reason);
                if (canAssign)
                {
                    subText = "状态: 可配置";
                    actionText = "配置";
                    interactable = true;
                }
                else
                {
                    subText = "状态: 不可配置(" + reason + ")";
                    actionText = "不可配置";
                    interactable = false;
                }
            }

            RoomEmployeeSlotUI slot = Instantiate(warehouseSlotPrefab, slotContainer);
            slot.Setup(
                ResolveEmployeePortrait(employee),
                GetEmployeeDisplayName(employee),
                subText,
                actionText,
                interactable,
                () => OnClickWarehouseEmployee(employee),
                () => OnHoverEnterEmployee(employee),
                () => OnHoverExitEmployee(employee)
            );

            _spawnedWarehouseSlots.Add(slot);
        }
    }

    private void RefreshWorkingPanel()
    {
        if (!EnsureRequiredBindings())
        {
            return;
        }

        ClearSpawnedSlots(_spawnedWorkingSlots);

        if (_activeRoom == null)
        {
            CleanupManualWorkingEmployees();
            for (int i = 0; i < _manualWorkingEmployees.Count; i++)
            {
                HREmployeeData employee = _manualWorkingEmployees[i];
                if (employee == null)
                {
                    continue;
                }

                RoomEmployeeSlotUI slot = Instantiate(workingSlotPrefab, slotContainerWorking);
                slot.Setup(
                    ResolveEmployeePortrait(employee),
                    GetEmployeeDisplayName(employee),
                    "状态: 工作区暂存",
                    "移除",
                    true,
                    () => OnClickRemoveWorkingEmployee(employee),
                    () => OnHoverEnterEmployee(employee),
                    () => OnHoverExitEmployee(employee)
                );

                _spawnedWorkingSlots.Add(slot);
            }

            return;
        }

        RoomEmployeeAssignmentManager manager = EnsureAssignmentManager();
        if (manager == null)
        {
            return;
        }

        List<HREmployeeData> assigned = manager.GetAssignedEmployees(_activeRoom);
        for (int i = 0; i < assigned.Count; i++)
        {
            HREmployeeData employee = assigned[i];
            if (employee == null)
            {
                continue;
            }

            RoomEmployeeSlotUI slot = Instantiate(workingSlotPrefab, slotContainerWorking);
            slot.Setup(
                ResolveEmployeePortrait(employee),
                GetEmployeeDisplayName(employee),
                "状态: 工作中",
                "移除",
                true,
                () => OnClickRemoveWorkingEmployee(employee),
                () => OnHoverEnterEmployee(employee),
                () => OnHoverExitEmployee(employee)
            );

            _spawnedWorkingSlots.Add(slot);
        }
    }

    private void RefreshRoomInfoPanel()
    {
        RoomEmployeeAssignmentManager manager = EnsureAssignmentManager();

        if (roomNameText != null)
        {
            roomNameText.text = _activeRoom == null ? "当前未选择房间" : _activeRoom.name;
        }

        if (requirementText != null)
        {
            if (_activeRoom == null)
            {
                requirementText.text = "需求: -";
            }
            else
            {
                int required = Mathf.Max(0, _activeRoom.plan.requiredSquirrels);
                int assigned = manager == null ? 0 : manager.GetAssignedCount(_activeRoom);
                requirementText.text = "需求: " + required + " | 已配置: " + assigned;
            }
        }

        if (multiplierText != null)
        {
            if (_activeRoom == null)
            {
                multiplierText.text = "当前倍率: - | 下周期倍率: -";
            }
            else
            {
                float current = _activeRoom.GetActiveOutputMultiplier();
                float next = _activeRoom.GetPendingOutputMultiplier();
                multiplierText.text = "当前倍率: " + current.ToString("0.###") + " | 下周期倍率: " + next.ToString("0.###");
            }
        }

        if (clearButton != null)
        {
            clearButton.interactable = _activeRoom != null;
        }
    }

    private void RefreshEmployeeDetailPanel()
    {
        if (_hoveredEmployee == null)
        {
            if (nameText != null)
            {
                nameText.text = "将鼠标悬停在员工上查看详情";
            }

            if (statTexts != null)
            {
                statTexts.text = "耐力: -\n智力: -\n魔力: -";
            }

            if (traitText != null)
            {
                traitText.text = "词条: -";
            }

            if (descriptionText != null)
            {
                descriptionText.text = "描述: -";
            }

            return;
        }

        if (nameText != null)
        {
            nameText.text = GetEmployeeDisplayName(_hoveredEmployee);
        }

        if (statTexts != null)
        {
            statTexts.text = "耐力: " + _hoveredEmployee.stamina + "\n智力: " + _hoveredEmployee.intelligence + "\n魔力: " + _hoveredEmployee.magic;
        }

        if (traitText != null)
        {
            traitText.text = "词条: " + BuildTraitSummary(_hoveredEmployee);
        }

        if (descriptionText != null)
        {
            descriptionText.text = "描述: " + BuildEmployeeDescription(_hoveredEmployee);
        }
    }

    private void OnClickWarehouseEmployee(HREmployeeData employee)
    {
        if (employee == null)
        {
            return;
        }

        if (_activeRoom == null)
        {
            if (!ContainsManualWorkingEmployee(employee))
            {
                _manualWorkingEmployees.Add(employee);
                SetMessage("已从仓库区移至左下工作区。");
            }
            else
            {
                SetMessage("该员工已在左下工作区。");
            }

            RefreshAllPanels();
            return;
        }

        RoomEmployeeAssignmentManager manager = EnsureAssignmentManager();
        if (manager == null)
        {
            SetMessage("未找到房间分配管理器。");
            return;
        }

        if (manager.TryAssign(_activeRoom, employee.id, out string failReason))
        {
            SetMessage("配置成功，将在下一次30秒结算生效。");
        }
        else
        {
            SetMessage(failReason);
        }

        RefreshAllPanels();
    }

    private void OnClickRemoveWorkingEmployee(HREmployeeData employee)
    {
        if (employee == null)
        {
            return;
        }

        if (_activeRoom == null)
        {
            if (RemoveManualWorkingEmployee(employee))
            {
                SetMessage("已从左下工作区移回仓库区。");
            }

            RefreshAllPanels();
            return;
        }

        RoomEmployeeAssignmentManager manager = EnsureAssignmentManager();
        if (manager == null)
        {
            return;
        }

        if (manager.TryUnassign(_activeRoom, employee.id))
        {
            SetMessage("已移除员工，将在下一次30秒结算生效。");
        }

        RefreshAllPanels();
    }

    private void OnHoverEnterEmployee(HREmployeeData employee)
    {
        _hoveredEmployee = employee;
        RefreshEmployeeDetailPanel();
    }

    private void OnHoverExitEmployee(HREmployeeData employee)
    {
        if (_hoveredEmployee != null && employee != null && string.Equals(_hoveredEmployee.id, employee.id, StringComparison.Ordinal))
        {
            _hoveredEmployee = null;
            RefreshEmployeeDetailPanel();
        }
    }

    private void BuildWarehouseCandidates(List<HREmployeeData> output)
    {
        output.Clear();

        RoomEmployeeAssignmentManager manager = EnsureAssignmentManager();
        if (manager == null)
        {
            return;
        }

        EmployeeRepository repository = EnsureRepository();
        IReadOnlyList<HREmployeeData> all = repository == null ? Array.Empty<HREmployeeData>() : repository.Employees;
        for (int i = 0; i < all.Count; i++)
        {
            HREmployeeData employee = all[i];
            if (employee == null)
            {
                continue;
            }

            if (_activeRoom == null && ContainsManualWorkingEmployee(employee))
            {
                continue;
            }

            if (_activeRoom != null)
            {
                RoomProductionUnit owner = manager.GetEmployeeOwner(employee.id);
                if (owner == _activeRoom)
                {
                    continue;
                }
            }

            output.Add(employee);
        }

        output.Sort(CompareEmployeeForUi);
    }

    private int GetWarehouseTotalPages()
    {
        int pageSize = Mathf.Max(1, slotsPerPage);
        if (_cachedWarehouseCandidates.Count == 0)
        {
            return 0;
        }

        return Mathf.CeilToInt(_cachedWarehouseCandidates.Count / (float)pageSize);
    }

    private void SetPanelVisible(bool visible)
    {
        if (warehousePanelRoot != null)
        {
            warehousePanelRoot.SetActive(visible);
        }
    }

    private void SetMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }
    }

    private static void ClearSpawnedSlots(List<RoomEmployeeSlotUI> slots)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            RoomEmployeeSlotUI slot = slots[i];
            if (slot != null)
            {
                Destroy(slot.gameObject);
            }
        }

        slots.Clear();
    }

    private bool EnsureRequiredBindings()
    {
        TryAutoBindByHierarchyName();
        BindUiButtons();

        bool ok = true;
        ok &= warehousePanelRoot != null;
        ok &= slotContainer != null;
        ok &= slotContainerWorking != null;
        ok &= warehouseSlotPrefab != null;
        ok &= workingSlotPrefab != null;

        if (!ok && !_bindingWarningShown)
        {
            _bindingWarningShown = true;
            Debug.LogWarning("[RoomEmployeeWarehouseUI] UI 引用不完整。请按 WarehousePanel -> TopLeft/TopRight/BottomLeft/BottomRight 层级绑定。", this);
        }

        return ok;
    }

    private void TryAutoBindByHierarchyName()
    {
        if (warehousePanelRoot == null)
        {
            warehousePanelRoot = gameObject;
        }

        Transform root = warehousePanelRoot != null ? warehousePanelRoot.transform : null;
        if (root == null)
        {
            return;
        }

        if (slotContainer == null)
        {
            Transform t = root.Find("TopLeft_WarehouseList/SlotContainer");
            if (t != null)
            {
                slotContainer = t;
            }
        }

        if (prevButton == null)
        {
            Transform t = root.Find("TopLeft_WarehouseList/PrevButton");
            if (t != null)
            {
                prevButton = t.GetComponent<Button>();
            }
        }

        if (nextButton == null)
        {
            Transform t = root.Find("TopLeft_WarehouseList/NextButton");
            if (t != null)
            {
                nextButton = t.GetComponent<Button>();
            }
        }

        if (pageText == null)
        {
            Transform t = root.Find("TopLeft_WarehouseList/PageText");
            if (t != null)
            {
                pageText = t.GetComponent<TMP_Text>();
            }
        }

        if (nameText == null)
        {
            Transform t = root.Find("TopRight_EmployeeDetail/NameText");
            if (t != null)
            {
                nameText = t.GetComponent<TMP_Text>();
            }
        }

        if (statTexts == null)
        {
            Transform t = root.Find("TopRight_EmployeeDetail/StatTexts");
            if (t != null)
            {
                statTexts = t.GetComponent<TMP_Text>();
            }
        }

        if (traitText == null)
        {
            Transform t = root.Find("TopRight_EmployeeDetail/TraitText");
            if (t != null)
            {
                traitText = t.GetComponent<TMP_Text>();
            }
        }

        if (descriptionText == null)
        {
            Transform t = root.Find("TopRight_EmployeeDetail/DescriptionText");
            if (t != null)
            {
                descriptionText = t.GetComponent<TMP_Text>();
            }
        }

        if (slotContainerWorking == null)
        {
            Transform t = root.Find("BottomLeft_WorkingEmployees/SlotContainer_Working");
            if (t != null)
            {
                slotContainerWorking = t;
            }
        }

        if (roomNameText == null)
        {
            Transform t = root.Find("BottomRight_RoomInfo/RoomNameText");
            if (t != null)
            {
                roomNameText = t.GetComponent<TMP_Text>();
            }
        }

        if (requirementText == null)
        {
            Transform t = root.Find("BottomRight_RoomInfo/RequirementText");
            if (t != null)
            {
                requirementText = t.GetComponent<TMP_Text>();
            }
        }

        if (multiplierText == null)
        {
            Transform t = root.Find("BottomRight_RoomInfo/MultiplierText");
            if (t != null)
            {
                multiplierText = t.GetComponent<TMP_Text>();
            }
        }

        if (clearButton == null)
        {
            Transform t = root.Find("BottomRight_RoomInfo/ClearButton");
            if (t != null)
            {
                clearButton = t.GetComponent<Button>();
            }
        }

        if (closeButton == null)
        {
            Transform t = root.Find("CloseButton");
            if (t == null)
            {
                t = root.Find("BottomRight_RoomInfo/CloseButton");
            }

            if (t != null)
            {
                closeButton = t.GetComponent<Button>();
            }
        }
    }

    private Sprite ResolveEmployeePortrait(HREmployeeData employee)
    {
        if (employee == null)
        {
            return defaultEmployeePortrait;
        }

        // 1) 优先使用ID绑定（预留，用于特定员工头像）
        if (portraitBindingsById != null)
        {
            for (int i = 0; i < portraitBindingsById.Count; i++)
            {
                EmployeePortraitBinding binding = portraitBindingsById[i];
                if (binding == null || string.IsNullOrWhiteSpace(binding.employeeId))
                {
                    continue;
                }

                if (!string.Equals(binding.employeeId.Trim(), employee.id, StringComparison.Ordinal))
                {
                    continue;
                }

                if (binding.sprite != null)
                {
                    return binding.sprite;
                }
            }
        }

        // 2) 根据首词条匹配预设头像（与HR系统相同的逻辑）
        if (employee.traits != null && employee.traits.Count > 0)
        {
            HREmployeeTraitType firstTrait = employee.traits[0];

            // 先检查显式词条绑定
            if (portraitBindingsByTrait != null)
            {
                for (int i = 0; i < portraitBindingsByTrait.Count; i++)
                {
                    TraitPortraitBinding binding = portraitBindingsByTrait[i];
                    if (binding != null && binding.trait == firstTrait && binding.sprite != null)
                    {
                        return binding.sprite;
                    }
                }
            }

            // 使用预设映射（15张头像）
            Sprite preset = ResolvePresetPortraitByTrait(firstTrait);
            if (preset != null)
            {
                return preset;
            }

            // 未覆盖词条使用"未标题"头像复用
            if (reusableUntitledPortraits != null && reusableUntitledPortraits.Count > 0)
            {
                int idx = Mathf.Abs((int)firstTrait) % reusableUntitledPortraits.Count;
                Sprite untitled = reusableUntitledPortraits[idx];
                if (untitled != null)
                {
                    return untitled;
                }
            }
        }

        return defaultEmployeePortrait;
    }

    private Sprite ResolvePresetPortraitByTrait(HREmployeeTraitType trait)
    {
        switch (trait)
        {
            case HREmployeeTraitType.FitnessFan:
                return portraitFitnessFan;

            case HREmployeeTraitType.DarkCook:
                return portraitDarkCook;

            case HREmployeeTraitType.BigAppetite:
            case HREmployeeTraitType.UltimateBigAppetite:
                return portraitBigAppetite;

            case HREmployeeTraitType.SmartTalent:
                return portraitSmartTalent;

            case HREmployeeTraitType.LuckyMouse:
                return portraitLuckyMouse;

            case HREmployeeTraitType.EliteHR:
                return portraitEliteHR;

            case HREmployeeTraitType.StrongBody:
                return portraitStrongBody;

            case HREmployeeTraitType.BookLover:
                return portraitBookLover;

            case HREmployeeTraitType.MagicalGirl:
                return portraitMagicalGirl;

            case HREmployeeTraitType.SevereMyopia:
                return portraitSevereMyopia;

            case HREmployeeTraitType.InsectPhobia:
            case HREmployeeTraitType.PineconeAllergy:
            case HREmployeeTraitType.Sickly:
            case HREmployeeTraitType.KneeInjury:
            case HREmployeeTraitType.LazySyndrome:
            case HREmployeeTraitType.LearningDisability:
            case HREmployeeTraitType.Muggle:
            case HREmployeeTraitType.LowComprehension:
            case HREmployeeTraitType.BirdStomach:
            case HREmployeeTraitType.Strike:
                return portraitDebuff;

            case HREmployeeTraitType.GardeningExpert:
            case HREmployeeTraitType.MagicLover:
            default:
                return portraitNormal;
        }
    }

    private static int CompareEmployeeForUi(HREmployeeData a, HREmployeeData b)
    {
        string aName = GetEmployeeDisplayName(a);
        string bName = GetEmployeeDisplayName(b);
        int nameCompare = string.Compare(aName, bName, StringComparison.Ordinal);
        if (nameCompare != 0)
        {
            return nameCompare;
        }

        string aId = a == null ? string.Empty : (a.id ?? string.Empty);
        string bId = b == null ? string.Empty : (b.id ?? string.Empty);
        return string.Compare(aId, bId, StringComparison.Ordinal);
    }

    private static string GetEmployeeDisplayName(HREmployeeData employee)
    {
        if (employee == null)
        {
            return "未知员工";
        }

        if (!string.IsNullOrWhiteSpace(employee.displayName))
        {
            return employee.displayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(employee.id))
        {
            return employee.id.Trim();
        }

        return "未命名员工";
    }

    private static string BuildTraitSummary(HREmployeeData employee)
    {
        if (employee == null || employee.traits == null || employee.traits.Count == 0)
        {
            return "无";
        }

        List<string> names = new List<string>(employee.traits.Count);
        for (int i = 0; i < employee.traits.Count; i++)
        {
            names.Add(employee.traits[i].ToString());
        }

        return string.Join(" / ", names);
    }

    private static string BuildEmployeeDescription(HREmployeeData employee)
    {
        if (employee == null)
        {
            return "-";
        }

        List<string> works = new List<string>(3);
        if (employee.canFarm)
        {
            works.Add("农场");
        }

        if (employee.canCook)
        {
            works.Add("厨房");
        }

        if (employee.canPineconePlant)
        {
            works.Add("松果种植");
        }

        string workText = works.Count == 0 ? "无可用岗位" : string.Join("/", works);
        return "可用岗位: " + workText + "；每日果子消耗: " + employee.GetDailyFruitCost();
    }

    private bool ContainsManualWorkingEmployee(HREmployeeData employee)
    {
        if (employee == null || string.IsNullOrWhiteSpace(employee.id))
        {
            return false;
        }

        string id = employee.id.Trim();
        for (int i = 0; i < _manualWorkingEmployees.Count; i++)
        {
            HREmployeeData item = _manualWorkingEmployees[i];
            if (item == null || string.IsNullOrWhiteSpace(item.id))
            {
                continue;
            }

            if (string.Equals(item.id.Trim(), id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private bool RemoveManualWorkingEmployee(HREmployeeData employee)
    {
        if (employee == null || string.IsNullOrWhiteSpace(employee.id))
        {
            return false;
        }

        string id = employee.id.Trim();
        for (int i = _manualWorkingEmployees.Count - 1; i >= 0; i--)
        {
            HREmployeeData item = _manualWorkingEmployees[i];
            if (item == null || string.IsNullOrWhiteSpace(item.id))
            {
                continue;
            }

            if (!string.Equals(item.id.Trim(), id, StringComparison.Ordinal))
            {
                continue;
            }

            _manualWorkingEmployees.RemoveAt(i);
            return true;
        }

        return false;
    }

    private void CleanupManualWorkingEmployees()
    {
        if (_manualWorkingEmployees.Count == 0)
        {
            return;
        }

        EmployeeRepository repository = EnsureRepository();
        if (repository == null)
        {
            _manualWorkingEmployees.RemoveAll(e => e == null);
            return;
        }

        _manualWorkingEmployees.RemoveAll(e =>
        {
            if (e == null || string.IsNullOrWhiteSpace(e.id))
            {
                return true;
            }

            return !repository.TryGetById(e.id, out _);
        });
    }
}
