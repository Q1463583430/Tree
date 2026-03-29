using UnityEngine;
using UnityEngine.UI;

// 通用关闭脚本：监听按钮点击并隐藏指定面板。
public class CloseButtonHidePanel : MonoBehaviour
{
    [Header("UI 引用")]
    public Button closeButton;
    public GameObject panelToClose;

    [Header("兜底")]
    public bool closeSelfIfPanelMissing = true;

    private void Reset()
    {
        if (closeButton == null)
        {
            closeButton = GetComponent<Button>();
        }
    }

    private void OnEnable()
    {
        Bind();
    }

    private void OnDisable()
    {
        Unbind();
    }

    public void Bind()
    {
        if (closeButton == null)
        {
            return;
        }

        closeButton.onClick.RemoveListener(ClosePanel);
        closeButton.onClick.AddListener(ClosePanel);
    }

    public void Unbind()
    {
        if (closeButton == null)
        {
            return;
        }

        closeButton.onClick.RemoveListener(ClosePanel);
    }

    public void ClosePanel()
    {
        GameObject target = panelToClose;
        if (target == null && closeSelfIfPanelMissing)
        {
            target = gameObject;
        }

        if (target != null)
        {
            target.SetActive(false);
        }
    }
}