using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DefenseUI : MonoBehaviour
{
    [Header("Defense Info")]
    [SerializeField] private TMP_Text phaseTitle;
    [SerializeField] private TMP_Text rollsRemainingText;
    [SerializeField] private TMP_Text shieldComboText;
    [SerializeField] private TMP_Text bestShieldText;

    [Header("Dice Display")]
    [SerializeField] private TMP_Text diceResultsText;

    [Header("Button")]
    [SerializeField] private Button rollDefenseButton;
    [SerializeField] private TMP_Text rollDefenseButtonText;

    public event Action OnRollDefenseClicked;

    void Awake()
    {
        if (rollDefenseButton != null)
            rollDefenseButton.onClick.AddListener(() => OnRollDefenseClicked?.Invoke());
    }

    public void Show(int availableRolls)
    {
        gameObject.SetActive(true);

        if (phaseTitle != null)
            phaseTitle.text = "DEFENSE PHASE";

        UpdateRollsRemaining(availableRolls, availableRolls);
        ClearResults();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void UpdateRollsRemaining(int remaining, int total)
    {
        if (rollsRemainingText != null)
            rollsRemainingText.text = $"{remaining} rolls left";

        if (rollDefenseButton != null)
            rollDefenseButton.interactable = remaining > 0;

        if (rollDefenseButtonText != null)
            rollDefenseButtonText.text = remaining > 0 ? "ROLL DEFENSE" : "DONE";
    }

    public void ShowDefenseResult(int[] diceValues, CombinationResult combo, int shieldValue)
    {
        if (diceResultsText != null)
        {
            string dice = string.Join(" ", diceValues.Select(v => $"[{v}]"));
            diceResultsText.text = dice;
        }

        if (shieldComboText != null)
            shieldComboText.text = $"Shield combo: {FormatComboName(combo.Type)}";

        if (bestShieldText != null)
            bestShieldText.text = $"{shieldValue} shield";
    }

    private void ClearResults()
    {
        if (diceResultsText != null) diceResultsText.text = "";
        if (shieldComboText != null) shieldComboText.text = "";
        if (bestShieldText != null) bestShieldText.text = "";
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
