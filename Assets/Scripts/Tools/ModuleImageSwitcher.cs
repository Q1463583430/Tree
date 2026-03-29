using UnityEngine;
using UnityEngine.UI;

// 挂在 Image 上：初始显示默认图片，点击四个模块按钮切换对应图片。
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class ModuleImageSwitcher : MonoBehaviour
{
    [Header("目标 Image（为空时自动取本物体上的 Image）")]
    public Image targetImage;

    [Header("四个模块按钮（可在 Inspector 拖入）")]
    public Button module1Button;
    public Button module2Button;
    public Button module3Button;
    public Button module4Button;

    [Header("默认图片")]
    public Sprite defaultSprite;

    [Header("四个模块对应图片")]
    public Sprite module1Sprite;
    public Sprite module2Sprite;
    public Sprite module3Sprite;
    public Sprite module4Sprite;

    [Header("若默认图片为空，则初始显示该模块（1~4）")]
    [Range(1, 4)]
    public int defaultModule = 1;

    void Awake()
    {
        EnsureTargetImage();
    }

    void OnEnable()
    {
        BindButtons();
    }

    void OnDisable()
    {
        UnbindButtons();
    }

    void Start()
    {
        ShowDefaultImage();
    }

    public void ShowDefaultImage()
    {
        EnsureTargetImage();
        if (targetImage == null)
        {
            return;
        }

        if (defaultSprite != null)
        {
            targetImage.sprite = defaultSprite;
            return;
        }

        SwitchToModule(defaultModule);
    }

    public void SwitchToModule1()
    {
        SwitchToModule(1);
    }

    public void SwitchToModule2()
    {
        SwitchToModule(2);
    }

    public void SwitchToModule3()
    {
        SwitchToModule(3);
    }

    public void SwitchToModule4()
    {
        SwitchToModule(4);
    }

    public void SwitchToModule(int moduleIndex)
    {
        EnsureTargetImage();
        if (targetImage == null)
        {
            return;
        }

        Sprite nextSprite = GetSpriteByIndex(moduleIndex);
        if (nextSprite != null)
        {
            targetImage.sprite = nextSprite;
        }
    }

    private Sprite GetSpriteByIndex(int moduleIndex)
    {
        switch (moduleIndex)
        {
            case 1:
                return module1Sprite;
            case 2:
                return module2Sprite;
            case 3:
                return module3Sprite;
            case 4:
                return module4Sprite;
            default:
                return null;
        }
    }

    private void EnsureTargetImage()
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }
    }

    private void BindButtons()
    {
        if (module1Button != null)
        {
            module1Button.onClick.AddListener(SwitchToModule1);
        }

        if (module2Button != null)
        {
            module2Button.onClick.AddListener(SwitchToModule2);
        }

        if (module3Button != null)
        {
            module3Button.onClick.AddListener(SwitchToModule3);
        }

        if (module4Button != null)
        {
            module4Button.onClick.AddListener(SwitchToModule4);
        }
    }

    private void UnbindButtons()
    {
        if (module1Button != null)
        {
            module1Button.onClick.RemoveListener(SwitchToModule1);
        }

        if (module2Button != null)
        {
            module2Button.onClick.RemoveListener(SwitchToModule2);
        }

        if (module3Button != null)
        {
            module3Button.onClick.RemoveListener(SwitchToModule3);
        }

        if (module4Button != null)
        {
            module4Button.onClick.RemoveListener(SwitchToModule4);
        }
    }
}