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

    [Header("Attack — Craps Bet Indicator")]
    [SerializeField] private TMP_Text crapsBetIndicator;

    [Header("Attack Panel")]
    [SerializeField] private GameObject attackPanel;

    [Header("Defense")]
    [SerializeField] private GameObject defensePanel;
    [SerializeField] private TMP_Text defenseTitle;
    [SerializeField] private TMP_Text defenseRollCounterText;
    [SerializeField] private TMP_Text defenseComboPreviewText;
    [SerializeField] private TMP_Text defenseShieldText;
    [SerializeField] private Button rollDefenseButton;
    [SerializeField] private Button commitDefenseButton;
    [SerializeField] private Transform defenseDiceContainer;

    [Header("Enemy Attack")]
    [SerializeField] private GameObject enemyAttackPanel;
    [SerializeField] private TMP_Text enemyAttackTitle;
    [SerializeField] private TMP_Text enemyRollText;
    [SerializeField] private TMP_Text shieldAbsorbText;
    [SerializeField] private TMP_Text netDamageText;
    [SerializeField] private Button continueButton;

    // Attack state
    private DieSlotUI[] dieSlots;
    private RollResult[] currentResults;
    private DiceBag currentBag;

    // Defense state
    private DieSlotUI[] defenseSlots;
    private RollResult[] defenseResults;

    // Attack events
    public event Action<string> OnDieLockToggled;
    public event Action OnRerollClicked;
    public event Action OnCommitClicked;

    // Defense events
    public event Action<string> OnDefenseDieLockToggled;
    public event Action OnRollDefenseClicked;
    public event Action OnDefenseCommitClicked;

    // Enemy attack event
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
        TMP_Text defenseRollCounterRef, TMP_Text defenseComboPreviewRef,
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
        defenseRollCounterText = defenseRollCounterRef;
        defenseComboPreviewText = defenseComboPreviewRef;
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

    public void InitializeDefense(Transform defenseDiceContainerRef, Button commitDefenseRef)
    {
        defenseDiceContainer = defenseDiceContainerRef;
        commitDefenseButton = commitDefenseRef;
        if (commitDefenseButton != null)
        {
            commitDefenseButton.onClick.RemoveAllListeners();
            commitDefenseButton.onClick.AddListener(() => OnDefenseCommitClicked?.Invoke());
        }
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

    public void ShowAttackUI(RollResult[] results, HashSet<string> locked, DiceBag bag = null)
    {
        SetPanel(attackPanel, true);
        SetPanel(defensePanel, false);
        SetPanel(enemyAttackPanel, false);

        currentResults = results;
        currentBag = bag;
        ClearDiceSlots(diceContainer, ref dieSlots);
        dieSlots = BuildDiceSlots(results, locked, bag, diceContainer, OnDieClicked);
    }

    public void OnDieClicked(string diceId) => OnDieLockToggled?.Invoke(diceId);

    public void UpdateDieLock(string diceId, bool isLocked)
        => UpdateSlotLock(diceId, isLocked, currentResults, dieSlots);

    public void UpdateComboPreview(CombinationResult combo)
    {
        if (comboPreviewText == null) return;
        if (combo.Type == CombinationType.HighDie)
            comboPreviewText.text = "Combo: None";
        else
            comboPreviewText.text = $"Combo: {FormatComboName(combo.Type)} \u2192 {combo.BaseDamage} dmg";
    }

    public void ClearComboPreview()
    {
        if (comboPreviewText != null)
            comboPreviewText.text = "Combo: \u2014";
    }

    public void UpdateRollCounter(int current, int max)
    {
        if (rollCounterText != null)
            rollCounterText.text = $"Roll {current}/{max}";
    }

    public void SetRerollEnabled(bool enabled)
    {
        if (rerollButton != null) rerollButton.interactable = enabled;
    }

    public void SetCommitEnabled(bool enabled)
    {
        if (commitButton != null) commitButton.interactable = enabled;
    }

    // ── Defense UI ─────────────────────────────────────────────

    public void ShowDefenseDiceUI(RollResult[] results, HashSet<string> locked, DiceBag bag = null)
    {
        SetPanel(attackPanel, false);
        SetPanel(defensePanel, true);
        SetPanel(enemyAttackPanel, false);

        defenseResults = results;
        ClearDiceSlots(defenseDiceContainer, ref defenseSlots);
        defenseSlots = BuildDiceSlots(results, locked, bag, defenseDiceContainer, OnDefenseDieClicked);
    }

    public void OnDefenseDieClicked(string diceId) => OnDefenseDieLockToggled?.Invoke(diceId);

    public void UpdateDefenseDieLock(string diceId, bool isLocked)
        => UpdateSlotLock(diceId, isLocked, defenseResults, defenseSlots);

    public void UpdateDefenseComboPreview(CombinationResult combo)
    {
        if (defenseComboPreviewText == null) return;
        if (combo.Type == CombinationType.HighDie)
            defenseComboPreviewText.text = "Escudo: None";
        else
            defenseComboPreviewText.text = $"Escudo: {FormatComboName(combo.Type)}";
    }

    public void ClearDefenseComboPreview()
    {
        if (defenseComboPreviewText != null)
            defenseComboPreviewText.text = "Escudo: \u2014";
    }

    public void UpdateDefenseRollCounter(int current, int max)
    {
        if (defenseTitle != null)
            defenseTitle.text = $"DEFENSA — Roll {current}/{max}";
        if (defenseRollCounterText != null)
            defenseRollCounterText.text = $"Roll {current}/{max}";
    }

    public void SetDefenseRerollEnabled(bool enabled)
    {
        if (rollDefenseButton != null) rollDefenseButton.interactable = enabled;
    }

    public void SetDefenseCommitEnabled(bool enabled)
    {
        if (commitDefenseButton != null) commitDefenseButton.interactable = enabled;
    }

    public void UpdateDefenseShield(int shieldValue)
    {
        if (defenseShieldText != null)
            defenseShieldText.text = shieldValue > 0 ? $"Escudo: {shieldValue}" : "";
    }

    // ── Enemy Attack UI ────────────────────────────────────────

    public void ShowEnemyAttackResult(int rawDamage, int shield, int netDamage)
    {
        SetPanel(attackPanel, false);
        SetPanel(defensePanel, false);
        SetPanel(enemyAttackPanel, true);

        if (enemyAttackTitle != null) enemyAttackTitle.text = "ENEMY ATTACKS!";
        if (enemyRollText != null) enemyRollText.text = $"Enemy rolls: {rawDamage} damage";
        if (shieldAbsorbText != null) shieldAbsorbText.text = $"Your shield absorbs: {shield}";
        if (netDamageText != null)
            netDamageText.text = netDamage > 0 ? $"You take: {netDamage} damage" : "Fully blocked!";
    }

    public void HideCombatUI()
    {
        SetPanel(attackPanel, false);
        SetPanel(defensePanel, false);
        SetPanel(enemyAttackPanel, false);
        HideCrapsBetIndicator();
    }

    // ── Craps Bet Indicator ─────────────────────────────────────

    public void SetCrapsBetIndicator(TMP_Text indicator)
    {
        crapsBetIndicator = indicator;
    }

    public void ShowCrapsBetIndicator(CombinationType bet)
    {
        if (crapsBetIndicator == null) return;
        crapsBetIndicator.text = $"Craps Bet: {FormatComboName(bet)}";
        crapsBetIndicator.gameObject.SetActive(true);
    }

    public void HideCrapsBetIndicator()
    {
        if (crapsBetIndicator != null)
            crapsBetIndicator.gameObject.SetActive(false);
    }

    // ── Shared Helpers ─────────────────────────────────────────

    private DieSlotUI[] BuildDiceSlots(RollResult[] results, HashSet<string> locked,
        DiceBag bag, Transform container, Action<string> clickCallback)
    {
        if (results == null || container == null || dicePrefab == null)
            return new DieSlotUI[0];

        var slots = new DieSlotUI[results.Length];
        for (int i = 0; i < results.Length; i++)
        {
            var go = Instantiate(dicePrefab, container);
            go.SetActive(true);
            var slot = go.GetComponent<DieSlotUI>();
            if (slot == null) slot = go.AddComponent<DieSlotUI>();

            bool isLocked = locked != null && locked.Contains(results[i].DiceId);
            string diceId = results[i].DiceId;

            DiceData diceData = null;
            if (bag != null)
            {
                var inst = bag.Dice.Find(d => d.Id == diceId);
                if (inst != null) diceData = inst.BaseData;
            }

            slot.Setup(results[i].Value, diceData, isLocked);
            slot.OnClicked += () => clickCallback?.Invoke(diceId);
            slots[i] = slot;
        }
        return slots;
    }

    private void UpdateSlotLock(string diceId, bool isLocked, RollResult[] results, DieSlotUI[] slots)
    {
        if (slots == null || results == null) return;
        for (int i = 0; i < results.Length; i++)
        {
            if (results[i].DiceId == diceId && slots[i] != null)
            {
                slots[i].SetLocked(isLocked);
                break;
            }
        }
    }

    private void ClearDiceSlots(Transform container, ref DieSlotUI[] slots)
    {
        if (container == null) return;
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);
        slots = null;
    }

    private void SetPanel(GameObject panel, bool active)
    {
        if (panel != null) panel.SetActive(active);
    }

    private string FormatComboName(CombinationType type)
    {
        switch (type)
        {
            case CombinationType.HighDie:        return "High Die";
            case CombinationType.Pair:           return "Pair";
            case CombinationType.TwoPair:        return "Two Pair";
            case CombinationType.ThreeOfAKind:   return "Three of a Kind";
            case CombinationType.Straight:       return "Straight";
            case CombinationType.FullHouse:      return "Full House";
            case CombinationType.FourOfAKind:    return "Four of a Kind";
            case CombinationType.Generala:       return "GENERALA!";
            case CombinationType.DoubleGenerala: return "DOUBLE GENERALA!!";
            default:                             return type.ToString();
        }
    }
}
