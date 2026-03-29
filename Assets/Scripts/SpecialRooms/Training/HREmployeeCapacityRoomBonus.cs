using UnityEngine;

// 特殊房间：提供员工容量上限加成（如 +5 / +9）。
public class HREmployeeCapacityRoomBonus : MonoBehaviour
{
    [Min(0)]
    public int capacityBonus = 5;

    [Header("触发条件")]
    public bool requireBuiltRoom = true;

    public EmployeeRepository repository;

    private bool _registered;
    private RoomProductionUnit _roomUnit;

    void Awake()
    {
        _roomUnit = GetComponent<RoomProductionUnit>();

        if (repository == null)
        {
            repository = EmployeeRepository.GetOrCreateInstance();
        }
    }

    void OnEnable()
    {
        if (_roomUnit != null)
        {
            _roomUnit.OnConstructionCompleted += HandleRoomConstructionCompleted;
        }

        TryRegister();
    }

    void Start()
    {
        TryRegister();
    }

    void Update()
    {
        if (!_registered)
        {
            TryRegister();
        }
    }

    void OnDisable()
    {
        if (_roomUnit != null)
        {
            _roomUnit.OnConstructionCompleted -= HandleRoomConstructionCompleted;
        }

        Unregister();
    }

    void OnDestroy()
    {
        Unregister();
    }

    private void TryRegister()
    {
        if (_registered)
        {
            return;
        }

        if (requireBuiltRoom && _roomUnit != null && !_roomUnit.IsBuilt)
        {
            return;
        }

        if (repository == null)
        {
            repository = EmployeeRepository.GetOrCreateInstance();
        }

        if (repository == null)
        {
            return;
        }

        repository.RegisterCapacityBonus(this, capacityBonus);
        _registered = true;
    }

    private void HandleRoomConstructionCompleted(RoomProductionUnit _)
    {
        TryRegister();
    }

    private void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        if (repository != null)
        {
            repository.UnregisterCapacityBonus(this);
        }

        _registered = false;
    }
}