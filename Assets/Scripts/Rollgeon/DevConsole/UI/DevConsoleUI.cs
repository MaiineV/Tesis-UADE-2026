using System.Collections.Generic;
using System.Text;
using Patterns;
using Rollgeon.DevConsole.Autocomplete;
using Rollgeon.GameCamera;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Rollgeon.DevConsole.UI
{
    /// <summary>
    /// Overlay de la DevConsole: construye toda su UI por código (Canvas + panel + tabs + consola
    /// con autocompletado) y maneja el input por teclado. Se registra como <see cref="IDevConsoleService"/>.
    /// </summary>
    public sealed class DevConsoleUI : MonoBehaviour, IDevConsoleService
    {
        private const int MaxOutputLines = 30;
        private const int SuggestionRowCount = 8;

        private static readonly Color Bg = new Color(0.05f, 0.05f, 0.08f, 0.85f);
        private static readonly Color BtnBg = new Color(0.18f, 0.20f, 0.28f, 1f);
        private static readonly Color TabBg = new Color(0.12f, 0.13f, 0.18f, 1f);

        private DevConsoleSession _session;
        private CanvasGroup _group;
        private TMP_InputField _input;
        private TextMeshProUGUI _output;
        private GameObject _suggestionsHost;
        private readonly List<TextMeshProUGUI> _suggestionRows = new List<TextMeshProUGUI>();
        private GameObject _consolePanel, _playerPanel, _dicePanel, _worldPanel;

        private bool _open;
        private bool _suggestionsActive;
        private int _historyIndex = -1;
        private InputActionMap[] _disabledMaps;

        public bool IsOpen => _open;

        private void Awake()
        {
            _session = new DevConsoleSession();
            EnsureEventSystem();
            BuildUI();

            if (!ServiceLocator.HasService<IDevConsoleService>())
                ServiceLocator.AddService<IDevConsoleService>(this, ServiceScope.Global);

            SetOpen(false);
            RefreshOutput();
        }

        // ---- IDevConsoleService ------------------------------------------------

        public void Toggle() => SetOpen(!_open);
        public void Open() => SetOpen(true);
        public void Close() => SetOpen(false);

        public void Execute(string line)
        {
            _session.Execute(line);
            RefreshOutput();
        }

        // ---- Input -------------------------------------------------------------

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.backquoteKey.wasPressedThisFrame || kb.f1Key.wasPressedThisFrame)
            {
                Toggle();
                return;
            }

            if (!_open)
            {
                HandleFreeMove(kb);
                return;
            }

            if (kb.escapeKey.wasPressedThisFrame) { Close(); return; }

            bool hasSugg = _suggestionsActive && _session.Autocomplete.HasSuggestions;

            if (kb.tabKey.wasPressedThisFrame)
            {
                if (hasSugg) AcceptSuggestion();
                return;
            }
            if (kb.upArrowKey.wasPressedThisFrame)
            {
                if (hasSugg) { _session.Autocomplete.MoveUp(); RefreshSuggestions(); }
                else HistoryPrev();
                return;
            }
            if (kb.downArrowKey.wasPressedThisFrame)
            {
                if (hasSugg) { _session.Autocomplete.MoveDown(); RefreshSuggestions(); }
                else HistoryNext();
                return;
            }
            // Enter SIEMPRE ejecuta el input; aceptar la sugerencia seleccionada es solo con Tab.
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                ExecuteCurrent();
            }
        }

        private void HandleFreeMove(Keyboard kb)
        {
            if (_session?.FreeMove == null || !_session.FreeMove.Enabled) return;

            int dx = 0, dy = 0;
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) dy = 1;
            else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) dy = -1;
            else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) dx = 1;
            else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) dx = -1;
            if (dx == 0 && dy == 0) return;

            if (!ServiceLocator.TryGetService<IPlayerService>(out var ps) || ps == null) return;
            var pid = ps.PlayerGuid;
            if (pid == System.Guid.Empty) return;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return;
            if (!ServiceLocator.TryGetService<IMovementService>(out var mov) || mov == null) return;
            if (!grid.TryGetPosition(pid, out var cur)) return;

            mov.Move(pid, new GridCoord(cur.X + dx, cur.Y + dy));
        }

        // ---- Actions -----------------------------------------------------------

        private void ExecuteCurrent()
        {
            string line = _input.text;
            if (string.IsNullOrWhiteSpace(line)) return;

            _session.Execute(line);
            _input.SetTextWithoutNotify(string.Empty);
            _historyIndex = -1;
            _session.Autocomplete.Reset();
            RefreshSuggestions();
            RefreshOutput();
            FocusInput(0);
        }

        private void AcceptSuggestion()
        {
            if (!_session.Autocomplete.TryAccept(out var newInput, out var newCaret)) return;
            _input.SetTextWithoutNotify(newInput);
            RecomputeAutocomplete(newInput, newCaret);
            FocusInput(newCaret);
        }

        private void OnInputChanged(string text)
        {
            _historyIndex = -1;
            RecomputeAutocomplete(text, text?.Length ?? 0);
        }

        private void RecomputeAutocomplete(string text, int caret)
        {
            _session.Autocomplete.Compute(text ?? string.Empty, caret, _session.Ctx);
            RefreshSuggestions();
        }

        private void HistoryPrev()
        {
            var h = _session.History;
            if (h.Count == 0) return;
            if (_historyIndex < 0) _historyIndex = h.Count;
            _historyIndex = Mathf.Max(0, _historyIndex - 1);
            _input.SetTextWithoutNotify(h[_historyIndex]);
            FocusInput(_input.text.Length);
        }

        private void HistoryNext()
        {
            var h = _session.History;
            if (h.Count == 0 || _historyIndex < 0) return;
            _historyIndex++;
            if (_historyIndex >= h.Count) { _historyIndex = -1; _input.SetTextWithoutNotify(string.Empty); }
            else _input.SetTextWithoutNotify(h[_historyIndex]);
            FocusInput(_input.text.Length);
        }

        private void RunQuick(string cmd)
        {
            _session.Execute(cmd);
            RefreshOutput();
            SwitchTab(0);
        }

        private void Prefill(string template)
        {
            SwitchTab(0);
            _input.SetTextWithoutNotify(template);
            RecomputeAutocomplete(template, template.Length);
            FocusInput(template.Length);
        }

        // ---- Open / Close ------------------------------------------------------

        private void SetOpen(bool open)
        {
            _open = open;
            if (_group != null)
            {
                _group.alpha = open ? 1f : 0f;
                _group.interactable = open;
                _group.blocksRaycasts = open;
            }

            if (open)
            {
                DisableGameplayInput();
                SwitchTab(0);
                _input.SetTextWithoutNotify(string.Empty);
                _session.Autocomplete.Reset();
                RefreshSuggestions();
                RefreshOutput();
                FocusInput(0);
            }
            else
            {
                RestoreGameplayInput();
                if (_input != null) _input.DeactivateInputField();
                _session.Autocomplete.Reset();
                RefreshSuggestions();
            }
        }

        private void DisableGameplayInput()
        {
            if (!ServiceLocator.TryGetService<CameraInputConfig>(out var cfg) || cfg?.Actions == null) return;
            var disabled = new List<InputActionMap>();
            foreach (var mapName in new[] { "Player", "Camera" })
            {
                var map = cfg.Actions.FindActionMap(mapName, throwIfNotFound: false);
                if (map != null && map.enabled) { map.Disable(); disabled.Add(map); }
            }
            _disabledMaps = disabled.ToArray();
        }

        private void RestoreGameplayInput()
        {
            if (_disabledMaps == null) return;
            foreach (var map in _disabledMaps) map.Enable();
            _disabledMaps = null;
        }

        private void FocusInput(int caret)
        {
            if (_input == null) return;
            _input.ActivateInputField();
            _input.caretPosition = caret;
            _input.selectionAnchorPosition = caret;
            _input.selectionFocusPosition = caret;
        }

        // ---- Rendering ---------------------------------------------------------

        private void RefreshOutput()
        {
            if (_output == null) return;
            var lines = _session.Log.Lines;
            int start = Mathf.Max(0, lines.Count - MaxOutputLines);
            var sb = new StringBuilder();
            for (int i = start; i < lines.Count; i++) sb.AppendLine(lines[i]);
            _output.text = sb.ToString();
        }

        private void RefreshSuggestions()
        {
            var res = _session.Autocomplete.Current;
            bool show = _open
                        && _input != null && !string.IsNullOrEmpty(_input.text)
                        && res.Target != SuggestionTarget.None
                        && res.Suggestions.Count > 0;

            _suggestionsActive = show;
            if (_suggestionsHost != null) _suggestionsHost.SetActive(show);
            if (!show) return;

            int sel = _session.Autocomplete.SelectedIndex;
            int shown = Mathf.Min(_suggestionRows.Count, res.Suggestions.Count);
            for (int i = 0; i < _suggestionRows.Count; i++)
            {
                var row = _suggestionRows[i];
                if (i < shown)
                {
                    row.gameObject.SetActive(true);
                    bool isSel = i == sel;
                    row.text = (isSel ? "▶ " : "    ") + res.Suggestions[i].Text;
                    row.color = isSel ? new Color(1f, 0.9f, 0.35f) : new Color(0.78f, 0.80f, 0.85f);
                }
                else
                {
                    row.gameObject.SetActive(false);
                }
            }
        }

        private void SwitchTab(int idx)
        {
            if (_consolePanel != null) _consolePanel.SetActive(idx == 0);
            if (_playerPanel != null) _playerPanel.SetActive(idx == 1);
            if (_dicePanel != null) _dicePanel.SetActive(idx == 2);
            if (_worldPanel != null) _worldPanel.SetActive(idx == 3);
        }

        // ---- UI construction ---------------------------------------------------

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            if (FindObjectOfType<EventSystem>() != null) return;
            // Fallback no persistente: las escenas de gameplay traen su propio EventSystem.
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var root = UIFactory.Panel(canvasGO.transform, "Root", Bg);
            _group = root.AddComponent<CanvasGroup>();
            var rootRt = root.GetComponent<RectTransform>();
            // Bottom-left, compacto: ~50% ancho × ~58% alto, sin tapar toda la pantalla.
            rootRt.anchorMin = new Vector2(0f, 0f);
            rootRt.anchorMax = new Vector2(0.5f, 0.58f);
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            UIFactory.VLayout(root, 6, 4);

            BuildTabBar(root.transform);
            BuildMiddle(root.transform);
            BuildSuggestions(root.transform);
            BuildInputRow(root.transform);
        }

        private void BuildTabBar(Transform parent)
        {
            var bar = UIFactory.Panel(parent, "TabBar", TabBg);
            UIFactory.HLayout(bar, 2, 4);
            var le = bar.AddComponent<LayoutElement>();
            le.minHeight = 26; le.preferredHeight = 26; le.flexibleHeight = 0;

            UIFactory.Button(bar.transform, "Console", BtnBg, () => SwitchTab(0));
            UIFactory.Button(bar.transform, "Player", BtnBg, () => SwitchTab(1));
            UIFactory.Button(bar.transform, "Dice", BtnBg, () => SwitchTab(2));
            UIFactory.Button(bar.transform, "World", BtnBg, () => SwitchTab(3));
        }

        private void BuildMiddle(Transform parent)
        {
            var middle = UIFactory.Panel(parent, "Middle", new Color(0, 0, 0, 0));
            var le = middle.AddComponent<LayoutElement>();
            le.flexibleHeight = 1; le.minHeight = 120;

            // Console
            _consolePanel = UIFactory.Panel(middle.transform, "ConsolePanel", new Color(0, 0, 0, 0.25f));
            UIFactory.Stretch(_consolePanel);
            _consolePanel.AddComponent<RectMask2D>();
            _output = UIFactory.Text(_consolePanel.transform, "Output", string.Empty, 14,
                new Color(0.85f, 0.88f, 0.92f), TextAlignmentOptions.BottomLeft);
            UIFactory.Stretch(_output.gameObject);

            _playerPanel = BuildButtonTab(middle, "PlayerPanel", new (string, System.Action)[]
            {
                ("Heal full", () => RunQuick("heal full")),
                ("God mode (toggle)", () => RunQuick("god")),
                ("HP máximo (sethp 9999)", () => RunQuick("sethp 9999")),
                ("+100 oro", () => RunQuick("gold 100")),
                ("+1000 oro", () => RunQuick("gold 1000")),
                ("Energía infinita (toggle)", () => RunQuick("energy inf")),
                ("Matar enemigos", () => RunQuick("killall")),
                ("Prefill setstat", () => Prefill("setstat Attack 10")),
            });

            _dicePanel = BuildButtonTab(middle, "DicePanel", new (string, System.Action)[]
            {
                ("Listar dados", () => RunQuick("dice")),
                ("Prefill setdice", () => Prefill("setdice 0 D6")),
                ("Prefill setbag", () => Prefill("setbag D6 D6 D8 D20 D4")),
                ("Prefill ench add", () => Prefill("ench add 0 0 ")),
                ("Prefill ench list", () => Prefill("ench list 0")),
                ("Prefill setdiceroll", () => Prefill("setdiceroll 6 6 6 6 6")),
            });

            _worldPanel = BuildButtonTab(middle, "WorldPanel", new (string, System.Action)[]
            {
                ("Free move (toggle)", () => RunQuick("freemove")),
                ("Puerta Norte", () => RunQuick("door North")),
                ("Puerta Sur", () => RunQuick("door South")),
                ("Puerta Este", () => RunQuick("door East")),
                ("Puerta Oeste", () => RunQuick("door West")),
                ("Listar salas", () => RunQuick("floor")),
                ("Siguiente piso", () => RunQuick("floor next")),
                ("Prefill teleport", () => Prefill("tp 0 0")),
            });
        }

        private GameObject BuildButtonTab(GameObject middle, string name, (string label, System.Action action)[] buttons)
        {
            var panel = UIFactory.Panel(middle.transform, name, new Color(0, 0, 0, 0.25f));
            UIFactory.Stretch(panel);
            var v = UIFactory.VLayout(panel, 8, 6);
            v.childAlignment = TextAnchor.UpperCenter;
            v.childForceExpandHeight = false;
            foreach (var (label, action) in buttons)
                UIFactory.Button(panel.transform, label, BtnBg, () => action());
            return panel;
        }

        private void BuildSuggestions(Transform parent)
        {
            _suggestionsHost = UIFactory.Panel(parent, "Suggestions", new Color(0.10f, 0.11f, 0.16f, 0.98f));
            var v = UIFactory.VLayout(_suggestionsHost, 4, 1);
            v.childForceExpandHeight = false;

            for (int i = 0; i < SuggestionRowCount; i++)
            {
                var row = UIFactory.Text(_suggestionsHost.transform, "Sugg" + i, string.Empty, 14,
                    new Color(0.8f, 0.8f, 0.85f), TextAlignmentOptions.Left);
                var le = row.gameObject.AddComponent<LayoutElement>();
                le.minHeight = 18; le.preferredHeight = 18;
                _suggestionRows.Add(row);
            }
            _suggestionsHost.SetActive(false);
        }

        private void BuildInputRow(Transform parent)
        {
            var row = UIFactory.Panel(parent, "InputRow", new Color(0.10f, 0.11f, 0.16f, 1f));
            UIFactory.HLayout(row, 4, 4);
            var le = row.AddComponent<LayoutElement>();
            le.minHeight = 30; le.preferredHeight = 30; le.flexibleHeight = 0;

            var prompt = UIFactory.Text(row.transform, "Prompt", ">", 18, new Color(0.5f, 1f, 0.6f), TextAlignmentOptions.Center);
            var promptLe = prompt.gameObject.AddComponent<LayoutElement>();
            promptLe.minWidth = 16; promptLe.preferredWidth = 16; promptLe.flexibleWidth = 0;

            var inputGO = TMP_DefaultControls.CreateInputField(default(TMP_DefaultControls.Resources));
            inputGO.name = "CommandInput";
            inputGO.transform.SetParent(row.transform, false);
            var inputImg = inputGO.GetComponent<Image>();
            if (inputImg != null) inputImg.color = new Color(0.04f, 0.04f, 0.06f, 1f);
            var inputLe = inputGO.AddComponent<LayoutElement>();
            inputLe.flexibleWidth = 1; inputLe.minHeight = 28;

            _input = inputGO.GetComponent<TMP_InputField>();
            _input.lineType = TMP_InputField.LineType.SingleLine;
            _input.richText = false;
            _input.caretColor = Color.white;
            _input.customCaretColor = true;
            var nav = _input.navigation;
            nav.mode = Navigation.Mode.None;
            _input.navigation = nav;

            if (_input.textComponent != null) _input.textComponent.color = new Color(0.95f, 0.97f, 1f);
            if (_input.placeholder is TMP_Text ph)
            {
                ph.text = "comando…  (Tab autocompleta · ↑/↓ navega · Enter ejecuta)";
                ph.color = new Color(1f, 1f, 1f, 0.35f);
            }

            _input.onValueChanged.AddListener(OnInputChanged);
            // Evita que el backquote (toggle) y el Tab se inserten en el campo.
            _input.onValidateInput += (text, index, added) => (added == '`' || added == '\t') ? '\0' : added;
        }
    }
}
