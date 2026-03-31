using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CombatUI : MonoBehaviour
{
    public static CombatUI Instance;

    [Header("Dice Display")]
    [SerializeField] private Transform diceContainer;
    [SerializeField] private GameObject dicePrefab;

    [Header("Combo / Status Text")]
    [SerializeField] private TMP_Text comboPreviewText;
    [SerializeField] private TMP_Text rollCounterText;

    [Header("Movement Selection")]
    [SerializeField] private TMP_Text movementTotalText;
    [SerializeField] private Button confirmMovementButton;

    [Header("Generala Phase Buttons")]
    [SerializeField] private Button rerollButton;
    [SerializeField] private Button commitButton;

    [Header("Craps Bet Indicator")]
    [SerializeField] private TMP_Text crapsBetIndicator;

    [Header("Combat Panel")]
    [SerializeField] private GameObject attackPanel;

    [Header("Enemy Attack Panel")]
    [SerializeField] private GameObject enemyAttackPanel;
    [SerializeField] private TMP_Text enemyAttackTitle;
    [SerializeField] private TMP_Text enemyRollText;
    [SerializeField] private TMP_Text netDamageText;
    [SerializeField] private Button continueButton;

    // Internal state
    private DieSlotUI[] dieSlots;
    private RollResult[] currentResults;
    private DiceBag currentBag;
    private bool _inMovementSelectionMode;
    private HashSet<string> _selectedMovementIds = new HashSet<string>();

    // Movement selection events
    public event Action<string> OnMovementDieToggled;
    public event Action OnConfirmMovementClicked;

    // Generala phase events
    public event Action<string> OnDieLockToggled;
    public event Action OnRerollClicked;
    public event Action OnCommitClicked;

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
        GameObject enemyAttackPanelRef, TMP_Text enemyAttackTitleRef,
        TMP_Text enemyRollRef, TMP_Text netDamageRef, Button continueRef)
    {
        diceContainer = diceContainerRef;
        dicePrefab = dicePrefabRef;
        comboPreviewText = comboPreviewRef;
        rollCounterText = rollCounterRef;
        rerollButton = rerollRef;
        commitButton = commitRef;
        attackPanel = attackPanelRef;
        enemyAttackPanel = enemyAttackPanelRef;
        enemyAttackTitle = enemyAttackTitleRef;
        enemyRollText = enemyRollRef;
        netDamageText = netDamageRef;
        continueButton = continueRef;
        WireButtons();
    }

    public void InitializeMovementSelection(TMP_Text movementTotalRef, Button confirmMovementRef)
    {
        movementTotalText = movementTotalRef;
        confirmMovementButton = confirmMovementRef;
        if (confirmMovementButton != null)
        {
            confirmMovementButton.onClick.RemoveAllListeners();
            confirmMovementButton.onClick.AddListener(() => OnConfirmMovementClicked?.Invoke());
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
        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(() => OnContinueClicked?.Invoke());
        }
        if (confirmMovementButton != null)
        {
            confirmMovementButton.onClick.RemoveAllListeners();
            confirmMovementButton.onClick.AddListener(() => OnConfirmMovementClicked?.Invoke());
        }
    }

    // ── Pick & Roll — Movement Selection ───────────────────────

    // Shows all dice after the initial roll. Player clicks dice to assign to movement.
    public void ShowPickMovementUI(RollResult[] allResults, DiceBag bag)
    {
        _inMovementSelectionMode = true;
        _selectedMovementIds.Clear();

        SetPanel(attackPanel, true);
        SetPanel(enemyAttackPanel, false);

        currentResults = allResults;
        currentBag = bag;
        ClearDiceSlots(diceContainer, ref dieSlots);
        dieSlots = BuildDiceSlots(allResults, new HashSet<string>(), bag, diceContainer, OnDieClickedInternal);

        if (comboPreviewText != null)
            comboPreviewText.text = "Elegí dados para moverse (cara = tiles)";
        if (rollCounterText != null)
            rollCounterText.text = "PICK & ROLL — Roll 1/3";

        if (movementTotalText != null)
        {
            movementTotalText.text = "Movimiento: 0 tiles";
            movementTotalText.gameObject.SetActive(true);
        }
        if (rerollButton != null) rerollButton.gameObject.SetActive(false);
        if (commitButton != null) commitButton.gameObject.SetActive(false);
        if (confirmMovementButton != null)
        {
            confirmMovementButton.gameObject.SetActive(true);
            confirmMovementButton.interactable = true;
        }
        HideCrapsBetIndicator();
    }

    // Called by GameManager when a die is toggled for movement
    public void ToggleMovementDieSelection(string diceId, bool isSelected, int totalSteps)
    {
        if (isSelected) _selectedMovementIds.Add(diceId);
        else _selectedMovementIds.Remove(diceId);

        UpdateSlotLock(diceId, isSelected, currentResults, dieSlots);

        if (movementTotalText != null)
            movementTotalText.text = totalSteps > 0
                ? $"Movimiento: {totalSteps} tiles"
                : "Sin movimiento (0 tiles)";
    }

    // ── Generala Phase ──────────────────────────────────────────

    // Shows remaining dice (non-movement) for the Generala lock/reroll/commit phase
    public void ShowGeneralaUI(RollResult[] remainingDice, HashSet<string> locked, DiceBag bag = null)
    {
        _inMovementSelectionMode = false;

        SetPanel(attackPanel, true);
        SetPanel(enemyAttackPanel, false);

        currentResults = remainingDice;
        currentBag = bag;
        ClearDiceSlots(diceContainer, ref dieSlots);

        if (remainingDice != null && remainingDice.Length > 0)
            dieSlots = BuildDiceSlots(remainingDice, locked, bag, diceContainer, OnDieClickedInternal);
        else
            dieSlots = new DieSlotUI[0];

        if (movementTotalText != null) movementTotalText.gameObject.SetActive(false);
        if (confirmMovementButton != null) confirmMovementButton.gameObject.SetActive(false);
        if (rerollButton != null) rerollButton.gameObject.SetActive(true);
        if (commitButton != null) commitButton.gameObject.SetActive(true);
    }

    private void OnDieClickedInternal(string diceId)
    {
        if (_inMovementSelectionMode)
            OnMovementDieToggled?.Invoke(diceId);
        else
            OnDieLockToggled?.Invoke(diceId);
    }

    public void UpdateDieLock(string diceId, bool isLocked)
        => UpdateSlotLock(diceId, isLocked, currentResults, dieSlots);

    public void UpdateComboPreview(CombinationResult combo)
    {
        if (comboPreviewText == null) return;
        if (combo.Type == CombinationType.HighDie)
            comboPreviewText.text = "Combo: ninguno";
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

    // ── Enemy Attack Panel ──────────────────────────────────────

    public void ShowEnemyAttackResult(int rawDamage, int shield, int netDamage)
    {
        SetPanel(attackPanel, false);
        SetPanel(enemyAttackPanel, true);

        if (enemyAttackTitle != null) enemyAttackTitle.text = "ENEMIGO ATACA!";
        if (enemyRollText != null) enemyRollText.text = $"Daño del enemigo: {rawDamage}";
        if (netDamageText != null)
            netDamageText.text = netDamage > 0 ? $"Recibís: {netDamage} de daño" : "Sin daño!";
    }

    public void HideCombatUI()
    {
        SetPanel(attackPanel, false);
        SetPanel(enemyAttackPanel, false);
        HideCrapsBetIndicator();
        _inMovementSelectionMode = false;
    }

    // ── Craps Bet Indicator ─────────────────────────────────────

    public void SetCrapsBetIndicator(TMP_Text indicator)
    {
        crapsBetIndicator = indicator;
    }

    public void ShowCrapsBetIndicator(CombinationType bet)
    {
        if (crapsBetIndicator == null) return;
        crapsBetIndicator.text = $"Apuesta Craps: {FormatComboName(bet)}";
        crapsBetIndicator.gameObject.SetActive(true);
    }

    public void HideCrapsBetIndicator()
    {
        if (crapsBetIndicator != null)
            crapsBetIndicator.gameObject.SetActive(false);
    }

    // ── Shared Helpers ──────────────────────────────────────────

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
