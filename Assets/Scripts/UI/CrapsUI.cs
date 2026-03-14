using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CrapsUI : MonoBehaviour
{
    [Header("Bet Overlay")]
    [SerializeField] private GameObject betPanel;
    [SerializeField] private TMP_Text betTitle;
    [SerializeField] private Button betPairButton;
    [SerializeField] private Button betThreeOfAKindButton;
    [SerializeField] private Button betStraightButton;
    [SerializeField] private Button betFullHouseButton;
    [SerializeField] private Button betFourOfAKindButton;
    [SerializeField] private Button betGeneralaButton;

    [Header("Result Popup")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text resultTitle;
    [SerializeField] private TMP_Text resultBetText;
    [SerializeField] private TMP_Text resultActualText;
    [SerializeField] private TMP_Text resultDamageText;
    [SerializeField] private Image resultBackground;

    private static readonly Color SuccessColor;
    private static readonly Color FailureColor;

    public event Action<CombinationType> OnBetSelected;

    static CrapsUI()
    {
        ColorUtility.TryParseHtmlString("#2e7d32", out Color green);
        SuccessColor = green;
        ColorUtility.TryParseHtmlString("#c62828", out Color red);
        FailureColor = red;
    }

    void Awake()
    {
        WireBetButtons();
    }

    public void Initialize(
        GameObject betPanelRef, TMP_Text betTitleRef,
        Button pairBtn, Button threeBtn, Button straightBtn,
        Button fullHouseBtn, Button fourBtn, Button generalaBtn,
        GameObject resultPanelRef, TMP_Text resultTitleRef,
        TMP_Text resultBetRef, TMP_Text resultActualRef,
        TMP_Text resultDamageRef, Image resultBgRef)
    {
        betPanel = betPanelRef;
        betTitle = betTitleRef;
        betPairButton = pairBtn;
        betThreeOfAKindButton = threeBtn;
        betStraightButton = straightBtn;
        betFullHouseButton = fullHouseBtn;
        betFourOfAKindButton = fourBtn;
        betGeneralaButton = generalaBtn;
        resultPanel = resultPanelRef;
        resultTitle = resultTitleRef;
        resultBetText = resultBetRef;
        resultActualText = resultActualRef;
        resultDamageText = resultDamageRef;
        resultBackground = resultBgRef;
        WireBetButtons();
    }

    private void WireBetButtons()
    {
        SetupBetButton(betPairButton, CombinationType.Pair);
        SetupBetButton(betThreeOfAKindButton, CombinationType.ThreeOfAKind);
        SetupBetButton(betStraightButton, CombinationType.Straight);
        SetupBetButton(betFullHouseButton, CombinationType.FullHouse);
        SetupBetButton(betFourOfAKindButton, CombinationType.FourOfAKind);
        SetupBetButton(betGeneralaButton, CombinationType.Generala);
    }

    private void SetupBetButton(Button button, CombinationType combo)
    {
        if (button != null)
            button.onClick.AddListener(() =>
            {
                OnBetSelected?.Invoke(combo);
                HideBetOverlay();
            });
    }

    public void ShowBetOverlay()
    {
        SetPanel(betPanel, true);
        SetPanel(resultPanel, false);

        if (betTitle != null)
            betTitle.text = "CRAPS MODE ACTIVATED!\nPredict your next combo:";
    }

    public void HideBetOverlay()
    {
        SetPanel(betPanel, false);
    }

    public void ShowResult(CrapsResult result)
    {
        SetPanel(betPanel, false);
        SetPanel(resultPanel, true);

        if (resultTitle != null)
            resultTitle.text = result.Success ? "BET WON!" : "BET LOST!";

        if (resultBetText != null)
            resultBetText.text = $"You bet: {FormatCombo(result.BetCombo)}";

        if (resultActualText != null)
            resultActualText.text = $"You got: {FormatCombo(result.ActualCombo)}";

        if (resultDamageText != null)
        {
            int pct = Mathf.RoundToInt((result.DamageMultiplier - 1f) * 100f);
            string sign = pct >= 0 ? "+" : "";
            resultDamageText.text = $"Damage: {sign}{pct}% ({result.FinalDamage} dmg)";
        }

        if (resultBackground != null)
            resultBackground.color = result.Success ? SuccessColor : FailureColor;
    }

    public void HideResult()
    {
        SetPanel(resultPanel, false);
    }

    public void HideAll()
    {
        SetPanel(betPanel, false);
        SetPanel(resultPanel, false);
    }

    private void SetPanel(GameObject panel, bool active)
    {
        if (panel != null) panel.SetActive(active);
    }

    private string FormatCombo(CombinationType type)
    {
        switch (type)
        {
            case CombinationType.Pair: return "Pair";
            case CombinationType.ThreeOfAKind: return "Three of a Kind";
            case CombinationType.Straight: return "Straight";
            case CombinationType.FullHouse: return "Full House";
            case CombinationType.FourOfAKind: return "Four of a Kind";
            case CombinationType.Generala: return "GENERALA";
            default: return type.ToString();
        }
    }
}
