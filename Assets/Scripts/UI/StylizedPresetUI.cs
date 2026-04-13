using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StylizedPresetUI : MonoBehaviour
{
    Button _button;
    TMP_Text _label;
    StylizedPresetManager _manager;
    bool _subscribed;

    public void Initialize(Button button, TMP_Text label)
    {
        _button = button;
        _label = label;

        if (_button != null)
            _button.onClick.AddListener(OnCyclePresetClicked);

        BindManager();
        RefreshLabel();
    }

    void OnEnable()
    {
        BindManager();
        RefreshLabel();
    }

    void OnDisable()
    {
        UnbindManager();
    }

    void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OnCyclePresetClicked);

        UnbindManager();
    }

    void OnCyclePresetClicked()
    {
        BindManager();
        if (_manager == null)
        {
            RefreshLabel();
            return;
        }

        _manager.CyclePreset();
    }

    void OnPresetChanged(int _, string presetLabel)
    {
        if (_label != null)
            _label.text = $"STYLE: {presetLabel}";
    }

    void BindManager()
    {
        if (_manager == null)
            _manager = StylizedPresetManager.Instance ?? FindAnyObjectByType<StylizedPresetManager>();

        if (_manager != null && !_subscribed)
        {
            _manager.PresetChanged += OnPresetChanged;
            _subscribed = true;
        }
    }

    void UnbindManager()
    {
        if (_manager != null && _subscribed)
        {
            _manager.PresetChanged -= OnPresetChanged;
            _subscribed = false;
        }
    }

    void RefreshLabel()
    {
        if (_label == null)
            return;

        if (_manager != null)
            _label.text = $"STYLE: {_manager.CurrentPresetLabel}";
        else
            _label.text = "STYLE: N/A";
    }
}
