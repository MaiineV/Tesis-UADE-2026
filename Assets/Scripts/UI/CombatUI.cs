using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CombatUI : MonoBehaviour
{
    public static CombatUI Instance;

    [Header("Dice Display")]
    [SerializeField] private Transform diceContainer;
    [SerializeField] private GameObject dicePrefab;

    [Header("Combo Preview")]
    [SerializeField] private TMP_Text comboNameText;
    [SerializeField] private TMP_Text comboDamageText;

    [Header("Roll Info")]
    [SerializeField] private TMP_Text rollCounterText;

    [Header("Buttons")]
    [SerializeField] private Button rerollButton;
    [SerializeField] private Button commitButton;
    [SerializeField] private TMP_Text rerollButtonText;
    [SerializeField] private TMP_Text commitButtonText;

    private DieSlotUI[] dieSlots;
    private RollResult[] currentResults;

    public event Action<string> OnDieLockToggled;
    public event Action OnRerollClicked;
    public event Action OnCommitClicked;

    void Awake()
    {
        Instance = this;

        if (rerollButton != null)
            rerollButton.onClick.AddListener(() => OnRerollClicked?.Invoke());
        if (commitButton != null)
            commitButton.onClick.AddListener(() => OnCommitClicked?.Invoke());
    }

    public void ShowDiceResults(RollResult[] results, DiceInstance[] dice, HashSet<string> lockedIds)
    {
        currentResults = results;
        ClearDiceSlots();

        dieSlots = new DieSlotUI[results.Length];
        for (int i = 0; i < results.Length; i++)
        {
            if (dicePrefab == null || diceContainer == null) continue;

            var go = Instantiate(dicePrefab, diceContainer);
            var slot = go.GetComponent<DieSlotUI>();
            if (slot == null) slot = go.AddComponent<DieSlotUI>();

            var matchingDie = dice.FirstOrDefault(d => d.Id == results[i].DiceId);
            bool isLocked = lockedIds.Contains(results[i].DiceId);
            string diceId = results[i].DiceId;

            slot.Setup(results[i].Value, matchingDie?.BaseData, isLocked);
            slot.OnClicked += () => OnDieLockToggled?.Invoke(diceId);

            dieSlots[i] = slot;
        }
    }

    public void UpdateLockState(string diceId, bool isLocked)
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
        if (comboNameText != null)
            comboNameText.text = FormatComboName(combo.Type);

        if (comboDamageText != null)
            comboDamageText.text = $"{combo.BaseDamage} dmg";
    }

    public void UpdateRollCounter(int currentRoll, int maxRolls)
    {
        if (rollCounterText != null)
            rollCounterText.text = $"Roll {currentRoll}/{maxRolls}";
    }

    public void SetRerollButtonEnabled(bool enabled)
    {
        if (rerollButton != null)
            rerollButton.interactable = enabled;
    }

    public void SetCommitButtonEnabled(bool enabled)
    {
        if (commitButton != null)
            commitButton.interactable = enabled;
    }

    public void ClearDiceSlots()
    {
        if (diceContainer == null) return;
        for (int i = diceContainer.childCount - 1; i >= 0; i--)
            Destroy(diceContainer.GetChild(i).gameObject);
        dieSlots = null;
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
