using UnityEngine;
using UnityEngine.UI;

public class CustomizationItem : MonoBehaviour
{
    public enum ItemType { Clothes, Pets }
    public enum TargetCharacter { Any, MaleOnly, FemaleOnly }

    [Header("Item Details")]
    [SerializeField] private string itemID;             
    public string ItemID => itemID; // Getter so the Manager can read the ID

    [SerializeField] private ItemType itemType;         
    public ItemType Type => itemType; // Getter

    [SerializeField] private TargetCharacter targetCharacter; // NEW
    public TargetCharacter Target => targetCharacter;

    // --- NEW: Checkbox for Default Items ---
    [SerializeField] private bool isDefaultItem;
    public bool IsDefault => isDefaultItem;

    [SerializeField] private CustomizationManager manager; 

    [Header("UI References")]
    [SerializeField] private Button selectButton;
    [SerializeField] private Image buttonImage;
    [SerializeField] private Sprite selectSprite;       
    [SerializeField] private Sprite equippedSprite;     

    public void UpdateUI(bool isOwned, bool isEquipped)
    {
        if (isOwned)
        {
            if (isEquipped)
            {
                buttonImage.sprite = equippedSprite;
                selectButton.interactable = false;
            }
            else
            {
                buttonImage.sprite = selectSprite;
                selectButton.interactable = true;
            }
        }
    }

    public void OnClickSelect()
    {
        manager.EquipItem(this);
    }
}