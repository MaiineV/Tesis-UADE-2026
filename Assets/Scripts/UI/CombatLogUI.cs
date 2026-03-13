using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CombatLogUI : MonoBehaviour
{
    [SerializeField] private TMP_Text logText;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private int maxMessages = 50;

    private readonly List<string> messages = new List<string>();

    public void AddMessage(string message)
    {
        messages.Add($"> {message}");

        if (messages.Count > maxMessages)
            messages.RemoveAt(0);

        if (logText != null)
            logText.text = string.Join("\n", messages);

        ScrollToBottom();
    }

    public void Clear()
    {
        messages.Clear();
        if (logText != null)
            logText.text = "";
    }

    private void ScrollToBottom()
    {
        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }
}
