using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class StageTemplateConfig
{
    [Header("注册到 GridManager.stages 的索引")]
    public int stageIndex;

    [Header("阶段模板 (1或#=生成, 0=空)")]
    [TextArea(4, 20)]
    public string template;
}

public class GridTemplateGenerator : MonoBehaviour
{
    private const int FixedTemplateWidth = 15;
    private const int FixedTemplateHeight = 8;

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
    public float zOffset = 0f;

    [Header("生成到该阶段索引(包含该阶段)")]
    public int generateToStageIndex = 0;

    [Header("五阶段模板 (第0:31  第1:+25  第2:+25  第3:+19  第4:+12)")]
    public List<StageTemplateConfig> stageTemplates = new List<StageTemplateConfig>
    {
        // 第0阶段：绿色 31
        new StageTemplateConfig
        {
            stageIndex = 0,
            template =
                "000000000000000\n" +
                "000000000000000\n" +
                "000000000000000\n" +
                "000001111100000\n" +
                "000011111110000\n" +
                "000011111110000\n" +
                "000011111110000\n" +
                "000001111100000"
        },
        // 第1阶段：黄色 +25
        new StageTemplateConfig
        {
            stageIndex = 1,
            template =
                "000000000000000\n" +
                "000000000000000\n" +
                "000111111111000\n" +
                "001111000011100\n" +
                "001100000001100\n" +
                "001100000001100\n" +
                "000100000001000\n" +
                "000000000000000"
        },
        // 第2阶段：红色 +25
        new StageTemplateConfig
        {
            stageIndex = 2,
            template =
                "000000000000000\n" +
                "001111111111100\n" +
                "011000000000110\n" +
                "010000000000010\n" +
                "010000000000010\n" +
                "010000000000010\n" +
                "001000000000100\n" +
                "000010000010000"
        },
        // 第3阶段：蓝色 +19
        new StageTemplateConfig
        {
            stageIndex = 3,
            template =
                "000011111111000\n" +
                "000000000000000\n" +
                "000000000000000\n" +
                "100000000000001\n" +
                "100000000000001\n" +
                "100000000000001\n" +
                "010000000000010\n" +
                "001100000001100"
        },
        // 第4阶段：棕色 +12
        new StageTemplateConfig
        {
            stageIndex = 4,
            template =
                "001100000001100\n" +
                "010000000000010\n" +
                "100000000000001\n" +
                "000000000000000\n" +
                "000000000000000\n" +
                "000000000000000\n" +
                "100000000000001\n" +
                "010000000000010"
        }
    };

    [Header("可选: 自动注册到 GridManager 阶段")]
    public GridManager gridManager;
    public bool autoRegisterToStages = true;
    public bool clearStageCellListBeforeRegister = true;

    [Header("位置对齐")]
    public bool alignByLargestTemplate = true;

    private const string GeneratedNamePrefix = "Cell_";

    // 启动时自动生成到选中阶段。
    void Start()
    {
        if (!autoGenerateOnStart) return;
        GenerateToSelectedStageIndex();
    }

    // 根据 generateToStageIndex 生成从第0阶段到目标阶段(包含)的格子。
    [ContextMenu("Generate To Selected Stage")]
    public void GenerateToSelectedStageIndex()
    {
        if (verboseLog)
        {
            Debug.Log($"开始执行阶段生成: 0 -> {generateToStageIndex}");
        }

        if (!ValidateGenerationSettings()) return;
        if (stageTemplates == null || stageTemplates.Count == 0)
        {
            Debug.LogWarning("Generate 失败: stageTemplates 为空");
            return;
        }

        List<StageTemplateConfig> targets = GetTemplatesUpToStageIndex(generateToStageIndex);
        if (targets.Count == 0)
        {
            Debug.LogWarning($"Generate 失败: 没有可生成的阶段(<= {generateToStageIndex})");
            return;
        }

        if (clearBeforeGenerate)
        {
            ClearGeneratedCells();
        }

        if (autoRegisterToStages && clearStageCellListBeforeRegister)
        {
            ClearRegisteredStageCells(targets);
        }

        int totalCreated = 0;
        int totalSkippedOverlap = 0;
        HashSet<Vector2Int> usedCells = new HashSet<Vector2Int>();

        GetReferenceSize(targets, out int referenceWidth, out int referenceHeight);

        for (int i = 0; i < targets.Count; i++)
        {
            StageTemplateConfig config = targets[i];
            int created = GenerateTemplateCells(
                config.template,
                config.stageIndex,
                referenceWidth,
                referenceHeight,
                usedCells,
                out int overlapSkipped,
                out List<GameObject> createdCells
            );
            totalCreated += created;
            totalSkippedOverlap += overlapSkipped;

            RegisterToStageIfNeeded(config.stageIndex, createdCells);

            if (verboseLog)
            {
                Debug.Log($"阶段 {config.stageIndex} 生成完成: {created} 个, 重叠跳过: {overlapSkipped} 个");
            }
        }

        Debug.Log($"阶段生成完成: 0 -> {generateToStageIndex}, 总计 {totalCreated} 个格子, 重叠跳过 {totalSkippedOverlap} 个, 父节点: {container.name}");
    }

    // 清理 container 下此前由生成器创建的格子对象。
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

        if (verboseLog)
        {
            Debug.Log($"已清理生成格子: {toDelete.Count} 个");
        }
    }

    // 检查生成前必须参数是否齐全。
    private bool ValidateGenerationSettings()
    {
        if (container == null)
        {
            Debug.LogWarning("Generate 失败: container 为空");
            return false;
        }

        if (visualPrefab == null)
        {
            Debug.LogWarning("Generate 失败: visualPrefab 为空");
            return false;
        }

        if (cellSize <= 0f)
        {
            Debug.LogWarning("Generate 失败: cellSize 必须大于 0");
            return false;
        }

        if (!ValidateTemplateFixedSize())
        {
            return false;
        }

        return true;
    }

    // 校验所有模板均为固定8x15，避免因尺寸不一致导致偏移。
    private bool ValidateTemplateFixedSize()
    {
        if (stageTemplates == null || stageTemplates.Count == 0) return true;

        for (int i = 0; i < stageTemplates.Count; i++)
        {
            int[,] arr = ParseTemplateToArray(stageTemplates[i].template, out int width, out int height);
            if (arr == null)
            {
                Debug.LogWarning($"模板校验失败: Stage {stageTemplates[i].stageIndex} 模板为空");
                return false;
            }

            if (width != FixedTemplateWidth || height != FixedTemplateHeight)
            {
                Debug.LogWarning($"模板校验失败: Stage {stageTemplates[i].stageIndex} 需为 {FixedTemplateHeight}x{FixedTemplateWidth}，当前为 {height}x{width}");
                return false;
            }
        }

        return true;
    }

    // 将模板文本先转为二维数组，再转换为格子对象，避免字符串行长差异导致的错位。
    private int GenerateTemplateCells(
        string templateSource,
        int stageIndexForName,
        int referenceWidth,
        int referenceHeight,
        HashSet<Vector2Int> usedCells,
        out int overlapSkipped,
        out List<GameObject> createdCells
    )
    {
        createdCells = new List<GameObject>();
        overlapSkipped = 0;

        int[,] gridArray = ParseTemplateToArray(templateSource, out int width, out int height);
        if (gridArray == null)
        {
            Debug.LogWarning($"Generate 失败: stage {stageIndexForName} 的 template 为空");
            return 0;
        }

        int targetWidth = (alignByLargestTemplate && referenceWidth > 0) ? referenceWidth : width;
        int targetHeight = (alignByLargestTemplate && referenceHeight > 0) ? referenceHeight : height;

        int padLeft = Mathf.Max(0, (targetWidth - width) / 2);
        int padTop = Mathf.Max(0, (targetHeight - height) / 2);

        Vector2 centerOffset = Vector2.zero;
        if (centerTemplate)
        {
            centerOffset = new Vector2((targetWidth - 1) * 0.5f, (targetHeight - 1) * 0.5f);
        }

        int createdCount = 0;

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                if (gridArray[row, col] == 0) continue;

                int virtualCol = col + padLeft;
                int virtualRow = row + padTop;

                Vector2Int virtualCell = new Vector2Int(virtualCol, virtualRow);
                if (usedCells != null)
                {
                    if (usedCells.Contains(virtualCell))
                    {
                        overlapSkipped++;
                        continue;
                    }
                    usedCells.Add(virtualCell);
                }

                int virtualYIndex = (targetHeight - 1) - virtualRow;

                float localX = (virtualCol - centerOffset.x) * cellSize;
                float localY = (virtualYIndex - centerOffset.y) * cellSize;

                Vector3 worldPos = origin + new Vector3(localX, localY, zOffset);
                GameObject cell = Instantiate(visualPrefab, worldPos, Quaternion.identity, container);
                cell.name = $"{GeneratedNamePrefix}S{stageIndexForName}_{createdCount}";

                createdCells.Add(cell);
                createdCount++;
            }
        }

        return createdCount;
    }

    // 计算参与生成阶段的统一参考尺寸，保证同一坐标系下对齐。
    private void GetReferenceSize(List<StageTemplateConfig> targets, out int maxWidth, out int maxHeight)
    {
        maxWidth = 0;
        maxHeight = 0;

        if (targets == null) return;

        for (int i = 0; i < targets.Count; i++)
        {
            int[,] arr = ParseTemplateToArray(targets[i].template, out int width, out int height);
            if (arr == null) continue;

            if (width > maxWidth) maxWidth = width;
            if (height > maxHeight) maxHeight = height;
        }
    }

    // 清空本次参与生成阶段的 cellObjects，避免重复累积。
    private void ClearRegisteredStageCells(List<StageTemplateConfig> targets)
    {
        if (gridManager == null) return;
        if (gridManager.stages == null || gridManager.stages.Count == 0) return;
        if (targets == null) return;

        for (int i = 0; i < targets.Count; i++)
        {
            int idx = targets[i].stageIndex;
            if (idx < 0 || idx >= gridManager.stages.Count) continue;
            gridManager.stages[idx].cellObjects.Clear();
        }
    }

    // 返回阶段索引 <= targetStageIndex 的模板列表，并按 stageIndex 升序。
    private List<StageTemplateConfig> GetTemplatesUpToStageIndex(int targetStageIndex)
    {
        List<StageTemplateConfig> result = new List<StageTemplateConfig>();
        if (stageTemplates == null) return result;

        for (int i = 0; i < stageTemplates.Count; i++)
        {
            StageTemplateConfig cfg = stageTemplates[i];
            if (cfg == null) continue;
            if (cfg.stageIndex <= targetStageIndex)
            {
                result.Add(cfg);
            }
        }

        result.Sort((a, b) => a.stageIndex.CompareTo(b.stageIndex));
        return result;
    }

    // 将模板字符串解析为二维数组(行,列)：1表示生成格子，0表示空位。
    private int[,] ParseTemplateToArray(string source, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (string.IsNullOrWhiteSpace(source)) return null;

        string[] split = source.Replace("\r", "").Split('\n');
        List<string> rows = new List<string>();
        for (int i = 0; i < split.Length; i++)
        {
            string row = split[i].Trim();
            if (string.IsNullOrEmpty(row)) continue;
            rows.Add(row);
            if (row.Length > width) width = row.Length;
        }

        height = rows.Count;
        if (height == 0 || width == 0) return null;

        int[,] result = new int[height, width];
        for (int r = 0; r < height; r++)
        {
            string row = rows[r];
            for (int c = 0; c < width; c++)
            {
                if (c >= row.Length)
                {
                    result[r, c] = 0;
                    continue;
                }

                char ch = row[c];
                result[r, c] = (ch == '1' || ch == '#') ? 1 : 0;
            }
        }

        return result;
    }

    // 多阶段模式下注册到指定 registerStageIndex 阶段。
    private void RegisterToStageIfNeeded(int registerStageIndex, List<GameObject> createdCells)
    {
        if (!autoRegisterToStages || gridManager == null) return;
        if (gridManager.stages == null || gridManager.stages.Count == 0) return;
        if (registerStageIndex < 0 || registerStageIndex >= gridManager.stages.Count) return;

        GridStage stage = gridManager.stages[registerStageIndex];
        for (int i = 0; i < createdCells.Count; i++)
        {
            stage.cellObjects.Add(createdCells[i]);
        }
    }
}