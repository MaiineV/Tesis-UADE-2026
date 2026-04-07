using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShieldDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text shieldText;
    [SerializeField] private Image shieldFill;

    private int _maxShield = 1;

    public void Initialize(TMP_Text textRef, Image fillRef)
    {
        shieldText = textRef;
        shieldFill = fillRef;
        UpdateShield(0);
    }

    public void UpdateShield(int value)
    {
        if (value > _maxShield) _maxShield = value;

        if (shieldFill != null)
            shieldFill.fillAmount = _maxShield > 0 ? (float)value / _maxShield : 0f;

        if (shieldText != null)
            shieldText.text = value > 0 ? $"Shield: {value}" : "";

        gameObject.SetActive(value > 0);
    }

    public void ResetMaxShield()
    {
        _maxShield = 1;
    }
}
