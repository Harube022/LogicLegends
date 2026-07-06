using UnityEngine;
using System.Collections.Generic;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("Inventory Configuration")]
    [SerializeField] private InventorySlot[] slots = new InventorySlot[2];
    private int selectedSlotIndex = -1; 

    [Header("Mobile UI References")]
    [SerializeField] private MobileInventoryUI uiController; 

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        UpdateUI();
    }

    public int TryPickupBlock(bool isTrueBlock, TruthBlock blockInstance)
    {
        // Prevent duplicate counting if re-grabbing the same block
        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i].isEmpty && slots[i].physicalBlocks.Contains(blockInstance))
            {
                return i; 
            }
        }

        // 1. Try to stack into an existing slot matching the value
        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i].isEmpty && slots[i].blockValue == isTrueBlock)
            {
                if (slots[i].TryAdd(isTrueBlock, blockInstance))
                {
                    SetSelectedSlotDirectly(i); 
                    return i;
                }
            }
        }

        // 2. Try to find an empty slot
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].isEmpty)
            {
                if (slots[i].TryAdd(isTrueBlock, blockInstance))
                {
                    SetSelectedSlotDirectly(i); 
                    return i;
                }
            }
        }

        return -1; // Inventory full
    }

    public bool TryRemoveBlock(TruthBlock blockInstance)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].physicalBlocks.Contains(blockInstance))
            {
                slots[i].physicalBlocks.Remove(blockInstance);
                slots[i].count = slots[i].physicalBlocks.Count;
                
                if (slots[i].count <= 0)
                {
                    slots[i].isEmpty = true;
                    if (selectedSlotIndex == i) selectedSlotIndex = -1;
                }

                UpdateBlockVisibility();
                UpdateUI();
                return true;
            }
        }
        return false;
    }

    private void UpdateBlockVisibility()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            bool isCurrentSlotSelected = (i == selectedSlotIndex);

            // If the slot is completely empty, make sure any loose lingering references are cleaned up and hidden
            if (slots[i].isEmpty)
            {
                foreach (TruthBlock block in slots[i].physicalBlocks)
                {
                    if (block == null) continue;
                    
                    if (block.TryGetComponent(out MeshRenderer renderer)) renderer.enabled = false;
                    block.gameObject.SetActive(false);
                    block.transform.SetParent(null);
                }
                continue; 
            }

            // If the slot is NOT empty, handle normal showing/hiding states
            foreach (TruthBlock block in slots[i].physicalBlocks)
            {
                if (block == null) continue;

                if (isCurrentSlotSelected)
                {
                    // 1. SHOW SELECTED BLOCK IN HAND
                    block.gameObject.SetActive(true);
                    if (block.TryGetComponent(out MeshRenderer renderer)) renderer.enabled = true;

                    if (block.TryGetComponent(out GrabbableObject grabbable))
                    {
                        Transform playerHoldPoint = Player.LocalInstance != null ? Player.LocalInstance.HoldPoint : null;
                        grabbable.ConfigureInventoryState(true, playerHoldPoint, true);
                    }

                    if (Player.LocalInstance != null)
                    {
                        block.transform.SetParent(Player.LocalInstance.HoldPoint);
                        block.transform.localPosition = Vector3.zero;
                        block.transform.localRotation = Quaternion.identity;
                    }
                }
                else
                {
                    // 2. HIDE UNSELECTED SLOT BLOCKS
                    if (block.TryGetComponent(out MeshRenderer renderer)) renderer.enabled = false;

                    if (block.TryGetComponent(out GrabbableObject grabbable))
                    {
                        grabbable.ConfigureInventoryState(false, null, true);
                    }

                    block.transform.SetParent(null);
                    block.gameObject.SetActive(false); // Fully deactivate object so it disappears
                }
            }
        }
    }

    public void DropSelectedBlock()
    {
        if (selectedSlotIndex == -1 || slots[selectedSlotIndex].isEmpty)
        {
            Debug.LogWarning("Cannot drop: No slot selected or slot is empty.");
            return;
        }

        InventorySlot activeSlot = slots[selectedSlotIndex];
        TruthBlock blockToDrop = activeSlot.Consume();

        if (blockToDrop != null)
        {
            GameObject blockObj = blockToDrop.gameObject;

            // ---> ADD THIS CRITICAL LINE TO UNTETHER IT FROM THE HAND <---
            blockObj.transform.SetParent(null); 

            if (Player.LocalInstance != null)
            {
                Vector3 dropPosition = Player.LocalInstance.transform.position + (Player.LocalInstance.transform.forward * 1.5f) + (Vector3.up * 1f);
                blockObj.transform.position = dropPosition;
                blockObj.transform.rotation = Quaternion.identity;

                Player.LocalInstance.SetHeldObjectSilently(null);
            }

            // Reactivate components
            if (blockObj.TryGetComponent(out MeshRenderer renderer)) renderer.enabled = true;
            if (blockObj.TryGetComponent(out Collider col)) col.enabled = true;
            
            if (blockObj.TryGetComponent(out Rigidbody rb))
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                if (Player.LocalInstance != null)
                {
                    rb.AddForce(Player.LocalInstance.transform.forward * 2f, ForceMode.VelocityChange);
                }
            }

            if (blockObj.TryGetComponent(out GrabbableObject grabbable))
            {
                grabbable.ConfigureInventoryState(false, null, false);
            }
        }

        if (activeSlot.isEmpty)
        {
            selectedSlotIndex = -1;
        }

        UpdateBlockVisibility();
        UpdateUI();
    }

    public void SetSelectedSlotDirectly(int index)
    {
        // ---> NEW TOGGLE LOGIC <---
        // If the slot clicked is already selected, unselect it (set to -1) to hide the block
        if (selectedSlotIndex == index)
        {
            selectedSlotIndex = -1;
        }
        else
        {
            // Otherwise, select the new slot normally
            selectedSlotIndex = index;
        }
        
        // CRITICAL: Force the player's held object to match the new slot
        if (Player.LocalInstance != null)
        {
            if (selectedSlotIndex != -1 && !slots[selectedSlotIndex].isEmpty)
            {
                // Set the player's hand to the first block in the stack
                GrabbableObject obj = slots[selectedSlotIndex].physicalBlocks[0].GetComponent<GrabbableObject>();
                Player.LocalInstance.SetHeldObjectSilently(obj);
            }
            else
            {
                // Nothing selected, clear the hand
                Player.LocalInstance.SetHeldObjectSilently(null);
            }
        }

        UpdateBlockVisibility(); 
        UpdateUI();
    }

    public void ClearSelectionSilently()
    {
        selectedSlotIndex = -1;
        UpdateBlockVisibility();
        UpdateUI();
    }

    public bool HasBlockSelected() => selectedSlotIndex != -1 && !slots[selectedSlotIndex].isEmpty;
    public bool GetSelectedBlockValue() => slots[selectedSlotIndex].blockValue;

    public void ConsumeSelectedBlock()
    {
        if (selectedSlotIndex != -1)
        {
            slots[selectedSlotIndex].Consume();
            UpdateBlockVisibility();
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        if (uiController != null)
        {
            uiController.RefreshInventoryDisplay(slots, selectedSlotIndex);
        }
    }
}