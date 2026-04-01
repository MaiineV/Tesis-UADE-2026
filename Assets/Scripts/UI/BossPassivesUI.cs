using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class BossPassivesUI : MonoBehaviour
{
    public static BossPassivesUI Instance;

    private GameObject _panel;
    private TMP_Text _titleText;
    private TMP_Text _bodyText;
    private RectTransform _panelRT;

    void Awake() { Instance = this; }

    public void Initialize(GameObject panel, TMP_Text titleText, TMP_Text bodyText)
    {
        _panel = panel;
        _titleText = titleText;
        _bodyText = bodyText;
        if (_panel != null)
            _panelRT = _panel.GetComponent<RectTransform>();
    }

    public void Show(CombinationType resistedCombo)
    {
        if (_panel == null) return;
        _panel.SetActive(true);
        if (_titleText != null) _titleText.text = "PASIVAS DEL JEFE";
        if (_bodyText != null)
            _bodyText.text = $"Resistencia a {resistedCombo}:\n50% menos de da\u00f1o";
    }

    public void Show(List<BossDebuffData> debuffs)
    {
        if (_panel == null || debuffs == null || debuffs.Count == 0) return;
        _panel.SetActive(true);
        if (_titleText != null) _titleText.text = "PASIVAS DEL JEFE";

        if (_bodyText != null)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < debuffs.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append($"\u2022 {debuffs[i].Title}: {debuffs[i].Description}");
            }
            _bodyText.text = sb.ToString();
        }

        // Resize panel to fit debuff count
        if (_panelRT != null)
        {
            float height = 60f + debuffs.Count * 25f;
            _panelRT.sizeDelta = new Vector2(_panelRT.sizeDelta.x, height);
        }
    }

    public void Hide()
    {
        if (_panel != null) _panel.SetActive(false);
    }
}
