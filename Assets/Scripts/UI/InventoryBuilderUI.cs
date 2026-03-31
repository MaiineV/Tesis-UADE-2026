using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryBuilderUI : MonoBehaviour
{
    public static InventoryBuilderUI Instance;

    private TMP_Text counterText;
    private TMP_Text budgetText;
    private Transform cardContainer;
    private Button confirmButton;
    private GameObject cardPrefab;

    private List<InventoryDieCardUI> cards = new List<InventoryDieCardUI>();
    private List<DiceInstance> selectedDice = new List<DiceInstance>();
    private float maxPowerBudget;
    private float usedPower;

    public event Action<List<DiceInstance>> OnInventoryConfirmed;

    void Awake() { Instance = this; }

    public void Initialize(TMP_Text counter, TMP_Text budget, Transform container, Button confirm, GameObject prefab)
    {
        counterText = counter;
        budgetText = budget;
        cardContainer = container;
        confirmButton = confirm;
        cardPrefab = prefab;
        confirmButton.onClick.RemoveAllListeners();
        confirmButton.onClick.AddListener(OnConfirmClicked);
    }

    public void Show(List<DiceInstance> available, float powerBudget)
    {
        gameObject.SetActive(true);
        selectedDice.Clear();
        usedPower = 0;
        maxPowerBudget = powerBudget;
        BuildCards(available);
        UpdateCounter();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        ClearCards();
    }

    private void BuildCards(List<DiceInstance> available)
    {
        ClearCards();
        foreach (var die in available)
        {
            var go = Instantiate(cardPrefab, cardContainer);
            go.SetActive(true);
            var card = go.GetComponent<InventoryDieCardUI>();
            card.Setup(die);
            card.OnCardClicked += OnCardClicked;
            cards.Add(card);
        }
    }

    private void ClearCards()
    {
        foreach (var card in cards)
            if (card != null) Destroy(card.gameObject);
        cards.Clear();
    }

    private void OnCardClicked(InventoryDieCardUI card)
    {
        if (card.IsSelected)
        {
            usedPower -= card.DiceInstance.PowerCost;
            selectedDice.Remove(card.DiceInstance);
            card.SetSelected(false);
        }
        else
        {
            if (usedPower + card.DiceInstance.PowerCost > maxPowerBudget) return;
            usedPower += card.DiceInstance.PowerCost;
            selectedDice.Add(card.DiceInstance);
            card.SetSelected(true);
        }
        UpdateCounter();
    }

    private void UpdateCounter()
    {
        if (counterText != null)
            counterText.text = $"{selectedDice.Count} dados seleccionados";
        if (budgetText != null)
            budgetText.text = $"Poder: {usedPower:0.#}/{maxPowerBudget:0.#}";
        if (confirmButton != null)
            confirmButton.interactable = selectedDice.Count > 0;
    }

    private void OnConfirmClicked()
    {
        if (selectedDice.Count == 0) return;
        Hide();
        OnInventoryConfirmed?.Invoke(new List<DiceInstance>(selectedDice));
    }
}
