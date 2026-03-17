using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EnemyInfoUI : MonoBehaviour
{
    [SerializeField] private TMP_Text enemyNameText;
    [SerializeField] private Image hpFillBar;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private float smoothSpeed = 5f;

    private static readonly Color ColorGreen = new Color(0.31f, 0.78f, 0.47f);
    private static readonly Color ColorYellow = new Color(1f, 0.76f, 0f);
    private static readonly Color ColorRed;

    private float targetFill;
    private float currentFill;

    static EnemyInfoUI()
    {
        ColorUtility.TryParseHtmlString("#e53935", out Color red);
        ColorRed = red;
    }

    public void Initialize(TMP_Text nameRef, Image fillRef, TMP_Text hpTextRef)
    {
        enemyNameText = nameRef;
        hpFillBar = fillRef;
        hpText = hpTextRef;
        targetFill = 1f;
        currentFill = 1f;
    }

    void Update()
    {
        if (hpFillBar == null) return;
        if (Mathf.Abs(currentFill - targetFill) < 0.001f)
        {
            currentFill = targetFill;
            hpFillBar.fillAmount = currentFill;
            return;
        }

        currentFill = Mathf.Lerp(currentFill, targetFill, Time.deltaTime * smoothSpeed);
        hpFillBar.fillAmount = currentFill;
        UpdateColor(currentFill);
    }

    public void Show(EnemyEntity enemy)
    {
        if (enemyNameText != null)
            enemyNameText.text = enemy.State.BaseData.EnemyName;

        targetFill = 1f;
        currentFill = 1f;
        if (hpFillBar != null)
        {
            hpFillBar.fillAmount = 1f;
            UpdateColor(1f);
        }

        UpdateHP(enemy.State.CurrentHP, enemy.State.MaxHP);
        gameObject.SetActive(true);
    }

    public void UpdateHP(int current, int max)
    {
        float ratio = max > 0 ? (float)current / max : 0f;
        targetFill = ratio;
        UpdateColor(ratio);

        if (hpText != null)
            hpText.text = $"{current}/{max}";
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void UpdateColor(float ratio)
    {
        if (hpFillBar == null) return;

        if (ratio > 0.5f)
            hpFillBar.color = ColorGreen;
        else if (ratio > 0.25f)
            hpFillBar.color = ColorYellow;
        else
            hpFillBar.color = ColorRed;
    }
}
