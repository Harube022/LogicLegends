using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button), typeof(LaserEmitter))]
public sealed class DomainNode : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    private Button button;
    private LaserMappingController owner;
    public string Id { get; private set; }
    public LaserEmitter Emitter { get; private set; }

    public void Initialize(string id, LaserMappingController controller)
    {
        Id = id;
        owner = controller;
        button = GetComponent<Button>();
        Emitter = GetComponent<LaserEmitter>();
        if (label == null) label = GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = id;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => owner.SelectDomain(this));
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        var visual = transform.Find("Crystal Visual");
        var image = visual != null ? visual.GetComponent<Image>() : GetComponent<Image>();
        if (image != null) image.color = selected ? new Color(1f, .78f, .3f, 1f) : new Color(.16f, .43f, .66f, 1f);
    }
}
