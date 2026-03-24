using System.Collections;
using UnityEngine;
using TMPro;

public class FloatingDamageUI : MonoBehaviour
{
    public static FloatingDamageUI Instance;

    [SerializeField] private Canvas worldCanvas;

    private static readonly Color DamageColor;
    private static readonly Color HealColor;
    private static readonly Color StepCounterColor;
    private static readonly Color CrapsColor;

    static FloatingDamageUI()
    {
        ColorUtility.TryParseHtmlString("#e53935", out Color red);
        DamageColor = red;
        ColorUtility.TryParseHtmlString("#66bb6a", out Color green);
        HealColor = green;
        ColorUtility.TryParseHtmlString("#ffd54f", out Color gold);
        StepCounterColor = gold;
        CrapsColor = gold;
    }

    void Awake()
    {
        Instance = this;
        EnsureWorldCanvas();
    }

    private void EnsureWorldCanvas()
    {
        if (worldCanvas != null) return;

        var go = new GameObject("WorldCanvas");
        worldCanvas = go.AddComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.sortingOrder = 200;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(20, 20);
        rt.localScale = Vector3.one * 0.05f;
    }

    public void ShowDamage(int amount, Vector3 worldPosition)
    {
        SpawnText($"-{amount}", worldPosition, DamageColor);
    }

    public void ShowHeal(int amount, Vector3 worldPosition)
    {
        SpawnText($"+{amount}", worldPosition, HealColor);
    }

    public void ShowGold(int amount, Vector3 worldPosition)
    {
        ColorUtility.TryParseHtmlString("#ffd54f", out Color goldColor);
        SpawnText($"+{amount}G", worldPosition + Vector3.up * 0.3f, goldColor);
    }

    public void ShowCrapsDamage(int amount, Vector3 worldPosition)
    {
        SpawnCrapsText($"-{amount}!", worldPosition);
    }

    public void ShowText(string text, Vector3 worldPosition, Color color)
    {
        SpawnText(text, worldPosition, color);
    }

    private void SpawnCrapsText(string text, Vector3 worldPosition)
    {
        var go = new GameObject("FloatingCrapsDamage");
        go.transform.SetParent(worldCanvas.transform, false);
        go.transform.position = worldPosition + Vector3.up * 0.5f;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 12f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = CrapsColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(4f, 1.5f);

        StartCoroutine(AnimateCrapsText(go, tmp, 1.2f));
    }

    private IEnumerator AnimateCrapsText(GameObject go, TextMeshProUGUI tmp, float duration)
    {
        float elapsed = 0f;
        Vector3 startPos = go.transform.position;
        Color startColor = tmp.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            go.transform.position = startPos + Vector3.up * (t * 2.0f);

            if (t > 0.5f)
            {
                float fadeT = (t - 0.5f) * 2f;
                tmp.color = new Color(startColor.r, startColor.g, startColor.b, 1f - fadeT);
            }

            float scale = t < 0.1f ? Mathf.Lerp(0.5f, 1.5f, t / 0.1f) :
                          t < 0.25f ? Mathf.Lerp(1.5f, 1f, (t - 0.1f) / 0.15f) : 1f;
            go.transform.localScale = Vector3.one * scale;

            yield return null;
        }

        Destroy(go);
    }

    private void SpawnText(string text, Vector3 worldPosition, Color color)
    {
        var go = new GameObject("FloatingDamage");
        go.transform.SetParent(worldCanvas.transform, false);
        go.transform.position = worldPosition + Vector3.up * 0.5f;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 8f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(4f, 1f);

        StartCoroutine(AnimateFloatingText(go, tmp, 0.8f));
    }

    private IEnumerator AnimateFloatingText(GameObject go, TextMeshProUGUI tmp, float duration)
    {
        float elapsed = 0f;
        Vector3 startPos = go.transform.position;
        Color startColor = tmp.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Rise upward
            go.transform.position = startPos + Vector3.up * (t * 1.5f);

            // Fade out in second half
            if (t > 0.5f)
            {
                float fadeT = (t - 0.5f) * 2f;
                tmp.color = new Color(startColor.r, startColor.g, startColor.b, 1f - fadeT);
            }

            // Scale pulse at start
            float scale = t < 0.1f ? Mathf.Lerp(0.5f, 1.2f, t / 0.1f) :
                          t < 0.2f ? Mathf.Lerp(1.2f, 1f, (t - 0.1f) / 0.1f) : 1f;
            go.transform.localScale = Vector3.one * scale;

            yield return null;
        }

        Destroy(go);
    }

    // ── Step Counter ──

    public GameObject ShowStepCounter(Transform target, int steps)
    {
        EnsureWorldCanvas();

        var go = new GameObject("StepCounter");
        go.transform.SetParent(worldCanvas.transform, false);
        go.transform.position = target.position + Vector3.up * 0.6f;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = steps.ToString();
        tmp.fontSize = 8f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = StepCounterColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(4f, 1f);

        StartCoroutine(FollowTarget(go, target));
        return go;
    }

    public void UpdateStepCounter(GameObject counter, int remaining)
    {
        if (counter == null) return;
        var tmp = counter.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.text = remaining.ToString();
        StartCoroutine(PulseScale(counter, 0.15f));
    }

    public void HideStepCounter(GameObject counter)
    {
        if (counter == null) return;
        var tmp = counter.GetComponent<TextMeshProUGUI>();
        if (tmp != null)
            StartCoroutine(FadeOutAndDestroy(counter, tmp, 0.3f));
        else
            Destroy(counter);
    }

    private IEnumerator FollowTarget(GameObject go, Transform target)
    {
        while (go != null && target != null)
        {
            go.transform.position = target.position + Vector3.up * 0.6f;
            yield return null;
        }
    }

    private IEnumerator PulseScale(GameObject go, float duration)
    {
        if (go == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration && go != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float scale = t < 0.5f ? Mathf.Lerp(1f, 1.3f, t * 2f) : Mathf.Lerp(1.3f, 1f, (t - 0.5f) * 2f);
            go.transform.localScale = Vector3.one * scale;
            yield return null;
        }
        if (go != null) go.transform.localScale = Vector3.one;
    }

    private IEnumerator FadeOutAndDestroy(GameObject go, TextMeshProUGUI tmp, float duration)
    {
        float elapsed = 0f;
        Color startColor = tmp.color;
        while (elapsed < duration && go != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            tmp.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t);
            yield return null;
        }
        if (go != null) Destroy(go);
    }
}
