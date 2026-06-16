using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.EnergyLib;
using Rollgeon.Combos;
using Rollgeon.Dice;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.ActionRolls
{
    /// <summary>
    /// Implementacion default de <see cref="IActionRollService"/>. Pura C# (no MonoBehaviour);
    /// la UI se cablea via eventos C# (<see cref="OnPhaseChanged"/>) o via el bus
    /// legacy (<see cref="EventName.OnDiceRolled"/> para el visual de los 5 dados).
    /// </summary>
    /// <remarks>
    /// <b>Charge timing.</b> La energia base se cobra al entrar en
    /// <see cref="ActionRollPhase.Rolling"/> — despues del confirm dialog si lo hubo,
    /// antes de la primera tirada. Si el cobro falla, la fase pasa a
    /// <see cref="ActionRollPhase.Cancelled"/> y el outcome reporta Cancelled.
    /// El reroll se cobra en <see cref="RequestReroll"/>.
    /// </remarks>
    public sealed class ActionRollService : IActionRollService, IDisposable
    {
        private readonly IDiceRoller _roller;
        private readonly IEnergyService _energy;
        private readonly ComboCatalogSO _comboCatalog;

        private ActionRollSpec _spec;
        private Guid _playerGuid;
        private DiceBagSO _bag;
        private Action<ActionRollOutcome> _onCompleted;

        private int[] _currentRoll;
        private int _rollIndex;
        private ActionRollPhase _phase = ActionRollPhase.Inactive;

        // Cacheo del combo de la tirada actual — UI consume via CurrentCombo / CurrentEffectiveTotal
        // sin re-detectar.
        private BaseComboSO _currentCombo;
        private int _currentEffectiveTotal;

        // Mascara de holds — el user la actualiza via SetHolds() cuando clickea
        // dados en el panel. El combo y el effective total se calculan sobre el
        // SUBSET de dados con _currentHolds[i] == true (semantica del combate).
        private bool[] _currentHolds;

        public ActionRollService(IDiceRoller roller, IEnergyService energy,
            ComboCatalogSO comboCatalog = null)
        {
            _roller = roller ?? throw new ArgumentNullException(nameof(roller));
            _energy = energy ?? throw new ArgumentNullException(nameof(energy));
            _comboCatalog = comboCatalog;
        }

        public bool IsActive => _phase != ActionRollPhase.Inactive
                                 && _phase != ActionRollPhase.Resolved
                                 && _phase != ActionRollPhase.Cancelled;

        public ActionRollPhase Phase => _phase;

        public ActionRollSpec CurrentSpec => _spec;

        public Guid CurrentPlayerGuid => _playerGuid;

        public int[] CurrentRoll => _currentRoll;

        public int RollIndex => _rollIndex;

        public int CurrentSum => SumOf(_currentRoll);

        public int CurrentEffectiveTotal => _currentEffectiveTotal;

        public BaseComboSO CurrentCombo => _currentCombo;

        public IReadOnlyList<bool> CurrentHolds => _currentHolds;

        public void SetHolds(IReadOnlyList<bool> holds)
        {
            if (_currentRoll == null || _currentRoll.Length == 0) return;

            // Resize defensivo — cualquier mismatch entre holds.Count y la tirada
            // indica wiring roto, pero alineamos para no crashear.
            if (_currentHolds == null || _currentHolds.Length != _currentRoll.Length)
                _currentHolds = new bool[_currentRoll.Length];

            int n = Mathf.Min(holds != null ? holds.Count : 0, _currentHolds.Length);
            for (int i = 0; i < n; i++) _currentHolds[i] = holds[i];
            for (int i = n; i < _currentHolds.Length; i++) _currentHolds[i] = false;

            RecomputeComboAndTotal();
        }

        public event Action<ActionRollPhase> OnPhaseChanged;

        public void Dispose()
        {
            OnPhaseChanged = null;
            _onCompleted = null;
            _currentRoll = null;
            _bag = null;
            _phase = ActionRollPhase.Inactive;
        }

        public void StartFlow(ActionRollSpec spec, Guid playerGuid, DiceBagSO bag,
            Action<ActionRollOutcome> onCompleted)
        {
            Debug.LogWarning($"[ActionRollService] StartFlow → label='{spec.ActionLabel}' " +
                             $"threshold={spec.Threshold} cost={spec.EnergyCost} " +
                             $"requireConfirm={spec.RequireConfirm} allowReroll={spec.AllowReroll} " +
                             $"comboCatalog={(_comboCatalog != null ? "OK" : "NULL")}");
            if (IsActive)
            {
                Debug.LogWarning("[ActionRollService] StartFlow llamado mientras IsActive=true. " +
                                 "Cancelando flow previo.");
                CompleteCancelled();
            }

            if (bag == null || bag.Dice == null || bag.Dice.Count == 0)
            {
                Debug.LogError("[ActionRollService] StartFlow: DiceBag null o vacio. Abortando.");
                onCompleted?.Invoke(new ActionRollOutcome { Cancelled = true });
                return;
            }

            _spec = spec;
            _playerGuid = playerGuid;
            _bag = bag;
            _onCompleted = onCompleted;
            _currentRoll = null;
            _rollIndex = 0;

            if (spec.RequireConfirm)
            {
                SetPhase(ActionRollPhase.AwaitingConfirm);
            }
            else
            {
                BeginInitialRoll();
            }
        }

        public void Confirm()
        {
            if (_phase == ActionRollPhase.AwaitingConfirm)
            {
                BeginInitialRoll();
                return;
            }

            if (_phase == ActionRollPhase.AwaitingRerollDecision)
            {
                // En AwaitingRerollDecision, Confirm resuelve con la tirada actual
                // (equivalente a "el user clickeo Confirm en vez de Reroll").
                ResolveWithCurrentRoll();
                return;
            }

            Debug.LogWarning($"[ActionRollService] Confirm() ignored — phase={_phase}.");
        }

        public void Cancel()
        {
            if (_phase != ActionRollPhase.AwaitingConfirm
                && _phase != ActionRollPhase.AwaitingRerollDecision)
            {
                Debug.LogWarning($"[ActionRollService] Cancel() ignored — phase={_phase}.");
                return;
            }

            // Si cancelan despues del primer roll, ya gastaron la energia base + tienen
            // un resultado. La accion se considera fallida pero NO Cancelled — el outcome
            // refleja el roll real. Cancel-from-AwaitingConfirm si es un cancel limpio.
            if (_phase == ActionRollPhase.AwaitingRerollDecision)
            {
                ResolveWithCurrentRoll();
                return;
            }

            CompleteCancelled();
        }

        public void RequestReroll()
        {
            // Sin keep mask explícito → usar los holds internos (seteados por SetHolds
            // desde DiceZoneView). Los dados marcados como "held" se conservan en el
            // reroll; los demás se re-tiran. Semántica coherente con el botón Reroll
            // compartido entre combat y action rolls.
            RequestReroll((IReadOnlyList<bool>)_currentHolds);
        }

        public void RequestReroll(IReadOnlyList<bool> keep)
        {
            if (_phase != ActionRollPhase.AwaitingRerollDecision)
            {
                Debug.LogWarning($"[ActionRollService] RequestReroll() ignored — phase={_phase}.");
                return;
            }

            // Boss 1 (§2): los dados bloqueados nunca se re-rollean — forzamos keep=true en ellos.
            keep = ForceKeepBlocked(keep, _currentRoll?.Length ?? 0);

            // BUG-014: si el user holdeó todos los dados, el reroll no re-tiraría
            // ningún dado — cobrar energía sería un drain sin efecto. Bail sin
            // mutar phase ni cobrar; el panel debería haber deshabilitado el
            // botón antes vía CanAffordReroll, esto es solo el guard defensivo.
            if (keep != null && AllTrue(keep))
            {
                Debug.LogWarning("[ActionRollService] RequestReroll bloqueado — todos los dados están holdeados.");
                return;
            }

            // Multi-shot: la spec permite rerollear mientras haya energía. El único
            // gate es SpendEnergy: si falla, resolvemos con la tirada actual (el
            // panel debería haber deshabilitado el botón antes vía CanAffordReroll).
            int cost = Mathf.Max(0, _spec.RerollEnergyCost);
            if (cost > 0 && !_energy.SpendEnergy(_playerGuid, cost))
            {
                Debug.Log("[ActionRollService] Reroll bloqueado — sin energia. Resolviendo con tirada actual.");
                ResolveWithCurrentRoll();
                return;
            }

            EventManager.Trigger(EventName.OnRerollStarted, _playerGuid, _rollIndex);

            // Si el caller paso un keep mask, respetamos los holds — solo tiramos los
            // dados con keep[i]=false. Sino, fallback a re-tirar todo (legacy path).
            int[] faces = (keep != null)
                ? _roller.Reroll(_bag, _currentRoll, ToBoolArray(keep))
                : _roller.RollAll(_bag);

            _currentRoll = faces;
            _rollIndex++;

            RecomputeComboAndTotal();
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid, (IReadOnlyList<int>)faces);

            // Despues del reroll, NO resolvemos directo — devolvemos al user a
            // AwaitingRerollDecision para que vea los dados nuevos y decida si
            // confirma. El reroll button debe deshabilitarse en panel (rollIndex=2).
            SetPhase(ActionRollPhase.AwaitingRerollDecision);
        }

        // Boss 1 (§2): devuelve un keep con los dados bloqueados forzados a true (no se re-rollean).
        // Si no hay servicio o no hay dados bloqueados, devuelve el keep original sin materializar.
        private static IReadOnlyList<bool> ForceKeepBlocked(IReadOnlyList<bool> keep, int len)
        {
            if (!ServiceLocator.TryGetService<Rollgeon.Combat.DiceBlock.IDiceBlockService>(out var db)
                || db == null || db.BlockedIndices.Count == 0)
                return keep;

            int n = len > 0 ? len : (keep?.Count ?? 0);
            var arr = new bool[n];
            if (keep != null)
                for (int i = 0; i < n && i < keep.Count; i++) arr[i] = keep[i];
            foreach (var idx in db.BlockedIndices)
                if (idx >= 0 && idx < n) arr[idx] = true;
            return arr;
        }

        private static bool[] ToBoolArray(IReadOnlyList<bool> source)
        {
            var arr = new bool[source.Count];
            for (int i = 0; i < source.Count; i++) arr[i] = source[i];
            return arr;
        }

        private static bool AllTrue(IReadOnlyList<bool> mask)
        {
            if (mask == null || mask.Count == 0) return false;
            for (int i = 0; i < mask.Count; i++) if (!mask[i]) return false;
            return true;
        }

        public void DeclineReroll()
        {
            if (_phase != ActionRollPhase.AwaitingRerollDecision)
            {
                Debug.LogWarning($"[ActionRollService] DeclineReroll() ignored — phase={_phase}.");
                return;
            }
            ResolveWithCurrentRoll();
        }

        // ---- internos --------------------------------------------------------

        private void BeginInitialRoll()
        {
            int cost = Mathf.Max(0, _spec.EnergyCost);
            if (cost > 0 && !_energy.SpendEnergy(_playerGuid, cost))
            {
                Debug.Log("[ActionRollService] Energia insuficiente al confirmar — cancelando.");
                CompleteCancelled();
                return;
            }

            SetPhase(ActionRollPhase.Rolling);

            EventManager.Trigger(EventName.OnRollStarted, _playerGuid);

            var faces = _roller.RollAll(_bag);
            _currentRoll = faces;
            _rollIndex = 1;
            // Holds parten vacios — el user los marca despues clickeando dados.
            _currentHolds = new bool[faces.Length];

            RecomputeComboAndTotal();
            EventManager.Trigger(EventName.OnDiceRolled, _playerGuid, (IReadOnlyList<int>)faces);

            string comboTag = _currentCombo != null ? _currentCombo.DisplayName : "(no combo)";
            Debug.LogWarning($"[ActionRollService] Roll → dice=[{string.Join(",", faces)}] combo={comboTag} " +
                             $"effective={_currentEffectiveTotal} threshold={_spec.Threshold} " +
                             $"allowReroll={_spec.AllowReroll} energy={_energy.GetCurrent(_playerGuid)} " +
                             $"rerollCost={_spec.RerollEnergyCost}");

            // Despues del initial roll, SIEMPRE pasamos a AwaitingRerollDecision para
            // que el user vea los dados, decida si holdea/rerollea, y confirme cuando
            // este conforme. Este reemplaza el auto-resolver anterior — el user pidio
            // expresamente el flujo manual igual que combat (holdear, reroll, confirm).
            SetPhase(ActionRollPhase.AwaitingRerollDecision);
        }

        // Detecta el mejor combo + effective total considerando SOLO los dados holdeados.
        // Usa el ContractSheet del HEROE — los combos del catalogo global que no estan
        // en el contrato del jugador no cuentan (spec: "armar un combo de su contrato de
        // generala"). Sin sheet registrado, fallback al catalog global por defensa.
        private void RecomputeComboAndTotal()
        {
            if (_currentRoll == null || _currentRoll.Length == 0)
            {
                _currentCombo = null;
                _currentEffectiveTotal = 0;
                return;
            }

            // Boss 1 (§2): los dados bloqueados quedan excluidos del combo aunque estén holdeados.
            ServiceLocator.TryGetService<Rollgeon.Combat.DiceBlock.IDiceBlockService>(out var diceBlock);

            var heldDice = new List<int>(_currentRoll.Length);
            if (_currentHolds != null)
            {
                int n = Mathf.Min(_currentHolds.Length, _currentRoll.Length);
                for (int i = 0; i < n; i++)
                {
                    if (diceBlock != null && diceBlock.IsBlocked(i)) continue;
                    if (_currentHolds[i]) heldDice.Add(_currentRoll[i]);
                }
            }

            int heldSum = 0;
            for (int i = 0; i < heldDice.Count; i++) heldSum += heldDice[i];

            if (heldDice.Count == 0)
            {
                _currentCombo = null;
                _currentEffectiveTotal = 0;
                EmitComboMatched();
                return;
            }

            // 1) Primero ContractSheet del player — es la fuente de verdad para "combos del contrato".
            BaseComboSO fromSheet = null;
            if (ServiceLocator.TryGetService<IPlayerService>(out var ps)
                && ps?.CurrentHero?.Sheet != null)
            {
                fromSheet = ps.CurrentHero.Sheet.MatchBest(heldDice);
            }

            if (fromSheet != null)
            {
                _currentCombo = fromSheet;
                _currentEffectiveTotal = EffectiveBase(fromSheet);
                EmitComboMatched();
                return;
            }

            // 2) Fallback al catalog global (solo defensa — si el sheet no esta disponible).
            //    NO se usa para decidir éxito del action — los effects evalúan via combo no-null.
            if (_comboCatalog != null)
            {
                var result = ComboResolver.DetectBest(_comboCatalog, heldDice, out var best);
                if (result.IsMatch)
                {
                    _currentCombo = best;
                    _currentEffectiveTotal = EffectiveBase(best);
                    EmitComboMatched();
                    return;
                }
            }

            // 3) Sin combo del contrato — effective = suma cruda (fail path para Force Door,
            //    base-only para Heal). El user va a ver "(no combo)" en el panel.
            _currentCombo = null;
            _currentEffectiveTotal = heldSum;
            EmitComboMatched();
        }

        // Publica el combo actual en el bus tipado para que el DamageFormulaView (y
        // cualquier otra view) refleje en tiempo real lo que el user está armando.
        // Si no hay combo, emitir con BaseDamage=0 limpia la UI.
        private void EmitComboMatched()
        {
            TypedEvent<ComboMatchedPayload>.Raise(new ComboMatchedPayload
            {
                SourceGuid = _playerGuid,
                ComboId = _currentCombo != null ? _currentCombo.ComboId : string.Empty,
                DisplayName = _currentCombo != null ? _currentCombo.DisplayName : string.Empty,
                BaseDamage = EffectiveBase(_currentCombo),
            });
        }

        // Boss 3 (§4): daño base del combo tras la capa de modificadores del Contrato. Sin
        // servicio/modificadores ⇒ el base original.
        private static int EffectiveBase(BaseComboSO combo)
        {
            if (combo == null) return 0;
            int b = combo.BaseDamage;
            if (ServiceLocator.TryGetService<Rollgeon.Combat.ContractMod.IContractModifierService>(out var mods) && mods != null)
                b = mods.GetEffectiveBaseDamage(combo.ComboId, b);
            return b;
        }

        public bool CanAffordReroll
        {
            get
            {
                if (_phase != ActionRollPhase.AwaitingRerollDecision) return false;
                // BUG-014: si todos los dados están holdeados, no hay nada para
                // re-tirar — el botón debe quedar deshabilitado aunque sobre energía.
                if (_currentHolds != null && _currentHolds.Length > 0 && AllTrue(_currentHolds))
                    return false;
                int cost = Mathf.Max(0, _spec.RerollEnergyCost);
                if (cost <= 0) return true;
                return _energy.GetCurrent(_playerGuid) >= cost;
            }
        }

        public bool CanConfirm
        {
            get
            {
                if (_phase != ActionRollPhase.AwaitingRerollDecision) return false;
                if (_currentHolds == null) return false;
                for (int i = 0; i < _currentHolds.Length; i++)
                    if (_currentHolds[i]) return true;
                return false;
            }
        }

        private void ResolveWithCurrentRoll()
        {
            int rawSum = SumOf(_currentRoll);
            var outcome = new ActionRollOutcome
            {
                Cancelled = false,
                FinalRoll = _currentRoll,
                FinalSum = rawSum,
                EffectiveTotal = _currentEffectiveTotal,
                PassedThreshold = _currentEffectiveTotal >= _spec.Threshold,
                RollsUsed = _rollIndex,
                ComboId = _currentCombo != null ? _currentCombo.ComboId : string.Empty,
                ComboDisplayName = _currentCombo != null ? _currentCombo.DisplayName : string.Empty,
                HasCombo = _currentCombo != null,
            };

            EventManager.Trigger(EventName.OnRollResolved, _playerGuid,
                (IReadOnlyList<int>)(_currentRoll ?? Array.Empty<int>()));

            SetPhase(ActionRollPhase.Resolved);

            var cb = _onCompleted;
            ResetState();
            cb?.Invoke(outcome);
        }

        private void CompleteCancelled()
        {
            var outcome = new ActionRollOutcome { Cancelled = true };
            SetPhase(ActionRollPhase.Cancelled);

            var cb = _onCompleted;
            ResetState();
            cb?.Invoke(outcome);
        }

        private void ResetState()
        {
            _onCompleted = null;
            _bag = null;
            _currentRoll = null;
            _rollIndex = 0;
            _playerGuid = Guid.Empty;
            _spec = default;
            _currentCombo = null;
            _currentEffectiveTotal = 0;
            _currentHolds = null;
            // Phase queda en Resolved/Cancelled hasta el proximo StartFlow para que la UI
            // pueda leer el outcome final antes de que reseteemos a Inactive.
        }

        private void SetPhase(ActionRollPhase next)
        {
            if (_phase == next) return;
            _phase = next;
            OnPhaseChanged?.Invoke(next);
        }

        private static int SumOf(int[] arr)
        {
            if (arr == null) return 0;
            int s = 0;
            for (int i = 0; i < arr.Length; i++) s += arr[i];
            return s;
        }
    }
}
