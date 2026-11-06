using UnityEngine;

[CreateAssetMenu(menuName = "Logic Legends/Commerce Settings")]
public sealed class CommerceSettings : ScriptableObject
{
    [Tooltip("External backend HTTPS URL. Loopback HTTP is allowed only in the Editor.")]
    public string backendUrl = "http://127.0.0.1:18080";
    [Tooltip("Keep enabled until hosting, credentials and database rules are configured.")]
    public bool sandbox = true;
    [Header("Solo Economy v2 — programmer connection")]
    [Tooltip("Leave off while merging. Deploy Backend/commerce, run the read-only Economy connection check, then enable this before a release build. Uses the SAME backend URL, account and wallet above.")]
    public bool soloEconomyConnected = false;
}
