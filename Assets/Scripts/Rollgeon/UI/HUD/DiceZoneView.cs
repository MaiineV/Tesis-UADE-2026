using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.ActionRolls;
using Rollgeon.Combat.Damage;
using Rollgeon.Combat.Handoff;
using Rollgeon.Combos;
using Rollgeon.Dice;
using Rollgeon.Heroes;
using Rollgeon.Player;
using Rollgeon.UI.HUD.DiceAnim;
using Rollgeon.Upgrades.Dice;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Sub-view del Combat HUD que muestra los dados rolleados, gestiona el estado
    /// de hold por dado, y dispara detección de combos en tiempo real. T97c.
    /// Plan §3.6.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Dice Zone View")]
    public class DiceZoneView : MonoBehaviour
    {
        [Title("Dice Zone — Anchors")]
        [Required("Arrastrar el RectTransform de la roll area (donde se 'tiran' los dados).")]
        [SerializeField]
        private RectTransform _rollArea;

        [Required("Arrastrar el RectTransform de la hold area (donde se holdean los dados).")]
        [SerializeField]
        private RectTransform _holdArea;

        [Title("Dice Zone — Slots")]
        [InfoBox("5 anchor children (uno por dado del combo del guerrero). Cada GameObject " +
                 "debe tener DiceSlotView + Button para que el hold funcione.")]
        [SerializeField]
        private List<RectTransform> _diceSlots = new List<RectTransform>();

        [SerializeField, Tooltip("Stagger de aparición entre los '+N' de los dados contribuyentes.")]
        private float _contributionStaggerSeconds = 0.05f;

        // ---- Runtime state ---------------------------------------------------

        private Guid _playerGuid;
        private DiceSlotView[] _resolvedSlots;
        private int[] _currentFaces;
        private bool[] _heldStates;
        private DiceZoneAnimator _animator;

        // Reroll invertido: selección vigente al momento de arrancar el reroll
        // (= dados que van a volar). La stashea HandleRerollStarted antes de
        // consumir los holds y la usa el próximo HandleDiceRolled para no
        // re-spinear los dados que se quedaron. One-shot: se consume al reveal.
        private bool[] _pendingRerollMask;

        /// <summary>True mientras corre el spin del roll o el outro del confirm (modo
        /// Classic). Las views de botones lo usan para lockear Roll/Confirm.</summary>
        public bool IsDiceAnimating => _animator != null && (_animator.IsSpinning || _animator.IsOutroPlaying);

        /// <summary>Se dispara cuando arranca/termina una animación de dados — las
        /// views de botones re-gatean acá (espejo de <see cref="DiceZoneAnimator.AnimationStateChanged"/>).</summary>
        public event Action DiceAnimationStateChanged;

        // ---- Public anchors (usados por T97c downstream si necesitan posicionar GOs) ---

        /// <summary>Anchor para la zona de tiro.</summary>
        public RectTransform GetRollArea() => _rollArea;

        /// <summary>Anchor para la zona de hold.</summary>
        public RectTransform GetHoldArea() => _holdArea;

        /// <summary>Lista readonly de los anchors de cada dado.</summary>
        public IReadOnlyList<RectTransform> GetDiceSlots() => _diceSlots;

        /// <summary>
        /// Snapshot del estado de hold (selección) por slot. Devuelve una copia
        /// defensiva — el caller puede mutarla. Si <c>Bind</c> aún no corrió, devuelve
        /// un array vacío. Consumido por el
        /// <see cref="Rollgeon.Combat.Handoff.CombatHandoffService"/>: para el combo es
        /// la máscara directa; para el reroll (invertido, Balatro) se complementa antes
        /// de pasarla como <c>keep[]</c> al <c>IDiceRoller.Reroll</c>.
        /// </summary>
        public bool[] GetHeldStates()
        {
            if (_heldStates == null) return Array.Empty<bool>();
            var copy = new bool[_heldStates.Length];
            Array.Copy(_heldStates, copy, _heldStates.Length);
            return copy;
        }

        /// <summary>
        /// True si hay al menos un dado seleccionado (holdeado). Lo usan el router de
        /// click derecho (deselect-all solo si hay algo que soltar) y el gate del botón
        /// de reroll (seleccionado = se re-tira; sin selección no hay nada que re-tirar).
        /// </summary>
        public bool AnyDieHeld()
        {
            if (_heldStates == null) return false;
            for (int i = 0; i < _heldStates.Length; i++)
                if (_heldStates[i]) return true;
            return false;
        }

        /// <summary>
        /// True si TODOS los dados están seleccionados. Gate del botón de reroll en
        /// modo clásico (seleccionado = se queda): con todo lockeado no queda nada
        /// que re-tirar. Sin holds (pre-Bind) devuelve false.
        /// </summary>
        public bool AllDiceHeld()
        {
            if (_heldStates == null || _heldStates.Length == 0) return false;
            for (int i = 0; i < _heldStates.Length; i++)
                if (!_heldStates[i]) return false;
            return true;
        }

        // ---- Bind / Unbind ---------------------------------------------------

        private bool _bound;

        public void Bind(Guid playerGuid)
        {
            // Idempotente: ambos HUDs (combat + exploration) bindean a este componente
            // ahora que vive en el Canvas raíz. Doble Bind sin Unbind generaría doble
            // subscripción a eventos. Si ya estoy bindeado al mismo guid, no-op; si es
            // otro guid (transición de jugador), Unbind primero.
            if (_bound)
            {
                if (_playerGuid == playerGuid) return;
                Unbind();
            }

            _playerGuid = playerGuid;
            int count = _diceSlots.Count;
            _resolvedSlots = new DiceSlotView[count];
            _currentFaces = new int[count];
            _heldStates = new bool[count];
            _bound = true;

            for (int i = 0; i < count; i++)
            {
                if (_diceSlots[i] == null) continue;
                _resolvedSlots[i] = _diceSlots[i].GetComponent<DiceSlotView>();
                if (_resolvedSlots[i] == null)
                {
                    Debug.LogWarning($"[DiceZoneView] Slot {i} no tiene DiceSlotView. " +
                                     "Agregá el componente en el Inspector.", this);
                    continue;
                }
                int captured = i;
                _resolvedSlots[i].OnToggled.AddListener(() => ToggleHold(captured));
            }

            // Animación legacy (Classic). Vive como componente hermano agregado por
            // código: sin cirugía de prefab, y si no está el servicio de throw o el
            // modo no es Classic, todos sus TryBegin* devuelven false (path instantáneo).
            _animator = GetComponent<DiceZoneAnimator>();
            if (_animator == null) _animator = gameObject.AddComponent<DiceZoneAnimator>();
            _animator.Bind(_resolvedSlots, _rollArea);
            _animator.AnimationStateChanged += RaiseDiceAnimationStateChanged;

            EventManager.Subscribe(EventName.OnDiceRolled, HandleDiceRolled);
            EventManager.Subscribe(EventName.OnRerollStarted, HandleRerollStarted);
            EventManager.Subscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.Subscribe(EventName.OnRollResolved, HandleRollResolved);
            EventManager.Subscribe(EventName.OnDiceBlockChanged, HandleDiceBlockChanged);
            EventManager.Subscribe(EventName.OnEnchantmentApplied, HandleEnchantmentChanged);
            EventManager.Subscribe(EventName.OnEnchantmentRemoved, HandleEnchantmentChanged);

            BindDieHotkeys();

            // Estado inicial: slots apagados hasta que el jugador presione Roll.
            ClearAll();

            // El altar encanta durante exploración y este mismo componente ya está bindeado
            // ahí, así que Bind cubre el estado inicial y los eventos los cambios en vivo.
            RefreshEnchantVisuals();
        }

        public void Unbind()
        {
            if (!_bound) return;
            UnbindDieHotkeys();
            CancelDeferredOutro();
            if (_animator != null)
            {
                _animator.AnimationStateChanged -= RaiseDiceAnimationStateChanged;
                _animator.Unbind();
            }
            EventManager.UnSubscribe(EventName.OnDiceRolled, HandleDiceRolled);
            EventManager.UnSubscribe(EventName.OnRerollStarted, HandleRerollStarted);
            EventManager.UnSubscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.UnSubscribe(EventName.OnRollResolved, HandleRollResolved);
            EventManager.UnSubscribe(EventName.OnDiceBlockChanged, HandleDiceBlockChanged);
            EventManager.UnSubscribe(EventName.OnEnchantmentApplied, HandleEnchantmentChanged);
            EventManager.UnSubscribe(EventName.OnEnchantmentRemoved, HandleEnchantmentChanged);
            if (_resolvedSlots != null)
                foreach (var s in _resolvedSlots)
                {
                    s?.OnToggled.RemoveAllListeners();
                }
            _resolvedSlots = null;
            _currentFaces = null;
            _heldStates = null;
            _pendingRerollMask = null;
            _bound = false;
        }

        // ---- Hotkeys 1-5 -----------------------------------------------------

        // Las teclas 1..5 togglean el hold del dado 1..5, mismo camino que el click
        // (ToggleHold ya gatea fase, bloqueos y CapPreventHolding). Un array de
        // handlers y no lambdas inline: Unsubscribe necesita la MISMA referencia.
        private Rollgeon.Input.IGameplayHotkeyService _hotkeys;
        private Action<UnityEngine.InputSystem.InputAction.CallbackContext>[] _dieHotkeyHandlers;

        private static readonly Rollgeon.Input.GameplayHotkey[] DieHotkeys =
        {
            Rollgeon.Input.GameplayHotkey.SelectDie1,
            Rollgeon.Input.GameplayHotkey.SelectDie2,
            Rollgeon.Input.GameplayHotkey.SelectDie3,
            Rollgeon.Input.GameplayHotkey.SelectDie4,
            Rollgeon.Input.GameplayHotkey.SelectDie5,
        };

        private void BindDieHotkeys()
        {
            if (!ServiceLocator.TryGetService<Rollgeon.Input.IGameplayHotkeyService>(out _hotkeys)
                || _hotkeys == null) return;

            _dieHotkeyHandlers = new Action<UnityEngine.InputSystem.InputAction.CallbackContext>[DieHotkeys.Length];
            for (int i = 0; i < DieHotkeys.Length; i++)
            {
                int captured = i;
                _dieHotkeyHandlers[i] = _ => OnDieHotkey(captured);
                _hotkeys.Subscribe(DieHotkeys[i], _dieHotkeyHandlers[i]);
            }
        }

        private void UnbindDieHotkeys()
        {
            if (_hotkeys == null || _dieHotkeyHandlers == null) return;
            for (int i = 0; i < DieHotkeys.Length; i++)
                if (_dieHotkeyHandlers[i] != null)
                    _hotkeys.Unsubscribe(DieHotkeys[i], _dieHotkeyHandlers[i]);
            _dieHotkeyHandlers = null;
            _hotkeys = null;
        }

        private void OnDieHotkey(int index)
        {
            // Sin slot resuelto (bag más chica, slot roto) la tecla no hace nada; el
            // resto de los gates (fase de roll, dado bloqueado, Lento) los aplica
            // ToggleHold — exactamente los mismos que el click sobre el dado.
            if (_resolvedSlots == null || index >= _resolvedSlots.Length) return;
            if (_resolvedSlots[index] == null) return;
            ToggleHold(index);
        }

        // ---- Event handler ---------------------------------------------------

        private void HandleDiceRolled(params object[] args)
        {
            if (args == null || args.Length < 2) return;
            if (args[0] is not Guid guid || guid != _playerGuid) return;
            var faces = (IReadOnlyList<int>)args[1];

            // Un roll nuevo puede llegar con el outro del confirm anterior todavía en
            // el aire (chain phases). Completar el teardown diferido ANTES de escribir
            // el estado nuevo — su ClearAll pisaría las caras recién asignadas.
            _animator?.CancelOutroAndComplete();

            // Antes del loop de reveal: la silueta tiene que ser la correcta mientras el dado
            // gira, no recién al frenar.
            RefreshDiceShapes();

            int count = _resolvedSlots?.Length ?? 0;
            var willReveal = new bool[count];
            // Qué dados volaron lo dice la máscara stasheada en HandleRerollStarted
            // (invertido: la selección, que ya se consumió; clásico: su complemento —
            // los holds persisten y el skip de holdeados de abajo mantiene estáticos
            // los lockeados). Sin máscara (primer roll, fase de chain), revela todo
            // como siempre.
            var rerollMask = _pendingRerollMask;
            _pendingRerollMask = null;
            for (int i = 0; i < count; i++)
            {
                if (_heldStates != null && i < _heldStates.Length && _heldStates[i]) continue;
                int newFace = i < faces.Count ? faces[i] : 0;
                // Dado conservado en el reroll (no seleccionado) con la cara intacta:
                // no re-spinea ni re-revela — quedaría idéntico pero LEERÍA como
                // re-tirado. El chequeo de cara-distinta cubre el grab-to-reroll 2D,
                // donde lo agarrado puede diferir de lo que estaba seleccionado.
                if (rerollMask != null
                    && !(i < rerollMask.Length && rerollMask[i])
                    && newFace == _currentFaces[i])
                {
                    continue;
                }
                willReveal[i] = true;
                _currentFaces[i] = newFace;
                if (_resolvedSlots[i] != null) _resolvedSlots[i].gameObject.SetActive(true);
                _resolvedSlots[i]?.SetHeld(false);
            }

            // Path animado (Classic): el spin cicla caras random y revela al final —
            // ShowFace y el refresh de combo/bloqueos corren recién en el reveal, así
            // la preview de combo no spoilea el resultado durante el giro.
            if (_animator != null && _animator.TryBeginSpin(willReveal, _currentFaces, RefreshDiceBlock))
                return;

            for (int i = 0; i < count; i++)
                if (willReveal[i]) _resolvedSlots[i]?.ShowFace(_currentFaces[i]);
            RefreshDiceBlock();
        }

        // Al ARRANCAR el reroll (RerollBudgetService / ActionRollService lo emiten
        // después de computar el keep y cobrar, antes del OnDiceRolled del resultado)
        // se stashea la máscara "estos volaron": el reveal la usa para que los dados
        // que se quedan NO re-spineen (sin la máscara, el spin de Classic ciclaría
        // caras random en los 5 y leería como reroll de toda la mano).
        //
        // Invertido (default, Balatro): los seleccionados son los que vuelan y el
        // descarte consume la selección — stash de los holds + ClearHolds. Clásico:
        // vuelan los NO seleccionados y los holds PERSISTEN (los lockeados siguen
        // siendo el pick de combo, espejo de ActionRollService). En grab-mode 2D se
        // mantiene el comportamiento invertido: lo que vuela es lo agarrado
        // (keepOverride físico), no la selección — persistir holds ahí congelaría la
        // cara de un dado agarrado-pero-holdeado en el reveal.
        // En el primer roll no hay holds — no-op.
        private void HandleRerollStarted(params object[] args)
        {
            if (args == null || args.Length < 1) return;
            if (args[0] is not Guid guid || guid != _playerGuid) return;
            if (RerollSelectionPrefs.KeepSelected && !IsGrabRerollMode())
            {
                // Lento vuela aunque esté seleccionado (ApplyKeepConstraints): se suelta su
                // hold ANTES de armar la máscara para que el reveal procese su cara nueva
                // en vez de saltearlo como lockeado.
                ReleaseHoldsThatAlwaysReroll();
                _pendingRerollMask = ComplementOfHeldStates();
                return;
            }
            _pendingRerollMask = AnyDieHeld() ? GetHeldStates() : null;
            ClearHolds();
        }

        private void ReleaseHoldsThatAlwaysReroll()
        {
            if (_heldStates == null) return;
            for (int i = 0; i < _heldStates.Length; i++)
            {
                if (!_heldStates[i]) continue;
                if (!EnchantmentCapabilityQueries.PlayerSlotHasCapability<CapPreventHolding>(i)) continue;
                _heldStates[i] = false;
                _resolvedSlots?[i]?.SetHeld(false);
                _animator?.SetRaised(i, false);
            }
        }

        // Clásico: "estos volaron" = los no seleccionados. Sin holds (mano entera
        // re-tirada) devuelve null — sin máscara el reveal procesa todo, correcto.
        private bool[] ComplementOfHeldStates()
        {
            if (_heldStates == null || !AnyDieHeld()) return null;
            var mask = new bool[_heldStates.Length];
            for (int i = 0; i < _heldStates.Length; i++)
                mask[i] = !_heldStates[i];
            return mask;
        }

        private static bool IsGrabRerollMode()
            => ServiceLocator.TryGetService<Rollgeon.Dice.Throw.IDiceThrowService>(out var t)
               && t != null && t.Mode == Rollgeon.Dice.Throw.DiceThrowMode.TwoD;

        /// <summary>
        /// Pinta la silueta de cada slot según el tipo de dado que tiene en el bag. El índice de
        /// slot mapea 1:1 al del bag (<see cref="RunComboDetection"/> ya lo asume).
        /// </summary>
        /// <remarks>
        /// No se llama desde <c>Bind</c>: el bag es run-scoped y ahí todavía es null. Un roll no
        /// puede ocurrir antes de que exista, así que engancharlo al roll alcanza. Idempotente y
        /// gratis — el setter de <c>Image.sprite</c> early-outea si no cambió.
        /// </remarks>
        private void RefreshDiceShapes()
        {
            if (_resolvedSlots == null) return;
            if (!ServiceLocator.TryGetService<IDiceEnchantmentService>(out var enchants)) return;
            if (enchants == null || !enchants.IsReady) return;

            var bag = enchants.Bag.Dice;
            int count = Mathf.Min(_resolvedSlots.Length, bag.Count);
            for (int i = 0; i < count; i++)
                _resolvedSlots[i]?.SetDiceType(bag[i]);
        }

        // El payload trae (playerGuid, upgradeId, bagIndex, enchSlotIndex); lo ignoramos y
        // refrescamos entero, como HandleDiceBlockChanged. Es idempotente e inmune a un error
        // de mapeo de índice, y son 5 slots.
        private void HandleEnchantmentChanged(params object[] args) => RefreshEnchantVisuals();

        /// <summary>
        /// Pinta el visual de encantamiento de cada slot según su dado en el bag.
        /// </summary>
        private void RefreshEnchantVisuals()
        {
            if (_resolvedSlots == null) return;
            if (!ServiceLocator.TryGetService<IDiceEnchantmentService>(out var enchants)) return;
            if (enchants == null || !enchants.IsReady) return;

            int count = Mathf.Min(_resolvedSlots.Length, enchants.Bag.Dice.Count);
            for (int i = 0; i < count; i++)
                _resolvedSlots[i]?.SetEnchantVisual(
                    DiceEnchantVisualResolver.ResolvePrimary(enchants.Bag.GetEnchantments(i)));
        }

        // Boss 1 (§2): refleja el estado de IDiceBlockService en los slots — grayed + candado,
        // fuerza hold off en los bloqueados, y re-corre la detección de combo (que ya excluye
        // los bloqueados). Se llama tras cada roll y al cambiar el set de dados bloqueados.
        private void HandleDiceBlockChanged(params object[] args)
        {
            // El payload trae el playerGuid; refrescamos siempre (el set es del jugador activo).
            RefreshDiceBlock();
        }

        private void RefreshDiceBlock()
        {
            if (_resolvedSlots == null) return;
            ServiceLocator.TryGetService<Rollgeon.Combat.DiceBlock.IDiceBlockService>(out var db);

            for (int i = 0; i < _resolvedSlots.Length; i++)
            {
                bool bossBlocked = db != null && db.IsBlocked(i);
                // Sediento / Vampiro: mismo candado que el boss, pero derivado del estado del
                // jugador (oro / vida) — se re-evalúa en cada reveal, que es cuando cambia.
                string lockLabel = null;
                bool locked = !bossBlocked && DiceSelectionLocks.IsPlayerSlotLocked(i, out lockLabel);
                bool blocked = bossBlocked || locked;
                if (blocked && _heldStates != null && i < _heldStates.Length)
                {
                    _heldStates[i] = false; // un dado bloqueado no puede quedar holdeado
                    // El unhold forzado no pasa por SetHeld/ToggleHold — bajar el
                    // raise explícitamente o el dado queda flotando bloqueado.
                    _animator?.SetRaised(i, false);
                }
                // La etiqueta dice QUIÉN se llevó el dado (el número que cantó el Croupier) o
                // QUÉ falta para usarlo ("2 oro"). Sin ella, el jugador ve un candado y no
                // tiene cómo atarlo a su causa.
                string label = bossBlocked ? db.LabelOf(i) : locked ? lockLabel : null;
                _resolvedSlots[i]?.SetBlocked(blocked, label);
            }
            PropagateHoldsToActionRoll();
            RunComboDetection();
        }

        private void HandleTurnStarted(params object[] args)
        {
            // OnTurnStarted dispara para cada participante (player + enemigos);
            // sólo limpiamos cuando arranca el turno del jugador propietario del HUD.
            if (args == null || args.Length < 1) return;
            if (args[0] is not Guid guid || guid != _playerGuid) return;
            ClearAll();
        }

        private void HandleRollResolved(params object[] args)
        {
            if (args == null || args.Length < 1) return;
            if (args[0] is not Guid guid || guid != _playerGuid) return;
            // Con la secuencia de breakdown (N×M) corriendo, los dados se quedan en su
            // slot — la animación vuela sus valores desde ahí. El outro arranca recién
            // cuando el gate se libera.
            if (Rollgeon.Feedback.BreakdownUiGate.Pending)
            {
                if (!_outroDeferredByBreakdown)
                {
                    _outroDeferredByBreakdown = true;
                    Rollgeon.Feedback.BreakdownUiGate.Changed += RunOutroWhenBreakdownEnds;
                }
                return;
            }
            BeginOutroOrClear();
        }

        // Latch del outro diferido por la secuencia de breakdown.
        private bool _outroDeferredByBreakdown;

        private void RunOutroWhenBreakdownEnds()
        {
            if (Rollgeon.Feedback.BreakdownUiGate.Pending) return;
            CancelDeferredOutro();
            BeginOutroOrClear();
        }

        private void CancelDeferredOutro()
        {
            if (!_outroDeferredByBreakdown) return;
            _outroDeferredByBreakdown = false;
            Rollgeon.Feedback.BreakdownUiGate.Changed -= RunOutroWhenBreakdownEnds;
        }

        private void BeginOutroOrClear()
        {
            // Path animado (Classic): los holdeados vuelan al centro de la mesa y los
            // demás se descartan; el ClearAll corre diferido al terminar el outro.
            if (_animator != null && _animator.TryBeginOutro(_heldStates, BuildActiveMask(), ClearAll))
                return;
            ClearAll();
        }

        /// <summary>
        /// Slot view por bag slot (1:1 con el índice visual). Lo consume el director del
        /// breakdown para animar el dado físico. Null fuera de rango o sin resolver.
        /// </summary>
        public DiceSlotView GetSlotView(int bagSlot)
            => _resolvedSlots != null && bagSlot >= 0 && bagSlot < _resolvedSlots.Length
                ? _resolvedSlots[bagSlot]
                : null;

        private bool[] BuildActiveMask()
        {
            int count = _resolvedSlots?.Length ?? 0;
            var active = new bool[count];
            for (int i = 0; i < count; i++)
                active[i] = _resolvedSlots[i] != null && _resolvedSlots[i].gameObject.activeSelf;
            return active;
        }

        private void RaiseDiceAnimationStateChanged() => DiceAnimationStateChanged?.Invoke();

        // ---- Clear / reset --------------------------------------------------

        /// <summary>
        /// Apaga todos los slots y resetea holds/faces. Pública para que el
        /// <c>CombatHandoffService</c> u otros pueden forzar el clear ante eventos
        /// no estándar (cancel, retreat, etc.).
        /// </summary>
        public void ClearAll()
        {
            // Un clear forzado (turn start, retreat) invalida el outro que esperaba a la
            // secuencia de breakdown — sin esto, el handler colgado dispararía un outro
            // sobre slots ya limpios.
            CancelDeferredOutro();
            // Aborta cualquier animación en curso sin ejecutar callbacks diferidos
            // (si ClearAll llegó como completion del outro, esto ya es no-op).
            _animator?.ResetAll();
            // Una máscara de reroll pendiente no sobrevive a un clear forzado — el
            // próximo roll es una mano nueva y debe revelarse entera.
            _pendingRerollMask = null;
            if (_currentFaces != null)
                Array.Clear(_currentFaces, 0, _currentFaces.Length);
            if (_heldStates != null)
                Array.Clear(_heldStates, 0, _heldStates.Length);
            if (_resolvedSlots != null)
            {
                foreach (var s in _resolvedSlots)
                {
                    s?.Clear();
                    // Flow manual: los slots se ocultan hasta el primer roll. Apenas
                    // OnDiceRolled dispara HandleDiceRolled, cada slot se vuelve a
                    // activar antes de pintar su cara.
                    if (s != null) s.gameObject.SetActive(false);
                }
            }
            RunComboDetection();
        }

        // ---- Throw manual (CNF-008) -------------------------------------------

        private bool[] _hiddenForThrow;

        /// <summary>
        /// Una sesión de throw manual acaba de abrir (chains: puede pasar con el outro
        /// del confirm anterior todavía volando — el <c>CancelOutroAndComplete</c> de
        /// <see cref="HandleDiceRolled"/> no alcanza porque en 2D/3D el reveal llega
        /// recién al settle). Completa YA el teardown diferido para que el gate no
        /// quede pendiente durante la sesión nueva.
        /// </summary>
        public void NotifyThrowSessionStarted()
        {
            _animator?.CancelOutroAndComplete();
        }

        /// <summary>
        /// Oculta los slots cuyos dados van a volar en una sesión de throw manual —
        /// en un reroll seguían mostrando la cara vieja debajo de los dados voladores.
        /// <see cref="HandleDiceRolled"/> los reactiva solo en el reveal; si la sesión
        /// se aborta, <see cref="RestoreSlotsAfterThrow"/> los devuelve como estaban.
        /// </summary>
        public void HideSlotsForThrow(IReadOnlyList<bool> thrownMask)
        {
            if (_resolvedSlots == null || thrownMask == null) return;
            _hiddenForThrow = new bool[_resolvedSlots.Length];
            for (int i = 0; i < _resolvedSlots.Length && i < thrownMask.Count; i++)
            {
                if (!thrownMask[i] || _resolvedSlots[i] == null) continue;
                if (!_resolvedSlots[i].gameObject.activeSelf) continue;
                _hiddenForThrow[i] = true;
                _resolvedSlots[i].gameObject.SetActive(false);
            }
        }

        /// <summary>Deshace <see cref="HideSlotsForThrow"/> tras un abort (sin reveal).</summary>
        public void RestoreSlotsAfterThrow()
        {
            if (_resolvedSlots == null || _hiddenForThrow == null) return;
            for (int i = 0; i < _resolvedSlots.Length && i < _hiddenForThrow.Length; i++)
            {
                if (_hiddenForThrow[i] && _resolvedSlots[i] != null)
                    _resolvedSlots[i].gameObject.SetActive(true);
            }
            _hiddenForThrow = null;
        }

        // ---- Hold toggle -----------------------------------------------------

        private void ToggleHold(int i)
        {
            if (!CanChangeHold(i, nameof(ToggleHold))) return;
            ApplyHold(i, !_heldStates[i]);
        }

        // Gates del toggle (y de invocaciones programáticas).
        private bool CanChangeHold(int i, string caller)
        {
            if (_heldStates == null || i < 0 || i >= _heldStates.Length)
            {
                Debug.Log($"[DiceZoneView] {caller}({i}) — aborted: _heldStates null={_heldStates == null} len={_heldStates?.Length}");
                return false;
            }
            // Boss 1 (§2): un dado bloqueado no puede holdearse.
            if (ServiceLocator.TryGetService<Rollgeon.Combat.DiceBlock.IDiceBlockService>(out var db)
                && db != null && db.IsBlocked(i))
                return false;

            // Sediento / Vampiro (CapSelectionRequirement): sin el recurso, el dado tiene
            // candado y no arma combo. Lento (CapPreventHolding) NO gatea acá: seleccionar
            // es armar la mano, y Lento tiene que poder jugarse — su "no se guarda" vive en
            // CombatHandoffService.ApplyKeepConstraints (Fix#0053).
            if (DiceSelectionLocks.IsPlayerSlotLocked(i, out _))
                return false;

            // Un dado que todavía gira no se holdea — el botón ya está deshabilitado
            // durante el spin, esto cubre invocaciones programáticas (hotkeys, tests).
            if (_animator != null && _animator.IsSlotSpinning(i)) return false;

            return true;
        }

        private void ApplyHold(int i, bool held)
        {
            _heldStates[i] = held;
            _resolvedSlots[i]?.SetHeld(held);
            _animator?.SetRaised(i, held);
            PropagateHoldsToActionRoll();
            RunComboDetection();
        }

        /// <summary>
        /// Suelta todos los holds sin apagar los slots (a diferencia de
        /// <c>ClearAll</c>). BUG-030: el forced reroll del Torpe re-tira la mano
        /// completa — los holds que el jugador marcó en la ventana previa quedarían
        /// fuera de sync con el reveal nuevo.
        /// </summary>
        public void ClearHolds()
        {
            if (_heldStates == null) return;
            for (int i = 0; i < _heldStates.Length; i++)
            {
                if (!_heldStates[i]) continue;
                _heldStates[i] = false;
                if (_resolvedSlots != null && i < _resolvedSlots.Length)
                    _resolvedSlots[i]?.SetHeld(false);
                _animator?.SetRaised(i, false);
            }
            PropagateHoldsToActionRoll();
            RunComboDetection();
        }

        // Si hay un ActionRollService activo (Heal / Forzar Puerta), propagamos los
        // holds. El service usa esos holds para computar el effective total contra el
        // threshold — sin esta llamada, el service mantiene un _currentHolds vacío y
        // el outcome falla aunque el user vea el combo en pantalla.
        private void PropagateHoldsToActionRoll()
        {
            if (_heldStates == null) return;
            if (ServiceLocator.TryGetService<IActionRollService>(out var rs)
                && rs != null && rs.IsActive)
            {
                rs.SetHolds(_heldStates);
            }
        }

        // ---- Combo detection -------------------------------------------------

        private void RunComboDetection()
        {
            if (_currentFaces == null) return;

            // Boss 1 (§2) y candados de encantamiento (Sediento/Vampiro): la preview de combo
            // excluye los dados bloqueados, igual que la resolución real en CombatHandoffService.
            var comboKeep = _heldStates != null
                ? CombatHandoffService.KeepExcludingBlockedDice(_heldStates, _heldStates.Length)
                : null;

            var keptDice = CombatHandoffService.FilterKeptDice(_currentFaces, comboKeep);
            var keptOriginalIndices = CombatHandoffService.FilterKeptIndices(comboKeep, _currentFaces.Length);

            // Fuerza Bruta matchea por rango del dado — los tipos entran a la detección
            // misma, no solo al multi de daño de después.
            System.Collections.Generic.IReadOnlyList<Rollgeon.Dice.DiceType> keptTypes = null;
            bool hasBag = ServiceLocator.TryGetService<IDiceEnchantmentService>(out var enchants)
                          && enchants?.Bag != null;
            if (hasBag)
                keptTypes = ContributingDiceResolver.ResolveKept(
                    keptOriginalIndices, enchants.Bag.Dice, keptDice.Length);

            // Preferimos el ContractSheet del hero (respeta priorities y el set
            // específico de combos que ese hero puede usar). Fallback: catálogo
            // global si el hero/contract no está disponible.
            var sheet = ResolvePlayerContractSheet();
            BaseComboSO best = sheet != null
                ? sheet.MatchBest(keptDice, keptTypes)
                : MatchBestFromCatalog(keptDice, keptTypes);

            // Capa 1: Detect con el override plano de la tabla por clase (Spec Daño v2).
            // Desde Fix#0047 el BaseDamage del resultado es SOLO el piso plano — la parte
            // variable de los combos dinámicos entra al preview vía ContributingDice (Σcaras),
            // igual que el golpe real (DetectWithContractMods). Capa 2 (Boss 3 §4):
            // modificadores del Contrato.
            var detection = best != null
                ? best.Detect(keptDice, keptTypes, sheet?.GetBaseDamageOverride(best.ComboId))
                : ComboDetectionResult.NoMatch();
            int baseDmg = detection.IsMatch ? detection.BaseDamage : 0;
            if (best != null
                && ServiceLocator.TryGetService<Rollgeon.Combat.ContractMod.IContractModifierService>(out var cmods)
                && cmods != null)
                baseDmg = cmods.GetEffectiveBaseDamage(best.ComboId, baseDmg);

            // Hoisteado fuera del if para poder pasarlo al payload (el HUD lo usa para
            // recomputar el daño y el escudo reales vía la fórmula compartida).
            System.Collections.Generic.IReadOnlyList<Rollgeon.Combat.Damage.ContributingDie> contributingDice = null;
            if (best != null)
            {
                var comboResult = detection;
                if (comboResult.IsMatch && hasBag)
                {
                    contributingDice = ContributingDiceResolver.ResolveDetailed(
                        comboResult.ContributingIndices, keptOriginalIndices, keptDice, enchants.Bag.Dice);
                }
            }

            TypedEvent<ComboMatchedPayload>.Raise(new ComboMatchedPayload
            {
                SourceGuid = _playerGuid,
                ComboId = best?.ComboId ?? string.Empty,
                DisplayName = best != null ? Rollgeon.Localization.LocalizedContent.Name(best.ComboId, best.DisplayName) : string.Empty,
                BaseDamage = baseDmg,
                DynamicBonus = detection.IsMatch ? detection.DynamicBonus : 0,
                ContributingDice = contributingDice,
            });

            // DESPUÉS del Raise: los services de encantos ya poblaron LastComboScratch
            // (y su journal) procesando este mismo payload.
            UpdateContributionLabels(contributingDice, hasBag ? enchants : null);
        }

        /// <summary>
        /// Pinta el "+N" bajo cada dado contribuyente: cara + bonos aditivos de los
        /// encantamientos de ESE dado (journal at-match). Los no contribuyentes lo ocultan.
        /// </summary>
        private void UpdateContributionLabels(
            System.Collections.Generic.IReadOnlyList<Rollgeon.Combat.Damage.ContributingDie> contributingDice,
            IDiceEnchantmentService enchants)
        {
            if (_resolvedSlots == null) return;

            var journal = enchants?.LastComboScratch?.Journal;
            int visibleIndex = 0;
            for (int slot = 0; slot < _resolvedSlots.Length; slot++)
            {
                int? amount = null;
                int bonus = 0;
                if (contributingDice != null)
                {
                    for (int i = 0; i < contributingDice.Count; i++)
                    {
                        if (contributingDice[i].BagSlot != slot) continue;
                        int total = contributingDice[i].Face;
                        // Cara mutada (Oxidado/Volátil) + bonos del dado: el label muestra lo
                        // que ESTE dado va a poner en N, nunca bajo 0 (mismo clamp que la fórmula).
                        int faceDelta = enchants?.LastComboScratch?.GetFaceDelta(slot) ?? 0;
                        if (journal != null)
                        {
                            for (int j = 0; j < journal.Count; j++)
                                if (journal[j].BagSlot == slot) bonus += journal[j].BonusDelta;
                        }
                        int effectiveFace = faceDelta == 0 ? total : Math.Max(0, total + faceDelta);
                        amount = effectiveFace + bonus;
                        bonus += effectiveFace - total;
                        break;
                    }
                }
                // Stagger de aparición: los "+N" caen en cascada, no todos juntos.
                float delay = amount.HasValue ? visibleIndex++ * _contributionStaggerSeconds : 0f;
                _resolvedSlots[slot]?.SetContribution(amount, bonus, delay);
            }
        }

        private static ContractSheet ResolvePlayerContractSheet()
        {
            return ServiceLocator.TryGetService<IPlayerService>(out var ps) && ps?.CurrentHero != null
                ? ps.CurrentHero.Sheet
                : null;
        }

        private static BaseComboSO MatchBestFromCatalog(int[] keptDice,
            System.Collections.Generic.IReadOnlyList<Rollgeon.Dice.DiceType> keptTypes)
        {
            if (!ServiceLocator.TryGetService<ComboCatalogSO>(out var catalog) || catalog == null) return null;

            BaseComboSO best = null;
            int bestPriority = -1;
            foreach (var combo in catalog.Entries)
            {
                var result = combo.Detect(keptDice, keptTypes, null);
                if (result.IsMatch && combo.Priority > bestPriority)
                {
                    best = combo;
                    bestPriority = combo.Priority;
                }
            }
            return best;
        }
    }
}
