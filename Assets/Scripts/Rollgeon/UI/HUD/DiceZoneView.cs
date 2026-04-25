using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Handoff;
using Rollgeon.Combos;
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

        // ---- Bind / Unbind ---------------------------------------------------

        public void Bind(Guid playerGuid)
        {
            _playerGuid = playerGuid;
            int count = _diceSlots.Count;
            _resolvedSlots = new DiceSlotView[count];
            _currentFaces = new int[count];
            _heldStates = new bool[count];

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

            EventManager.Subscribe(EventName.OnDiceRolled, HandleDiceRolled);
            EventManager.Subscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.Subscribe(EventName.OnRollResolved, HandleRollResolved);

            // Estado inicial: slots apagados hasta que el jugador presione Roll.
            ClearAll();
        }

        public void Unbind()
        {
            EventManager.UnSubscribe(EventName.OnDiceRolled, HandleDiceRolled);
            EventManager.UnSubscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.UnSubscribe(EventName.OnRollResolved, HandleRollResolved);
            if (_resolvedSlots != null)
                foreach (var s in _resolvedSlots)
                    s?.OnToggled.RemoveAllListeners();
            _resolvedSlots = null;
            _currentFaces = null;
            _heldStates = null;
        }

        // ---- Event handler ---------------------------------------------------

        private void HandleDiceRolled(params object[] args)
        {
            if (args == null || args.Length < 2) return;
            if (args[0] is not Guid guid || guid != _playerGuid) return;
            var faces = (IReadOnlyList<int>)args[1];

            for (int i = 0; i < _resolvedSlots?.Length; i++)
            {
                if (_heldStates != null && i < _heldStates.Length && _heldStates[i]) continue;
                _currentFaces[i] = i < faces.Count ? faces[i] : 0;
                _resolvedSlots[i]?.ShowFace(_currentFaces[i]);
                _resolvedSlots[i]?.SetHeld(false);
            }
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
            ClearAll();
        }

        // ---- Clear / reset --------------------------------------------------

        /// <summary>
        /// Apaga todos los slots y resetea holds/faces. Pública para que el
        /// <c>CombatHandoffService</c> u otros pueden forzar el clear ante eventos
        /// no estándar (cancel, retreat, etc.).
        /// </summary>
        public void ClearAll()
        {
            if (_currentFaces != null)
                Array.Clear(_currentFaces, 0, _currentFaces.Length);
            if (_heldStates != null)
                Array.Clear(_heldStates, 0, _heldStates.Length);
            if (_resolvedSlots != null)
            {
                foreach (var s in _resolvedSlots)
                    s?.Clear();
            }
            RunComboDetection();
        }

        // ---- Hold toggle -----------------------------------------------------

        private void ToggleHold(int i)
        {
            if (_heldStates == null || i >= _heldStates.Length)
            {
                Debug.Log($"[DiceZoneView] ToggleHold({i}) — aborted: _heldStates null={_heldStates == null} len={_heldStates?.Length}");
                return;
            }
            _heldStates[i] = !_heldStates[i];
            _resolvedSlots[i]?.SetHeld(_heldStates[i]);
            Debug.Log($"[DiceZoneView] ToggleHold({i}) — held={_heldStates[i]}, calling RunComboDetection");
            RunComboDetection();
        }

        // ---- Combo detection -------------------------------------------------

        private void RunComboDetection()
        {
            if (_currentFaces == null)
            {
                Debug.Log("[DiceZoneView] RunComboDetection — aborted: _currentFaces is null");
                return;
            }
            if (!ServiceLocator.TryGetService<ComboCatalogSO>(out var catalog) || catalog == null)
            {
                Debug.Log("[DiceZoneView] RunComboDetection — aborted: ComboCatalogSO not in ServiceLocator");
                return;
            }

            var keptDice = CombatHandoffService.FilterKeptDice(_currentFaces, _heldStates);
            Debug.Log($"[DiceZoneView] RunComboDetection — keptDice.Length={keptDice.Length} values=[{string.Join(",", keptDice)}] catalogEntries={catalog.Entries?.Count}");

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

            Debug.Log($"[DiceZoneView] RunComboDetection — best={best?.ComboId ?? "null"} displayName={best?.DisplayName ?? "null"} baseDmg={best?.BaseDamage ?? 0}");

            TypedEvent<ComboMatchedPayload>.Raise(new ComboMatchedPayload
            {
                SourceGuid = _playerGuid,
                ComboId = best?.ComboId ?? string.Empty,
                DisplayName = best?.DisplayName ?? string.Empty,
                BaseDamage = best?.BaseDamage ?? 0
            });
        }
    }
}
