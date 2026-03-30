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

    [Header("Level")]
    [SerializeField] private TMP_Text levelText;

    [Header("Phase Label")]
    [SerializeField] private TMP_Text phaseLabel;
    [SerializeField] private float phaseLabelDuration = 1.5f;

    [Header("Panels")]
    [SerializeField] private GameObject combatPanel;
    [SerializeField] private GameObject crapsOverlay;
    [SerializeField] private GameObject rewardOverlay;
    [SerializeField] private GameObject gameOverOverlay;
    [SerializeField] private GameObject victoryOverlay;
    [SerializeField] private MovementRollUI movementRollUI;
    [SerializeField] private InventoryBuilderUI inventoryBuilderUI;
    [SerializeField] private EnemyInfoUI enemyInfoUI;

    private Coroutine phaseLabelCoroutine;

    void Awake()
    {
        Instance = this;
    }

    public void Initialize(HealthBarUI healthBarRef, EnergyBarUI energyBarRef, ShieldDisplay shieldRef,
        TMP_Text phaseLabelRef, GameObject combatPanelRef, GameObject crapsOverlayRef,
        GameObject rewardOverlayRef, GameObject gameOverOverlayRef, GameObject victoryOverlayRef,
        MovementRollUI movementRollRef = null)
    {
        healthBar = healthBarRef;
        energyBar = energyBarRef;
        shieldDisplay = shieldRef;
        phaseLabel = phaseLabelRef;
        combatPanel = combatPanelRef;
        crapsOverlay = crapsOverlayRef;
        rewardOverlay = rewardOverlayRef;
        gameOverOverlay = gameOverOverlayRef;
        victoryOverlay = victoryOverlayRef;
        movementRollUI = movementRollRef;
    }

    public void SetInventoryBuilderUI(InventoryBuilderUI invBuilderRef)
    {
        inventoryBuilderUI = invBuilderRef;
    }

    public void SetEnemyInfoUI(EnemyInfoUI enemyInfoRef)
    {
        enemyInfoUI = enemyInfoRef;
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

    // --- Level ---

    public void SetLevelText(TMP_Text levelRef)
    {
        levelText = levelRef;
    }

    public void UpdateLevel(int level)
    {
        if (levelText != null) levelText.text = $"Level {level}";
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

    public void ShowInventoryBuilder(System.Collections.Generic.List<DiceInstance> inventory, int slots, float powerBudget)
        => inventoryBuilderUI?.Show(inventory, slots, powerBudget);
    public void HideInventoryBuilder() => inventoryBuilderUI?.Hide();

    public void ShowMovementRollPanel(int min, int max) => movementRollUI?.Show(min, max);
    public void ShowMovementRollResult(int steps) => movementRollUI?.ShowResult(steps);
    public void HideMovementRollPanel() => movementRollUI?.Hide();

    // --- Gold ---

    [SerializeField] private TMPro.TMP_Text goldText;

    public void SetGoldText(TMPro.TMP_Text goldRef) { goldText = goldRef; }

    public void UpdateGold(int amount)
    {
        if (goldText != null) goldText.text = $"{amount} G";
    }

    // --- Dexterity ---

    [SerializeField] private TMPro.TMP_Text _dexterityText;

    public void SetDexterityText(TMPro.TMP_Text dexRef) { _dexterityText = dexRef; }

    public void UpdateDexterity(int value)
    {
        if (_dexterityText != null) _dexterityText.text = $"DEX: {value}";
    }

    // --- Exploration Actions ---

    private ExplorationActionsUI explorationActionsUI;

    public void SetExplorationActionsUI(ExplorationActionsUI ui) { explorationActionsUI = ui; }
    public void ShowExplorationActions() { if (explorationActionsUI != null) explorationActionsUI.Show(); }
    public void HideExplorationActions() { if (explorationActionsUI != null) explorationActionsUI.Hide(); }

    // --- Minimap ---

    private MinimapUI minimapUI;

    public void SetMinimapUI(MinimapUI ui) { minimapUI = ui; }
    public MinimapUI GetMinimapUI() => minimapUI;

    // --- Enemy Info ---

    private EnemyInfoUI secondEnemyInfoUI;

    public void SetSecondEnemyInfoUI(EnemyInfoUI ui) { secondEnemyInfoUI = ui; }

    public void ShowEnemyInfo(EnemyEntity enemy) => enemyInfoUI?.Show(enemy);
    public void HideEnemyInfo()
    {
        enemyInfoUI?.Hide();
        secondEnemyInfoUI?.Hide();
    }
    public void UpdateEnemyHP(int current, int max) => enemyInfoUI?.UpdateHP(current, max);

    public void ShowSecondEnemyInfo(EnemyEntity enemy)
    {
        if (secondEnemyInfoUI != null) secondEnemyInfoUI.Show(enemy);
    }
    public void HideSecondEnemyInfo()
    {
        if (secondEnemyInfoUI != null) secondEnemyInfoUI.Hide();
    }
    public void UpdateSecondEnemyHP(int current, int max)
    {
        if (secondEnemyInfoUI != null) secondEnemyInfoUI.UpdateHP(current, max);
    }

    public EnemyInfoUI GetEnemyInfoUI() => enemyInfoUI;
    public EnemyInfoUI GetSecondEnemyInfoUI() => secondEnemyInfoUI;

    public void ShowTargetSelection(bool show)
    {
        if (enemyInfoUI != null) enemyInfoUI.ShowTargetButton(show);
        if (secondEnemyInfoUI != null) secondEnemyInfoUI.ShowTargetButton(show);
    }

    public void HideAllPanels()
    {
        HideCombatPanel();
        HideCrapsOverlay();
        HideRewardOverlay();
        HideGameOverOverlay();
        HideVictoryOverlay();
        HideMovementRollPanel();
        HideInventoryBuilder();
        HideEnemyInfo();
        HideExplorationActions();
    }

    private void SetPanel(GameObject panel, bool active)
    {
        if (panel != null) panel.SetActive(active);
    }
}
