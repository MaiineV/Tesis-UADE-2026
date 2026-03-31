using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

public class ActiveBuffsUI : MonoBehaviour
{
    public static ActiveBuffsUI Instance;

    private GameObject _panel;
    private Transform _listParent;
    private GameObject _toggleButton;
    private TMP_Text _tooltipTitle;
    private TMP_Text _tooltipDesc;
    private GameObject _tooltipGO;

    private List<GameObject> _cardInstances = new List<GameObject>();
    private PlayerState _playerState;

    void Awake()
    {
        Instance = this;
    }

    public void Initialize(GameObject panel, Transform listParent, GameObject toggleButton,
        GameObject tooltipGO, TMP_Text tooltipTitle, TMP_Text tooltipDesc)
    {
        _panel = panel;
        _listParent = listParent;
        _toggleButton = toggleButton;
        _tooltipGO = tooltipGO;
        _tooltipTitle = tooltipTitle;
        _tooltipDesc = tooltipDesc;

        if (_toggleButton != null)
        {
            var btn = _toggleButton.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(TogglePanel);
        }

        if (_panel != null)
            _panel.SetActive(false);
        if (_tooltipGO != null)
            _tooltipGO.SetActive(false);
    }

    public void SetPlayerState(PlayerState state)
    {
        _playerState = state;
    }

    public void TogglePanel()
    {
        if (_panel == null) return;
        bool show = !_panel.activeSelf;
        _panel.SetActive(show);
        if (show) Refresh();
    }

    public void Refresh()
    {
        clearCards();

        if (_playerState == null || _playerState.ActiveBuffs == null || _playerState.ActiveBuffs.Count == 0)
        {
            createEmptyCard();
            return;
        }

        for (int i = 0; i < _playerState.ActiveBuffs.Count; i++)
        {
            createBuffCard(_playerState.ActiveBuffs[i]);
        }
    }

    private void createEmptyCard()
    {
        ColorUtility.TryParseHtmlString("#e0e0e0", out Color textColor);

        var card = new GameObject("EmptyBuff");
        card.transform.SetParent(_listParent, false);
        var rt = card.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(260, 40);

        var tmp = card.AddComponent<TextMeshProUGUI>();
        tmp.text = "Sin buffs activos";
        tmp.fontSize = 16;
        tmp.color = new Color(textColor.r, textColor.g, textColor.b, 0.6f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Italic;
        tmp.raycastTarget = false;

        _cardInstances.Add(card);
    }

    private void createBuffCard(RunBuffData buff)
    {
        ColorUtility.TryParseHtmlString("#1e1e3a", out Color panelBg);
        ColorUtility.TryParseHtmlString("#e0e0e0", out Color textColor);
        ColorUtility.TryParseHtmlString("#ab47bc", out Color purple);
        ColorUtility.TryParseHtmlString("#ffd54f", out Color gold);

        var card = new GameObject($"BuffCard_{buff.Type}");
        card.transform.SetParent(_listParent, false);
        var cardRT = card.AddComponent<RectTransform>();
        cardRT.sizeDelta = new Vector2(260, 45);
        var cardImg = card.AddComponent<Image>();
        cardImg.color = new Color(panelBg.r, panelBg.g, panelBg.b, 0.9f);

        var hlg = card.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.padding = new RectOffset(10, 10, 5, 5);
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;

        // Buff icon indicator
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(card.transform, false);
        var iconRT = iconGO.AddComponent<RectTransform>();
        var iconLE = iconGO.AddComponent<LayoutElement>();
        iconLE.preferredWidth = 8;
        iconLE.preferredHeight = 30;
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.color = purple;

        // Name
        var nameGO = new GameObject("Name");
        nameGO.transform.SetParent(card.transform, false);
        var nameRT = nameGO.AddComponent<RectTransform>();
        var nameLE = nameGO.AddComponent<LayoutElement>();
        nameLE.flexibleWidth = 1;
        var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text = buff.Title;
        nameTMP.fontSize = 14;
        nameTMP.color = textColor;
        nameTMP.alignment = TextAlignmentOptions.Left;
        nameTMP.raycastTarget = false;

        // Value
        var valGO = new GameObject("Value");
        valGO.transform.SetParent(card.transform, false);
        var valRT = valGO.AddComponent<RectTransform>();
        var valLE = valGO.AddComponent<LayoutElement>();
        valLE.preferredWidth = 60;
        var valTMP = valGO.AddComponent<TextMeshProUGUI>();
        valTMP.text = formatBuffValue(buff);
        valTMP.fontSize = 14;
        valTMP.color = gold;
        valTMP.alignment = TextAlignmentOptions.Right;
        valTMP.fontStyle = FontStyles.Bold;
        valTMP.raycastTarget = false;

        // Tooltip trigger
        var trigger = card.AddComponent<EventTrigger>();
        string title = buff.Title;
        string desc = buff.Description;

        var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener((data) => showTooltip(title, desc));
        trigger.triggers.Add(enterEntry);

        var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener((data) => hideTooltip());
        trigger.triggers.Add(exitEntry);

        _cardInstances.Add(card);
    }

    private string formatBuffValue(RunBuffData buff)
    {
        switch (buff.Type)
        {
            case RunBuffType.DamageBoost:
                return $"+{buff.Value * 100f:F0}%";
            case RunBuffType.ExtraRoll:
                return $"+{buff.Value:F0}";
            case RunBuffType.ShieldOnCombatStart:
                return $"+{buff.Value:F0}";
            case RunBuffType.HealPerRoom:
                return $"+{buff.Value:F0} HP";
            case RunBuffType.CritBonus:
                return $"+{buff.Value * 100f:F0}%";
            case RunBuffType.EnergyGainBoost:
                return $"+{buff.Value * 100f:F0}%";
            default:
                return $"+{buff.Value:F1}";
        }
    }

    private void showTooltip(string title, string description)
    {
        if (_tooltipGO == null) return;
        _tooltipGO.SetActive(true);
        if (_tooltipTitle != null) _tooltipTitle.text = title;
        if (_tooltipDesc != null) _tooltipDesc.text = description;
    }

    private void hideTooltip()
    {
        if (_tooltipGO != null) _tooltipGO.SetActive(false);
    }

    void Update()
    {
        // Follow mouse for tooltip
        if (_tooltipGO != null && _tooltipGO.activeSelf)
        {
            var rt = _tooltipGO.GetComponent<RectTransform>();
            if (rt != null)
            {
                Vector2 pos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rt.parent as RectTransform, Input.mousePosition, null, out pos);
                rt.anchoredPosition = pos + new Vector2(15, -15);
            }
        }
    }

    private void clearCards()
    {
        for (int i = 0; i < _cardInstances.Count; i++)
        {
            if (_cardInstances[i] != null)
                Destroy(_cardInstances[i]);
        }
        _cardInstances.Clear();
    }
}
