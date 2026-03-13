using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EnemyAttackUI : MonoBehaviour
{
    [Header("Enemy Info")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text enemyRollText;
    [SerializeField] private TMP_Text shieldAbsorbText;
    [SerializeField] private TMP_Text netDamageText;

    [Header("Button")]
    [SerializeField] private Button continueButton;
    [SerializeField] private TMP_Text continueButtonText;

    public event Action OnContinueClicked;

    void Awake()
    {
        if (continueButton != null)
            continueButton.onClick.AddListener(() => OnContinueClicked?.Invoke());
    }

    public void Show(string enemyName, int[] enemyRoll, int totalDamage, int shieldAbsorbed, int netDamage)
    {
        gameObject.SetActive(true);

        if (titleText != null)
            titleText.text = "ENEMY ATTACKS!";

        if (enemyRollText != null)
        {
            string dice = string.Join(" ", System.Array.ConvertAll(enemyRoll, v => $"[{v}]"));
            enemyRollText.text = $"{enemyName} rolls: {dice} = {totalDamage} damage";
        }

        if (shieldAbsorbText != null)
            shieldAbsorbText.text = $"Your shield absorbs: {shieldAbsorbed}";

        if (netDamageText != null)
        {
            netDamageText.text = netDamage > 0
                ? $"You take: {netDamage} damage"
                : "Fully blocked!";
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
