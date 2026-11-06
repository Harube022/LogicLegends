using System.Collections.Generic;
using UnityEngine;

/// <summary>Truth-value selection and the four physical blocks carried in this scene.</summary>
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }
    public const int CarryLimit = 4;
    [SerializeField] private InventorySlot[] slots = new InventorySlot[2];
    [Header("Carry weight: one, two, three, four blocks")]
    [SerializeField] private float[] carryingSpeedMultipliers = { .95f, .88f, .8f, .75f };
    [SerializeField, Min(0)] private float stackGap = .025f;
    [SerializeField, Min(.02f)] private float stackCompactionSeconds = .14f;
    [Header("Mobile UI References")]
    [SerializeField] private MobileInventoryUI uiController;
    [SerializeField] private GameObject inventoryBarUI;

    readonly List<TruthBlock> carried = new List<TruthBlock>();
    int selectedSlotIndex = -1;
    Player carrier;
    TruthTableCarryStack stack;
    bool clearing;
    public int CarriedCount => carried.Count;
    public int EquippedCount => HasBlockSelected() ? slots[selectedSlotIndex].count : 0;
    public bool HasStoredBlocks => CarriedCount > 0;
    public IReadOnlyList<TruthBlock> CarriedBlocks => carried;
    public bool Contains(TruthBlock block) => block != null && carried.Contains(block);
    public bool IsEquipped(TruthBlock block) => HasBlockSelected() && slots[selectedSlotIndex].physicalBlocks.Contains(block);
    public float StackGap => stackGap;
    public float CompactionSeconds => stackCompactionSeconds;
    public float CarrySpeedMultiplier => EquippedCount == 0 ? 1 : carryingSpeedMultipliers[Mathf.Clamp(EquippedCount-1,0,3)];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Instance = null; }
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        ValidateConfiguration();
        if (slots == null || slots.Length != 2) slots = new InventorySlot[2];
        for (int i=0;i<slots.Length;i++) { if(slots[i]==null)slots[i]=new InventorySlot(); slots[i].Clear(); }
        SetInventoryVisibility(false);
        UpdateUI();
    }
    void OnValidate() { ValidateConfiguration(); }
    void ValidateConfiguration()
    {
        if(carryingSpeedMultipliers==null||carryingSpeedMultipliers.Length!=4)
            carryingSpeedMultipliers=new[]{.95f,.88f,.8f,.75f};
        float previous=1f;
        for(int i=0;i<4;i++)
        { carryingSpeedMultipliers[i]=Mathf.Clamp(carryingSpeedMultipliers[i],.05f+(3-i)*.01f,previous-.01f); previous=carryingSpeedMultipliers[i]; }
    }
    void LateUpdate()
    {
        if (clearing || Instance != this) return;
        bool removed=carried.RemoveAll(b=>b==null)>0;
        if(carrier!=Player.LocalInstance || stack==null) RefreshCarry();
        else if(removed) RefreshCarry();
    }
    public int TryPickupBlock(bool value,TruthBlock block)
    {
        if(block==null || block.value!=value || clearing)return -1;
        int existing=SlotOf(block);
        if(existing>=0) { selectedSlotIndex=existing; RefreshCarry(); return existing; }
        if(carried.Count>=CarryLimit)return -1;
        int index=-1;
        for(int i=0;i<slots.Length;i++)if(!slots[i].isEmpty&&slots[i].blockValue==value){index=i;break;}
        if(index<0)for(int i=0;i<slots.Length;i++)if(slots[i].isEmpty){index=i;break;}
        if(index<0||!slots[index].TryAdd(value,block))return -1;
        carried.Add(block);
        block.MarkCollectedFromSpawn();
        selectedSlotIndex=index;
        RefreshCarry();
        return index;
    }
    public bool TryStoreReturnedBlock(TruthBlock block) => block!=null && TryPickupBlock(block.value,block)>=0;
    int SlotOf(TruthBlock block)
    {
        for(int i=0;i<slots.Length;i++)if(slots[i].physicalBlocks.Contains(block))return i;
        return -1;
    }
    public bool TryRemoveBlock(TruthBlock block)
    {
        if(block==null)return false;
        bool removed=carried.Remove(block);
        foreach(var slot in slots)removed|=slot.Remove(block);
        if(!removed)return false;
        stack?.Release(block);
        if(!clearing)RefreshCarry();
        return true;
    }
    void RefreshCarry()
    {
        carried.RemoveAll(b=>b==null);
        foreach(var slot in slots)slot.Synchronize();
        if(selectedSlotIndex<0 || selectedSlotIndex>=slots.Length || slots[selectedSlotIndex].isEmpty)
            selectedSlotIndex=-1;
        var next=Player.LocalInstance;
        if(carrier!=next)
        {
            if(carrier!=null){carrier.RemoveMovementSpeedModifier(this);carrier.SetRunningSuppressed(this,false);}
            if(stack!=null)stack.Clear();
            carrier=next;stack=null;
        }
        if(carrier!=null)
        {
            if(stack==null){stack=carrier.GetComponent<TruthTableCarryStack>();if(stack==null)stack=carrier.gameObject.AddComponent<TruthTableCarryStack>();}
            stack.Synchronize(this,carried);
            carrier.SetMovementSpeedModifier(this,CarrySpeedMultiplier);
            carrier.SetRunningSuppressed(this,EquippedCount>0);
            var selected=HasBlockSelected()?slots[selectedSlotIndex].physicalBlocks[slots[selectedSlotIndex].count-1]:null;
            carrier.SetHeldObjectSilently(selected!=null?selected.GetComponent<GrabbableObject>():null);
        }
        UpdateUI();
    }
    // Pickup/placement callers equip explicitly; repeated calls must not toggle.
    public void SetSelectedSlotDirectly(int index)
    {
        if(index<0||index>=slots.Length||slots[index].isEmpty)return;
        selectedSlotIndex=index;RefreshCarry();
    }
    public void ToggleSelectedSlot(int index)
    {
        if(index<0||index>=slots.Length||slots[index].isEmpty)return;
        selectedSlotIndex=selectedSlotIndex==index?-1:index;
        RefreshCarry();
    }
    public void ClearSelectionSilently() { selectedSlotIndex=-1; RefreshCarry(); }
    public bool HasBlockSelected()=>selectedSlotIndex>=0&&selectedSlotIndex<slots.Length&&!slots[selectedSlotIndex].isEmpty;
    public bool GetSelectedBlockValue()=>HasBlockSelected()&&slots[selectedSlotIndex].blockValue;
    public void DropSelectedBlock()
    {
        if(!HasBlockSelected())return;
        var slot=slots[selectedSlotIndex];
        var block=slot.physicalBlocks[slot.physicalBlocks.Count-1];
        TryRemoveBlock(block);
        var item=block.GetComponent<GrabbableObject>();
        item.ReleaseForPlacement();
        block.transform.SetParent(null,true);
        if(carrier!=null)block.transform.SetPositionAndRotation(carrier.transform.position+carrier.transform.forward*2+Vector3.up,Quaternion.identity);
        PlayerItemRelease.Protect(block.transform,carrier,true);
        item.ConfigureInventoryState(false,null,false);
        var body=block.GetComponent<Rigidbody>();
        if(body!=null){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;body.interpolation=RigidbodyInterpolation.Interpolate;}
        RefreshCarry();
    }
    // Kept for older callers; detach the consumed identity atomically.
    public void ConsumeSelectedBlock()
    {
        if(!HasBlockSelected())return;
        var slot=slots[selectedSlotIndex];var block=slot.physicalBlocks[slot.count-1];
        block.GetComponent<GrabbableObject>()?.ReleaseForPlacement();TryRemoveBlock(block);
    }
    public void ResetInventory()
    {
        clearing=true;
        var previous=carried.ToArray();carried.Clear();
        if(carrier!=null){carrier.RemoveMovementSpeedModifier(this);carrier.SetRunningSuppressed(this,false);carrier.SetHeldObjectSilently(null);}
        if(stack!=null)stack.Clear();
        foreach(var slot in slots)slot?.Clear();
        selectedSlotIndex=-1;
        foreach(var block in previous)if(block!=null){block.gameObject.SetActive(false);Destroy(block.gameObject);}
        clearing=false;UpdateUI();
    }
    void OnDisable() { if(Instance==this)ResetInventory(); }
    void OnDestroy() { if(Instance==this){ResetInventory();Instance=null;} }
    void UpdateUI() { if(uiController!=null)uiController.RefreshInventoryDisplay(slots,selectedSlotIndex); }
    public void SetInventoryVisibility(bool visible) { if(inventoryBarUI!=null)inventoryBarUI.SetActive(visible); }
}
