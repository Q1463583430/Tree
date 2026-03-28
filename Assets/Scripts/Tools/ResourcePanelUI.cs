using TMPro;
using UnityEngine;

// 将 ResourceManager 的四项核心资源实时显示到同一 Panel。
public class ResourcePanelUI : MonoBehaviour
{
    [Header("依赖")]
    public ResourceManager resourceManager;

    [Header("四项资源文本")]
    public TMP_Text energyText;
    public TMP_Text fruitText;
    public TMP_Text rootText;
    public TMP_Text squirrelText;

    [Header("自动绑定(可选)")]
    public bool autoBindTextsByName = true;
    public string energyNameKey = "energy";
    public string fruitNameKey = "fruit";
    public string rootNameKey = "root";
    public string squirrelNameKey = "squirrel";

    [Header("显示格式")]
    public string energyPrefix = "Energy";
    public string fruitPrefix = "Fruit";
    public string rootPrefix = "Root";
    public string squirrelPrefix = "Squirrel";
    public bool showMaxValue;
    public float syncIntervalSeconds = 0.2f;

    private bool isSubscribed;
    private float syncTimer;
    private bool hasSnapshot;
    private int lastEnergy;
    private int lastFruit;
    private int lastRoot;
    private int lastSquirrel;

    private void Awake()
    {
        TryAutoBindTexts();
        TryResolveResourceManager();
    }

    private void OnEnable()
    {
        TryResolveResourceManager();
        TrySubscribe();
        ValidateBindings();
        syncTimer = 0f;
        RefreshAll();
    }

    private void Update()
    {
        if (resourceManager == null)
        {
            TryResolveResourceManager();
            TrySubscribe();
            RefreshAll();
            return;
        }

        syncTimer += Time.unscaledDeltaTime;
        float interval = Mathf.Max(0.02f, syncIntervalSeconds);
        if (syncTimer >= interval)
        {
            syncTimer = 0f;
            RefreshAll();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public void RefreshAll()
    {
        if (resourceManager == null)
        {
            return;
        }

        int energy = resourceManager.Get(ResourceType.Energy);
        int fruit = resourceManager.Get(ResourceType.Fruit);
        int root = resourceManager.Get(ResourceType.Root);
        int squirrel = resourceManager.Get(ResourceType.Squirrel);

        UpdateSingle(ResourceType.Energy, energyText, energyPrefix, energy);
        UpdateSingle(ResourceType.Fruit, fruitText, fruitPrefix, fruit);
        UpdateSingle(ResourceType.Root, rootText, rootPrefix, root);
        UpdateSingle(ResourceType.Squirrel, squirrelText, squirrelPrefix, squirrel);

        SaveSnapshot(energy, fruit, root, squirrel);
    }

    private void TryResolveResourceManager()
    {
        if (resourceManager != null)
        {
            return;
        }

        resourceManager = ResourceManager.Instance;
        if (resourceManager == null)
        {
            resourceManager = FindObjectOfType<ResourceManager>();
        }
    }

    private void TryAutoBindTexts()
    {
        if (!autoBindTextsByName)
        {
            return;
        }

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        if (texts == null || texts.Length == 0)
        {
            return;
        }

        if (energyText == null)
        {
            energyText = FindTextByKey(texts, energyNameKey);
        }

        if (fruitText == null)
        {
            fruitText = FindTextByKey(texts, fruitNameKey);
        }

        if (rootText == null)
        {
            rootText = FindTextByKey(texts, rootNameKey);
        }

        if (squirrelText == null)
        {
            squirrelText = FindTextByKey(texts, squirrelNameKey);
        }
    }

    private void ValidateBindings()
    {
        if (resourceManager == null)
        {
            Debug.LogWarning(name + " 未找到 ResourceManager，资源面板无法刷新。", this);
        }

        if (energyText == null)
        {
            Debug.LogWarning(name + " 未绑定 Energy 文本。", this);
        }

        if (fruitText == null)
        {
            Debug.LogWarning(name + " 未绑定 Fruit 文本。", this);
        }

        if (rootText == null)
        {
            Debug.LogWarning(name + " 未绑定 Root 文本。", this);
        }

        if (squirrelText == null)
        {
            Debug.LogWarning(name + " 未绑定 Squirrel 文本。", this);
        }
    }

    private void TrySubscribe()
    {
        if (isSubscribed || resourceManager == null)
        {
            return;
        }

        resourceManager.OnResourceChanged += HandleResourceChanged;
        resourceManager.OnResourcesInitialized += HandleResourcesInitialized;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed || resourceManager == null)
        {
            return;
        }

        resourceManager.OnResourceChanged -= HandleResourceChanged;
        resourceManager.OnResourcesInitialized -= HandleResourcesInitialized;
        isSubscribed = false;
    }

    private void HandleResourcesInitialized()
    {
        RefreshAll();
    }

    private void HandleResourceChanged(ResourceType type, int before, int current)
    {
        switch (type)
        {
            case ResourceType.Energy:
                UpdateSingle(ResourceType.Energy, energyText, energyPrefix, current);
                lastEnergy = current;
                break;
            case ResourceType.Fruit:
                UpdateSingle(ResourceType.Fruit, fruitText, fruitPrefix, current);
                lastFruit = current;
                break;
            case ResourceType.Root:
                UpdateSingle(ResourceType.Root, rootText, rootPrefix, current);
                lastRoot = current;
                break;
            case ResourceType.Squirrel:
                UpdateSingle(ResourceType.Squirrel, squirrelText, squirrelPrefix, current);
                lastSquirrel = current;
                break;
        }

        hasSnapshot = true;
    }

    private void PollAndRefreshIfChanged()
    {
        if (resourceManager == null)
        {
            return;
        }

        int energy = resourceManager.Get(ResourceType.Energy);
        int fruit = resourceManager.Get(ResourceType.Fruit);
        int root = resourceManager.Get(ResourceType.Root);
        int squirrel = resourceManager.Get(ResourceType.Squirrel);

        if (!hasSnapshot || energy != lastEnergy || fruit != lastFruit || root != lastRoot || squirrel != lastSquirrel)
        {
            UpdateSingle(ResourceType.Energy, energyText, energyPrefix, energy);
            UpdateSingle(ResourceType.Fruit, fruitText, fruitPrefix, fruit);
            UpdateSingle(ResourceType.Root, rootText, rootPrefix, root);
            UpdateSingle(ResourceType.Squirrel, squirrelText, squirrelPrefix, squirrel);
            SaveSnapshot(energy, fruit, root, squirrel);
        }
    }

    private void SaveSnapshot(int energy, int fruit, int root, int squirrel)
    {
        lastEnergy = energy;
        lastFruit = fruit;
        lastRoot = root;
        lastSquirrel = squirrel;
        hasSnapshot = true;
    }

    private static TMP_Text FindTextByKey(TMP_Text[] texts, string key)
    {
        if (texts == null || string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        string loweredKey = key.Trim().ToLowerInvariant();
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
            {
                continue;
            }

            if (text.name.ToLowerInvariant().Contains(loweredKey))
            {
                return text;
            }
        }

        return null;
    }

    private void UpdateSingle(ResourceType type, TMP_Text target, string prefix)
    {
        if (resourceManager == null)
        {
            return;
        }

        UpdateSingle(type, target, prefix, resourceManager.Get(type));
    }

    private void UpdateSingle(ResourceType type, TMP_Text target, string prefix, int current)
    {
        if (target == null)
        {
            return;
        }

        string valueText;
        if (showMaxValue && resourceManager != null)
        {
            int max = resourceManager.GetMax(type);
            valueText = current + "/" + max;
        }
        else
        {
            valueText = current.ToString();
        }

        if (string.IsNullOrEmpty(prefix))
        {
            target.text = valueText;
            return;
        }

        target.text = prefix + ": " + valueText;
    }
}