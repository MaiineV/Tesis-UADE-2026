using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CrapsUI : MonoBehaviour
{
    public static CrapsUI Instance;

    [Header("Bet Overlay")]
    [SerializeField] private GameObject betPanel;
    [SerializeField] private TMP_Text betTitle;
    [SerializeField] private Button betPairButton;
    [SerializeField] private Button betThreeOfAKindButton;
    [SerializeField] private Button betStraightButton;
    [SerializeField] private Button betFullHouseButton;
    [SerializeField] private Button betFourOfAKindButton;
    [SerializeField] private Button betGeneralaButton;

    public event Action<CombinationType> OnBetSelected;

    void Awake()
    {
        Instance = this;
        WireBetButtons();
    }

    void OnEnable()
    {
        ShowBetOverlay();
    }

    public void Initialize(
        GameObject betPanelRef, TMP_Text betTitleRef,
        Button pairBtn, Button threeBtn, Button straightBtn,
        Button fullHouseBtn, Button fourBtn, Button generalaBtn)
    {
        betPanel = betPanelRef;
        betTitle = betTitleRef;
        betPairButton = pairBtn;
        betThreeOfAKindButton = threeBtn;
        betStraightButton = straightBtn;
        betFullHouseButton = fullHouseBtn;
        betFourOfAKindButton = fourBtn;
        betGeneralaButton = generalaBtn;
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
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                OnBetSelected?.Invoke(combo);
                HideBetOverlay();
            });
        }
    }

    public void Show() => ShowBetOverlay();

    public void ShowBetOverlay()
    {
        SetPanel(betPanel, true);

        if (betTitle != null)
            betTitle.text = "CRAPS MODE ACTIVATED!\nPredict your next combo:";
    }

    public void HideBetOverlay()
    {
        SetPanel(betPanel, false);
    }

    public void HideAll()
    {
        SetPanel(betPanel, false);
    }

    private void SetPanel(GameObject panel, bool active)
    {
        if (panel != null) panel.SetActive(active);
    }
}
