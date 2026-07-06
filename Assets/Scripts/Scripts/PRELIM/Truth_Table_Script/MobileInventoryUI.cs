using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MobileInventoryUI : MonoBehaviour
{
    [System.Serializable]
    public struct SlotUIElements
    {
        public Image slotBackground;
        public TextMeshProUGUI blockTypeText;
        public TextMeshProUGUI countText;
    }

    [SerializeField] private SlotUIElements[] uiSlots = new SlotUIElements[2];
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
                uiSlots[i].blockTypeText.gameObject.SetActive(false);
                uiSlots[i].countText.gameObject.SetActive(false);
            }
            else
            {
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