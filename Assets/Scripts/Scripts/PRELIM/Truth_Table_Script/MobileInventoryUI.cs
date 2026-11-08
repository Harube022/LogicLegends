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

    private void Awake()
    {
        // Own the two existing slot callbacks, including legacy serialized
        // SetSelectedSlotDirectly listeners, so a tap toggles exactly once.
        for(int i=0;i<uiSlots.Length;i++)
        {
            var background=uiSlots[i].slotBackground;
            var button=background!=null?background.GetComponent<UnityEngine.UI.Button>():null;
            if(button==null)continue;
            button.onClick=new UnityEngine.UI.Button.ButtonClickedEvent();
            if(i==0)button.onClick.AddListener(OnClickSlot0);
            else if(i==1)button.onClick.AddListener(OnClickSlot1);
            background.raycastTarget=true;
            if(uiSlots[i].blockIcon!=null)uiSlots[i].blockIcon.raycastTarget=false;
            if(uiSlots[i].blockTypeText!=null)uiSlots[i].blockTypeText.raycastTarget=false;
            if(uiSlots[i].countText!=null)uiSlots[i].countText.raycastTarget=false;
        }
    }

    public void RefreshInventoryDisplay(InventorySlot[] slots, int selectedIndex)
    {
        for (int i = 0; slots!=null&&i < uiSlots.Length&&i<slots.Length; i++)
        {
            if(slots[i]==null)continue;
            bool isSelected = (i == selectedIndex);
            if(uiSlots[i].slotBackground!=null)uiSlots[i].slotBackground.color = isSelected ? selectedColor : normalColor;

            if (slots[i].isEmpty)
            {
                if (uiSlots[i].blockIcon != null)
                    uiSlots[i].blockIcon.gameObject.SetActive(false);
                if(uiSlots[i].blockTypeText!=null)uiSlots[i].blockTypeText.gameObject.SetActive(false);
                if(uiSlots[i].countText!=null){uiSlots[i].countText.text = string.Empty;uiSlots[i].countText.gameObject.SetActive(false);}
            }
            else
            {
                if (uiSlots[i].blockIcon != null)
                {
                    uiSlots[i].blockIcon.sprite = slots[i].blockValue ? trueBlockIcon : falseBlockIcon;
                    uiSlots[i].blockIcon.gameObject.SetActive(uiSlots[i].blockIcon.sprite != null);
                }
                // ALWAYS turn on text components once a block occupies the slot data
                if(uiSlots[i].blockTypeText!=null)uiSlots[i].blockTypeText.gameObject.SetActive(true);
                if(uiSlots[i].countText!=null)uiSlots[i].countText.gameObject.SetActive(true);

                // Assign values safely
                if(uiSlots[i].blockTypeText!=null)uiSlots[i].blockTypeText.text = slots[i].blockValue ? "T" : "F";

                // ---> FIXED: Always show the count string regardless of how many items are in the stack <---
                if(uiSlots[i].countText!=null)uiSlots[i].countText.text = $"x{slots[i].count}";
            }
        }
    }
    // Add these public methods inside MobileInventoryUI.cs

public void OnClickSlot0()
{
    // Tells the inventory manager to swap to Slot 0
    if(InventoryManager.Instance!=null)InventoryManager.Instance.ToggleSelectedSlot(0);
}

public void OnClickSlot1()
{
    // Tells the inventory manager to swap to Slot 1
    if(InventoryManager.Instance!=null)InventoryManager.Instance.ToggleSelectedSlot(1);
}
}
