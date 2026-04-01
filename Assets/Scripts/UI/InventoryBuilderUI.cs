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
    private int _minCount;
    private int _maxCount;
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

    public void Show(DiceData[] availableTypes, int minSlots, int maxSlots, float powerBudget)
    {
        gameObject.SetActive(true);
        selectedDice.Clear();
        usedPower = 0;
        _minCount = minSlots;
        _maxCount = maxSlots;
        maxPowerBudget = powerBudget;

        var pool = generateRandomPool(availableTypes, 10);
        BuildCards(pool);
        UpdateCounter();
    }

    // Legacy overload kept for backwards compatibility
    public void Show(List<DiceInstance> available, int slotsRequired, float powerBudget)
    {
        gameObject.SetActive(true);
        selectedDice.Clear();
        usedPower = 0;
        _minCount = slotsRequired;
        _maxCount = slotsRequired;
        maxPowerBudget = powerBudget;
        BuildCards(available);
        UpdateCounter();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        ClearCards();
    }

    private List<DiceInstance> generateRandomPool(DiceData[] availableTypes, int poolSize)
    {
        if (availableTypes == null || availableTypes.Length == 0)
        {
            Debug.LogError("[InventoryBuilderUI] availableTypes is null or empty! Creating fallback d6 pool.");
            var pool = new List<DiceInstance>(poolSize);
            var fallback = ScriptableObject.CreateInstance<DiceData>();
            fallback.DiceName = "d6";
            fallback.NumberOfFaces = 6;
            fallback.DefaultFaces = new[] { 1, 2, 3, 4, 5, 6 };
            fallback.PowerCost = 1f;
            for (int i = 0; i < poolSize; i++)
                pool.Add(DiceInstance.Create(fallback));
            return pool;
        }

        var result = new List<DiceInstance>(poolSize);

        // Find d6 in available types
        DiceData d6Type = null;
        for (int i = 0; i < availableTypes.Length; i++)
        {
            if (availableTypes[i].NumberOfFaces == 6)
            {
                d6Type = availableTypes[i];
                break;
            }
        }

        // Guarantee minimum 5 d6
        int guaranteed = 5;
        if (d6Type != null)
        {
            for (int i = 0; i < guaranteed && i < poolSize; i++)
                result.Add(DiceInstance.Create(d6Type));
        }

        // Fill remaining slots randomly
        for (int i = result.Count; i < poolSize; i++)
        {
            int idx = UnityEngine.Random.Range(0, availableTypes.Length);
            result.Add(DiceInstance.Create(availableTypes[idx]));
        }

        // Shuffle so d6 aren't always first
        for (int i = result.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }

        return result;
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
            if (selectedDice.Count >= _maxCount) return;
            if (usedPower + card.DiceInstance.PowerCost > maxPowerBudget) return;
            usedPower += card.DiceInstance.PowerCost;
            selectedDice.Add(card.DiceInstance);
            card.SetSelected(true);
        }
        UpdateCounter();
    }

    private void UpdateCounter()
    {
        bool inRange = selectedDice.Count >= _minCount && selectedDice.Count <= _maxCount;

        if (counterText != null)
        {
            if (_minCount == _maxCount)
                counterText.text = $"{selectedDice.Count}/{_maxCount} dados seleccionados";
            else
                counterText.text = $"{selectedDice.Count}/{_minCount}\u2013{_maxCount} dados seleccionados";
        }

        if (budgetText != null)
            budgetText.text = $"Poder: {usedPower:0.#}/{maxPowerBudget:0.#}";

        if (confirmButton != null)
            confirmButton.interactable = inRange;
    }

    private void OnConfirmClicked()
    {
        if (selectedDice.Count < _minCount || selectedDice.Count > _maxCount) return;
        Hide();
        OnInventoryConfirmed?.Invoke(new List<DiceInstance>(selectedDice));
    }
}
