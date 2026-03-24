using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryDieCardUI : MonoBehaviour
{
    private TMP_Text dieNameText;
    private TMP_Text infoText;
    private TMP_Text costText;
    private Image bgImage;
    private Image selectBorder;
    private DiceInstance diceInstance;
    private bool isSelected;

    public event Action<InventoryDieCardUI> OnCardClicked;

    public DiceInstance DiceInstance => diceInstance;
    public bool IsSelected => isSelected;

    void Awake()
    {
        // Find refs by child name so each instance sets itself up correctly
        foreach (var t in GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.gameObject.name == "DieNameText") dieNameText = t;
            else if (t.gameObject.name == "RangeText") infoText = t;
            else if (t.gameObject.name == "CostText") costText = t;
        }

        bgImage = GetComponent<Image>();

        var borderTf = transform.Find("SelectBorder");
        if (borderTf != null) selectBorder = borderTf.GetComponent<Image>();

        var btn = GetComponent<Button>();
        if (btn != null)
            btn.onClick.AddListener(() => OnCardClicked?.Invoke(this));
    }

    public void Setup(DiceInstance die)
    {
        diceInstance = die;
        if (dieNameText != null)
            dieNameText.text = die.BaseData.DiceName;
        if (infoText != null)
            infoText.text = $"1\u2013{die.BaseData.NumberOfFaces}";
        if (costText != null)
            costText.text = $"Costo: {die.PowerCost:0.#}";
        if (bgImage != null)
            bgImage.color = die.BaseData.DiceColor;
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (selectBorder != null)
            selectBorder.gameObject.SetActive(selected);
    }
}
