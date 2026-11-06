using UnityEngine;

/// <summary>Local visual references only. These entries never grant an inventory item.</summary>
public sealed class AdminCharacterCatalog : ScriptableObject
{
    [System.Serializable] public class Entry { public string name; public GameObject prefab; }
    public Entry[] skins;
}
