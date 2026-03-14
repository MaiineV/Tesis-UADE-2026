using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private Image fillBar;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private float smoothSpeed = 5f;

    private static readonly Color ColorGreen = new Color(0.31f, 0.78f, 0.47f);
    private static readonly Color ColorYellow = new Color(1f, 0.76f, 0f);
    private static readonly Color ColorRed;

    private float targetFill;
    private float currentFill;

    static HealthBarUI()
    {
        ColorUtility.TryParseHtmlString("#e53935", out Color red);
        ColorRed = red;
    }

    void Awake()
    {
        targetFill = 1f;
        currentFill = 1f;
    }

    public void Initialize(Image fillBarRef, TMP_Text hpTextRef)
    {
        fillBar = fillBarRef;
        hpText = hpTextRef;
    }

    void Update()
    {
        if (fillBar == null) return;
        if (Mathf.Abs(currentFill - targetFill) < 0.001f)
        {
            currentFill = targetFill;
            fillBar.fillAmount = currentFill;
            return;
        }

        currentFill = Mathf.Lerp(currentFill, targetFill, Time.deltaTime * smoothSpeed);
        fillBar.fillAmount = currentFill;
        UpdateColor(currentFill);
    }

    public void UpdateHP(int current, int max)
    {
        float ratio = max > 0 ? (float)current / max : 0f;
        targetFill = ratio;
        UpdateColor(ratio);

        if (hpText != null)
            hpText.text = $"{current}/{max}";
    }

    private void UpdateColor(float ratio)
    {
        if (fillBar == null) return;

        if (ratio > 0.5f)
            fillBar.color = ColorGreen;
        else if (ratio > 0.25f)
            fillBar.color = ColorYellow;
        else
            fillBar.color = ColorRed;
    }
}
