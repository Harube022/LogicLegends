using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Menu navigation only. Settings controls are intentionally unconnected.</summary>
public sealed class MainMenuSettingsTabs : MonoBehaviour
{
    [SerializeField] private GameObject[] pages;
    [SerializeField] private Button[] tabs;
    [SerializeField] private Graphic[] selectedFaces;
    [SerializeField] private TMP_Text[] labels;
    private UnityAction[] listeners;
    public int CurrentSection { get; private set; }

    public void Configure(GameObject[] sectionPages, Button[] sectionTabs,
        Graphic[] faces, TMP_Text[] sectionLabels)
    {
        pages = sectionPages;
        tabs = sectionTabs;
        selectedFaces = faces;
        labels = sectionLabels;
        ShowSection(0);
    }

    private void OnEnable()
    {
        if (tabs == null) return;
        listeners = new UnityAction[tabs.Length];
        for (int index = 0; index < tabs.Length; index++)
        {
            int section = index;
            listeners[index] = () => ShowSection(section);
            if (tabs[index]) tabs[index].onClick.AddListener(listeners[index]);
        }
        ShowSection(0);
    }

    private void OnDisable()
    {
        if (tabs == null || listeners == null) return;
        for (int index = 0; index < tabs.Length && index < listeners.Length; index++)
            if (tabs[index]) tabs[index].onClick.RemoveListener(listeners[index]);
        listeners = null;
    }

    public void ShowSection(int index)
    {
        if (pages == null || index < 0 || index >= pages.Length) return;
        CurrentSection = index;
        for (int section = 0; section < pages.Length; section++)
        {
            bool selected = section == index;
            if (pages[section]) pages[section].SetActive(selected);
            if (selectedFaces != null && section < selectedFaces.Length && selectedFaces[section])
                selectedFaces[section].gameObject.SetActive(selected);
            if (labels != null && section < labels.Length && labels[section])
                labels[section].color = selected ? new Color32(255, 242, 212, 255) : new Color32(55, 32, 15, 255);
        }
    }
}
