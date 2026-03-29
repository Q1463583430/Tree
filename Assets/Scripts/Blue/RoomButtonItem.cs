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

        RefreshLabel();

        if (button != null)
        {
            button.onClick.RemoveListener(ClickFromButton);
            button.onClick.AddListener(ClickFromButton);
        }
    }

    public void RefreshLabel()
    {
        if (label != null && roomData != null)
        {
            label.text = roomData.roomName;
        }
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
