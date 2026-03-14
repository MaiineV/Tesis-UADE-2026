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

    public void Initialize(TMP_Text valueTextRef, TMP_Text typeLabelRef, Image backgroundRef, Image lockBorderRef)
    {
        valueText = valueTextRef;
        typeLabel = typeLabelRef;
        background = backgroundRef;
        lockBorder = lockBorderRef;
    }

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
        float duration = 0.2f;
        float elapsed = 0f;
        Vector3 originalScale = transform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Pop in: scale from 0 to 1.1, then settle to 1
            float scale = t < 0.6f
                ? Mathf.Lerp(0f, 1.1f, t / 0.6f)
                : Mathf.Lerp(1.1f, 1f, (t - 0.6f) / 0.4f);
            transform.localScale = originalScale * scale;
            yield return null;
        }

        transform.localScale = originalScale;
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
