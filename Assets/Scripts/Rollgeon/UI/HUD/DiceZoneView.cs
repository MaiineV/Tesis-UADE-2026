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

        // ---- Runtime state ---------------------------------------------------

        private Guid _playerGuid;
        private DiceSlotView[] _resolvedSlots;
        private int[] _currentFaces;
        private bool[] _heldStates;
        private DiceZoneAnimator _animator;

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
        /// Snapshot del estado de hold por slot. Devuelve una copia defensiva — el
        /// caller puede mutarla. Si <c>Bind</c> aún no corrió, devuelve un array vacío.
        /// Consumido por el <see cref="Rollgeon.Combat.Handoff.CombatHandoffService"/>
        /// para pasar el <c>keep[]</c> al <c>IDiceRoller.Reroll</c>.
        /// </summary>
        public bool[] GetHeldStates()
        {
            if (_heldStates == null) return Array.Empty<bool>();
            var copy = new bool[_heldStates.Length];
            Array.Copy(_heldStates, copy, _heldStates.Length);
            return copy;
        }

        /// <summary>
        /// BUG-014: True si <see cref="Bind"/> ya corrió, hay slots activos visibles
        /// (al menos un dado rolleado) y todos están holdeados. Permite a la UI del
        /// reroll button deshabilitarse cuando no hay dados para re-tirar.
        /// </summary>
        public bool AreAllDiceHeld()
        {
            if (_heldStates == null || _heldStates.Length == 0) return false;
            // Si _resolvedSlots aún no se activó (sin roll todavía), no aplica.
            bool anySlotActive = false;
            if (_resolvedSlots != null)
            {
                for (int i = 0; i < _resolvedSlots.Length; i++)
                {
                    if (_resolvedSlots[i] != null && _resolvedSlots[i].gameObject.activeSelf)
                    {
                        anySlotActive = true;
                        break;
                    }
                }
            }
            if (!anySlotActive) return false;
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
            EventManager.Subscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.Subscribe(EventName.OnRollResolved, HandleRollResolved);
            EventManager.Subscribe(EventName.OnDiceBlockChanged, HandleDiceBlockChanged);

            // Estado inicial: slots apagados hasta que el jugador presione Roll.
            ClearAll();
        }

        public void Unbind()
        {
            if (!_bound) return;
            if (_animator != null)
            {
                _animator.AnimationStateChanged -= RaiseDiceAnimationStateChanged;
                _animator.Unbind();
            }
            EventManager.UnSubscribe(EventName.OnDiceRolled, HandleDiceRolled);
            EventManager.UnSubscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.UnSubscribe(EventName.OnRollResolved, HandleRollResolved);
            EventManager.UnSubscribe(EventName.OnDiceBlockChanged, HandleDiceBlockChanged);
            if (_resolvedSlots != null)
                foreach (var s in _resolvedSlots)
                    s?.OnToggled.RemoveAllListeners();
            _resolvedSlots = null;
            _currentFaces = null;
            _heldStates = null;
            _bound = false;
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

            int count = _resolvedSlots?.Length ?? 0;
            var willReveal = new bool[count];
            for (int i = 0; i < count; i++)
            {
                if (_heldStates != null && i < _heldStates.Length && _heldStates[i]) continue;
                willReveal[i] = true;
                _currentFaces[i] = i < faces.Count ? faces[i] : 0;
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
                bool blocked = db != null && db.IsBlocked(i);
                if (blocked && _heldStates != null && i < _heldStates.Length)
                {
                    _heldStates[i] = false; // un dado bloqueado no puede quedar holdeado
                    // El unhold forzado no pasa por SetHeld/ToggleHold — bajar el
                    // raise explícitamente o el dado queda flotando bloqueado.
                    _animator?.SetRaised(i, false);
                }
                _resolvedSlots[i]?.SetBlocked(blocked);
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
            // Path animado (Classic): los holdeados vuelan al centro de la mesa y los
            // demás se descartan; el ClearAll corre diferido al terminar el outro.
            if (_animator != null && _animator.TryBeginOutro(_heldStates, BuildActiveMask(), ClearAll))
                return;
            ClearAll();
        }

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
            // Aborta cualquier animación en curso sin ejecutar callbacks diferidos
            // (si ClearAll llegó como completion del outro, esto ya es no-op).
            _animator?.ResetAll();
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
            if (_heldStates == null || i >= _heldStates.Length)
            {
                Debug.Log($"[DiceZoneView] ToggleHold({i}) — aborted: _heldStates null={_heldStates == null} len={_heldStates?.Length}");
                return;
            }
            // Boss 1 (§2): un dado bloqueado no puede holdearse.
            if (ServiceLocator.TryGetService<Rollgeon.Combat.DiceBlock.IDiceBlockService>(out var db)
                && db != null && db.IsBlocked(i))
                return;

            // Un dado que todavía gira no se holdea — el botón ya está deshabilitado
            // durante el spin, esto cubre invocaciones programáticas (hotkeys, tests).
            if (_animator != null && _animator.IsSlotSpinning(i)) return;

            _heldStates[i] = !_heldStates[i];
            _resolvedSlots[i]?.SetHeld(_heldStates[i]);
            _animator?.SetRaised(i, _heldStates[i]);
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

            // Boss 1 (§2): la preview de combo excluye los dados bloqueados, igual que la
            // resolución real en CombatHandoffService.
            var comboKeep = _heldStates;
            if (ServiceLocator.TryGetService<Rollgeon.Combat.DiceBlock.IDiceBlockService>(out var db)
                && db != null && db.BlockedIndices.Count > 0 && _heldStates != null)
            {
                comboKeep = (bool[])_heldStates.Clone();
                for (int i = 0; i < comboKeep.Length; i++)
                    if (db.IsBlocked(i)) comboKeep[i] = false;
            }

            var keptDice = CombatHandoffService.FilterKeptDice(_currentFaces, comboKeep);

            // Preferimos el ContractSheet del hero (respeta priorities y el set
            // específico de combos que ese hero puede usar). Fallback: catálogo
            // global si el hero/contract no está disponible.
            var sheet = ResolvePlayerContractSheet();
            BaseComboSO best = sheet != null
                ? sheet.MatchBest(keptDice)
                : MatchBestFromCatalog(keptDice);

            // Capa 1: base plano de la tabla por clase (Spec Daño v2). Capa 2 (Boss 3 §4):
            // la preview del daño refleja la capa de modificadores del Contrato.
            int baseDmg = best == null ? 0 : (sheet != null ? sheet.GetBaseDamage(best) : best.BaseDamage);
            if (best != null
                && ServiceLocator.TryGetService<Rollgeon.Combat.ContractMod.IContractModifierService>(out var cmods)
                && cmods != null)
                baseDmg = cmods.GetEffectiveBaseDamage(best.ComboId, baseDmg);

            float multiDmgCombo = 1f;
            int shieldPreview = 0;
            if (best != null)
            {
                var comboResult = best.Detect(keptDice);
                System.Collections.Generic.IReadOnlyList<Rollgeon.Dice.DiceType> contributingDice = null;
                if (comboResult.IsMatch
                    && ServiceLocator.TryGetService<IDiceEnchantmentService>(out var enchants)
                    && enchants?.Bag != null)
                {
                    var keptOriginalIndices = CombatHandoffService.FilterKeptIndices(comboKeep, _currentFaces.Length);
                    contributingDice = ContributingDiceResolver.Resolve(
                        comboResult.ContributingIndices, keptOriginalIndices, enchants.Bag.Dice);
                    multiDmgCombo = PlayerComboDamage.ComputeMultiDmgCombo(contributingDice);
                }

                // Preview del escudo (Spec Escudo v2) — mismos dados contribuyentes que
                // el multi; sin sheet no hay tabla y el preview queda en 0.
                if (comboResult.IsMatch && sheet != null)
                    shieldPreview = PlayerComboShield.Resolve(
                        sheet.GetShieldBase(best.ComboId), contributingDice);
            }

            TypedEvent<ComboMatchedPayload>.Raise(new ComboMatchedPayload
            {
                SourceGuid = _playerGuid,
                ComboId = best?.ComboId ?? string.Empty,
                DisplayName = best?.DisplayName ?? string.Empty,
                BaseDamage = baseDmg,
                MultiDmgCombo = multiDmgCombo,
                ShieldPreview = shieldPreview,
            });
        }

        private static ContractSheet ResolvePlayerContractSheet()
        {
            return ServiceLocator.TryGetService<IPlayerService>(out var ps) && ps?.CurrentHero != null
                ? ps.CurrentHero.Sheet
                : null;
        }

        private static BaseComboSO MatchBestFromCatalog(int[] keptDice)
        {
            if (!ServiceLocator.TryGetService<ComboCatalogSO>(out var catalog) || catalog == null) return null;

            BaseComboSO best = null;
            int bestPriority = -1;
            foreach (var combo in catalog.Entries)
            {
                var result = combo.Detect(keptDice);
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
