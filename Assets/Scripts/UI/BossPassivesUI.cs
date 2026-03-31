using UnityEngine;
using TMPro;

// TODO: Panel needs to be set up in the scene via Unity Editor or MCP
public class BossPassivesUI : MonoBehaviour
{
    public static BossPassivesUI Instance;

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;

    void Awake() { Instance = this; }

    public void Show(CombinationType resistedCombo)
    {
        if (panel == null) return;
        panel.SetActive(true);
        if (titleText != null) titleText.text = "PASIVAS DEL JEFE";
        if (bodyText != null)
            bodyText.text = $"Resistencia a {resistedCombo}:\n50% menos de da\u00f1o";
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }
}
