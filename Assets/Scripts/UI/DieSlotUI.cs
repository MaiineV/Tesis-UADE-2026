using System;
using System.Collections;
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

        // Roll shake animation
        StartCoroutine(RollShakeAnimation());
    }

    public void SetLocked(bool locked)
    {
        isLocked = locked;
        if (lockBorder != null)
        {
            lockBorder.gameObject.SetActive(locked);
            lockBorder.color = LockedBorderColor;
        }

        // Lock/unlock scale pulse
        if (gameObject.activeInHierarchy)
            StartCoroutine(LockPulseAnimation());
    }

    private IEnumerator RollShakeAnimation()
    {
        float duration = 0.3f;
        float elapsed = 0f;
        RectTransform rt = GetComponent<RectTransform>();
        if (rt == null) yield break;

        Vector2 originalPos = rt.anchoredPosition;
        float intensity = 5f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float shake = intensity * (1f - t);
            rt.anchoredPosition = originalPos + new Vector2(
                UnityEngine.Random.Range(-shake, shake),
                UnityEngine.Random.Range(-shake, shake)
            );
            yield return null;
        }

        rt.anchoredPosition = originalPos;
    }

    private IEnumerator LockPulseAnimation()
    {
        float duration = 0.15f;
        float elapsed = 0f;
        Vector3 originalScale = transform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float scale = t < 0.5f
                ? Mathf.Lerp(1f, 1.15f, t * 2f)
                : Mathf.Lerp(1.15f, 1f, (t - 0.5f) * 2f);
            transform.localScale = originalScale * scale;
            yield return null;
        }

        transform.localScale = originalScale;
    }
}
