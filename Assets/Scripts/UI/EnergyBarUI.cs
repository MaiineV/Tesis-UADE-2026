using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EnergyBarUI : MonoBehaviour
{
    [SerializeField] private Image fillBar;
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private TMP_Text crapsReadyText;

    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseMinScale = 0.95f;
    [SerializeField] private float pulseMaxScale = 1.05f;

    private static readonly Color ColorBlue = new Color(0.26f, 0.65f, 0.96f);
    private static readonly Color ColorYellow = new Color(1f, 0.7f, 0f);
    private static readonly Color ColorGold;

    private bool isFull;
    private Vector3 originalScale;

    static EnergyBarUI()
    {
        ColorUtility.TryParseHtmlString("#ffb300", out Color gold);
        ColorGold = gold;
    }

    void Awake()
    {
        originalScale = transform.localScale;
        if (crapsReadyText != null)
            crapsReadyText.gameObject.SetActive(false);
    }

    public void Initialize(Image fillBarRef, TMP_Text energyTextRef, TMP_Text crapsReadyTextRef)
    {
        fillBar = fillBarRef;
        energyText = energyTextRef;
        crapsReadyText = crapsReadyTextRef;
    }

    void Update()
    {
        if (!isFull || fillBar == null) return;

        float t = Mathf.PingPong(Time.time * pulseSpeed, 1f);
        float scale = Mathf.Lerp(pulseMinScale, pulseMaxScale, t);
        fillBar.transform.localScale = originalScale * scale;
    }

    public void UpdateEnergy(float normalized)
    {
        if (fillBar == null) return;

        normalized = Mathf.Clamp01(normalized);
        fillBar.fillAmount = normalized;

        if (normalized < 0.5f)
            fillBar.color = Color.Lerp(ColorBlue, ColorYellow, normalized * 2f);
        else
            fillBar.color = Color.Lerp(ColorYellow, ColorGold, (normalized - 0.5f) * 2f);

        isFull = normalized >= 1f;

        if (crapsReadyText != null)
            crapsReadyText.gameObject.SetActive(isFull);

        if (!isFull)
            fillBar.transform.localScale = originalScale;

        if (energyText != null)
            energyText.text = $"{Mathf.RoundToInt(normalized * 100)}%";
    }
}
