using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 员工卡片UI：用于仓库与工作列表，支持点击与悬停回调。
public class RoomEmployeeSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("组件")]
    public Image portraitImage;
    public TMP_Text nameText;
    public TMP_Text statusText;
    public TMP_Text actionText;
    public Button actionButton;
    public Image disabledOverlay;

    private Action _onClick;
    private Action _onHoverEnter;
    private Action _onHoverExit;
    private bool _interactable;

    void Awake()
    {
        if (actionButton == null)
        {
            actionButton = GetComponent<Button>();
        }

        if (actionButton == null)
        {
            actionButton = GetComponentInChildren<Button>(true);
        }

        if (actionButton != null)
        {
            actionButton.onClick.AddListener(HandleClick);
        }
    }

    void OnDestroy()
    {
        if (actionButton != null)
        {
            actionButton.onClick.RemoveListener(HandleClick);
        }
    }

    public void Setup(
        Sprite portrait,
        string displayName,
        string status,
        string actionLabel,
        bool interactable,
        Action onClick,
        Action onHoverEnter,
        Action onHoverExit)
    {
        _onClick = onClick;
        _onHoverEnter = onHoverEnter;
        _onHoverExit = onHoverExit;
        _interactable = interactable;

        if (portraitImage != null)
        {
            portraitImage.sprite = portrait;
            portraitImage.enabled = portrait != null;
        }

        if (nameText != null)
        {
            nameText.text = string.IsNullOrWhiteSpace(displayName) ? "未命名员工" : displayName;
        }

        if (statusText != null)
        {
            statusText.text = status ?? string.Empty;
        }

        if (actionText != null)
        {
            actionText.text = actionLabel ?? string.Empty;
        }

        if (actionButton != null)
        {
            actionButton.interactable = interactable;
        }

        if (disabledOverlay != null)
        {
            disabledOverlay.gameObject.SetActive(!interactable);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _onHoverEnter?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _onHoverExit?.Invoke();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        HandleClick();
    }

    private void HandleClick()
    {
        if (!_interactable)
        {
            return;
        }

        if (actionButton != null && !actionButton.interactable)
        {
            return;
        }

        _onClick?.Invoke();
    }
}
