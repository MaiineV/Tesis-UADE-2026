using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopUI : MonoBehaviour
{
    public static ShopUI Instance;

    // Legacy single-item fields (kept for backward compatibility)
    private TMP_Text itemNameText;
    private TMP_Text itemDescText;
    private TMP_Text itemPriceText;
    private Button buyButton;
    private Button cancelButton;

    // Multi-item fields
    private TMP_Text[] slotNameTexts = new TMP_Text[3];
    private TMP_Text[] slotDescTexts = new TMP_Text[3];
    private TMP_Text[] slotPriceTexts = new TMP_Text[3];
    private Button[] slotBuyButtons = new Button[3];
    private Button leaveButton;

    private ShopItemData currentItem;
    private List<ShopItemData> _currentItems;

    public event Action<ShopItemData> OnBuyClicked;
    public event Action OnLeaveClicked;

    void Awake() { Instance = this; }

    public void Initialize(TMP_Text nameText, TMP_Text descText, TMP_Text priceText, Button buy, Button cancel)
    {
        itemNameText = nameText;
        itemDescText = descText;
        itemPriceText = priceText;
        buyButton = buy;
        cancelButton = cancel;

        buyButton.onClick.AddListener(() => { OnBuyClicked?.Invoke(currentItem); });
        cancelButton.onClick.AddListener(Hide);
    }

    public void InitializeMultiSlot(
        TMP_Text[] names, TMP_Text[] descs, TMP_Text[] prices,
        Button[] buyBtns, Button leaveBtn)
    {
        for (int i = 0; i < 3; i++)
        {
            slotNameTexts[i] = names[i];
            slotDescTexts[i] = descs[i];
            slotPriceTexts[i] = prices[i];
            slotBuyButtons[i] = buyBtns[i];

            int idx = i;
            slotBuyButtons[i].onClick.AddListener(() =>
            {
                if (_currentItems != null && idx < _currentItems.Count)
                    OnBuyClicked?.Invoke(_currentItems[idx]);
            });
        }
        leaveButton = leaveBtn;
        if (leaveButton != null)
            leaveButton.onClick.AddListener(Hide);
    }

    public void ShowItem(ShopItemData item, int playerGold)
    {
        currentItem = item;
        gameObject.SetActive(true);

        if (itemNameText != null) itemNameText.text = item.ItemName;
        if (itemDescText != null) itemDescText.text = item.Description;
        if (itemPriceText != null) itemPriceText.text = $"{item.GoldCost} G";
        if (buyButton != null) buyButton.interactable = playerGold >= item.GoldCost;
    }

    public void ShowAllItems(List<ShopItemData> items, int playerGold)
    {
        _currentItems = items;
        gameObject.SetActive(true);

        // Hide legacy single-item UI
        if (itemNameText != null) itemNameText.gameObject.SetActive(false);
        if (itemDescText != null) itemDescText.gameObject.SetActive(false);
        if (itemPriceText != null) itemPriceText.gameObject.SetActive(false);
        if (buyButton != null) buyButton.gameObject.SetActive(false);
        if (cancelButton != null) cancelButton.gameObject.SetActive(false);

        for (int i = 0; i < 3; i++)
        {
            if (slotNameTexts[i] == null) continue;

            if (i < items.Count)
            {
                var item = items[i];
                slotNameTexts[i].transform.parent.gameObject.SetActive(true);
                slotNameTexts[i].text = item.ItemName;
                slotDescTexts[i].text = item.Description;
                slotPriceTexts[i].text = $"{item.GoldCost} G";

                bool canBuy = !item.Purchased && playerGold >= item.GoldCost;
                slotBuyButtons[i].interactable = canBuy;

                if (item.Purchased)
                {
                    slotPriceTexts[i].text = "SOLD";
                    slotBuyButtons[i].interactable = false;
                }
            }
            else
            {
                slotNameTexts[i].transform.parent.gameObject.SetActive(false);
            }
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
