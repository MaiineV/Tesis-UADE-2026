using UnityEngine;
using TMPro;

public class BossPassivesUI : MonoBehaviour
{
    public static BossPassivesUI Instance;

    private GameObject _panel;
    private TMP_Text _titleText;
    private TMP_Text _bodyText;

    void Awake() { Instance = this; }

    public void Initialize(GameObject panel, TMP_Text titleText, TMP_Text bodyText)
    {
        _panel = panel;
        _titleText = titleText;
        _bodyText = bodyText;
    }

    public void Show(CombinationType resistedCombo)
    {
        if (_panel == null) return;
        _panel.SetActive(true);
        if (_titleText != null) _titleText.text = "PASIVAS DEL JEFE";
        if (_bodyText != null)
            _bodyText.text = $"Resistencia a {resistedCombo}:\n50% menos de da\u00f1o";
    }

    public void Hide()
    {
        if (_panel != null) _panel.SetActive(false);
    }
}
