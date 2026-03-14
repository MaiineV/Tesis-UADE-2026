using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ScreenFlashUI : MonoBehaviour
{
    public static ScreenFlashUI Instance;

    [SerializeField] private Image flashImage;

    private static readonly Color CrapsSuccessColor;
    private static readonly Color CrapsFailureColor;
    private static readonly Color DamageFlashColor;

    static ScreenFlashUI()
    {
        ColorUtility.TryParseHtmlString("#2e7d32", out Color green);
        CrapsSuccessColor = new Color(green.r, green.g, green.b, 0.3f);
        ColorUtility.TryParseHtmlString("#c62828", out Color red);
        CrapsFailureColor = new Color(red.r, red.g, red.b, 0.3f);
        DamageFlashColor = new Color(red.r, red.g, red.b, 0.2f);
    }

    void Awake()
    {
        Instance = this;
        EnsureFlashImage();
        if (flashImage != null)
        {
            flashImage.color = Color.clear;
            flashImage.raycastTarget = false;
        }
    }

    private void EnsureFlashImage()
    {
        if (flashImage != null) return;

        // Find or create a canvas
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        var go = new GameObject("ScreenFlash");
        go.transform.SetParent(canvas.transform, false);

        flashImage = go.AddComponent<Image>();
        flashImage.color = Color.clear;
        flashImage.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Put it on top
        go.transform.SetAsLastSibling();
    }

    public void FlashCrapsSuccess()
    {
        StartCoroutine(FlashRoutine(CrapsSuccessColor, 0.5f));
    }

    public void FlashCrapsFailure()
    {
        StartCoroutine(FlashAndShakeRoutine(CrapsFailureColor, 0.5f));
    }

    public void FlashDamage()
    {
        StartCoroutine(FlashRoutine(DamageFlashColor, 0.3f));
    }

    private IEnumerator FlashRoutine(Color color, float duration)
    {
        if (flashImage == null) yield break;

        flashImage.color = color;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            flashImage.color = new Color(color.r, color.g, color.b, color.a * (1f - t));
            yield return null;
        }

        flashImage.color = Color.clear;
    }

    private IEnumerator FlashAndShakeRoutine(Color color, float duration)
    {
        if (flashImage == null) yield break;

        flashImage.color = color;
        var cam = Camera.main;
        Vector3 originalPos = cam != null ? cam.transform.position : Vector3.zero;
        float elapsed = 0f;
        float shakeIntensity = 0.15f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            flashImage.color = new Color(color.r, color.g, color.b, color.a * (1f - t));

            // Camera shake
            if (cam != null)
            {
                float shake = shakeIntensity * (1f - t);
                cam.transform.position = originalPos + new Vector3(
                    Random.Range(-shake, shake),
                    Random.Range(-shake, shake),
                    0
                );
            }

            yield return null;
        }

        flashImage.color = Color.clear;
        if (cam != null)
            cam.transform.position = originalPos;
    }
}
