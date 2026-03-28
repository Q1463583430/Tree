using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[Serializable]
public class ResourceTextBinding
{
    public ResourceType type;
    public TMP_Text valueText;
    public string labelPrefix; //前缀文本
}

public class ResourceUIBinder : MonoBehaviour
{
    [Header("依赖")]
    public ResourceManager resourceManager;

    [Header("资源文本绑定")]
    public List<ResourceTextBinding> bindings = new List<ResourceTextBinding>();

    void Awake()
    {
        if (resourceManager == null)
        {
            resourceManager = ResourceManager.Instance;
            if (resourceManager == null)
            {
                resourceManager = FindObjectOfType<ResourceManager>();
            }
        }
    }

    void OnEnable()
    {
        if (resourceManager == null) return;

        resourceManager.OnResourceChanged += HandleResourceChanged;
        RefreshAll();
    }

    void OnDisable()
    {
        if (resourceManager == null) return;
        resourceManager.OnResourceChanged -= HandleResourceChanged;
    }

    public void RefreshAll()
    {
        if (resourceManager == null || bindings == null) return;

        for (int i = 0; i < bindings.Count; i++)
        {
            ResourceTextBinding binding = bindings[i];
            UpdateBindingText(binding, resourceManager.Get(binding.type));
        }
    }

    private void HandleResourceChanged(ResourceType type, int before, int current)
    {
        if (bindings == null) return;

        for (int i = 0; i < bindings.Count; i++)
        {
            ResourceTextBinding binding = bindings[i];
            if (binding.type != type) continue;
            UpdateBindingText(binding, current);
        }
    }

    private void UpdateBindingText(ResourceTextBinding binding, int value)
    {
        if (binding == null || binding.valueText == null) return;

        if (string.IsNullOrEmpty(binding.labelPrefix))
        {
            binding.valueText.text = value.ToString();
            return;
        }

        binding.valueText.text = $"{binding.labelPrefix}: {value}";
    }
}
