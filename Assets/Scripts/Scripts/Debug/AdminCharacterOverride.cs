using Photon.Pun;
using UnityEngine;

/// <summary>Disposable, local Admin preview. Account equipment and network identity are untouched.</summary>
[DisallowMultipleComponent]
public sealed class AdminCharacterOverride : MonoBehaviour
{
    GameObject visual;
    PlayerEquipmentLoader equipment;
    public string SelectedName { get; private set; }
    public int SelectedIndex { get; private set; } = -1;
    public static AdminCharacterCatalog Catalog => Resources.Load<AdminCharacterCatalog>("AdminCharacterCatalog");

    public bool Select(int index)
    {
        var view = GetComponent<PhotonView>();
        if (!AdminAuthorization.IsAuthorized || PhotonNetwork.InRoom && view != null && !view.IsMine) return false;
        return ApplyVisual(index);
    }
    bool ApplyVisual(int index)
    {
        var catalog = Catalog;
        if (catalog == null || catalog.skins == null || catalog.skins.Length == 0) return false;
        index = (index % catalog.skins.Length + catalog.skins.Length) % catalog.skins.Length;
        var entry = catalog.skins[index]; if (entry.prefab == null) return false;
        equipment = GetComponent<PlayerEquipmentLoader>();
        if (equipment == null) return false;
        equipment.SetDebugCharacterActive(true);
        if (visual != null) { visual.SetActive(false); Destroy(visual); }
        visual = Instantiate(entry.prefab, transform, false);
        visual.name = "Admin Character Preview — " + entry.name;
        var controller = GetComponent<CharacterController>();
        float foot = controller != null ? controller.center.y - controller.height * .5f + controller.skinWidth : 0;
        visual.transform.localPosition = new Vector3(0, foot, 0);
        visual.transform.localRotation = Quaternion.identity;
        // Preserve the prefab's prepared uniform scale; no new player/collider is created.
        SelectedIndex = index; SelectedName = entry.name;
        GetComponent<PlayerVisibilityController>()?.RefreshRenderers();
        return true;
    }
    public void RestoreEquipped()
    {
        if (visual != null) { visual.SetActive(false); Destroy(visual); visual = null; }
        if (equipment != null) equipment.SetDebugCharacterActive(false);
        SelectedName = null; SelectedIndex = -1;
        GetComponent<PlayerVisibilityController>()?.RefreshRenderers();
    }
    void Update() { if (visual != null && !AdminAuthorization.IsAuthorized) RestoreEquipped(); }
    void OnDisable() { RestoreEquipped(); }
}
