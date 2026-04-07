using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CombatUI : MonoBehaviour
{
    public static CombatUI Instance;

    [Header("Attack — Dice Display")]
    [SerializeField] private Transform diceContainer;
    [SerializeField] private GameObject dicePrefab;

    [Header("Attack — Combo Preview")]
    [SerializeField] private TMP_Text comboPreviewText;

    [Header("Attack — Roll Counter")]
    [SerializeField] private TMP_Text rollCounterText;

    [Header("Attack — Buttons")]
    [SerializeField] private Button rerollButton;
    [SerializeField] private Button commitButton;

    [Header("Attack — Craps Bet Indicator")]
    [SerializeField] private TMP_Text crapsBetIndicator;

    [Header("Attack Panel")]
    [SerializeField] private GameObject attackPanel;

    [Header("Defense")]
    [SerializeField] private GameObject defensePanel;
    [SerializeField] private TMP_Text defenseTitle;
    [SerializeField] private TMP_Text defenseRollCounterText;
    [SerializeField] private TMP_Text defenseComboPreviewText;
    [SerializeField] private TMP_Text defenseShieldText;
    [SerializeField] private Button rollDefenseButton;
    [SerializeField] private Button commitDefenseButton;
    [SerializeField] private Transform defenseDiceContainer;

    [Header("Enemy Attack")]
    [SerializeField] private GameObject enemyAttackPanel;
    [SerializeField] private TMP_Text enemyAttackTitle;
    [SerializeField] private TMP_Text enemyRollText;
    [SerializeField] private TMP_Text shieldAbsorbText;
    [SerializeField] private TMP_Text netDamageText;
    [SerializeField] private Button continueButton;

    // Attack state
    private DieSlotUI[] dieSlots;
    private RollResult[] currentResults;
    private DiceBag currentBag;

    // Defense state
    private DieSlotUI[] defenseSlots;
    private RollResult[] defenseResults;

    // Attack events
    public event Action<string> OnDieLockToggled;
    public event Action OnRerollClicked;
    public event Action OnCommitClicked;

    // Defense events
    public event Action<string> OnDefenseDieLockToggled;
    public event Action OnRollDefenseClicked;
    public event Action OnDefenseCommitClicked;

    // Enemy attack event
    public event Action OnContinueClicked;

    void Awake()
    {
        Instance = this;
        WireButtons();
    }

    public void Initialize(
        Transform diceContainerRef, GameObject dicePrefabRef,
        TMP_Text comboPreviewRef, TMP_Text rollCounterRef,
        Button rerollRef, Button commitRef,
        GameObject attackPanelRef,
        GameObject defensePanelRef, TMP_Text defenseTitleRef,
        TMP_Text defenseRollCounterRef, TMP_Text defenseComboPreviewRef,
        TMP_Text defenseShieldRef, Button rollDefenseRef,
        GameObject enemyAttackPanelRef, TMP_Text enemyAttackTitleRef,
        TMP_Text enemyRollRef, TMP_Text shieldAbsorbRef,
        TMP_Text netDamageRef, Button continueRef)
    {
        diceContainer = diceContainerRef;
        dicePrefab = dicePrefabRef;
        comboPreviewText = comboPreviewRef;
        rollCounterText = rollCounterRef;
        rerollButton = rerollRef;
        commitButton = commitRef;
        attackPanel = attackPanelRef;
        defensePanel = defensePanelRef;
        defenseTitle = defenseTitleRef;
        defenseRollCounterText = defenseRollCounterRef;
        defenseComboPreviewText = defenseComboPreviewRef;
        defenseShieldText = defenseShieldRef;
        rollDefenseButton = rollDefenseRef;
        enemyAttackPanel = enemyAttackPanelRef;
        enemyAttackTitle = enemyAttackTitleRef;
        enemyRollText = enemyRollRef;
        shieldAbsorbText = shieldAbsorbRef;
        netDamageText = netDamageRef;
        continueButton = continueRef;
        WireButtons();
    }

    public void InitializeDefense(Transform defenseDiceContainerRef, Button commitDefenseRef)
    {
        defenseDiceContainer = defenseDiceContainerRef;
        commitDefenseButton = commitDefenseRef;
        if (commitDefenseButton != null)
        {
            commitDefenseButton.onClick.RemoveAllListeners();
            commitDefenseButton.onClick.AddListener(() => OnDefenseCommitClicked?.Invoke());
        }
    }

    private void WireButtons()
    {
        if (rerollButton != null)
        {
            rerollButton.onClick.RemoveAllListeners();
            rerollButton.onClick.AddListener(() => OnRerollClicked?.Invoke());
        }
        if (commitButton != null)
        {
            commitButton.onClick.RemoveAllListeners();
            commitButton.onClick.AddListener(() => OnCommitClicked?.Invoke());
        }
        if (rollDefenseButton != null)
        {
            rollDefenseButton.onClick.RemoveAllListeners();
            rollDefenseButton.onClick.AddListener(() => OnRollDefenseClicked?.Invoke());
        }
        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(() => OnContinueClicked?.Invoke());
        }
    }

    // ── Attack UI ──────────────────────────────────────────────

    public void ShowAttackUI(RollResult[] results, HashSet<string> locked, DiceBag bag = null)
    {
        SetPanel(attackPanel, true);
        SetPanel(defensePanel, false);
        SetPanel(enemyAttackPanel, false);

        currentResults = results;
        currentBag = bag;
        ClearDiceSlots(diceContainer, ref dieSlots);
        dieSlots = BuildDiceSlots(results, locked, bag, diceContainer, OnDieClicked);
    }

    public void OnDieClicked(string diceId) => OnDieLockToggled?.Invoke(diceId);

    public void UpdateDieLock(string diceId, bool isLocked)
        => UpdateSlotLock(diceId, isLocked, currentResults, dieSlots);

    public void UpdateComboPreview(CombinationResult combo, float multiplier = 1f)
    {
        if (comboPreviewText == null) return;
        if (multiplier > 1f)
        {
            int finalDmg = Mathf.RoundToInt(combo.BaseDamage * multiplier);
            comboPreviewText.text = $"Combo: {FormatComboName(combo.Type)} \u2192 {combo.BaseDamage} x {multiplier:F2} = {finalDmg} dmg";
        }
        else
        {
            comboPreviewText.text = $"Combo: {FormatComboName(combo.Type)} \u2192 {combo.BaseDamage} dmg";
        }
    }

    public void ClearComboPreview()
    {
        if (comboPreviewText != null)
            comboPreviewText.text = "Combo: \u2014";
    }

    public void UpdateRollCounter(int current, int max)
    {
        if (rollCounterText != null)
            rollCounterText.text = $"Roll {current}/{max}";
    }

    public void SetRerollEnabled(bool enabled)
    {
        if (rerollButton != null) rerollButton.interactable = enabled;
    }

    public void SetCommitEnabled(bool enabled)
    {
        if (commitButton != null) commitButton.interactable = enabled;
    }

    public void SetCommitText(string text)
    {
        if (commitButton == null) return;
        var tmp = commitButton.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) tmp.text = text;
    }

    // ── Defense UI ─────────────────────────────────────────────

    public void ShowDefenseDiceUI(RollResult[] results, HashSet<string> locked, DiceBag bag = null)
    {
        SetPanel(attackPanel, false);
        SetPanel(defensePanel, true);
        SetPanel(enemyAttackPanel, false);

        defenseResults = results;
        ClearDiceSlots(defenseDiceContainer, ref defenseSlots);
        defenseSlots = BuildDiceSlots(results, locked, bag, defenseDiceContainer, OnDefenseDieClicked);
    }

    public void OnDefenseDieClicked(string diceId) => OnDefenseDieLockToggled?.Invoke(diceId);

    public void UpdateDefenseDieLock(string diceId, bool isLocked)
        => UpdateSlotLock(diceId, isLocked, defenseResults, defenseSlots);

    public void UpdateDefenseComboPreview(CombinationResult combo)
    {
        if (defenseComboPreviewText == null) return;
        defenseComboPreviewText.text = $"Escudo: {FormatComboName(combo.Type)}";
    }

    public void ClearDefenseComboPreview()
    {
        if (defenseComboPreviewText != null)
            defenseComboPreviewText.text = "Escudo: \u2014";
    }

    public void UpdateDefenseRollCounter(int current, int max)
    {
        if (defenseTitle != null)
            defenseTitle.text = $"DEFENSA — Roll {current}/{max}";
        if (defenseRollCounterText != null)
            defenseRollCounterText.text = $"Roll {current}/{max}";
    }

    public void SetDefenseRerollEnabled(bool enabled)
    {
        if (rollDefenseButton != null) rollDefenseButton.interactable = enabled;
    }

    public void SetDefenseCommitEnabled(bool enabled)
    {
        if (commitDefenseButton != null) commitDefenseButton.interactable = enabled;
    }

    public void UpdateDefenseShield(int shieldValue)
    {
        if (defenseShieldText != null)
            defenseShieldText.text = shieldValue > 0 ? $"Escudo: {shieldValue}" : "";
    }

    // ── Enemy Attack UI ────────────────────────────────────────

    public void ShowEnemyAttackResult(int rawDamage, int shield, int netDamage)
    {
        SetPanel(attackPanel, false);
        SetPanel(defensePanel, false);
        SetPanel(enemyAttackPanel, true);

        if (enemyAttackTitle != null) enemyAttackTitle.text = "ENEMY ATTACKS!";
        if (enemyRollText != null) enemyRollText.text = $"Enemy rolls: {rawDamage} damage";
        if (shieldAbsorbText != null) shieldAbsorbText.text = $"Your shield absorbs: {shield}";
        if (netDamageText != null)
            netDamageText.text = netDamage > 0 ? $"You take: {netDamage} damage" : "Fully blocked!";
    }

    public void HideCombatUI()
    {
        SetPanel(attackPanel, false);
        SetPanel(defensePanel, false);
        SetPanel(enemyAttackPanel, false);
        HideCrapsBetIndicator();
    }

    // ── Craps Bet Indicator ─────────────────────────────────────

    public void SetCrapsBetIndicator(TMP_Text indicator)
    {
        crapsBetIndicator = indicator;
    }

    public void ShowCrapsBetIndicator(CombinationType bet)
    {
        if (crapsBetIndicator == null) return;
        crapsBetIndicator.text = $"Craps Bet: {FormatComboName(bet)}";
        crapsBetIndicator.gameObject.SetActive(true);
    }

    public void HideCrapsBetIndicator()
    {
        if (crapsBetIndicator != null)
            crapsBetIndicator.gameObject.SetActive(false);
    }

    // ── Shared Helpers ─────────────────────────────────────────

    private DieSlotUI[] BuildDiceSlots(RollResult[] results, HashSet<string> locked,
        DiceBag bag, Transform container, Action<string> clickCallback)
    {
        if (results == null || container == null || dicePrefab == null)
            return new DieSlotUI[0];

        var slots = new DieSlotUI[results.Length];
        for (int i = 0; i < results.Length; i++)
        {
            var go = Instantiate(dicePrefab, container);
            go.SetActive(true);
            var slot = go.GetComponent<DieSlotUI>();
            if (slot == null) slot = go.AddComponent<DieSlotUI>();

            bool isLocked = locked != null && locked.Contains(results[i].DiceId);
            string diceId = results[i].DiceId;

            DiceData diceData = null;
            if (bag != null)
            {
                var inst = bag.Dice.Find(d => d.Id == diceId);
                if (inst != null) diceData = inst.BaseData;
            }

            slot.Setup(results[i].Value, diceData, isLocked);
            slot.OnClicked += () => clickCallback?.Invoke(diceId);
            slots[i] = slot;
        }
        return slots;
    }

    private void UpdateSlotLock(string diceId, bool isLocked, RollResult[] results, DieSlotUI[] slots)
    {
        if (slots == null || results == null) return;
        for (int i = 0; i < results.Length; i++)
        {
            if (results[i].DiceId == diceId && slots[i] != null)
            {
                slots[i].SetLocked(isLocked);
                break;
            }
        }
    }

    private void ClearDiceSlots(Transform container, ref DieSlotUI[] slots)
    {
        if (container == null) return;
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);
        slots = null;
    }

    private void SetPanel(GameObject panel, bool active)
    {
        if (panel != null) panel.SetActive(active);
    }

    private string FormatComboName(CombinationType type)
    {
        switch (type)
        {
            case CombinationType.HighDie:        return "Dado Mas Alto";
            case CombinationType.Pair:           return "Pair";
            case CombinationType.TwoPair:        return "Two Pair";
            case CombinationType.ThreeOfAKind:   return "Three of a Kind";
            case CombinationType.SmallStraight:  return "Small Straight";
            case CombinationType.MediumStraight: return "Medium Straight";
            case CombinationType.Straight:       return "Straight";
            case CombinationType.FullHouse:      return "Full House";
            case CombinationType.FourOfAKind:    return "Four of a Kind";
            case CombinationType.Generala:       return "GENERALA!";
            case CombinationType.DoubleGenerala: return "DOUBLE GENERALA!!";
            default:                             return type.ToString();
        }
    }

    // ══════════════════════════════════════════════
    // === 3AP UI Methods ===
    // ══════════════════════════════════════════════

    private static readonly Color APPanelBg = new Color(0.118f, 0.118f, 0.227f, 0.92f);
    private static readonly Color APBtnColor = new Color(0.31f, 0.76f, 0.97f, 1f);
    private static readonly Color APBtnHover = new Color(0.22f, 0.55f, 0.78f, 1f);
    private static readonly Color APTextColor = new Color(0.93f, 0.93f, 0.93f, 1f);

    private GameObject _apSelectorPanel;
    private Canvas _cachedScreenCanvas;

    private Transform GetCanvasTransform()
    {
        if (_cachedScreenCanvas != null) return _cachedScreenCanvas.transform;
        var allCanvases = FindObjectsOfType<Canvas>();
        for (int i = 0; i < allCanvases.Length; i++)
        {
            if (allCanvases[i].renderMode == RenderMode.ScreenSpaceOverlay)
            {
                _cachedScreenCanvas = allCanvases[i];
                return _cachedScreenCanvas.transform;
            }
        }
        // Fallback: any canvas
        if (allCanvases.Length > 0)
        {
            _cachedScreenCanvas = allCanvases[0];
            return _cachedScreenCanvas.transform;
        }
        return null;
    }

    /// Creates a button at an absolute position inside a parent panel
    private GameObject MakeButton(Transform parent, string label, Vector2 pos, Vector2 size, Action onClick)
    {
        var go = new GameObject(label + "Btn");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0.5f);
        rt.anchorMax = new Vector2(0, 0.5f);
        rt.pivot = new Vector2(0, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.color = APBtnColor;
        img.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = APBtnColor;
        colors.highlightedColor = APBtnHover;
        colors.pressedColor = new Color(0.18f, 0.45f, 0.65f, 1f);
        colors.selectedColor = APBtnColor;
        btn.colors = colors;
        btn.targetGraphic = img;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero; textRt.offsetMax = Vector2.zero;
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 15;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        btn.onClick.AddListener(() => onClick?.Invoke());
        return go;
    }

    /// Creates a text label at an absolute position inside a parent panel
    private TMP_Text MakeLabel(Transform parent, string text, Vector2 pos, Vector2 size, float fontSize = 13)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0.5f);
        rt.anchorMax = new Vector2(0, 0.5f);
        rt.pivot = new Vector2(0, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color = APTextColor;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        return tmp;
    }

    /// Helper: create a panel anchored to left-center of screen
    private GameObject MakeAPPanel(string name, float width, float height)
    {
        var parent = GetCanvasTransform();
        if (parent == null) return null;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0.5f);
        rt.anchorMax = new Vector2(0, 0.5f);
        rt.pivot = new Vector2(0, 0.5f);
        rt.anchoredPosition = new Vector2(10, -40);
        rt.sizeDelta = new Vector2(width, height);
        var img = go.AddComponent<Image>();
        img.color = APPanelBg;
        img.raycastTarget = false; // don't block clicks outside buttons
        return go;
    }

    public void ShowAP1Movement(int steps)
    {
        HideCombatUI();
        HideAPSelector();

        // Panel: [AP1: X pasos]  [Quedarse]
        _apSelectorPanel = MakeAPPanel("AP1Panel", 300, 40);
        if (_apSelectorPanel == null) return;

        MakeLabel(_apSelectorPanel.transform, $"AP1: {steps} pasos",
            new Vector2(10, 0), new Vector2(140, 30), 13);
        MakeButton(_apSelectorPanel.transform, "Quedarse",
            new Vector2(160, 0), new Vector2(120, 32), () =>
        {
            GameManager.Instance.OnAP1Stay();
        });
    }

    public void ShowAPSelector(bool canAttack, bool canBag, bool canShield, bool canSkip)
    {
        HideAPSelector();

        // Panel: [AP2:]  [Atacar]  [Pasar]
        float totalWidth = 60; // label
        if (canAttack) totalWidth += 130; // atacar btn + gap
        totalWidth += 130; // pasar btn + gap
        totalWidth += 20; // padding

        _apSelectorPanel = MakeAPPanel("APSelectorPanel", totalWidth, 44);
        if (_apSelectorPanel == null) return;

        float x = 10;
        MakeLabel(_apSelectorPanel.transform, "AP2:",
            new Vector2(x, 0), new Vector2(50, 30), 13);
        x += 55;

        if (canAttack)
        {
            MakeButton(_apSelectorPanel.transform, "Atacar",
                new Vector2(x, 0), new Vector2(120, 34), () => { HideAPSelector(); GameManager.Instance.OnAPSelectorAttack(); });
            x += 130;
        }
        MakeButton(_apSelectorPanel.transform, "Pasar",
            new Vector2(x, 0), new Vector2(120, 34), () => { HideAPSelector(); GameManager.Instance.OnAPSelectorSkipAP2(); });
    }

    public void HideAPSelector()
    {
        if (_apSelectorPanel != null)
        {
            Destroy(_apSelectorPanel);
            _apSelectorPanel = null;
        }
    }

    // --- Shield Roll UI ---
    private GameObject _shieldRollPanel;
    public event Action OnShieldRollClicked;

    public void ShowShieldRollUI()
    {
        HideCombatUI();
        HideAPSelector();
        HideShieldRollUI();

        // Panel: [AP3 — Escudo]  [Tirar Escudo]
        _shieldRollPanel = MakeAPPanel("ShieldRollPanel", 320, 44);
        if (_shieldRollPanel == null) return;

        MakeLabel(_shieldRollPanel.transform, "AP3 — Escudo",
            new Vector2(10, 0), new Vector2(130, 30), 13);
        MakeButton(_shieldRollPanel.transform, "Tirar Escudo",
            new Vector2(150, 0), new Vector2(150, 34), () => OnShieldRollClicked?.Invoke());
    }

    public void ShowShieldResult(int value)
    {
        if (_shieldRollPanel == null) return;
        for (int i = _shieldRollPanel.transform.childCount - 1; i >= 0; i--)
            Destroy(_shieldRollPanel.transform.GetChild(i).gameObject);
        MakeLabel(_shieldRollPanel.transform, $"Escudo: {value}",
            new Vector2(10, 0), new Vector2(200, 30), 14);
    }

    public void HideShieldRollUI()
    {
        if (_shieldRollPanel != null)
        {
            Destroy(_shieldRollPanel);
            _shieldRollPanel = null;
        }
    }

    // --- Damage Breakdown ---
    public void ShowDamageBreakdown(int comboBase, float mult, int bonus, int total)
    {
        if (comboPreviewText != null)
        {
            string breakdownText = bonus > 0
                ? $"Combo: {comboBase} x{mult:F2} + Bonus: +{bonus} = {total}"
                : $"Combo: {comboBase} x{mult:F2} = {total}";
            comboPreviewText.text = breakdownText;
        }
    }
}
