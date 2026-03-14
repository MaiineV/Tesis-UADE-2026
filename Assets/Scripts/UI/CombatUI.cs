using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CombatUI : MonoBehaviour
{
    public static CombatUI Instance;

    [Header("Attack — Dice Display")]
    [SerializeField] private Transform diceContainer;
    [SerializeField] private GameObject dicePrefab;

    [Header("Attack — Combo Preview")]
    [SerializeField] private TMP_Text comboPreviewText;

    [Header("Attack — Roll Counter")]
    [SerializeField] private TMP_Text rollCounterText;

    [Header("Attack — Buttons")]
    [SerializeField] private Button rerollButton;
    [SerializeField] private Button commitButton;

    [Header("Attack Panel")]
    [SerializeField] private GameObject attackPanel;

    [Header("Defense")]
    [SerializeField] private GameObject defensePanel;
    [SerializeField] private TMP_Text defenseTitle;
    [SerializeField] private TMP_Text defenseRollsText;
    [SerializeField] private TMP_Text defenseResultText;
    [SerializeField] private TMP_Text defenseShieldText;
    [SerializeField] private Button rollDefenseButton;

    [Header("Enemy Attack")]
    [SerializeField] private GameObject enemyAttackPanel;
    [SerializeField] private TMP_Text enemyAttackTitle;
    [SerializeField] private TMP_Text enemyRollText;
    [SerializeField] private TMP_Text shieldAbsorbText;
    [SerializeField] private TMP_Text netDamageText;
    [SerializeField] private Button continueButton;

    private DieSlotUI[] dieSlots;
    private RollResult[] currentResults;

    public event Action<string> OnDieLockToggled;
    public event Action OnRerollClicked;
    public event Action OnCommitClicked;
    public event Action OnRollDefenseClicked;
    public event Action OnContinueClicked;

    void Awake()
    {
        Instance = this;
        WireButtons();
    }

    public void Initialize(
        Transform diceContainerRef, GameObject dicePrefabRef,
        TMP_Text comboPreviewRef, TMP_Text rollCounterRef,
        Button rerollRef, Button commitRef,
        GameObject attackPanelRef,
        GameObject defensePanelRef, TMP_Text defenseTitleRef,
        TMP_Text defenseRollsRef, TMP_Text defenseResultRef,
        TMP_Text defenseShieldRef, Button rollDefenseRef,
        GameObject enemyAttackPanelRef, TMP_Text enemyAttackTitleRef,
        TMP_Text enemyRollRef, TMP_Text shieldAbsorbRef,
        TMP_Text netDamageRef, Button continueRef)
    {
        diceContainer = diceContainerRef;
        dicePrefab = dicePrefabRef;
        comboPreviewText = comboPreviewRef;
        rollCounterText = rollCounterRef;
        rerollButton = rerollRef;
        commitButton = commitRef;
        attackPanel = attackPanelRef;
        defensePanel = defensePanelRef;
        defenseTitle = defenseTitleRef;
        defenseRollsText = defenseRollsRef;
        defenseResultText = defenseResultRef;
        defenseShieldText = defenseShieldRef;
        rollDefenseButton = rollDefenseRef;
        enemyAttackPanel = enemyAttackPanelRef;
        enemyAttackTitle = enemyAttackTitleRef;
        enemyRollText = enemyRollRef;
        shieldAbsorbText = shieldAbsorbRef;
        netDamageText = netDamageRef;
        continueButton = continueRef;
        WireButtons();
    }

    private void WireButtons()
    {
        if (rerollButton != null)
        {
            rerollButton.onClick.RemoveAllListeners();
            rerollButton.onClick.AddListener(() => OnRerollClicked?.Invoke());
        }
        if (commitButton != null)
        {
            commitButton.onClick.RemoveAllListeners();
            commitButton.onClick.AddListener(() => OnCommitClicked?.Invoke());
        }
        if (rollDefenseButton != null)
        {
            rollDefenseButton.onClick.RemoveAllListeners();
            rollDefenseButton.onClick.AddListener(() => OnRollDefenseClicked?.Invoke());
        }
        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(() => OnContinueClicked?.Invoke());
        }
    }

    // ── Attack UI ──────────────────────────────────────────────

    public void ShowAttackUI(RollResult[] results, HashSet<string> locked)
    {
        SetPanel(attackPanel, true);
        SetPanel(defensePanel, false);
        SetPanel(enemyAttackPanel, false);

        currentResults = results;
        ClearDiceSlots();

        dieSlots = new DieSlotUI[results.Length];
        for (int i = 0; i < results.Length; i++)
        {
            if (dicePrefab == null || diceContainer == null) continue;

            var go = Instantiate(dicePrefab, diceContainer);
            var slot = go.GetComponent<DieSlotUI>();
            if (slot == null) slot = go.AddComponent<DieSlotUI>();

            bool isLocked = locked != null && locked.Contains(results[i].DiceId);
            string diceId = results[i].DiceId;

            slot.Setup(results[i].Value, null, isLocked);
            slot.OnClicked += () => OnDieClicked(diceId);

            dieSlots[i] = slot;
        }
    }

    public void OnDieClicked(string diceId)
    {
        OnDieLockToggled?.Invoke(diceId);
    }

    public void UpdateDieLock(string diceId, bool isLocked)
    {
        if (dieSlots == null || currentResults == null) return;

        for (int i = 0; i < currentResults.Length; i++)
        {
            if (currentResults[i].DiceId == diceId && dieSlots[i] != null)
            {
                dieSlots[i].SetLocked(isLocked);
                break;
            }
        }
    }

    public void UpdateComboPreview(CombinationResult combo)
    {
        if (comboPreviewText != null)
            comboPreviewText.text = $"Best combo: {FormatComboName(combo.Type)} \u2192 {combo.BaseDamage} dmg";
    }

    public void UpdateRollCounter(int current, int max)
    {
        if (rollCounterText != null)
            rollCounterText.text = $"Roll {current}/{max}";
    }

    public void SetRerollEnabled(bool enabled)
    {
        if (rerollButton != null)
            rerollButton.interactable = enabled;
    }

    public void SetCommitEnabled(bool enabled)
    {
        if (commitButton != null)
            commitButton.interactable = enabled;
    }

    // ── Defense UI ─────────────────────────────────────────────

    public void ShowDefenseUI(int availableRolls)
    {
        SetPanel(attackPanel, false);
        SetPanel(defensePanel, true);
        SetPanel(enemyAttackPanel, false);

        if (defenseTitle != null)
            defenseTitle.text = $"DEFENSE PHASE ({availableRolls} rolls left)";

        if (defenseRollsText != null)
            defenseRollsText.text = $"{availableRolls} rolls available";

        if (defenseResultText != null) defenseResultText.text = "";
        if (defenseShieldText != null) defenseShieldText.text = "";

        if (rollDefenseButton != null)
            rollDefenseButton.interactable = availableRolls > 0;
    }

    public void ShowDefenseRollResult(CombinationResult combo)
    {
        if (defenseResultText != null)
            defenseResultText.text = $"Shield combo: {FormatComboName(combo.Type)}";
    }

    public void UpdateDefenseRolls(int remaining)
    {
        if (defenseTitle != null)
            defenseTitle.text = $"DEFENSE PHASE ({remaining} rolls left)";

        if (defenseRollsText != null)
            defenseRollsText.text = $"{remaining} rolls remaining";

        if (rollDefenseButton != null)
            rollDefenseButton.interactable = remaining > 0;
    }

    public void UpdateDefenseShield(int shieldValue)
    {
        if (defenseShieldText != null)
            defenseShieldText.text = $"{shieldValue} shield";
    }

    // ── Enemy Attack UI ────────────────────────────────────────

    public void ShowEnemyAttackResult(int rawDamage, int shield, int netDamage)
    {
        SetPanel(attackPanel, false);
        SetPanel(defensePanel, false);
        SetPanel(enemyAttackPanel, true);

        if (enemyAttackTitle != null)
            enemyAttackTitle.text = "ENEMY ATTACKS!";

        if (enemyRollText != null)
            enemyRollText.text = $"Enemy rolls: {rawDamage} damage";

        if (shieldAbsorbText != null)
            shieldAbsorbText.text = $"Your shield absorbs: {shield}";

        if (netDamageText != null)
            netDamageText.text = netDamage > 0
                ? $"You take: {netDamage} damage"
                : "Fully blocked!";
    }

    public void HideCombatUI()
    {
        SetPanel(attackPanel, false);
        SetPanel(defensePanel, false);
        SetPanel(enemyAttackPanel, false);
    }

    // ── Helpers ────────────────────────────────────────────────

    private void ClearDiceSlots()
    {
        if (diceContainer == null) return;
        for (int i = diceContainer.childCount - 1; i >= 0; i--)
            Destroy(diceContainer.GetChild(i).gameObject);
        dieSlots = null;
    }

    private void SetPanel(GameObject panel, bool active)
    {
        if (panel != null) panel.SetActive(active);
    }

    private string FormatComboName(CombinationType type)
    {
        switch (type)
        {
            case CombinationType.HighDie: return "High Die";
            case CombinationType.Pair: return "Pair";
            case CombinationType.TwoPair: return "Two Pair";
            case CombinationType.ThreeOfAKind: return "Three of a Kind";
            case CombinationType.Straight: return "Straight";
            case CombinationType.FullHouse: return "Full House";
            case CombinationType.FourOfAKind: return "Four of a Kind";
            case CombinationType.Generala: return "GENERALA!";
            case CombinationType.DoubleGenerala: return "DOUBLE GENERALA!!";
            default: return type.ToString();
        }
    }
}
