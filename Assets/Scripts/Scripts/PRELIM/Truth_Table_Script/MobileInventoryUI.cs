using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MobileInventoryUI : MonoBehaviour
{
    [System.Serializable]
    public struct SlotUIElements
    {
        public Image slotBackground;
        public Image blockIcon;
        public TextMeshProUGUI blockTypeText;
        public TextMeshProUGUI countText;
    }

    [SerializeField] private SlotUIElements[] uiSlots = new SlotUIElements[2];
    [Header("Block Icons")]
    [SerializeField] private Sprite trueBlockIcon;
    [SerializeField] private Sprite falseBlockIcon;
    [SerializeField] private Color selectedColor = Color.green;
    [SerializeField] private Color normalColor = Color.white;

    public void RefreshInventoryDisplay(InventorySlot[] slots, int selectedIndex)
    {
        for (int i = 0; i < uiSlots.Length; i++)
        {
            bool isSelected = (i == selectedIndex);
            uiSlots[i].slotBackground.color = isSelected ? selectedColor : normalColor;

            if (slots[i].isEmpty)
            {
                if (uiSlots[i].blockIcon != null)
                    uiSlots[i].blockIcon.gameObject.SetActive(false);
                uiSlots[i].blockTypeText.gameObject.SetActive(false);
                uiSlots[i].countText.text = string.Empty;
                uiSlots[i].countText.gameObject.SetActive(false);
            }
            else
            {
                if (uiSlots[i].blockIcon != null)
                {
                    uiSlots[i].blockIcon.sprite = slots[i].blockValue ? trueBlockIcon : falseBlockIcon;
                    uiSlots[i].blockIcon.gameObject.SetActive(uiSlots[i].blockIcon.sprite != null);
                }
                // ALWAYS turn on text components once a block occupies the slot data
                uiSlots[i].blockTypeText.gameObject.SetActive(true);
                uiSlots[i].countText.gameObject.SetActive(true);

                // Assign values safely
                uiSlots[i].blockTypeText.text = slots[i].blockValue ? "T" : "F";

                // ---> FIXED: Always show the count string regardless of how many items are in the stack <---
                uiSlots[i].countText.text = $"x{slots[i].count}";
            }
        }
    }
    // Add these public methods inside MobileInventoryUI.cs

public void OnClickSlot0()
{
    // Tells the inventory manager to swap to Slot 0
    InventoryManager.Instance.SetSelectedSlotDirectly(0);
}

public void OnClickSlot1()
{
    // Tells the inventory manager to swap to Slot 1
    InventoryManager.Instance.SetSelectedSlotDirectly(1);
}
}
