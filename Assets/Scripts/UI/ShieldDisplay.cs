using UnityEngine;
using TMPro;

public class ShieldDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text shieldText;

    private static readonly Color ShieldColor;

    static ShieldDisplay()
    {
        ColorUtility.TryParseHtmlString("#78909c", out Color c);
        ShieldColor = c;
    }

    void Awake()
    {
        if (shieldText != null)
            shieldText.color = ShieldColor;
    }

    public void Initialize(TMP_Text shieldTextRef)
    {
        shieldText = shieldTextRef;
        if (shieldText != null)
            shieldText.color = ShieldColor;
    }

    public void UpdateShield(int value)
    {
        if (shieldText != null)
            shieldText.text = value > 0 ? $"Shield: {value}" : "";
    }
}
