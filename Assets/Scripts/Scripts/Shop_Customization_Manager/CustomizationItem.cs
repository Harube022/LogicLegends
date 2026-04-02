using UnityEngine;
using UnityEngine.UI;

public class CustomizationItem : MonoBehaviour
{
    public enum ItemType { Clothes, Pets }

    [Header("Item Details")]
    [SerializeField] private string itemID;             
    public string ItemID => itemID; // Getter so the Manager can read the ID

    [SerializeField] private ItemType itemType;         
    public ItemType Type => itemType; // Getter

    [SerializeField] private CustomizationManager manager; 

    [Header("UI References")]
    [SerializeField] private Button selectButton;
    [SerializeField] private Image buttonImage;
    [SerializeField] private Sprite selectSprite;       
    [SerializeField] private Sprite equippedSprite;     

    public void UpdateUI(bool isOwned, bool isEquipped)
    {
        if (!isOwned)
        {
            gameObject.SetActive(false); 
        }
        else
        {
            gameObject.SetActive(true);

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