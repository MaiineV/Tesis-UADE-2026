using System;
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

    public event Action<int> OnRewardChosen; // 0 = A, 1 = B

    void Awake()
    {
        if (cardAButton != null)
            cardAButton.onClick.AddListener(() =>
            {
                OnRewardChosen?.Invoke(0);
                Hide();
            });

        if (cardBButton != null)
            cardBButton.onClick.AddListener(() =>
            {
                OnRewardChosen?.Invoke(1);
                Hide();
            });
    }

    public void Show(FaceUpgradeOffer offerA, FaceUpgradeOffer offerB)
    {
        if (rewardPanel != null) rewardPanel.SetActive(true);

        if (titleText != null)
            titleText.text = "ENEMY DEFEATED!\nChoose a reward:";

        SetupCard(cardATitleText, cardADescriptionText, offerA, "OPTION A");
        SetupCard(cardBTitleText, cardBDescriptionText, offerB, "OPTION B");
    }

    public void Hide()
    {
        if (rewardPanel != null) rewardPanel.SetActive(false);
    }

    private void SetupCard(TMP_Text title, TMP_Text desc, FaceUpgradeOffer offer, string label)
    {
        if (title != null)
            title.text = $"{label}\n{offer.TargetDiceName}";

        if (desc != null)
            desc.text = offer.Upgrade != null ? offer.Upgrade.Description : "";
    }
}
