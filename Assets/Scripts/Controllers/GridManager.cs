using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GridCell
{
    public Vector2Int position; //每个单元格的位置
    public bool isOccupied = false; //这个单元格是否被占用
    public bool isUnlocked = false; //是否已经解锁可用
    public GameObject visualObject = null; //对应的场景格子游戏物体，用于控制显隐
    public PlaceableRoom placeableRoom = null;
}

[System.Serializable]
public enum RoomCategory
{
    Production,
    Conversion,
    Growth,
    Other
}

[System.Serializable]
public class PlaceableRoom
{
    public string roomName; //每个房间的名称
    public RoomCategory category = RoomCategory.Other;
    [TextArea(2, 6)] public string description;
    public List<Vector2Int> occupiedCells; //房间占用的格子的相对坐标
    public GameObject roomPrefab; //房间的预制体
}

[System.Serializable]
public class GridStage
{
    [Header("阶段名称 (仅用于标识)")]
    public string stageName;
    [Header("当前阶段包含的所有格子物体")]
    public List<GameObject> cellObjects = new List<GameObject>();
}

public class GridManager : MonoBehaviour
{
    public float gridSize = 1f;
    public float placementZ = 0f;
    [Header("网格世界偏移(按格子单位)")]
    public float worldXOffsetInCells = 0f;
    public float worldYOffsetInCells = 0f;

    [Header("按照解锁顺序配置各个阶段的格子集合")]
    public List<GridStage> stages = new List<GridStage>();

    [Header("调试显示")]
    public bool drawGridGizmos = true;

    private int currentStageIndex = 0;

    //字典用来存储地图的网格位置信息
    private Dictionary<Vector2Int, GridCell> grid = new Dictionary<Vector2Int, GridCell>();
    private bool hasGridBounds;
    private int minGridX;
    private int maxGridX;
    private int minGridY;
    private int maxGridY;
    private bool hasBuildSignature;
    private float lastBuiltGridSize;
    private float lastBuiltXOffsetInCells;
    private float lastBuiltYOffsetInCells;

    // 初始化网格系统
    void Awake()
    {
        InitializeAllGrids();
    }

    // 运行时若模板生成器更新了 stages，可调用此方法重建网格字典。
    public void RebuildGridFromStages()
    {
        InitializeAllGrids();
    }

    //这个函数用于判断grid大小是否合规（感觉没什么用）
    void OnValidate()
    {
        if (gridSize <= 0f)
        {
            gridSize = 1f;
        }

        if (stages != null)
        {
            InitializeAllGrids();
        }
    }

    private void InitializeAllGrids() //这个函数只用来初始化第0阶段的格子（初始格子）
    {
        grid.Clear();
        currentStageIndex = 0;
        hasGridBounds = false;

        //如果当前没有定义阶段就不初始化
        if (stages == null || stages.Count == 0) return;

        // 遍历所有定义的阶段
        for (int i = 0; i < stages.Count; i++)
        {
            GridStage stage = stages[i];
            bool unlockInitially = (i == 0); // 只有第0阶段一开始是解锁的

            // 遍历当前阶段列表中直接指定的那些格子物体
            foreach (GameObject cellVisual in stage.cellObjects) //cellVisual是用来表示可视
            {
                if (cellVisual == null) continue; // 防空检查

                Vector2Int gridPos = WorldToGridPosition(cellVisual.transform.position);
                ExpandGridBounds(gridPos);

                if (!grid.ContainsKey(gridPos)) //containsKey判断grid字典是否包含gridPos位置
                {
                    GridCell newCell = new GridCell();
                    newCell.position = gridPos;
                    newCell.isOccupied = false;
                    newCell.isUnlocked = unlockInitially;
                    newCell.visualObject = cellVisual;

                    grid.Add(gridPos, newCell); //把第0阶段的格子放到grid里面
                }

                // 根据是否解锁来显示或隐藏格子
                cellVisual.SetActive(unlockInitially);
            }
        }

        hasBuildSignature = true;
        lastBuiltGridSize = gridSize;
        lastBuiltXOffsetInCells = worldXOffsetInCells;
        lastBuiltYOffsetInCells = worldYOffsetInCells;
    }

    // 解锁下一阶段的新格子（可视化这些新格子）
    public void UnlockNextStage()
    {
        if (stages == null || stages.Count == 0) return;

        currentStageIndex++;
        if (currentStageIndex < stages.Count)
        {
            GridStage nextStage = stages[currentStageIndex]; //获取到下一阶段的stage信息

            foreach (GameObject cellVisual in nextStage.cellObjects)
            {
                if (cellVisual == null) continue;

                Vector2Int gridPos = WorldToGridPosition(cellVisual.transform.position);
                if (grid.ContainsKey(gridPos))
                {
                    grid[gridPos].isUnlocked = true;
                    // 将之前隐藏的格子显示出来（可视化）
                    grid[gridPos].visualObject.SetActive(true);
                }
            }
            Debug.Log($"解锁并可视化了阶段 {currentStageIndex} : {nextStage.stageName} 的格子");
        }
        else
        {
            currentStageIndex = stages.Count - 1;
            Debug.Log("所有阶段都已解锁。");
        }
    }

    // 将世界坐标转换为网格坐标（XY平面）。
    public Vector2Int WorldToGridPosition(Vector3 worldPosition)
    {
        worldPosition.x -= GetWorldXOffset();
        worldPosition.y -= GetWorldYOffset();

        int x = RoundHalfUp(worldPosition.x / gridSize);
        int y = RoundHalfUp(worldPosition.y / gridSize);
        return new Vector2Int(x, y);
    }

    // 将网格坐标转换为世界坐标（XY平面，Z为固定深度 placementZ）。
    public Vector3 GridToWorldPosition(Vector2Int gridPosition)
    {
        float xOffset = GetWorldXOffset();
        float yOffset = GetWorldYOffset();

        return new Vector3(gridPosition.x * gridSize + xOffset, gridPosition.y * gridSize + yOffset, placementZ);
    }

    private float GetWorldXOffset()
    {
        return worldXOffsetInCells * gridSize;
    }

    private float GetWorldYOffset()
    {
        return worldYOffsetInCells * gridSize;
    }

    private static int RoundHalfUp(float value)
    {
        // 统一使用 floor(v + 0.5) 处理正负半格，保证整数格映射连续。
        return Mathf.FloorToInt(value + 0.5f);
    }

    //判断grid的这个位置是否有格子
    public bool HasCell(Vector2Int pos)
    {
        EnsureGridReadyFromStages();
        return grid.ContainsKey(pos);
    }

    public bool HasAnyRegisteredCells()
    {
        EnsureGridReadyFromStages();
        return grid.Count > 0;
    }

    //尝试获取到pos位置的cell
    public bool TryGetCell(Vector2Int pos, out GridCell cell)
    {
        EnsureGridReadyFromStages();
        return grid.TryGetValue(pos, out cell);
    }

    // 判断是否在网格外边界内(不代表可放置)。
    public bool IsInsideGridBounds(Vector2Int pos)
    {
        EnsureGridReadyFromStages();

        if (!hasGridBounds)
        {
            return false;
        }

        return pos.x >= minGridX && pos.x <= maxGridX && pos.y >= minGridY && pos.y <= maxGridY;
    }

    public bool TryGetGridBounds(out Vector2Int min, out Vector2Int max)
    {
        EnsureGridReadyFromStages();

        min = Vector2Int.zero;
        max = Vector2Int.zero;
        if (!hasGridBounds)
        {
            return false;
        }

        min = new Vector2Int(minGridX, minGridY);
        max = new Vector2Int(maxGridX, maxGridY);
        return true;
    }

    //判断能不能占用该cell
    public bool CanOccupyCell(Vector2Int pos)
    {
        EnsureGridReadyFromStages();
        return grid.TryGetValue(pos, out GridCell cell) && cell.isUnlocked && !cell.isOccupied;
    }

    //将某个cell设置为被占用
    public void SetCellOccupied(Vector2Int pos, bool occupied, PlaceableRoom room)
    {
        EnsureGridReadyFromStages();

        if (!grid.TryGetValue(pos, out GridCell cell)) return;

        cell.isOccupied = occupied;
        cell.placeableRoom = occupied ? room : null; //设置cell所归属的room
    }

    // 在编辑器中绘制网格线，便于调试
    void OnDrawGizmos()
    {
        if (!drawGridGizmos || grid == null) return;

        foreach (KeyValuePair<Vector2Int, GridCell> kv in grid) //键值对绑定v2i和cell
        {
            GridCell cell = kv.Value;
            Vector3 center = GridToWorldPosition(cell.position);
            center.z = placementZ;

            if (!cell.isUnlocked)
            {
                Gizmos.color = new Color(0.4f, 0.4f, 0.4f, 0.8f);
            }
            else if (cell.isOccupied)
            {
                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
            }
            else
            {
                Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.9f);
            }

            Gizmos.DrawWireCube(center, new Vector3(gridSize * 0.9f, gridSize * 0.9f, 0.05f));
        }
    }

    //判断某个位置的cell是否已经解锁了
    public bool IsUnlockedCell(Vector2Int pos)
    {
        EnsureGridReadyFromStages();
        return grid.TryGetValue(pos, out GridCell cell) && cell.isUnlocked;
    }

    //判断是否被占用
    public bool IsOccupiedCell(Vector2Int pos)
    {
        EnsureGridReadyFromStages();
        return grid.TryGetValue(pos, out GridCell cell) && cell.isOccupied;
    }

    private void EnsureGridReadyFromStages()
    {
        if (grid.Count > 0 && !IsBuildSignatureDirty())
        {
            return;
        }

        if (stages == null || stages.Count == 0)
        {
            return;
        }

        bool hasAnyCell = false;
        for (int i = 0; i < stages.Count; i++)
        {
            GridStage stage = stages[i];
            if (stage == null || stage.cellObjects == null)
            {
                continue;
            }

            for (int j = 0; j < stage.cellObjects.Count; j++)
            {
                if (stage.cellObjects[j] != null)
                {
                    hasAnyCell = true;
                    break;
                }
            }

            if (hasAnyCell)
            {
                break;
            }
        }

        if (!hasAnyCell)
        {
            return;
        }

        InitializeAllGrids();
    }

    private bool IsBuildSignatureDirty()
    {
        if (!hasBuildSignature)
        {
            return true;
        }

        return !Mathf.Approximately(lastBuiltGridSize, gridSize)
            || !Mathf.Approximately(lastBuiltXOffsetInCells, worldXOffsetInCells)
            || !Mathf.Approximately(lastBuiltYOffsetInCells, worldYOffsetInCells);
    }

    private void ExpandGridBounds(Vector2Int pos)
    {
        if (!hasGridBounds)
        {
            minGridX = pos.x;
            maxGridX = pos.x;
            minGridY = pos.y;
            maxGridY = pos.y;
            hasGridBounds = true;
            return;
        }

        if (pos.x < minGridX) minGridX = pos.x;
        if (pos.x > maxGridX) maxGridX = pos.x;
        if (pos.y < minGridY) minGridY = pos.y;
        if (pos.y > maxGridY) maxGridY = pos.y;
    }
}
