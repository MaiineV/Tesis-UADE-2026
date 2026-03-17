using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CrapsToastUI : MonoBehaviour
{
    public static CrapsToastUI Instance;

    [SerializeField] private GameObject toastPanel;
    [SerializeField] private Image toastBackground;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text detailsText;

    private CanvasGroup canvasGroup;
    private RectTransform panelRT;
    private Coroutine toastCoroutine;

    private static readonly Color SuccessColor;
    private static readonly Color FailureColor;

    static CrapsToastUI()
    {
        ColorUtility.TryParseHtmlString("#2e7d32", out Color green);
        SuccessColor = green;
        ColorUtility.TryParseHtmlString("#c62828", out Color red);
        FailureColor = red;
    }

    void Awake()
    {
        Instance = this;
        if (toastPanel != null)
        {
            canvasGroup = toastPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = toastPanel.AddComponent<CanvasGroup>();
            panelRT = toastPanel.GetComponent<RectTransform>();
            toastPanel.SetActive(false);
        }
    }

    public void Initialize(GameObject panel, Image bg, TMP_Text title, TMP_Text details)
    {
        toastPanel = panel;
        toastBackground = bg;
        titleText = title;
        detailsText = details;

        canvasGroup = toastPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = toastPanel.AddComponent<CanvasGroup>();
        panelRT = toastPanel.GetComponent<RectTransform>();
        toastPanel.SetActive(false);
    }

    public void ShowResult(CrapsResult result)
    {
        if (toastPanel == null) return;

        if (toastCoroutine != null) StopCoroutine(toastCoroutine);

        if (titleText != null)
            titleText.text = result.Success ? "CRAPS BET WON!" : "CRAPS BET LOST!";

        if (detailsText != null)
        {
            int pct = Mathf.RoundToInt((result.DamageMultiplier - 1f) * 100f);
            string sign = pct >= 0 ? "+" : "";
            detailsText.text = $"Bet: {FormatCombo(result.BetCombo)} | Got: {FormatCombo(result.ActualCombo)}\nDamage: {sign}{pct}% ({result.FinalDamage} dmg)";
        }

        if (toastBackground != null)
            toastBackground.color = result.Success ? SuccessColor : FailureColor;

        // Activate before starting coroutine (can't start coroutine on inactive GO)
        toastPanel.SetActive(true);
        canvasGroup.alpha = 0f;
        toastCoroutine = StartCoroutine(AnimateToast());
    }

    private IEnumerator AnimateToast()
    {

        float slideOffset = 300f;
        Vector2 hiddenPos = new Vector2(-slideOffset, panelRT.anchoredPosition.y);
        Vector2 shownPos = new Vector2(10f, panelRT.anchoredPosition.y);

        // Slide in (0.3s)
        float elapsed = 0f;
        float slideInDuration = 0.3f;
        while (elapsed < slideInDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / slideInDuration;
            float ease = 1f - (1f - t) * (1f - t); // ease-out quad
            panelRT.anchoredPosition = Vector2.Lerp(hiddenPos, shownPos, ease);
            canvasGroup.alpha = t;
            yield return null;
        }
        panelRT.anchoredPosition = shownPos;
        canvasGroup.alpha = 1f;

        // Hold (3s)
        yield return new WaitForSeconds(3f);

        // Fade out (0.5s)
        elapsed = 0f;
        float fadeOutDuration = 0.5f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeOutDuration;
            canvasGroup.alpha = 1f - t;
            yield return null;
        }

        toastPanel.SetActive(false);
        canvasGroup.alpha = 1f;
        panelRT.anchoredPosition = shownPos;
        toastCoroutine = null;
    }

    private string FormatCombo(CombinationType type)
    {
        switch (type)
        {
            case CombinationType.Pair: return "Pair";
            case CombinationType.ThreeOfAKind: return "3 of a Kind";
            case CombinationType.Straight: return "Straight";
            case CombinationType.FullHouse: return "Full House";
            case CombinationType.FourOfAKind: return "4 of a Kind";
            case CombinationType.Generala: return "GENERALA";
            default: return type.ToString();
        }
    }
}
