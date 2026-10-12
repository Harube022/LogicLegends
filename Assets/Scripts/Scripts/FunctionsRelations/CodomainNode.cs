using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button), typeof(LaserReceiver))]
public sealed class CodomainNode : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    private LaserMappingController owner;
    public string Id { get; private set; }
    public LaserReceiver Receiver { get; private set; }

    public void Initialize(string id, LaserMappingController controller)
    {
        Id = id;
        owner = controller;
        Receiver = GetComponent<LaserReceiver>();
        if (label == null) label = GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = id;
        var button = GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => owner.SelectCodomain(this));
    }
}
