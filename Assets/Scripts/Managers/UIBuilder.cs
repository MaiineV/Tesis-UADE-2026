using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIBuilder : MonoBehaviour
{
    // Colors
    private static Color PanelBgColor;
    private static Color TextColor;
    private static Color AccentGold;
    private static Color HPColor;
    private static Color EnergyColor;
    private static Color ShieldColor;
    private static Color D6Color;
    private static Color D8Color;
    private static Color D12Color;
    private static Color DarkBg;

    static UIBuilder()
    {
        ColorUtility.TryParseHtmlString("#1e1e3a", out PanelBgColor);
        ColorUtility.TryParseHtmlString("#e0e0e0", out TextColor);
        ColorUtility.TryParseHtmlString("#ffd54f", out AccentGold);
        ColorUtility.TryParseHtmlString("#e53935", out HPColor);
        ColorUtility.TryParseHtmlString("#ffb300", out EnergyColor);
        ColorUtility.TryParseHtmlString("#78909c", out ShieldColor);
        ColorUtility.TryParseHtmlString("#42a5f5", out D6Color);
        ColorUtility.TryParseHtmlString("#66bb6a", out D8Color);
        ColorUtility.TryParseHtmlString("#ab47bc", out D12Color);
        ColorUtility.TryParseHtmlString("#1a1a2e", out DarkBg);
    }

    public void BuildAllUI(Canvas canvas)
    {
        var canvasTransform = canvas.transform;

        // ── HUD Panel (always visible, top) ──
        var hudPanel = CreatePanel("HUDPanel", canvasTransform,
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(0, -10), new Vector2(0, -10),
            150f, anchorTopStretch: true);

        // Health Bar
        var healthBarGO = CreatePanel("HealthBar", hudPanel.transform,
            Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, 0);
        var healthRT = healthBarGO.GetComponent<RectTransform>();
        healthRT.anchorMin = new Vector2(0, 0.5f);
        healthRT.anchorMax = new Vector2(0, 0.5f);
        healthRT.pivot = new Vector2(0, 0.5f);
        healthRT.anchoredPosition = new Vector2(20, 20);
        healthRT.sizeDelta = new Vector2(300, 35);

        var hpBgImage = healthBarGO.GetComponent<Image>();
        hpBgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

        var hpFillGO = CreateChildImage("HPFill", healthBarGO.transform, HPColor);
        var hpFillRT = hpFillGO.GetComponent<RectTransform>();
        hpFillRT.anchorMin = Vector2.zero;
        hpFillRT.anchorMax = Vector2.one;
        hpFillRT.offsetMin = new Vector2(2, 2);
        hpFillRT.offsetMax = new Vector2(-2, -2);
        var hpFillImage = hpFillGO.GetComponent<Image>();
        hpFillImage.type = Image.Type.Filled;
        hpFillImage.fillMethod = Image.FillMethod.Horizontal;

        var hpText = CreateTMPText("HPText", healthBarGO.transform, "100/100", 16,
            TextAlignmentOptions.Center, Color.white);
        StretchFull(hpText.GetComponent<RectTransform>());

        var healthBarUI = healthBarGO.AddComponent<HealthBarUI>();
        healthBarUI.Initialize(hpFillImage, hpText);

        // Energy Bar
        var energyBarGO = CreatePanel("EnergyBar", hudPanel.transform,
            Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, 0);
        var energyRT = energyBarGO.GetComponent<RectTransform>();
        energyRT.anchorMin = new Vector2(0, 0.5f);
        energyRT.anchorMax = new Vector2(0, 0.5f);
        energyRT.pivot = new Vector2(0, 0.5f);
        energyRT.anchoredPosition = new Vector2(20, -20);
        energyRT.sizeDelta = new Vector2(300, 25);

        var energyBgImage = energyBarGO.GetComponent<Image>();
        energyBgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

        var energyFillGO = CreateChildImage("EnergyFill", energyBarGO.transform, EnergyColor);
        var energyFillRT = energyFillGO.GetComponent<RectTransform>();
        energyFillRT.anchorMin = Vector2.zero;
        energyFillRT.anchorMax = Vector2.one;
        energyFillRT.offsetMin = new Vector2(2, 2);
        energyFillRT.offsetMax = new Vector2(-2, -2);
        var energyFillImage = energyFillGO.GetComponent<Image>();
        energyFillImage.type = Image.Type.Filled;
        energyFillImage.fillMethod = Image.FillMethod.Horizontal;
        energyFillImage.fillAmount = 0f;

        var energyText = CreateTMPText("EnergyText", energyBarGO.transform, "0%", 14,
            TextAlignmentOptions.Center, Color.white);
        StretchFull(energyText.GetComponent<RectTransform>());

        var crapsReadyText = CreateTMPText("CrapsReadyText", energyBarGO.transform, "CRAPS MODE READY", 14,
            TextAlignmentOptions.Center, AccentGold);
        var crapsReadyRT = crapsReadyText.GetComponent<RectTransform>();
        crapsReadyRT.anchorMin = new Vector2(1, 0.5f);
        crapsReadyRT.anchorMax = new Vector2(1, 0.5f);
        crapsReadyRT.pivot = new Vector2(0, 0.5f);
        crapsReadyRT.anchoredPosition = new Vector2(10, 0);
        crapsReadyRT.sizeDelta = new Vector2(200, 25);
        crapsReadyText.gameObject.SetActive(false);

        var energyBarUI = energyBarGO.AddComponent<EnergyBarUI>();
        energyBarUI.Initialize(energyFillImage, energyText, crapsReadyText);

        // Shield Display
        var shieldGO = new GameObject("ShieldDisplay");
        shieldGO.transform.SetParent(hudPanel.transform, false);
        var shieldRT = shieldGO.AddComponent<RectTransform>();
        shieldRT.anchorMin = new Vector2(0, 0.5f);
        shieldRT.anchorMax = new Vector2(0, 0.5f);
        shieldRT.pivot = new Vector2(0, 0.5f);
        shieldRT.anchoredPosition = new Vector2(340, 20);
        shieldRT.sizeDelta = new Vector2(150, 35);

        var shieldText = CreateTMPText("ShieldText", shieldGO.transform, "", 18,
            TextAlignmentOptions.Left, ShieldColor);
        StretchFull(shieldText.GetComponent<RectTransform>());

        var shieldDisplay = shieldGO.AddComponent<ShieldDisplay>();
        shieldDisplay.Initialize(shieldText);

        // Level Indicator
        var levelTextGO = new GameObject("LevelText");
        levelTextGO.transform.SetParent(hudPanel.transform, false);
        var levelTextRT = levelTextGO.AddComponent<RectTransform>();
        levelTextRT.anchorMin = new Vector2(0.5f, 0.5f);
        levelTextRT.anchorMax = new Vector2(0.5f, 0.5f);
        levelTextRT.pivot = new Vector2(0.5f, 0.5f);
        levelTextRT.anchoredPosition = new Vector2(0, 40);
        levelTextRT.sizeDelta = new Vector2(200, 40);
        var levelTMP = levelTextGO.AddComponent<TextMeshProUGUI>();
        levelTMP.text = "Level 1";
        levelTMP.fontSize = 22;
        levelTMP.alignment = TextAlignmentOptions.Center;
        levelTMP.color = AccentGold;
        levelTMP.fontStyle = FontStyles.Bold;
        levelTMP.raycastTarget = false;

        // ── Volume Slider (top-right of HUD) ──
        var volumePanel = new GameObject("VolumePanel");
        volumePanel.transform.SetParent(hudPanel.transform, false);
        var volumePanelRT = volumePanel.AddComponent<RectTransform>();
        volumePanelRT.anchorMin = new Vector2(1, 0.5f);
        volumePanelRT.anchorMax = new Vector2(1, 0.5f);
        volumePanelRT.pivot = new Vector2(1, 0.5f);
        volumePanelRT.anchoredPosition = new Vector2(-20, 20);
        volumePanelRT.sizeDelta = new Vector2(180, 30);

        // Volume icon
        var volumeIcon = CreateTMPText("VolumeIcon", volumePanel.transform, "\u266a", 18,
            TextAlignmentOptions.Left, TextColor);
        var volumeIconRT = volumeIcon.GetComponent<RectTransform>();
        volumeIconRT.anchorMin = new Vector2(0, 0);
        volumeIconRT.anchorMax = new Vector2(0, 1);
        volumeIconRT.pivot = new Vector2(0, 0.5f);
        volumeIconRT.anchoredPosition = Vector2.zero;
        volumeIconRT.sizeDelta = new Vector2(25, 0);

        // Slider GO
        var sliderGO = new GameObject("VolumeSlider");
        sliderGO.transform.SetParent(volumePanel.transform, false);
        var sliderRT = sliderGO.AddComponent<RectTransform>();
        sliderRT.anchorMin = new Vector2(0, 0);
        sliderRT.anchorMax = new Vector2(1, 1);
        sliderRT.offsetMin = new Vector2(25, 4);
        sliderRT.offsetMax = new Vector2(0, -4);

        // Background
        var sliderBg = CreateChildImage("SliderBg", sliderGO.transform, new Color(0.1f, 0.1f, 0.1f, 0.8f));
        StretchFull(sliderBg.GetComponent<RectTransform>());

        // Fill Area
        var fillArea = new GameObject("FillArea");
        fillArea.transform.SetParent(sliderGO.transform, false);
        var fillAreaRT = fillArea.AddComponent<RectTransform>();
        fillAreaRT.anchorMin = Vector2.zero;
        fillAreaRT.anchorMax = Vector2.one;
        fillAreaRT.offsetMin = new Vector2(2, 2);
        fillAreaRT.offsetMax = new Vector2(-2, -2);

        var fillGO = CreateChildImage("Fill", fillArea.transform, AccentGold);
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;

        // Handle area
        var handleArea = new GameObject("HandleSlideArea");
        handleArea.transform.SetParent(sliderGO.transform, false);
        var handleAreaRT = handleArea.AddComponent<RectTransform>();
        handleAreaRT.anchorMin = Vector2.zero;
        handleAreaRT.anchorMax = Vector2.one;
        handleAreaRT.offsetMin = new Vector2(2, 0);
        handleAreaRT.offsetMax = new Vector2(-2, 0);

        var handleGO = CreateChildImage("Handle", handleArea.transform, Color.white);
        var handleRT = handleGO.GetComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(12, 0);
        handleRT.anchorMin = new Vector2(0, 0);
        handleRT.anchorMax = new Vector2(0, 1);

        // Slider component
        var slider = sliderGO.AddComponent<Slider>();
        slider.targetGraphic = handleGO.GetComponent<Image>();
        slider.fillRect = fillRT;
        slider.handleRect = handleRT;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;

        var volumeSliderUI = volumePanel.AddComponent<VolumeSliderUI>();
        volumeSliderUI.Initialize(slider);

        // Phase Label (centered, large)
        var phaseLabel = CreateTMPText("PhaseLabel", hudPanel.transform, "", 36,
            TextAlignmentOptions.Center, AccentGold);
        var phaseLabelRT = phaseLabel.GetComponent<RectTransform>();
        phaseLabelRT.anchorMin = new Vector2(0.5f, 0.5f);
        phaseLabelRT.anchorMax = new Vector2(0.5f, 0.5f);
        phaseLabelRT.sizeDelta = new Vector2(600, 60);
        phaseLabel.fontStyle = FontStyles.Bold;
        phaseLabel.gameObject.SetActive(false);

        // ── Gold Display (HUD) ──
        var goldTextGO = new GameObject("GoldText");
        goldTextGO.transform.SetParent(hudPanel.transform, false);
        var goldTextRT = goldTextGO.AddComponent<RectTransform>();
        goldTextRT.anchorMin = new Vector2(1, 0.5f);
        goldTextRT.anchorMax = new Vector2(1, 0.5f);
        goldTextRT.pivot = new Vector2(1, 0.5f);
        goldTextRT.anchoredPosition = new Vector2(-20, -20);
        goldTextRT.sizeDelta = new Vector2(150, 30);
        var goldTMP = goldTextGO.AddComponent<TextMeshProUGUI>();
        goldTMP.text = "0 G";
        goldTMP.fontSize = 20;
        goldTMP.alignment = TextAlignmentOptions.Right;
        goldTMP.color = AccentGold;
        goldTMP.fontStyle = FontStyles.Bold;
        goldTMP.raycastTarget = false;

        if (UIManager.Instance != null)
            UIManager.Instance.SetGoldText(goldTMP);

        // Dexterity Display
        var dexTextGO = new GameObject("DexterityText");
        dexTextGO.transform.SetParent(hudPanel.transform, false);
        var dexTextRT = dexTextGO.AddComponent<RectTransform>();
        dexTextRT.anchorMin = new Vector2(1, 0.5f);
        dexTextRT.anchorMax = new Vector2(1, 0.5f);
        dexTextRT.pivot = new Vector2(1, 0.5f);
        dexTextRT.anchoredPosition = new Vector2(-20, -50);
        dexTextRT.sizeDelta = new Vector2(150, 30);
        var dexTMP = dexTextGO.AddComponent<TextMeshProUGUI>();
        dexTMP.text = "DEX: 0";
        dexTMP.fontSize = 18;
        dexTMP.alignment = TextAlignmentOptions.Right;
        dexTMP.color = TextColor;
        dexTMP.fontStyle = FontStyles.Bold;
        dexTMP.raycastTarget = false;

        if (UIManager.Instance != null)
            UIManager.Instance.SetDexterityText(dexTMP);

        // ── Exploration Action Buttons (left side) ──
        var explorationPanel = CreatePanel("ExplorationPanel", canvasTransform,
            new Vector2(0, 0.3f), new Vector2(0, 0.7f),
            new Vector2(100, 0), new Vector2(100, 0), 0);
        var explorationPanelRT = explorationPanel.GetComponent<RectTransform>();
        explorationPanelRT.sizeDelta = new Vector2(150, 0);
        explorationPanel.GetComponent<Image>().color = new Color(PanelBgColor.r, PanelBgColor.g, PanelBgColor.b, 0.85f);

        var vlg = explorationPanel.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8;
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var moveBtn = CreateButton("MoveBtn", explorationPanel.transform, "MOVER", D6Color);
        var moveBtnLE = moveBtn.AddComponent<LayoutElement>();
        moveBtnLE.preferredHeight = 36;

        var bowBtn = CreateButton("BowBtn", explorationPanel.transform, "ARCO", new Color(0.9f, 0.4f, 0.3f));
        var bowBtnLE = bowBtn.AddComponent<LayoutElement>();
        bowBtnLE.preferredHeight = 36;

        var potionBtn = CreateButton("PotionBtn", explorationPanel.transform, "POCION", new Color(0.3f, 0.8f, 0.5f));
        var potionBtnLE = potionBtn.AddComponent<LayoutElement>();
        potionBtnLE.preferredHeight = 36;

        var potionCountText = CreateTMPText("PotionCount", potionBtn.transform, "x1", 12,
            TextAlignmentOptions.Right, Color.white);
        var potionCountRT = potionCountText.GetComponent<RectTransform>();
        potionCountRT.anchorMin = new Vector2(0.6f, 0);
        potionCountRT.anchorMax = Vector2.one;
        potionCountRT.offsetMin = Vector2.zero;
        potionCountRT.offsetMax = new Vector2(-4, 0);

        var fleeBtn = CreateButton("FleeBtn", explorationPanel.transform, "HUIR", new Color(0.8f, 0.6f, 0.2f));
        var fleeBtnLE = fleeBtn.AddComponent<LayoutElement>();
        fleeBtnLE.preferredHeight = 36;
        fleeBtn.SetActive(false);

        var forceDoorBtn = CreateButton("ForceDoorBtn", explorationPanel.transform, "FORZAR", ShieldColor);
        var forceDoorBtnLE = forceDoorBtn.AddComponent<LayoutElement>();
        forceDoorBtnLE.preferredHeight = 36;
        forceDoorBtn.SetActive(false);

        var explorationActionsUI = explorationPanel.AddComponent<ExplorationActionsUI>();
        explorationActionsUI.Initialize(
            moveBtn.GetComponent<Button>(),
            bowBtn.GetComponent<Button>(),
            potionBtn.GetComponent<Button>(),
            fleeBtn.GetComponent<Button>(),
            forceDoorBtn.GetComponent<Button>(),
            potionCountText);
        explorationPanel.SetActive(false);

        if (UIManager.Instance != null)
            UIManager.Instance.SetExplorationActionsUI(explorationActionsUI);

        // ── Minimap (top-right corner) ──
        var minimapPanel = CreatePanel("MinimapPanel", canvasTransform,
            new Vector2(1, 1), new Vector2(1, 1),
            Vector2.zero, Vector2.zero, 0);
        var minimapPanelRT = minimapPanel.GetComponent<RectTransform>();
        minimapPanelRT.pivot = new Vector2(1, 1);
        minimapPanelRT.anchoredPosition = new Vector2(-10, -160);
        minimapPanelRT.sizeDelta = new Vector2(180, 140);
        minimapPanel.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);

        var minimapLabel = CreateTMPText("MinimapLabel", minimapPanel.transform, "MAP", 12,
            TextAlignmentOptions.Center, AccentGold);
        var minimapLabelRT = minimapLabel.GetComponent<RectTransform>();
        minimapLabelRT.anchorMin = new Vector2(0, 0.85f);
        minimapLabelRT.anchorMax = Vector2.one;
        minimapLabelRT.offsetMin = Vector2.zero;
        minimapLabelRT.offsetMax = Vector2.zero;

        var minimapCellContainer = new GameObject("MinimapCells");
        minimapCellContainer.transform.SetParent(minimapPanel.transform, false);
        var minimapCellContainerRT = minimapCellContainer.AddComponent<RectTransform>();
        minimapCellContainerRT.anchorMin = new Vector2(0.1f, 0.05f);
        minimapCellContainerRT.anchorMax = new Vector2(0.9f, 0.85f);
        minimapCellContainerRT.offsetMin = Vector2.zero;
        minimapCellContainerRT.offsetMax = Vector2.zero;

        var minimapUI = minimapPanel.AddComponent<MinimapUI>();
        minimapUI.Initialize(minimapCellContainer.transform);

        if (UIManager.Instance != null)
            UIManager.Instance.SetMinimapUI(minimapUI);

        // ── Combat Panel ──
        var combatPanel = CreatePanel("CombatPanel", canvasTransform,
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(10, 10), new Vector2(-10, 10),
            280f, anchorBottomStretch: true);
        // Note: SetActive(false) is deferred until after CombatUI is added below

        // -- Attack Panel --
        var attackPanel = CreatePanel("AttackPanel", combatPanel.transform,
            Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, 0);
        StretchFull(attackPanel.GetComponent<RectTransform>());

        // Dice Container with HorizontalLayoutGroup
        var diceContainer = new GameObject("DiceContainer");
        diceContainer.transform.SetParent(attackPanel.transform, false);
        var diceContainerRT = diceContainer.AddComponent<RectTransform>();
        diceContainerRT.anchorMin = new Vector2(0, 0.4f);
        diceContainerRT.anchorMax = new Vector2(1, 1);
        diceContainerRT.offsetMin = new Vector2(20, 0);
        diceContainerRT.offsetMax = new Vector2(-20, -10);
        var hlg = diceContainer.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        // Craps bet indicator (shown during craps round)
        var crapsBetIndicator = CreateTMPText("CrapsBetIndicator", attackPanel.transform,
            "", 16, TextAlignmentOptions.Right, AccentGold);
        crapsBetIndicator.fontStyle = FontStyles.Bold;
        var crapsBetRT = crapsBetIndicator.GetComponent<RectTransform>();
        crapsBetRT.anchorMin = new Vector2(0.5f, 0);
        crapsBetRT.anchorMax = new Vector2(1, 0.4f);
        crapsBetRT.offsetMin = new Vector2(10, 10);
        crapsBetRT.offsetMax = new Vector2(-20, -5);
        crapsBetIndicator.gameObject.SetActive(false);

        // Combo preview text
        var comboPreview = CreateTMPText("ComboPreview", attackPanel.transform,
            "Combo: \u2014", 18, TextAlignmentOptions.Left, TextColor);
        var comboRT = comboPreview.GetComponent<RectTransform>();
        comboRT.anchorMin = new Vector2(0, 0);
        comboRT.anchorMax = new Vector2(0.5f, 0.4f);
        comboRT.offsetMin = new Vector2(20, 10);
        comboRT.offsetMax = new Vector2(-10, -5);

        // Roll counter text
        var rollCounter = CreateTMPText("RollCounter", attackPanel.transform,
            "Roll 0/3", 16, TextAlignmentOptions.Left, TextColor);
        var rollCounterRT = rollCounter.GetComponent<RectTransform>();
        rollCounterRT.anchorMin = new Vector2(0, 0);
        rollCounterRT.anchorMax = new Vector2(0.3f, 0.15f);
        rollCounterRT.offsetMin = new Vector2(20, 0);
        rollCounterRT.offsetMax = new Vector2(-10, 0);

        // Reroll Button
        var rerollButton = CreateButton("RerollButton", attackPanel.transform,
            "REROLL", D6Color);
        var rerollBtnRT = rerollButton.GetComponent<RectTransform>();
        rerollBtnRT.anchorMin = new Vector2(0.55f, 0.05f);
        rerollBtnRT.anchorMax = new Vector2(0.75f, 0.35f);
        rerollBtnRT.offsetMin = Vector2.zero;
        rerollBtnRT.offsetMax = Vector2.zero;

        // Commit Button
        var commitButton = CreateButton("CommitButton", attackPanel.transform,
            "COMMIT ATTACK", AccentGold);
        var commitBtnRT = commitButton.GetComponent<RectTransform>();
        commitBtnRT.anchorMin = new Vector2(0.77f, 0.05f);
        commitBtnRT.anchorMax = new Vector2(0.98f, 0.35f);
        commitBtnRT.offsetMin = Vector2.zero;
        commitBtnRT.offsetMax = Vector2.zero;

        // -- Defense Panel --
        var defensePanel = CreatePanel("DefensePanel", combatPanel.transform,
            Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, 0);
        StretchFull(defensePanel.GetComponent<RectTransform>());
        defensePanel.SetActive(false);

        // Defense Dice Container (mirrors attack diceContainer)
        var defenseDiceContainer = new GameObject("DefenseDiceContainer");
        defenseDiceContainer.transform.SetParent(defensePanel.transform, false);
        var defenseDiceRT = defenseDiceContainer.AddComponent<RectTransform>();
        defenseDiceRT.anchorMin = new Vector2(0, 0.42f);
        defenseDiceRT.anchorMax = new Vector2(1, 1);
        defenseDiceRT.offsetMin = new Vector2(20, 0);
        defenseDiceRT.offsetMax = new Vector2(-20, -10);
        var defenseHLG = defenseDiceContainer.AddComponent<HorizontalLayoutGroup>();
        defenseHLG.spacing = 10;
        defenseHLG.childAlignment = TextAnchor.MiddleCenter;
        defenseHLG.childForceExpandWidth = true;
        defenseHLG.childForceExpandHeight = false;
        defenseHLG.childControlWidth = false;
        defenseHLG.childControlHeight = false;

        var defenseTitle = CreateTMPText("DefenseTitle", defensePanel.transform,
            "DEFENSA", 20, TextAlignmentOptions.Center, AccentGold);
        SetRect(defenseTitle, 0, 0.31f, 1, 0.43f);
        defenseTitle.fontStyle = FontStyles.Bold;

        var defenseComboPreview = CreateTMPText("DefenseComboPreview", defensePanel.transform,
            "Escudo: \u2014", 18, TextAlignmentOptions.Left, TextColor);
        SetRect(defenseComboPreview, 0, 0.21f, 0.7f, 0.31f);
        defenseComboPreview.GetComponent<RectTransform>().offsetMin = new Vector2(20, 0);

        var defenseShieldText = CreateTMPText("DefenseShield", defensePanel.transform,
            "", 18, TextAlignmentOptions.Right, ShieldColor);
        SetRect(defenseShieldText, 0.5f, 0.21f, 1, 0.31f);
        defenseShieldText.GetComponent<RectTransform>().offsetMax = new Vector2(-20, 0);

        var defenseRollsText = CreateTMPText("DefenseRolls", defensePanel.transform,
            "Roll 0/1", 16, TextAlignmentOptions.Left, TextColor);
        SetRect(defenseRollsText, 0, 0.13f, 0.5f, 0.21f);
        defenseRollsText.GetComponent<RectTransform>().offsetMin = new Vector2(20, 0);

        var rollDefenseButton = CreateButton("RollDefenseButton", defensePanel.transform,
            "REROLL", ShieldColor);
        SetRect(rollDefenseButton, 0.02f, 0.02f, 0.47f, 0.14f);

        var commitDefenseButton = CreateButton("CommitDefenseButton", defensePanel.transform,
            "CONFIRMAR DEFENSA", AccentGold);
        SetRect(commitDefenseButton, 0.51f, 0.02f, 0.98f, 0.14f);

        // -- Enemy Attack Panel --
        var enemyAttackPanel = CreatePanel("EnemyAttackPanel", combatPanel.transform,
            Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, 0);
        StretchFull(enemyAttackPanel.GetComponent<RectTransform>());
        enemyAttackPanel.SetActive(false);

        var enemyAttackTitle = CreateTMPText("EnemyAttackTitle", enemyAttackPanel.transform,
            "ENEMY ATTACKS!", 24, TextAlignmentOptions.Center, HPColor);
        SetRect(enemyAttackTitle, 0, 0.75f, 1, 1);
        enemyAttackTitle.fontStyle = FontStyles.Bold;

        var enemyRollText = CreateTMPText("EnemyRollText", enemyAttackPanel.transform,
            "", 18, TextAlignmentOptions.Center, TextColor);
        SetRect(enemyRollText, 0, 0.55f, 1, 0.75f);

        var shieldAbsorbText = CreateTMPText("ShieldAbsorbText", enemyAttackPanel.transform,
            "", 18, TextAlignmentOptions.Center, ShieldColor);
        SetRect(shieldAbsorbText, 0, 0.35f, 1, 0.55f);

        var netDamageText = CreateTMPText("NetDamageText", enemyAttackPanel.transform,
            "", 20, TextAlignmentOptions.Center, HPColor);
        SetRect(netDamageText, 0, 0.15f, 1, 0.35f);

        var continueButton = CreateButton("ContinueButton", enemyAttackPanel.transform,
            "CONTINUE", TextColor);
        SetRect(continueButton, 0.3f, 0.02f, 0.7f, 0.18f);

        // -- Dice Slot Prefab (stored in memory, not on disk) --
        var dicePrefab = CreateDiceSlotPrefab();

        // ── CombatUI component ──
        var combatUIGO = combatPanel;
        var combatUI = combatUIGO.AddComponent<CombatUI>();
        combatUI.Initialize(
            diceContainer.transform,
            dicePrefab,
            comboPreview,
            rollCounter,
            rerollButton.GetComponent<Button>(),
            commitButton.GetComponent<Button>(),
            attackPanel,
            defensePanel,
            defenseTitle,
            defenseRollsText,
            defenseComboPreview,
            defenseShieldText,
            rollDefenseButton.GetComponent<Button>(),
            enemyAttackPanel,
            enemyAttackTitle,
            enemyRollText,
            shieldAbsorbText,
            netDamageText,
            continueButton.GetComponent<Button>()
        );
        combatUI.InitializeDefense(
            defenseDiceContainer.transform,
            commitDefenseButton.GetComponent<Button>()
        );
        combatUI.SetCrapsBetIndicator(crapsBetIndicator);

        // Now hide combat panel (after CombatUI.Awake has set Instance)
        combatPanel.SetActive(false);

        // ── Craps Overlay ──
        var crapsOverlay = CreateOverlayPanel("CrapsOverlay", canvasTransform);
        // SetActive(false) deferred until after CrapsUI is added

        var crapsInner = CreatePanel("CrapsInner", crapsOverlay.transform,
            new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f),
            Vector2.zero, Vector2.zero, 0);
        StretchAnchors(crapsInner.GetComponent<RectTransform>(), 0.1f, 0.1f, 0.9f, 0.9f);

        // Bet panel (the main content)
        var betPanel = crapsInner;

        var betTitle = CreateTMPText("BetTitle", betPanel.transform,
            "CRAPS MODE ACTIVATED!\nPredict your next combo:", 22,
            TextAlignmentOptions.Center, AccentGold);
        SetRect(betTitle, 0, 0.75f, 1, 1);
        betTitle.fontStyle = FontStyles.Bold;

        // Bet Buttons - 2 rows of 3
        string[] betNames = { "Pair", "Three of a Kind", "Straight", "Full House", "Four of a Kind", "Generala" };
        GameObject[] betButtons = new GameObject[6];
        for (int i = 0; i < 6; i++)
        {
            int row = i / 3;
            int col = i % 3;
            float xMin = 0.05f + col * 0.31f;
            float xMax = xMin + 0.28f;
            float yMax = 0.7f - row * 0.35f;
            float yMin = yMax - 0.3f;

            betButtons[i] = CreateButton($"Bet{betNames[i].Replace(" ", "")}", betPanel.transform,
                betNames[i], AccentGold);
            SetRect(betButtons[i], xMin, yMin, xMax, yMax);
        }

        var crapsUI = crapsOverlay.AddComponent<CrapsUI>();
        crapsUI.Initialize(
            betPanel,
            betTitle,
            betButtons[0].GetComponent<Button>(),
            betButtons[1].GetComponent<Button>(),
            betButtons[2].GetComponent<Button>(),
            betButtons[3].GetComponent<Button>(),
            betButtons[4].GetComponent<Button>(),
            betButtons[5].GetComponent<Button>()
        );
        crapsOverlay.SetActive(false);

        // ── Craps Toast (top-left, below HUD) ──
        var crapsToastGO = new GameObject("CrapsToast");
        crapsToastGO.transform.SetParent(canvasTransform, false);
        var toastRT = crapsToastGO.AddComponent<RectTransform>();
        toastRT.anchorMin = new Vector2(0, 1);
        toastRT.anchorMax = new Vector2(0, 1);
        toastRT.pivot = new Vector2(0, 1);
        toastRT.anchoredPosition = new Vector2(10, -165);
        toastRT.sizeDelta = new Vector2(320, 100);

        var toastBg = crapsToastGO.AddComponent<Image>();
        toastBg.color = new Color(0.18f, 0.49f, 0.2f, 0.95f);

        var toastTitle = CreateTMPText("ToastTitle", crapsToastGO.transform,
            "", 20, TextAlignmentOptions.Left, AccentGold);
        toastTitle.fontStyle = FontStyles.Bold;
        var toastTitleRT = toastTitle.GetComponent<RectTransform>();
        toastTitleRT.anchorMin = new Vector2(0, 0.55f);
        toastTitleRT.anchorMax = new Vector2(1, 1);
        toastTitleRT.offsetMin = new Vector2(10, 0);
        toastTitleRT.offsetMax = new Vector2(-10, -5);

        var toastDetails = CreateTMPText("ToastDetails", crapsToastGO.transform,
            "", 14, TextAlignmentOptions.Left, TextColor);
        var toastDetailsRT = toastDetails.GetComponent<RectTransform>();
        toastDetailsRT.anchorMin = new Vector2(0, 0);
        toastDetailsRT.anchorMax = new Vector2(1, 0.55f);
        toastDetailsRT.offsetMin = new Vector2(10, 5);
        toastDetailsRT.offsetMax = new Vector2(-10, 0);

        var crapsToastUI = crapsToastGO.AddComponent<CrapsToastUI>();
        crapsToastUI.Initialize(crapsToastGO, toastBg, toastTitle, toastDetails);
        crapsToastGO.SetActive(false);

        // ── Reward Overlay ──
        var rewardOverlay = CreateOverlayPanel("RewardOverlay", canvasTransform);

        var rewardPanel = rewardOverlay; // the overlay IS the panel

        var rewardTitle = CreateTMPText("RewardTitle", rewardPanel.transform,
            "ENEMY DEFEATED!\nChoose a reward:", 24, TextAlignmentOptions.Center, AccentGold);
        SetRect(rewardTitle, 0, 0.75f, 1, 0.95f);
        rewardTitle.fontStyle = FontStyles.Bold;

        // Card A
        var cardA = CreatePanel("CardA", rewardPanel.transform,
            Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, 0);
        var cardABg = cardA.GetComponent<Image>();
        cardABg.color = new Color(PanelBgColor.r, PanelBgColor.g, PanelBgColor.b, 0.95f);
        SetRect(cardA, 0.05f, 0.1f, 0.47f, 0.72f);

        var cardATitle = CreateTMPText("CardATitle", cardA.transform,
            "OPTION A", 20, TextAlignmentOptions.Center, AccentGold);
        SetRect(cardATitle, 0.05f, 0.55f, 0.95f, 0.95f);
        cardATitle.fontStyle = FontStyles.Bold;

        var cardADesc = CreateTMPText("CardADesc", cardA.transform,
            "", 16, TextAlignmentOptions.Center, TextColor);
        SetRect(cardADesc, 0.05f, 0.25f, 0.95f, 0.55f);

        var cardAButton = CreateButton("CardAButton", cardA.transform, "CHOOSE", AccentGold);
        SetRect(cardAButton, 0.2f, 0.05f, 0.8f, 0.22f);

        // Card B
        var cardB = CreatePanel("CardB", rewardPanel.transform,
            Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, 0);
        var cardBBg = cardB.GetComponent<Image>();
        cardBBg.color = new Color(PanelBgColor.r, PanelBgColor.g, PanelBgColor.b, 0.95f);
        SetRect(cardB, 0.53f, 0.1f, 0.95f, 0.72f);

        var cardBTitle = CreateTMPText("CardBTitle", cardB.transform,
            "OPTION B", 20, TextAlignmentOptions.Center, AccentGold);
        SetRect(cardBTitle, 0.05f, 0.55f, 0.95f, 0.95f);
        cardBTitle.fontStyle = FontStyles.Bold;

        var cardBDesc = CreateTMPText("CardBDesc", cardB.transform,
            "", 16, TextAlignmentOptions.Center, TextColor);
        SetRect(cardBDesc, 0.05f, 0.25f, 0.95f, 0.55f);

        var cardBButton = CreateButton("CardBButton", cardB.transform, "CHOOSE", AccentGold);
        SetRect(cardBButton, 0.2f, 0.05f, 0.8f, 0.22f);

        var rewardUI = rewardOverlay.AddComponent<RewardUI>();
        rewardUI.Initialize(
            rewardPanel,
            rewardTitle,
            cardA, cardATitle, cardADesc, cardAButton.GetComponent<Button>(),
            cardB, cardBTitle, cardBDesc, cardBButton.GetComponent<Button>()
        );
        rewardOverlay.SetActive(false);

        // ── Shop Overlay ──
        var shopOverlay = CreateOverlayPanel("ShopOverlay", canvasTransform);

        var shopInner = CreatePanel("ShopInner", shopOverlay.transform,
            new Vector2(0.1f, 0.15f), new Vector2(0.9f, 0.85f),
            Vector2.zero, Vector2.zero, 0);
        StretchAnchors(shopInner.GetComponent<RectTransform>(), 0.1f, 0.15f, 0.9f, 0.85f);

        var shopTitle = CreateTMPText("ShopTitle", shopInner.transform,
            "TIENDA", 28, TextAlignmentOptions.Center, AccentGold);
        SetRect(shopTitle, 0.05f, 0.85f, 0.95f, 0.97f);
        shopTitle.fontStyle = FontStyles.Bold;

        // Legacy single-item fields (hidden when multi-item is used)
        var shopItemName = CreateTMPText("ShopItemName", shopInner.transform, "", 22, TextAlignmentOptions.Center, TextColor);
        SetRect(shopItemName, 0.1f, 0.62f, 0.9f, 0.78f);
        shopItemName.gameObject.SetActive(false);
        var shopItemDesc = CreateTMPText("ShopItemDesc", shopInner.transform, "", 16, TextAlignmentOptions.Center, TextColor);
        SetRect(shopItemDesc, 0.1f, 0.42f, 0.9f, 0.62f);
        shopItemDesc.gameObject.SetActive(false);
        var shopItemPrice = CreateTMPText("ShopItemPrice", shopInner.transform, "", 24, TextAlignmentOptions.Center, AccentGold);
        SetRect(shopItemPrice, 0.1f, 0.28f, 0.9f, 0.42f);
        shopItemPrice.gameObject.SetActive(false);
        var shopBuyBtn = CreateButton("ShopBuyBtn", shopInner.transform, "COMPRAR", AccentGold);
        SetRect(shopBuyBtn, 0.1f, 0.08f, 0.48f, 0.24f);
        shopBuyBtn.SetActive(false);
        var shopCancelBtn = CreateButton("ShopCancelBtn", shopInner.transform, "CERRAR", ShieldColor);
        SetRect(shopCancelBtn, 0.52f, 0.08f, 0.9f, 0.24f);
        shopCancelBtn.SetActive(false);

        // 3 item slots
        var slotNames = new TMP_Text[3];
        var slotDescs = new TMP_Text[3];
        var slotPrices = new TMP_Text[3];
        var slotBuyBtns = new Button[3];

        for (int s = 0; s < 3; s++)
        {
            float xMin = 0.02f + s * 0.33f;
            float xMax = xMin + 0.30f;

            var slotPanel = CreatePanel($"ShopSlot{s}", shopInner.transform,
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, 0);
            SetRect(slotPanel, xMin, 0.18f, xMax, 0.82f);

            slotNames[s] = CreateTMPText($"SlotName{s}", slotPanel.transform,
                "", 20, TextAlignmentOptions.Center, TextColor);
            SetRect(slotNames[s], 0.05f, 0.70f, 0.95f, 0.92f);
            slotNames[s].fontStyle = FontStyles.Bold;

            slotDescs[s] = CreateTMPText($"SlotDesc{s}", slotPanel.transform,
                "", 14, TextAlignmentOptions.Center, TextColor);
            SetRect(slotDescs[s], 0.05f, 0.40f, 0.95f, 0.68f);
            slotDescs[s].enableWordWrapping = true;

            slotPrices[s] = CreateTMPText($"SlotPrice{s}", slotPanel.transform,
                "", 22, TextAlignmentOptions.Center, AccentGold);
            SetRect(slotPrices[s], 0.05f, 0.22f, 0.95f, 0.40f);
            slotPrices[s].fontStyle = FontStyles.Bold;

            var buyBtn = CreateButton($"SlotBuy{s}", slotPanel.transform, "COMPRAR", AccentGold);
            SetRect(buyBtn, 0.1f, 0.04f, 0.9f, 0.20f);
            slotBuyBtns[s] = buyBtn.GetComponent<Button>();
        }

        // Leave button at bottom
        var shopLeaveBtn = CreateButton("ShopLeaveBtn", shopInner.transform, "SALIR", ShieldColor);
        SetRect(shopLeaveBtn, 0.35f, 0.03f, 0.65f, 0.14f);

        var shopUI = shopOverlay.AddComponent<ShopUI>();
        shopUI.Initialize(shopItemName, shopItemDesc, shopItemPrice,
            shopBuyBtn.GetComponent<Button>(), shopCancelBtn.GetComponent<Button>());
        shopUI.InitializeMultiSlot(slotNames, slotDescs, slotPrices, slotBuyBtns,
            shopLeaveBtn.GetComponent<Button>());
        shopOverlay.SetActive(false);

        // ── Game Over Overlay ──
        var gameOverOverlay = CreateOverlayPanel("GameOverOverlay", canvasTransform);

        var gameOverTitle = CreateTMPText("GameOverTitle", gameOverOverlay.transform,
            "GAME OVER", 40, TextAlignmentOptions.Center, HPColor);
        SetRect(gameOverTitle, 0, 0.6f, 1, 0.9f);
        gameOverTitle.fontStyle = FontStyles.Bold;

        var gameOverStats = CreateTMPText("GameOverStats", gameOverOverlay.transform,
            "", 20, TextAlignmentOptions.Center, TextColor);
        SetRect(gameOverStats, 0.1f, 0.25f, 0.9f, 0.6f);

        var gameOverRestart = CreateButton("GameOverRestart", gameOverOverlay.transform,
            "TRY AGAIN", AccentGold);
        SetRect(gameOverRestart, 0.3f, 0.08f, 0.7f, 0.22f);

        var gameOverUI = gameOverOverlay.AddComponent<GameOverUI>();
        gameOverUI.Initialize(gameOverOverlay, gameOverTitle, gameOverStats,
            gameOverRestart.GetComponent<Button>());
        gameOverOverlay.SetActive(false);

        // ── Victory Overlay ──
        var victoryOverlay = CreateOverlayPanel("VictoryOverlay", canvasTransform);

        var victoryTitle = CreateTMPText("VictoryTitle", victoryOverlay.transform,
            "VICTORY!", 40, TextAlignmentOptions.Center, AccentGold);
        SetRect(victoryTitle, 0, 0.6f, 1, 0.9f);
        victoryTitle.fontStyle = FontStyles.Bold;

        var victoryStats = CreateTMPText("VictoryStats", victoryOverlay.transform,
            "", 20, TextAlignmentOptions.Center, TextColor);
        SetRect(victoryStats, 0.1f, 0.25f, 0.9f, 0.6f);

        var victoryRestart = CreateButton("VictoryRestart", victoryOverlay.transform,
            "PLAY AGAIN", AccentGold);
        SetRect(victoryRestart, 0.3f, 0.08f, 0.7f, 0.22f);

        var victoryUI = victoryOverlay.AddComponent<VictoryUI>();
        victoryUI.Initialize(victoryOverlay, victoryTitle, victoryStats,
            victoryRestart.GetComponent<Button>());
        victoryOverlay.SetActive(false);

        // ── Combat Log ──
        var combatLogGO = CreatePanel("CombatLog", canvasTransform,
            Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, 0);
        var combatLogRT = combatLogGO.GetComponent<RectTransform>();
        combatLogRT.anchorMin = new Vector2(1, 0);
        combatLogRT.anchorMax = new Vector2(1, 0);
        combatLogRT.pivot = new Vector2(1, 0);
        combatLogRT.anchoredPosition = new Vector2(-10, 300);
        combatLogRT.sizeDelta = new Vector2(350, 200);
        var combatLogBg = combatLogGO.GetComponent<Image>();
        combatLogBg.color = new Color(PanelBgColor.r, PanelBgColor.g, PanelBgColor.b, 0.7f);

        // ScrollRect setup
        var scrollViewport = new GameObject("Viewport");
        scrollViewport.transform.SetParent(combatLogGO.transform, false);
        var viewportRT = scrollViewport.AddComponent<RectTransform>();
        StretchFull(viewportRT);
        viewportRT.offsetMin = new Vector2(10, 5);
        viewportRT.offsetMax = new Vector2(-10, -5);
        var viewportMask = scrollViewport.AddComponent<Mask>();
        var viewportImage = scrollViewport.AddComponent<Image>();
        viewportImage.color = Color.clear;
        viewportMask.showMaskGraphic = false;

        var logContent = new GameObject("LogContent");
        logContent.transform.SetParent(scrollViewport.transform, false);
        var logContentRT = logContent.AddComponent<RectTransform>();
        logContentRT.anchorMin = new Vector2(0, 0);
        logContentRT.anchorMax = new Vector2(1, 1);
        logContentRT.offsetMin = Vector2.zero;
        logContentRT.offsetMax = Vector2.zero;
        logContentRT.pivot = new Vector2(0, 0);

        var logText = CreateTMPText("LogText", logContent.transform, "", 14,
            TextAlignmentOptions.BottomLeft, TextColor);
        StretchFull(logText.GetComponent<RectTransform>());
        logText.enableWordWrapping = true;
        logText.overflowMode = TextOverflowModes.Truncate;

        var scrollRect = combatLogGO.AddComponent<ScrollRect>();
        scrollRect.viewport = viewportRT;
        scrollRect.content = logContentRT;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        var combatLogUI = combatLogGO.AddComponent<CombatLogUI>();
        combatLogUI.Initialize(logText, scrollRect);

        // ── Inventory Builder Overlay ──
        var inventoryOverlay = CreateOverlayPanel("InventoryBuilderOverlay", canvasTransform);

        var invTitle = CreateTMPText("InvTitle", inventoryOverlay.transform,
            "SELECCIONA TU INVENTARIO DE COMBATE", 26, TextAlignmentOptions.Center, AccentGold);
        SetRect(invTitle, 0.05f, 0.86f, 0.95f, 0.97f);
        invTitle.fontStyle = FontStyles.Bold;

        var invCounter = CreateTMPText("InvCounter", inventoryOverlay.transform,
            "0/5 dados seleccionados", 20, TextAlignmentOptions.Center, TextColor);
        SetRect(invCounter, 0.05f, 0.81f, 0.50f, 0.88f);

        var invBudget = CreateTMPText("InvBudget", inventoryOverlay.transform,
            "Poder: 0/8", 20, TextAlignmentOptions.Center, AccentGold);
        SetRect(invBudget, 0.50f, 0.81f, 0.95f, 0.88f);

        var invCardContainer = new GameObject("InvCardContainer");
        invCardContainer.transform.SetParent(inventoryOverlay.transform, false);
        var invCardContainerRT = invCardContainer.AddComponent<RectTransform>();
        invCardContainerRT.anchorMin = new Vector2(0.05f, 0.22f);
        invCardContainerRT.anchorMax = new Vector2(0.95f, 0.78f);
        invCardContainerRT.offsetMin = Vector2.zero;
        invCardContainerRT.offsetMax = Vector2.zero;
        var invHLG = invCardContainer.AddComponent<HorizontalLayoutGroup>();
        invHLG.spacing = 14;
        invHLG.childAlignment = TextAnchor.MiddleCenter;
        invHLG.childForceExpandWidth = false;
        invHLG.childForceExpandHeight = false;
        invHLG.childControlWidth = false;
        invHLG.childControlHeight = false;

        var invConfirmButton = CreateButton("InvConfirmButton", inventoryOverlay.transform,
            "CONFIRMAR INVENTARIO", AccentGold);
        SetRect(invConfirmButton, 0.25f, 0.07f, 0.75f, 0.18f);

        var invCardPrefab = CreateInventoryCardPrefab();

        var inventoryBuilderUI = inventoryOverlay.AddComponent<InventoryBuilderUI>();
        inventoryBuilderUI.Initialize(
            invCounter,
            invBudget,
            invCardContainer.transform,
            invConfirmButton.GetComponent<Button>(),
            invCardPrefab
        );
        inventoryOverlay.SetActive(false);

        if (UIManager.Instance != null)
            UIManager.Instance.SetInventoryBuilderUI(inventoryBuilderUI);

        // ── Movement Roll Panel ──
        var movementRollPanel = CreatePanel("MovementRollPanel", canvasTransform,
            new Vector2(0.3f, 0.35f), new Vector2(0.7f, 0.65f),
            Vector2.zero, Vector2.zero, 0);

        var movementRollInfo = CreateTMPText("MovementRollInfo", movementRollPanel.transform,
            "Tirar dado de movimiento", 20, TextAlignmentOptions.Center, TextColor);
        SetRect(movementRollInfo, 0.05f, 0.58f, 0.95f, 0.95f);
        movementRollInfo.enableWordWrapping = true;

        var movementRollResult = CreateTMPText("MovementRollResult", movementRollPanel.transform,
            "", 32, TextAlignmentOptions.Center, AccentGold);
        SetRect(movementRollResult, 0.05f, 0.32f, 0.95f, 0.58f);
        movementRollResult.fontStyle = FontStyles.Bold;

        var movementRollButton = CreateButton("MovementRollButton", movementRollPanel.transform,
            "TIRAR DADO", D6Color);
        SetRect(movementRollButton, 0.15f, 0.06f, 0.85f, 0.30f);

        var movementRollUI = movementRollPanel.AddComponent<MovementRollUI>();
        movementRollUI.Initialize(
            movementRollInfo,
            movementRollResult,
            movementRollButton.GetComponent<Button>()
        );
        movementRollPanel.SetActive(false);

        // ── Enemy Info Panel (right side, shown during combat) ──
        var enemyInfoPanel = CreatePanel("EnemyInfoPanel", canvasTransform,
            new Vector2(0.75f, 0.45f), new Vector2(0.98f, 0.62f),
            Vector2.zero, Vector2.zero, 0);

        var enemyNameText = CreateTMPText("EnemyNameText", enemyInfoPanel.transform,
            "", 20, TextAlignmentOptions.Center, AccentGold);
        SetRect(enemyNameText, 0.05f, 0.60f, 0.95f, 0.95f);
        enemyNameText.fontStyle = FontStyles.Bold;

        // HP bar background
        var hpBarBg = CreateChildImage("HPBarBg", enemyInfoPanel.transform, DarkBg);
        SetRect(hpBarBg, 0.08f, 0.30f, 0.92f, 0.55f);

        // HP bar fill
        var hpBarFillGO = CreateChildImage("HPBarFill", hpBarBg.transform, Color.white);
        var hpBarFillRT = hpBarFillGO.GetComponent<RectTransform>();
        hpBarFillRT.anchorMin = Vector2.zero;
        hpBarFillRT.anchorMax = Vector2.one;
        hpBarFillRT.offsetMin = Vector2.zero;
        hpBarFillRT.offsetMax = Vector2.zero;
        var hpBarFillImage = hpBarFillGO.GetComponent<Image>();
        hpBarFillImage.type = Image.Type.Filled;
        hpBarFillImage.fillMethod = Image.FillMethod.Horizontal;
        hpBarFillImage.fillAmount = 1f;

        var enemyHpText = CreateTMPText("EnemyHPText", enemyInfoPanel.transform,
            "", 14, TextAlignmentOptions.Center, TextColor);
        SetRect(enemyHpText, 0.05f, 0.05f, 0.95f, 0.30f);

        var enemyInfoUI = enemyInfoPanel.AddComponent<EnemyInfoUI>();
        enemyInfoUI.Initialize(enemyNameText, hpBarFillImage, enemyHpText);
        enemyInfoPanel.SetActive(false);

        // ── Wire UIManager ──
        var uiManager = UIManager.Instance;
        if (uiManager != null)
        {
            uiManager.Initialize(
                healthBarUI,
                energyBarUI,
                shieldDisplay,
                phaseLabel,
                combatPanel,
                crapsOverlay,
                rewardOverlay,
                gameOverOverlay,
                victoryOverlay,
                movementRollUI
            );
            uiManager.SetLevelText(levelTMP);
            uiManager.SetEnemyInfoUI(enemyInfoUI);
        }
    }

    // ── Factory Helpers ──

    private GameObject CreateInventoryCardPrefab()
    {
        var prefab = new GameObject("InventoryCardPrefab");
        prefab.SetActive(false);

        var rt = prefab.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(100, 120);

        var bg = prefab.AddComponent<Image>();
        bg.color = D6Color;

        // Selected border (gold, hidden by default)
        var borderGO = CreateChildImage("SelectBorder", prefab.transform, AccentGold);
        var borderRT = borderGO.GetComponent<RectTransform>();
        StretchFull(borderRT);
        borderRT.offsetMin = new Vector2(-4, -4);
        borderRT.offsetMax = new Vector2(4, 4);
        var borderImg = borderGO.GetComponent<Image>();
        borderImg.fillCenter = false;
        borderGO.SetActive(false);

        var btn = prefab.AddComponent<Button>();
        var btnColors = btn.colors;
        btnColors.normalColor = Color.white;
        btnColors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        btnColors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        btn.colors = btnColors;

        // Die name (large, centered top)
        var nameText = CreateTMPText("DieNameText", prefab.transform, "d6", 30,
            TextAlignmentOptions.Center, Color.white);
        var nameRT = nameText.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0, 0.45f);
        nameRT.anchorMax = new Vector2(1, 0.95f);
        nameRT.offsetMin = Vector2.zero;
        nameRT.offsetMax = Vector2.zero;
        nameText.fontStyle = FontStyles.Bold;

        // Range (small, middle)
        var rangeText = CreateTMPText("RangeText", prefab.transform, "1\u20136", 14,
            TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.85f));
        var rangeRT = rangeText.GetComponent<RectTransform>();
        rangeRT.anchorMin = new Vector2(0, 0.28f);
        rangeRT.anchorMax = new Vector2(1, 0.50f);
        rangeRT.offsetMin = Vector2.zero;
        rangeRT.offsetMax = Vector2.zero;

        // Cost (small, bottom)
        var costText = CreateTMPText("CostText", prefab.transform, "Costo: 1", 12,
            TextAlignmentOptions.Center, AccentGold);
        var costRT = costText.GetComponent<RectTransform>();
        costRT.anchorMin = new Vector2(0, 0.04f);
        costRT.anchorMax = new Vector2(1, 0.26f);
        costRT.offsetMin = Vector2.zero;
        costRT.offsetMax = Vector2.zero;

        prefab.AddComponent<InventoryDieCardUI>();
        // refs wired in Awake() per-instance — no Initialize needed

        var layout = prefab.AddComponent<LayoutElement>();
        layout.minWidth = 100;
        layout.minHeight = 120;

        DontDestroyOnLoad(prefab);
        return prefab;
    }

    private GameObject CreateDiceSlotPrefab()
    {
        var prefab = new GameObject("DieSlotPrefab");
        prefab.SetActive(false);

        var rt = prefab.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(80, 80);

        var bg = prefab.AddComponent<Image>();
        bg.color = D6Color;
        
        // Lock border (hidden by default)
        var lockBorderGO = CreateChildImage("LockBorder", prefab.transform, AccentGold);
        var lockBorderRT = lockBorderGO.GetComponent<RectTransform>();
        StretchFull(lockBorderRT);
        lockBorderRT.offsetMin = new Vector2(-3, -3);
        lockBorderRT.offsetMax = new Vector2(3, 3);
        var lockBorderImage = lockBorderGO.GetComponent<Image>();
        lockBorderImage.type = Image.Type.Sliced;
        // Make it look like a border by clearing the center
        lockBorderImage.fillCenter = false;
        lockBorderGO.SetActive(false);

        // Value text (large, centered)
        var valueText = CreateTMPText("ValueText", prefab.transform, "0", 32,
            TextAlignmentOptions.Center, Color.white);
        StretchFull(valueText.GetComponent<RectTransform>());
        valueText.fontStyle = FontStyles.Bold;

        // Type label (small, bottom)
        var typeLabel = CreateTMPText("TypeLabel", prefab.transform, "d6", 12,
            TextAlignmentOptions.Bottom, new Color(1, 1, 1, 0.7f));
        var typeLabelRT = typeLabel.GetComponent<RectTransform>();
        typeLabelRT.anchorMin = Vector2.zero;
        typeLabelRT.anchorMax = new Vector2(1, 0.3f);
        typeLabelRT.offsetMin = Vector2.zero;
        typeLabelRT.offsetMax = Vector2.zero;

        // Add DieSlotUI component
        var dieSlot = prefab.AddComponent<DieSlotUI>();
        dieSlot.Initialize(valueText, typeLabel, bg, lockBorderImage);

        var layoutElement = prefab.AddComponent<LayoutElement>();
        layoutElement.minHeight = 80;
        layoutElement.minWidth = 80;

        // Keep it out of the scene hierarchy
        DontDestroyOnLoad(prefab);

        return prefab;
    }

    private GameObject CreatePanel(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax,
        float height = 0,
        bool anchorTopStretch = false,
        bool anchorBottomStretch = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = new Color(PanelBgColor.r, PanelBgColor.g, PanelBgColor.b, 0.85f);

        if (anchorTopStretch)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(offsetMin.x, 0);
            rt.offsetMax = new Vector2(offsetMax.x, 0);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
        }
        else if (anchorBottomStretch)
        {
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = offsetMin;
            rt.offsetMax = new Vector2(offsetMax.x, height + offsetMin.y);
        }
        else
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        return go;
    }

    private GameObject CreateOverlayPanel(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        StretchFull(rt);

        var img = go.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0.85f);

        return go;
    }

    private TMP_Text CreateTMPText(string name, Transform parent, string text, float fontSize,
        TextAlignmentOptions alignment, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200, 40);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;

        return tmp;
    }

    private GameObject CreateChildImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = color;

        return go;
    }

    private GameObject CreateButton(string name, Transform parent, string label, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(160, 40);

        var img = go.AddComponent<Image>();
        img.color = new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, 0.95f);

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        btn.colors = colors;

        var labelText = CreateTMPText($"{name}Label", go.transform, label, 16,
            TextAlignmentOptions.Center, color);
        StretchFull(labelText.GetComponent<RectTransform>());
        labelText.raycastTarget = false;

        return go;
    }

    private void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void StretchAnchors(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
    {
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void SetRect(GameObject go, float xMin, float yMin, float xMax, float yMax)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void SetRect(TMP_Text tmp, float xMin, float yMin, float xMax, float yMax)
    {
        SetRect(tmp.gameObject, xMin, yMin, xMax, yMax);
    }
}
