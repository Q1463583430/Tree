using System.Collections.Generic;
using UnityEngine;

public class GridTemplateGenerator : MonoBehaviour
{
    [Header("生成目标")]
    public Transform container;
    public GameObject visualPrefab;

    [Header("触发选项")]
    public bool autoGenerateOnStart = false;
    public bool clearBeforeGenerate = true;
    public bool verboseLog = true;

    [Header("网格参数")]
    public float cellSize = 1f;
    public Vector3 origin = Vector3.zero;
    public bool centerTemplate = true;
    public float yOffset = 0f;

    [Header("模板 (每行一个字符串: 1=生成, 0=空)")]
    public string template =
        "0111110\n" +
        "1111111\n" +
        "1111111\n" +
        "1111111\n" +
        "0111110";

    [Header("可选: 自动注册到 GridManager 阶段")]
    public GridManager gridManager;
    public bool autoRegisterToStage = false;
    public int stageIndex = 0;
    public bool clearStageCellListBeforeRegister = true;

    private const string GeneratedNamePrefix = "Cell_";

    void Start()
    {
        if (!autoGenerateOnStart) return;
        GenerateFromTemplate();
    }

    [ContextMenu("Generate From Template")]
    public void GenerateFromTemplate()
    {
        if (verboseLog)
        {
            Debug.Log("开始执行模板生成...");
        }

        if (container == null)
        {
            Debug.LogWarning("Generate 失败: container 为空");
            return;
        }

        if (visualPrefab == null)
        {
            Debug.LogWarning("Generate 失败: visualPrefab 为空");
            return;
        }

        if (cellSize <= 0f)
        {
            Debug.LogWarning("Generate 失败: cellSize 必须大于 0");
            return;
        }

        if (clearBeforeGenerate)
        {
            ClearGeneratedCells();
        }

        List<string> rows = ParseRows(template);
        if (rows.Count == 0)
        {
            Debug.LogWarning("Generate 失败: template 为空");
            return;
        }

        int height = rows.Count;
        int width = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Length > width) width = rows[i].Length;
        }

        Vector2 centerOffset = Vector2.zero;
        if (centerTemplate)
        {
            centerOffset = new Vector2((width - 1) * 0.5f, (height - 1) * 0.5f);
        }

        int createdCount = 0;
        List<GameObject> createdCells = new List<GameObject>();

        for (int row = 0; row < height; row++)
        {
            string line = rows[row];
            int zIndex = (height - 1) - row; // 第一行放在上方

            for (int col = 0; col < line.Length; col++)
            {
                char c = line[col];
                if (c != '1' && c != '#') continue;

                float localX = (col - centerOffset.x) * cellSize;
                float localZ = (zIndex - centerOffset.y) * cellSize;

                Vector3 worldPos = origin + new Vector3(localX, yOffset, localZ);
                GameObject cell = Instantiate(visualPrefab, worldPos, Quaternion.identity, container);
                cell.name = GeneratedNamePrefix + createdCount;

                createdCells.Add(cell);
                createdCount++;
            }
        }

        RegisterToStageIfNeeded(createdCells);
        Debug.Log($"模板生成完成: {createdCount} 个格子, 父节点: {container.name}");
    }

    [ContextMenu("Clear Generated Cells")]
    public void ClearGeneratedCells()
    {
        if (container == null) return;

        List<GameObject> toDelete = new List<GameObject>();
        foreach (Transform child in container)
        {
            if (child.name.StartsWith(GeneratedNamePrefix))
            {
                toDelete.Add(child.gameObject);
            }
        }

        for (int i = 0; i < toDelete.Count; i++)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(toDelete[i]);
            }
            else
            {
                Destroy(toDelete[i]);
            }
#else
            Destroy(toDelete[i]);
#endif
        }

        Debug.Log($"已清理生成格子: {toDelete.Count} 个");
    }

    private List<string> ParseRows(string source)
    {
        List<string> rows = new List<string>();
        if (string.IsNullOrWhiteSpace(source)) return rows;

        string[] split = source.Replace("\r", "").Split('\n');
        for (int i = 0; i < split.Length; i++)
        {
            string row = split[i].Trim();
            if (string.IsNullOrEmpty(row)) continue;
            rows.Add(row);
        }

        return rows;
    }

    private void RegisterToStageIfNeeded(List<GameObject> createdCells)
    {
        if (!autoRegisterToStage || gridManager == null) return;
        if (gridManager.stages == null || gridManager.stages.Count == 0) return;
        if (stageIndex < 0 || stageIndex >= gridManager.stages.Count) return;

        GridStage stage = gridManager.stages[stageIndex];
        if (clearStageCellListBeforeRegister)
        {
            stage.cellObjects.Clear();
        }

        for (int i = 0; i < createdCells.Count; i++)
        {
            stage.cellObjects.Add(createdCells[i]);
        }
    }
}
