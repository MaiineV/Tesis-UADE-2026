using System.Collections;
using UnityEngine;
using TMPro;

public class FloatingDamageUI : MonoBehaviour
{
    public static FloatingDamageUI Instance;

    [SerializeField] private Canvas worldCanvas;

    private static readonly Color DamageColor;
    private static readonly Color HealColor;

    static FloatingDamageUI()
    {
        ColorUtility.TryParseHtmlString("#e53935", out Color red);
        DamageColor = red;
        ColorUtility.TryParseHtmlString("#66bb6a", out Color green);
        HealColor = green;
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

    public void ShowText(string text, Vector3 worldPosition, Color color)
    {
        SpawnText(text, worldPosition, color);
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
}
