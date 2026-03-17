using UnityEngine;
using UnityEngine.UI;

public class VolumeSliderUI : MonoBehaviour
{
    private Slider volumeSlider;

    public void Initialize(Slider slider)
    {
        volumeSlider = slider;
        volumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1f);
        volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
    }

    private void OnVolumeChanged(float value)
    {
        AudioManager.SetVolume(value);
    }
}
