using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HR 招募界面控制：负责显示、按钮流程，不负责抽卡计算。
public class HRRecruitPanel : MonoBehaviour
{
    [System.Serializable]
    public class TraitImageBinding
    {
        public HREmployeeTraitType trait;
        public Sprite sprite;
    }

    [Header("依赖")]
    public HR hrRoom;

    [Header("面板节点")]
    public GameObject root;
    public GameObject resultRoot;

    [Header("按钮")]
    public Button closeButton;           // 主关闭按钮（通常在右上角）
    public Button closeButtonSecondary;  // 副关闭按钮（通常在招募按钮旁边）
    public Button recruitButton;         // 招募新员工（加入仓库）
    public Button rerollButton;          // 重新招募（替换上一次招募的员工）
    public Button openWarehouseButton;   // 打开员工仓库

    [Header("结果图片")]
    public Image resultImage;
    public Sprite recruitSuccessSprite;
    public List<TraitImageBinding> firstTraitImageBindings = new List<TraitImageBinding>();
    [Header("头像预设映射(按你提供的15张图)")]
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
    public bool logTraitImageResolve = true;
    public bool hideResultImageOnOpen = true;

    [Header("文本")]
    public TMP_Text titleText;
    public TMP_Text messageText;
    public TMP_Text nameText;
    public TMP_Text staminaText;
    public TMP_Text intelligenceText;
    public TMP_Text magicText;
    public TMP_Text traitsText;

    void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);
        if (closeButtonSecondary != null) closeButtonSecondary.onClick.AddListener(ClosePanel);
        if (recruitButton != null) recruitButton.onClick.AddListener(OnRecruitClicked);
        if (rerollButton != null) rerollButton.onClick.AddListener(OnRerollClicked);
        if (openWarehouseButton != null) openWarehouseButton.onClick.AddListener(OpenWarehouse);

        if (root != null) root.SetActive(false);
    }

    public void OpenPanel()
    {
        if (root != null) root.SetActive(true);

        if (titleText != null)
        {
            titleText.text = "HR 招募";
        }

        if (messageText != null)
        {
            if (hrRoom != null)
            {
                messageText.text = $"消耗 {hrRoom.recruitFruitCost} 果实进行招募";
            }
            else
            {
                messageText.text = "未绑定 HR 房间";
            }
        }

        if (resultRoot != null) resultRoot.SetActive(false);
        bool showReroll = hrRoom != null && hrRoom.HasRecruitedOnce;
        ApplyRecruitButtonState(showReroll);

        if (resultImage != null && hideResultImageOnOpen)
        {
            resultImage.gameObject.SetActive(false);
        }
    }

    public void ClosePanel()
    {
        if (root != null) root.SetActive(false);
    }

    private void OnRecruitClicked()
    {
        if (hrRoom == null)
        {
            if (messageText != null) messageText.text = "未绑定 HR 房间";
            return;
        }

        if (!hrRoom.TryRecruit(out HREmployeeData employee, out string failReason))
        {
            if (messageText != null) messageText.text = failReason;
            return;
        }

        if (messageText != null)
        {
            messageText.text = "招募成功！可继续重新招募。";
        }

        if (resultRoot != null) resultRoot.SetActive(true);
        ApplyRecruitButtonState(true);

        RefreshResult(employee);
        RefreshResultImage(employee);
        NotifyWarehouseUiUpdated();
    }

    private void OnRerollClicked()
    {
        if (hrRoom == null)
        {
            if (messageText != null) messageText.text = "未绑定 HR 房间";
            return;
        }

        if (!hrRoom.TryReroll(out HREmployeeData employee, out string failReason))
        {
            if (messageText != null) messageText.text = failReason;
            return;
        }

        if (messageText != null)
        {
            messageText.text = "重新招募成功！已替换上一只鼠鼠并加入仓库。";
        }

        if (resultRoot != null) resultRoot.SetActive(true);
        ApplyRecruitButtonState(true);

        RefreshResult(employee);
        RefreshResultImage(employee);
        NotifyWarehouseUiUpdated();
    }

    private void OpenWarehouse()
    {
        RoomEmployeeWarehouseUI ui = RoomEmployeeWarehouseUI.EnsureInstance();
        RoomProductionUnit hrRoomUnit = hrRoom != null ? hrRoom.GetRoomProductionUnit() : null;
        if (hrRoomUnit != null)
        {
            ui.OpenRoomConfig(hrRoomUnit);
            return;
        }

        ui.OpenWarehouse();
    }

    private void RefreshResult(HREmployeeData e)
    {
        if (e == null) return;

        if (nameText != null) nameText.text = $"{e.displayName}";
        if (staminaText != null) staminaText.text = $"耐力: {e.stamina}";
        if (intelligenceText != null) intelligenceText.text = $"智力: {e.intelligence}";
        if (magicText != null) magicText.text = $"魔力: {e.magic}";

        if (traitsText != null)
        {
            traitsText.text = "词条: " + FormatTraits(e.traits);
        }
    }

    private string FormatTraits(List<HREmployeeTraitType> traits)
    {
        if (traits == null || traits.Count == 0) return "无";

        List<string> names = new List<string>();
        for (int i = 0; i < traits.Count; i++)
        {
            names.Add(GetTraitDisplayName(traits[i]));
        }

        return string.Join(" / ", names);
    }

    private string GetTraitDisplayName(HREmployeeTraitType trait)
    {
        switch (trait)
        {
            case HREmployeeTraitType.InsectPhobia: return "昆虫恐惧症";
            case HREmployeeTraitType.DarkCook: return "黑暗料理者";
            case HREmployeeTraitType.PineconeAllergy: return "松果过敏";
            case HREmployeeTraitType.Sickly: return "体弱多病";
            case HREmployeeTraitType.KneeInjury: return "膝盖中了一箭";
            case HREmployeeTraitType.SevereMyopia: return "重度近视";
            case HREmployeeTraitType.LazySyndrome: return "懒癌";
            case HREmployeeTraitType.LearningDisability: return "学习困难症";
            case HREmployeeTraitType.Muggle: return "麻瓜";
            case HREmployeeTraitType.LowComprehension: return "低悟性";
            case HREmployeeTraitType.BigAppetite: return "大胃袋";
            case HREmployeeTraitType.UltimateBigAppetite: return "究极大胃袋";
            case HREmployeeTraitType.GardeningExpert: return "园艺高手";
            case HREmployeeTraitType.StrongBody: return "身强体壮";
            case HREmployeeTraitType.SmartTalent: return "天资聪颖";
            case HREmployeeTraitType.MagicalGirl: return "马猴烧酒";
            case HREmployeeTraitType.FitnessFan: return "健美爱好者";
            case HREmployeeTraitType.BookLover: return "酷爱阅读者";
            case HREmployeeTraitType.MagicLover: return "喜爱魔法";
            case HREmployeeTraitType.LuckyMouse: return "幸运鼠";
            case HREmployeeTraitType.EliteHR: return "精英HR";
            case HREmployeeTraitType.BirdStomach: return "小鸟胃";
            case HREmployeeTraitType.Strike: return "罢工";
            default: return trait.ToString();
        }
    }

    private void RefreshResultImage(HREmployeeData employee)
    {
        if (resultImage == null)
        {
            if (logTraitImageResolve)
            {
                Debug.Log("[HRRecruitPanel] resultImage 未绑定，无法显示招募结果图。", this);
            }
            return;
        }

        Sprite selectedSprite = ResolveResultSprite(employee);
        if (selectedSprite != null)
        {
            resultImage.sprite = selectedSprite;
        }
        else if (logTraitImageResolve)
        {
            Debug.Log("[HRRecruitPanel] 未解析到可用的结果图（首词条映射与默认图都为空）。", this);
        }

        resultImage.enabled = true;
        Color color = resultImage.color;
        color.a = 1f;
        resultImage.color = color;

        resultImage.gameObject.SetActive(true);
    }

    private Sprite ResolveResultSprite(HREmployeeData employee)
    {
        if (employee != null && employee.traits != null && employee.traits.Count > 0)
        {
            HREmployeeTraitType firstTrait = employee.traits[0];

            // 1) 优先使用显式绑定（兼容你现有 firstTraitImageBindings 配置）
            for (int i = 0; i < firstTraitImageBindings.Count; i++)
            {
                TraitImageBinding binding = firstTraitImageBindings[i];
                if (binding == null)
                {
                    continue;
                }

                if (binding.trait == firstTrait && binding.sprite != null)
                {
                    if (logTraitImageResolve)
                    {
                        Debug.Log($"[HRRecruitPanel] 结果图命中首词条映射: {firstTrait}。", this);
                    }
                    return binding.sprite;
                }
            }

            // 2) 使用代码内置的预设映射（按你提供的 15 张头像）
            Sprite preset = ResolvePresetPortraitByTrait(firstTrait);
            if (preset != null)
            {
                if (logTraitImageResolve)
                {
                    Debug.Log($"[HRRecruitPanel] 结果图命中预设映射: {firstTrait}。", this);
                }
                return preset;
            }

            // 3) 未覆盖词条使用“未标题”头像复用
            if (reusableUntitledPortraits != null && reusableUntitledPortraits.Count > 0)
            {
                int idx = Mathf.Abs((int)firstTrait) % reusableUntitledPortraits.Count;
                Sprite untitled = reusableUntitledPortraits[idx];
                if (untitled != null)
                {
                    if (logTraitImageResolve)
                    {
                        Debug.Log($"[HRRecruitPanel] 首词条 {firstTrait} 使用未标题复用头像索引 {idx}。", this);
                    }
                    return untitled;
                }
            }

            if (logTraitImageResolve)
            {
                Debug.Log($"[HRRecruitPanel] 首词条 {firstTrait} 未配置对应图片，将回退默认图。", this);
            }
        }
        else if (logTraitImageResolve)
        {
            Debug.Log("[HRRecruitPanel] 员工没有词条数据，将回退默认图。", this);
        }

        if (recruitSuccessSprite != null && logTraitImageResolve)
        {
            Debug.Log("[HRRecruitPanel] 使用默认招募结果图。", this);
        }

        return recruitSuccessSprite;
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

    /// <summary>
    /// 应用招募按钮状态：
    /// - 首次打开：只显示 recruitButton（招募新员工）
    /// - 已招募过：同时显示 recruitButton 和 rerollButton（可选替换上一次）
    /// - rerollButton 仅在已招募过时可用
    /// </summary>
    private void ApplyRecruitButtonState(bool showReroll)
    {
        // recruitButton 始终可用（首次招募或继续招募新员工）
        if (recruitButton != null)
        {
            recruitButton.gameObject.SetActive(true);
        }

        // rerollButton 仅在已招募过时显示
        if (rerollButton != null)
        {
            rerollButton.gameObject.SetActive(showReroll);
        }
    }

    private void NotifyWarehouseUiUpdated()
    {
        RoomEmployeeWarehouseUI ui = RoomEmployeeWarehouseUI.Instance;
        if (ui == null)
        {
            return;
        }

        ui.NotifyWarehouseDataChanged();
    }

}
