using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Scene-authored settings controls, shared by the menu and gameplay pause panels.</summary>
public sealed class AudioVolumePanel : MonoBehaviour
{
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider soundFXSlider;
    [SerializeField] private TMP_Text musicValue;
    [SerializeField] private TMP_Text soundFXValue;

    private void OnEnable()
    {
        var modal = GetComponent<Canvas>();
        if (modal != null) { modal.overrideSorting = true; modal.sortingOrder = 30000; }
        if (musicSlider != null) musicSlider.onValueChanged.AddListener(MusicChanged);
        if (soundFXSlider != null) soundFXSlider.onValueChanged.AddListener(SoundFXChanged);
        AudioVolumeSettings.Changed += Refresh;
        Refresh();
    }
    private void OnDisable()
    {
        if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(MusicChanged);
        if (soundFXSlider != null) soundFXSlider.onValueChanged.RemoveListener(SoundFXChanged);
        AudioVolumeSettings.Changed -= Refresh;
        AudioVolumeSettings.Save();
    }
    private void MusicChanged(float value) { AudioVolumeSettings.SetMusicVolume(value / 100f); }
    private void SoundFXChanged(float value) { AudioVolumeSettings.SetSoundFXVolume(value / 100f); }
    private void Refresh()
    {
        int music = Mathf.RoundToInt(AudioVolumeSettings.MusicVolume * 100f);
        int soundFX = Mathf.RoundToInt(AudioVolumeSettings.SoundFXVolume * 100f);
        if (musicSlider != null) musicSlider.SetValueWithoutNotify(music);
        if (soundFXSlider != null) soundFXSlider.SetValueWithoutNotify(soundFX);
        if (musicValue != null) musicValue.text = music + "%";
        if (soundFXValue != null) soundFXValue.text = soundFX + "%";
    }
}