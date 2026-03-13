using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DieSlotUI : MonoBehaviour
{
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private TMP_Text typeLabel;
    [SerializeField] private Image background;
    [SerializeField] private Image lockBorder;

    private static readonly Color LockedBorderColor;
    private bool isLocked;

    static DieSlotUI()
    {
        ColorUtility.TryParseHtmlString("#ffd54f", out Color gold);
        LockedBorderColor = gold;
    }

    public event Action OnClicked;

    public void Setup(int value, DiceData data, bool locked)
    {
        if (valueText != null)
            valueText.text = value.ToString();

        if (typeLabel != null && data != null)
            typeLabel.text = $"d{data.NumberOfFaces}";

        if (background != null && data != null)
            background.color = data.DiceColor;

        SetLocked(locked);

        var button = GetComponent<Button>();
        if (button == null) button = gameObject.AddComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => OnClicked?.Invoke());
    }

    public void SetLocked(bool locked)
    {
        isLocked = locked;
        if (lockBorder != null)
        {
            lockBorder.gameObject.SetActive(locked);
            lockBorder.color = LockedBorderColor;
        }
    }
}
