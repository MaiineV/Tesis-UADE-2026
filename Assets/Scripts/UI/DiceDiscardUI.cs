using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

public class DiceDiscardUI : MonoBehaviour
{
    public static DiceDiscardUI Instance;

    private GameObject _panel;
    private Transform _listParent;
    private TMP_Text _titleText;

    // Callbacks
    public Action<string> OnDiceDiscarded;
    public Action OnCancelClicked;

    private List<GameObject> _cardInstances = new List<GameObject>();

    void Awake()
    {
        Instance = this;
    }

    public void Initialize(GameObject panel, Transform listParent, TMP_Text titleText)
    {
        _panel = panel;
        _listParent = listParent;
        _titleText = titleText;
    }

    public void Show(List<DiceInstance> currentDice, DiceInstance pendingDie)
    {
        if (_panel == null) return;
        _panel.SetActive(true);

        if (_titleText != null)
            _titleText.text = "BOLSA LLENA - Descarta un dado";

        clearCards();

        for (int i = 0; i < currentDice.Count; i++)
        {
            var die = currentDice[i];
            createDiceCard(die);
        }

        // Show the pending new die info at the bottom (no discard button)
        if (pendingDie != null)
        {
            var infoGO = new GameObject("PendingInfo");
            infoGO.transform.SetParent(_listParent, false);
            var rt = infoGO.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(260, 40);
            var img = infoGO.AddComponent<Image>();
            ColorUtility.TryParseHtmlString("#1e1e3a", out Color panelBg);
            img.color = new Color(panelBg.r, panelBg.g, panelBg.b, 0.6f);

            var label = new GameObject("PendingLabel");
            label.transform.SetParent(infoGO.transform, false);
            var lrt = label.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var tmp = label.AddComponent<TextMeshProUGUI>();
            tmp.text = $"Nuevo: {pendingDie.BaseData.DiceName} (costo {pendingDie.PowerCost})";
            tmp.fontSize = 14;
            tmp.alignment = TextAlignmentOptions.Center;
            ColorUtility.TryParseHtmlString("#ffd54f", out Color gold);
            tmp.color = gold;
            tmp.raycastTarget = false;

            _cardInstances.Add(infoGO);
        }
    }

    public void Hide()
    {
        if (_panel != null) _panel.SetActive(false);
        clearCards();
    }

    private void createDiceCard(DiceInstance die)
    {
        ColorUtility.TryParseHtmlString("#1e1e3a", out Color panelBg);
        ColorUtility.TryParseHtmlString("#e0e0e0", out Color textColor);
        ColorUtility.TryParseHtmlString("#e53935", out Color discardColor);

        var card = new GameObject($"DiceCard_{die.Id}");
        card.transform.SetParent(_listParent, false);
        var cardRT = card.AddComponent<RectTransform>();
        cardRT.sizeDelta = new Vector2(260, 50);
        var cardImg = card.AddComponent<Image>();
        cardImg.color = new Color(panelBg.r, panelBg.g, panelBg.b, 0.9f);

        var hlg = card.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 5;
        hlg.padding = new RectOffset(8, 8, 5, 5);
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;

        // Die name + faces
        var nameGO = new GameObject("Name");
        nameGO.transform.SetParent(card.transform, false);
        var nameRT = nameGO.AddComponent<RectTransform>();
        var nameLE = nameGO.AddComponent<LayoutElement>();
        nameLE.flexibleWidth = 1;
        var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        string facesStr = "";
        for (int f = 0; f < die.CurrentFaces.Length; f++)
        {
            if (f > 0) facesStr += ",";
            facesStr += die.CurrentFaces[f].ToString();
        }
        nameTMP.text = $"{die.BaseData.DiceName} [{facesStr}]";
        nameTMP.fontSize = 14;
        nameTMP.color = textColor;
        nameTMP.alignment = TextAlignmentOptions.Left;
        nameTMP.raycastTarget = false;

        // Discard button
        var btnGO = new GameObject("DiscardBtn");
        btnGO.transform.SetParent(card.transform, false);
        var btnRT = btnGO.AddComponent<RectTransform>();
        var btnLE = btnGO.AddComponent<LayoutElement>();
        btnLE.preferredWidth = 70;
        btnLE.preferredHeight = 35;
        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(discardColor.r * 0.6f, discardColor.g * 0.6f, discardColor.b * 0.6f, 0.95f);
        var btn = btnGO.AddComponent<Button>();

        var btnLabel = new GameObject("Label");
        btnLabel.transform.SetParent(btnGO.transform, false);
        var btnLabelRT = btnLabel.AddComponent<RectTransform>();
        btnLabelRT.anchorMin = Vector2.zero;
        btnLabelRT.anchorMax = Vector2.one;
        btnLabelRT.offsetMin = Vector2.zero;
        btnLabelRT.offsetMax = Vector2.zero;
        var btnTMP = btnLabel.AddComponent<TextMeshProUGUI>();
        btnTMP.text = "DESCARTAR";
        btnTMP.fontSize = 11;
        btnTMP.color = textColor;
        btnTMP.alignment = TextAlignmentOptions.Center;
        btnTMP.raycastTarget = false;

        string dieId = die.Id;
        btn.onClick.AddListener(() => OnDiceDiscarded?.Invoke(dieId));

        _cardInstances.Add(card);
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
