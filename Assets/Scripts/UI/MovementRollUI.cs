using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MovementRollUI : MonoBehaviour
{
    public static MovementRollUI Instance;

    [SerializeField] private TMP_Text infoText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Button rollButton;

    public event Action OnRollClicked;

    void Awake() { Instance = this; }

    public void Initialize(TMP_Text info, TMP_Text result, Button roll)
    {
        infoText = info;
        resultText = result;
        rollButton = roll;
        rollButton.onClick.RemoveAllListeners();
        rollButton.onClick.AddListener(() =>
        {
            rollButton.interactable = false;
            OnRollClicked?.Invoke();
        });
    }

    public void Show(int min, int max)
    {
        gameObject.SetActive(true);
        if (infoText != null)
            infoText.text = $"Tirar dado de movimiento\n({min}\u2013{max} pasos)";
        if (resultText != null)
            resultText.text = "";
        if (rollButton != null)
            rollButton.interactable = true;
    }

    public void ShowResult(int steps)
    {
        if (resultText != null)
            resultText.text = $"\u00a1{steps} pasos!";
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
