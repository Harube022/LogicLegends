using UnityEngine;

[CreateAssetMenu(menuName = "Logic Legends/Commerce Settings")]
public sealed class CommerceSettings : ScriptableObject
{
    [Tooltip("External backend HTTPS URL. Loopback HTTP is allowed only in the Editor.")]
    public string backendUrl = "http://127.0.0.1:18080";
    [Tooltip("Keep enabled until hosting, credentials and database rules are configured.")]
    public bool sandbox = true;
}
