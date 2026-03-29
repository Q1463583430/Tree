using UnityEngine;
using UnityEngine.UI;

// 通用打开脚本：监听按钮点击并显示指定面板。
public class MagicButtonShowPanel : MonoBehaviour
{
    [Header("UI 引用")]
    public Button magicButton;
    public GameObject panelToOpen;

    [Header("兜底")]
    public bool openSelfIfPanelMissing = true;

    private void Reset()
    {
        if (magicButton == null)
        {
            magicButton = GetComponent<Button>();
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
        if (magicButton == null)
        {
            return;
        }

        magicButton.onClick.RemoveListener(OpenPanel);
        magicButton.onClick.AddListener(OpenPanel);
    }

    public void Unbind()
    {
        if (magicButton == null)
        {
            return;
        }

        magicButton.onClick.RemoveListener(OpenPanel);
    }

    public void OpenPanel()
    {
        GameObject target = panelToOpen;
        if (target == null && openSelfIfPanelMissing)
        {
            target = gameObject;
        }

        if (target != null)
        {
            target.SetActive(true);
        }
    }
}