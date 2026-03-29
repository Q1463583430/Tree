using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RoomButtonItem : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, IPointerExitHandler, IPointerDownHandler
{
    public Button button;
    public TMP_Text label;

    private RoomDefinition roomData;
    private BluePrint owner;

    public void Init(RoomDefinition room, BluePrint manager)
    {
        roomData = room;
        owner = manager;

        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (label == null)
        {
            label = GetComponentInChildren<TMP_Text>(true);
        }

        RefreshLabel();

        if (button != null)
        {
            button.onClick.RemoveListener(ClickFromButton);
            button.onClick.AddListener(ClickFromButton);
        }
    }

    public void RefreshLabel()
    {
        if (label == null)
        {
            label = GetComponentInChildren<TMP_Text>(true);
        }

        if (label == null)
        {
            return;
        }

        if (roomData == null)
        {
            label.text = string.Empty;
            return;
        }

        string displayName = roomData.roomName != null ? roomData.roomName.Trim() : string.Empty;
        if (string.IsNullOrEmpty(displayName) && roomData.blockPrefab != null)
        {
            displayName = roomData.blockPrefab.name;
        }

        label.text = string.IsNullOrEmpty(displayName) ? "Unnamed Room" : displayName;
    }

    private void OnEnable()
    {
        RefreshLabel();
    }

    private void OnValidate()
    {
        RefreshLabel();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner != null && roomData != null)
        {
            owner.OnRoomHovered(roomData);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        ClickFromButton();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        if (owner != null && roomData != null)
        {
            owner.OnRoomPointerDown(roomData);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (owner != null && roomData != null)
        {
            owner.OnRoomHoverExit(roomData);
        }
    }

    private void ClickFromButton()
    {
        if (owner != null && roomData != null)
        {
            owner.OnRoomClicked(roomData);
        }
    }
}
