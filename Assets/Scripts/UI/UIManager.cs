using UnityEngine;
using TMPro;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("HUD")]
    [SerializeField] private HealthBarUI healthBar;
    [SerializeField] private EnergyBarUI energyBar;
    [SerializeField] private ShieldDisplay shieldDisplay;

    [Header("Phase Label")]
    [SerializeField] private TMP_Text phaseLabel;
    [SerializeField] private float phaseLabelDuration = 1.5f;

    [Header("Panels")]
    [SerializeField] private GameObject combatPanel;
    [SerializeField] private GameObject crapsOverlay;
    [SerializeField] private GameObject rewardOverlay;
    [SerializeField] private GameObject gameOverOverlay;
    [SerializeField] private GameObject victoryOverlay;

    private Coroutine phaseLabelCoroutine;

    void Awake()
    {
        Instance = this;
    }

    // --- HUD forwarding ---

    public void UpdateHP(int current, int max)
    {
        if (healthBar != null) healthBar.UpdateHP(current, max);
    }

    public void UpdateEnergy(float normalized)
    {
        if (energyBar != null) energyBar.UpdateEnergy(normalized);
    }

    public void UpdateShield(int value)
    {
        if (shieldDisplay != null) shieldDisplay.UpdateShield(value);
    }

    // --- Phase Label ---

    public void ShowPhaseLabel(string text)
    {
        if (phaseLabel == null) return;

        if (phaseLabelCoroutine != null)
            StopCoroutine(phaseLabelCoroutine);

        phaseLabelCoroutine = StartCoroutine(ShowPhaseLabelRoutine(text));
    }

    private IEnumerator ShowPhaseLabelRoutine(string text)
    {
        phaseLabel.text = text;
        phaseLabel.gameObject.SetActive(true);
        yield return new WaitForSeconds(phaseLabelDuration);
        phaseLabel.gameObject.SetActive(false);
        phaseLabelCoroutine = null;
    }

    // --- Panel show/hide ---

    public void ShowCombatPanel() => SetPanel(combatPanel, true);
    public void HideCombatPanel() => SetPanel(combatPanel, false);

    public void ShowCrapsOverlay() => SetPanel(crapsOverlay, true);
    public void HideCrapsOverlay() => SetPanel(crapsOverlay, false);

    public void ShowRewardOverlay() => SetPanel(rewardOverlay, true);
    public void HideRewardOverlay() => SetPanel(rewardOverlay, false);

    public void ShowGameOverOverlay() => SetPanel(gameOverOverlay, true);
    public void HideGameOverOverlay() => SetPanel(gameOverOverlay, false);

    public void ShowVictoryOverlay() => SetPanel(victoryOverlay, true);
    public void HideVictoryOverlay() => SetPanel(victoryOverlay, false);

    public void HideAllPanels()
    {
        HideCombatPanel();
        HideCrapsOverlay();
        HideRewardOverlay();
        HideGameOverOverlay();
        HideVictoryOverlay();
    }

    private void SetPanel(GameObject panel, bool active)
    {
        if (panel != null) panel.SetActive(active);
    }
}
