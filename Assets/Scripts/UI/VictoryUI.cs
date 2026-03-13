using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VictoryUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private Button restartButton;

    public event Action OnRestartClicked;

    void Awake()
    {
        if (restartButton != null)
            restartButton.onClick.AddListener(() => OnRestartClicked?.Invoke());
    }

    public void Show(RunStats stats)
    {
        if (panel != null) panel.SetActive(true);

        if (titleText != null)
            titleText.text = "VICTORY!";

        if (statsText != null)
        {
            statsText.text = $"Room cleared!\n" +
                             $"Rounds fought: {stats.RoundsFought}\n" +
                             $"Damage dealt: {stats.DamageDealt}\n" +
                             $"Best combo: {FormatCombo(stats.BestCombo)}\n" +
                             $"Craps bets won: {stats.CrapsWins}/{stats.CrapsAttempts}";
        }
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    private string FormatCombo(CombinationType type)
    {
        switch (type)
        {
            case CombinationType.Pair: return "Pair";
            case CombinationType.TwoPair: return "Two Pair";
            case CombinationType.ThreeOfAKind: return "Three of a Kind";
            case CombinationType.Straight: return "Straight";
            case CombinationType.FullHouse: return "Full House";
            case CombinationType.FourOfAKind: return "Four of a Kind";
            case CombinationType.Generala: return "Generala";
            case CombinationType.DoubleGenerala: return "Double Generala";
            default: return "High Die";
        }
    }
}
