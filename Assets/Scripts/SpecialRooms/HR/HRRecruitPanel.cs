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
    public Button closeButton;
    public Button recruitButton;
    public Button rerollButton;
    public Button confirmButton;
    public Button cancelButton;
    public Button openWarehouseButton;

    [Header("结果图片")]
    public Image resultImage;
    public Sprite recruitSuccessSprite;
    public List<TraitImageBinding> firstTraitImageBindings = new List<TraitImageBinding>();
    public bool logTraitImageResolve = true;
    public bool hideResultImageOnOpen = true;

    [Header("文本")]
    public TMP_Text titleText ;
    public TMP_Text messageText;
    public TMP_Text nameText;
    public TMP_Text staminaText;
    public TMP_Text intelligenceText;
    public TMP_Text magicText;
    public TMP_Text traitsText;

    void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);
        if (recruitButton != null) recruitButton.onClick.AddListener(OnRecruitClicked);
        if (rerollButton != null) rerollButton.onClick.AddListener(OnRerollClicked);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
        if (openWarehouseButton != null) openWarehouseButton.onClick.AddListener(OpenWarehouse);

        if (root != null) root.SetActive(false);
    }

    public void OpenPanel()
    {
        if (hrRoom != null)
        {
            hrRoom.ResetRecruitSession();
        }

        if (root != null) root.SetActive(true);

        if (titleText != null)
        {
            titleText.text = "HR 招募";
        }

        if (messageText != null)
        {
            if (hrRoom != null)
            {
                messageText.text = $"消耗 {hrRoom.recruitFruitCost} 果实进行招募，确认后才会加入仓库";
            }
            else
            {
                messageText.text = "未绑定 HR 房间";
            }
        }

        if (resultRoot != null) resultRoot.SetActive(false);
        ApplyRecruitButtonState(false);

        if (resultImage != null && hideResultImageOnOpen)
        {
            resultImage.gameObject.SetActive(false);
        }
    }

    public void ClosePanel()
    {
        if (hrRoom != null)
        {
            hrRoom.CancelPendingRecruit();
        }

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
            messageText.text = "已抽到候选鼠鼠。可重抽，或点确定加入仓库。";
        }

        if (resultRoot != null) resultRoot.SetActive(true);
        ApplyRecruitButtonState(true);

        RefreshResult(employee);
        RefreshResultImage(employee);
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
            messageText.text = "已重新抽取候选鼠鼠。可继续重抽，或点确定加入仓库。";
        }

        if (resultRoot != null) resultRoot.SetActive(true);
        ApplyRecruitButtonState(true);

        RefreshResult(employee);
        RefreshResultImage(employee);
    }

    private void OnConfirmClicked()
    {
        if (hrRoom == null)
        {
            if (messageText != null) messageText.text = "未绑定 HR 房间";
            return;
        }

        if (!hrRoom.TryConfirmRecruit(out HREmployeeData employee, out string failReason))
        {
            if (messageText != null) messageText.text = failReason;
            return;
        }

        if (messageText != null)
        {
            messageText.text = "招募成功！鼠鼠已加入仓库。";
        }

        if (resultRoot != null)
        {
            resultRoot.SetActive(true);
        }

        RefreshResult(employee);
        RefreshResultImage(employee);
        ApplyRecruitButtonState(false);
    }

    private void OnCancelClicked()
    {
        if (hrRoom == null)
        {
            if (messageText != null) messageText.text = "未绑定 HR 房间";
            return;
        }

        hrRoom.CancelPendingRecruit();

        if (messageText != null)
        {
            messageText.text = "已取消，本次候选鼠鼠已舍弃。";
        }

        if (resultRoot != null)
        {
            resultRoot.SetActive(false);
        }

        if (resultImage != null)
        {
            resultImage.gameObject.SetActive(false);
        }

        ApplyRecruitButtonState(false);
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

        if (nameText != null) nameText.text = $"姓名: {e.displayName}";
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

    private void ApplyRecruitButtonState(bool hasCandidate)
    {
        if (recruitButton != null)
        {
            recruitButton.gameObject.SetActive(true);
        }

        if (rerollButton != null)
        {
            rerollButton.gameObject.SetActive(hasCandidate);
        }

        if (confirmButton != null)
        {
            confirmButton.gameObject.SetActive(hasCandidate);
        }

        if (cancelButton != null)
        {
            cancelButton.gameObject.SetActive(hasCandidate);
        }
    }

}
