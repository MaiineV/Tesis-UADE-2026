using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RewardUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject rewardPanel;
    [SerializeField] private TMP_Text titleText;

    [Header("Option A")]
    [SerializeField] private GameObject cardA;
    [SerializeField] private TMP_Text cardATitleText;
    [SerializeField] private TMP_Text cardADescriptionText;
    [SerializeField] private Button cardAButton;

    [Header("Option B")]
    [SerializeField] private GameObject cardB;
    [SerializeField] private TMP_Text cardBTitleText;
    [SerializeField] private TMP_Text cardBDescriptionText;
    [SerializeField] private Button cardBButton;

    private List<FaceUpgradeOffer> currentOffers;

    public event Action<FaceUpgradeOffer> OnRewardChosen;

    void Awake()
    {
        if (cardAButton != null)
            cardAButton.onClick.AddListener(() =>
            {
                if (currentOffers != null && currentOffers.Count > 0)
                    OnRewardChosen?.Invoke(currentOffers[0]);
                Hide();
            });

        if (cardBButton != null)
            cardBButton.onClick.AddListener(() =>
            {
                if (currentOffers != null && currentOffers.Count > 1)
                    OnRewardChosen?.Invoke(currentOffers[1]);
                Hide();
            });
    }

    public void ShowOffers(List<FaceUpgradeOffer> offers)
    {
        currentOffers = offers;
        if (rewardPanel != null) rewardPanel.SetActive(true);

        if (titleText != null)
            titleText.text = "ENEMY DEFEATED!\nChoose a reward:";

        if (offers.Count > 0)
            SetupCard(cardA, cardATitleText, cardADescriptionText, offers[0], "OPTION A");
        else if (cardA != null)
            cardA.SetActive(false);

        if (offers.Count > 1)
            SetupCard(cardB, cardBTitleText, cardBDescriptionText, offers[1], "OPTION B");
        else if (cardB != null)
            cardB.SetActive(false);
    }

    public void Hide()
    {
        if (rewardPanel != null) rewardPanel.SetActive(false);
    }

    private void SetupCard(GameObject card, TMP_Text title, TMP_Text desc, FaceUpgradeOffer offer, string label)
    {
        if (card != null) card.SetActive(true);

        if (title != null)
            title.text = $"{label}\n{offer.TargetDiceName}";

        if (desc != null)
            desc.text = offer.Upgrade != null ? offer.Upgrade.Description : "";
    }
}
