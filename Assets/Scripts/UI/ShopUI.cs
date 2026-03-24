using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopUI : MonoBehaviour
{
    public static ShopUI Instance;

    private TMP_Text itemNameText;
    private TMP_Text itemDescText;
    private TMP_Text itemPriceText;
    private Button buyButton;
    private Button cancelButton;

    private ShopItemData currentItem;

    public event Action<ShopItemData> OnBuyClicked;

    void Awake() { Instance = this; }

    public void Initialize(TMP_Text nameText, TMP_Text descText, TMP_Text priceText, Button buy, Button cancel)
    {
        itemNameText = nameText;
        itemDescText = descText;
        itemPriceText = priceText;
        buyButton = buy;
        cancelButton = cancel;

        buyButton.onClick.AddListener(() => { OnBuyClicked?.Invoke(currentItem); Hide(); });
        cancelButton.onClick.AddListener(Hide);
    }

    public void ShowItem(ShopItemData item, int playerGold)
    {
        currentItem = item;
        gameObject.SetActive(true);

        itemNameText.text = item.ItemName;
        itemDescText.text = item.Description;
        itemPriceText.text = $"{item.GoldCost} G";
        buyButton.interactable = playerGold >= item.GoldCost;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
