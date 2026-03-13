using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private Image fillBar;
    [SerializeField] private TMP_Text hpText;

    private static readonly Color ColorGreen = new Color(0.31f, 0.78f, 0.47f);
    private static readonly Color ColorYellow = new Color(1f, 0.76f, 0f);
    private static readonly Color ColorRed;

    static HealthBarUI()
    {
        ColorUtility.TryParseHtmlString("#e53935", out Color red);
        ColorRed = red;
    }

    public void UpdateHP(int current, int max)
    {
        float ratio = max > 0 ? (float)current / max : 0f;
        fillBar.fillAmount = ratio;

        if (ratio > 0.5f)
            fillBar.color = ColorGreen;
        else if (ratio > 0.25f)
            fillBar.color = ColorYellow;
        else
            fillBar.color = ColorRed;

        if (hpText != null)
            hpText.text = $"{current}/{max}";
    }
}
