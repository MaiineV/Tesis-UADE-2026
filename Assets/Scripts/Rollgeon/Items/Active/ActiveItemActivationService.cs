using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Rolls;
using Rollgeon.Effects;
using Rollgeon.Effects.Selection;
using Rollgeon.Grid;
using Rollgeon.Player;
using Rollgeon.PreConditions;
using UnityEngine;

namespace Rollgeon.Items.Active
{
    /// <inheritdoc cref="IActiveItemActivationService"/>
    public sealed class ActiveItemActivationService : IActiveItemActivationService
    {
        private const string LogPrefix = "[ActiveItemActivationService] ";

        private readonly IEquippedActiveItemService _equipped;
        private readonly IActiveItemDieRoller _roller;

        public ActiveItemActivationService(IEquippedActiveItemService equipped, IActiveItemDieRoller roller)
        {
            _equipped = equipped;
            _roller = roller ?? new ActiveItemDieRoller();
        }

        public event Action<ActiveItemActivationResult> OnResolved;
        public event Action OnSelectionStarted;
        public event Action OnSelectionCancelled;

        public bool IsSelecting { get; private set; }

        // ======================================================================
        // Paso 1: tocar la ficha (gratis) → seleccion o activacion directa
        // ======================================================================

        public bool BeginActivation()
        {
            // Re-tocar la ficha con una seleccion abierta cancela, como el resto de las
            // acciones del combate.
            if (IsSelecting)
            {
                CancelActivation();
                return false;
            }

            if (CanActivate() != ActiveItemBlock.None) return false;

            var settings = ResolveSelectionSettings(_equipped.Current);
            var playerGuid = ResolvePlayerGuid();

            // Sin seleccion (o self-target): el GDD dice que estos items "activan de
            // forma directa, sin paso de seleccion", y ahi el pago ocurre en el mismo
            // instante que la activacion. No hay ventana de cancelacion.
            if (settings == null || settings.SlotState == SlotState.Self)
            {
                Confirm(selection: null);
                return true;
            }

            // El item pide target y no hay grilla para resolverlo. Activar igual seria
            // cobrar el roll y aplicar el efecto sobre el jugador — peor que no hacer
            // nada. Se rechaza.
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid)
                || grid == null
                || !grid.TryGetPosition(playerGuid, out var ownerPos))
            {
                Debug.LogWarning(LogPrefix + "el item pide objetivo pero no hay grilla — activacion rechazada.");
                return false;
            }

            if (settings.AutoResolve)
            {
                Confirm(settings.AutoResolveTargets(ownerPos, playerGuid));
                return true;
            }

            var validTargets = settings.ResolveValidTiles(ownerPos, playerGuid);
            if (validTargets == null || validTargets.Count == 0) return false;

            if (!ServiceLocator.TryGetService<ISelectionController>(out var controller) || controller == null)
            {
                Debug.LogWarning(LogPrefix + "ISelectionController no registrado — no se puede pedir target.");
                return false;
            }

            IsSelecting = true;
            controller.OnSelectionCompleted += HandleSelectionCompleted;
            controller.BeginSelection(new SelectionRequest
            {
                Settings = settings,
                ValidTargets = validTargets,
                OwnerGuid = playerGuid,
                HighlightStyle = "attack",
            });

            OnSelectionStarted?.Invoke();
            return true;
        }

        public void CancelActivation()
        {
            if (!IsSelecting) return;

            DetachSelection();
            if (ServiceLocator.TryGetService<ISelectionController>(out var controller) && controller != null)
                controller.CancelSelection();

            OnSelectionCancelled?.Invoke();
        }

        /// <summary>
        /// Paso 2: llega el target elegido. Confirmar cobra; cancelar no gasta el item,
        /// que es lo que el GDD pide explicitamente.
        /// </summary>
        private void HandleSelectionCompleted(TargetSelectionResult result)
        {
            if (!IsSelecting) return;
            DetachSelection();

            if (result == null || !result.WasCompleted)
            {
                OnSelectionCancelled?.Invoke();
                return;
            }

            Confirm(result);
        }

        private void DetachSelection()
        {
            IsSelecting = false;
            if (ServiceLocator.TryGetService<ISelectionController>(out var controller) && controller != null)
                controller.OnSelectionCompleted -= HandleSelectionCompleted;
        }

        // ======================================================================
        // Gating (§6, §7)
        // ======================================================================

        public ActiveItemBlock CanActivate()
        {
            // "Completamente oculta: fuera de combate" es la regla mas externa del GDD:
            // va antes que el slot vacio, porque en exploracion la ficha no se muestra ni
            // siquiera para decir que no hay item. El pool de rolls solo existe en
            // combate, asi que su ausencia ES la señal — mismo criterio que TurnManager.
            if (!ServiceLocator.TryGetService<IRollPoolService>(out var rolls)
                || rolls == null
                || !rolls.IsCombatActive)
            {
                return ActiveItemBlock.NotInCombat;
            }

            if (_equipped == null || !_equipped.HasItem) return ActiveItemBlock.NoItemEquipped;

            var playerGuid = ResolvePlayerGuid();

            if (ServiceLocator.TryGetService<Rollgeon.Combat.Actions.TurnManager>(out var turns)
                && turns != null
                && !turns.IsActingTurn(playerGuid))
            {
                return ActiveItemBlock.NotYourTurn;
            }

            if (rolls.GetCurrent(playerGuid) < RollCost) return ActiveItemBlock.NotEnoughRolls;

            if (!HasAnyValidTarget(_equipped.Current, playerGuid)) return ActiveItemBlock.NoValidTarget;

            return ActiveItemBlock.None;
        }

        /// <summary>
        /// Costo fijo de toda activacion. El GDD lo deja explicito: "1 roll, fijo, igual
        /// para todos los ítems activos", "sin excepciones a nivel de sistema".
        /// </summary>
        public const int RollCost = 1;

        // ======================================================================
        // Confirmacion (§22): cobrar → tirar → banda → efecto
        // ======================================================================

        public ActiveItemActivationResult? Confirm(TargetSelectionResult selection)
        {
            var block = CanActivate();
            if (block != ActiveItemBlock.None) return null;

            var item = _equipped.Current;
            var playerGuid = ResolvePlayerGuid();

            ServiceLocator.TryGetService<IRollPoolService>(out var rolls);

            // Punto de no retorno. Si el cobro falla no se tira el dado: el GDD no admite
            // una tirada gratis, y tampoco un reembolso (no hay ventana para uno).
            if (rolls == null || !rolls.TrySpendRolls(playerGuid, RollCost))
            {
                Debug.LogWarning(LogPrefix + "no se pudo cobrar el roll — la activacion se aborta.");
                return null;
            }

            int roll = _roller.Roll(item.ActiveDie);
            var band = ActiveItemBands.Resolve(roll, item.ActiveDie);

            var ctx = BuildContext(playerGuid, selection);
            var effects = item.GetBandEffects(band);

            // El roll ya se cobro: que la cadena de efectos corte no lo devuelve. Lo que
            // se reporta es si corrio entera, para el feedback.
            bool ok = effects.TryExecute(ctx, BuildPreCtx(ctx));

            var result = new ActiveItemActivationResult(item, roll, band, ok);
            OnResolved?.Invoke(result);
            return result;
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        private static Guid ResolvePlayerGuid()
            => ServiceLocator.TryGetService<IPlayerService>(out var ps) && ps != null
                ? ps.PlayerGuid
                : Guid.Empty;

        /// <summary>
        /// PRE-04: si el item pide seleccion, tiene que existir al menos un target valido.
        /// Un item que activa directo (sin seleccion) no tiene nada que validar.
        /// </summary>
        private static bool HasAnyValidTarget(ItemSO item, Guid playerGuid)
        {
            var settings = ResolveSelectionSettings(item);
            if (settings == null || settings.SlotState == SlotState.Self) return true;

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid)
                || grid == null
                || !grid.TryGetPosition(playerGuid, out var ownerPos))
            {
                // Sin grilla no hay forma de decidir: no bloqueamos por algo que no
                // podemos evaluar.
                return true;
            }

            var valid = settings.ResolveValidTiles(ownerPos, playerGuid);
            return valid != null && valid.Count > 0;
        }

        /// <summary>
        /// Primer <c>SelectionSettings</c> que pida seleccion entre los efectos de las
        /// tres bandas. Se mira en todas porque el target se elige <b>antes</b> de tirar:
        /// en ese momento todavia no se sabe que banda va a salir.
        /// </summary>
        public static SelectionSettings ResolveSelectionSettings(ItemSO item)
        {
            if (item == null) return null;

            foreach (var group in EnumerateBandGroups(item))
            {
                if (group?.Effects == null) continue;
                for (int i = 0; i < group.Effects.Count; i++)
                {
                    var eff = group.Effects[i];
                    if (eff != null && eff.HasSelectionRequirement()) return eff.GetSelection();
                }
            }
            return null;
        }

        private static IEnumerable<EffectData> EnumerateBandGroups(ItemSO item)
        {
            yield return item.OnNegativeBand;
            yield return item.OnMixedBand;
            yield return item.OnPositiveBand;
        }

        private static EffectContext BuildContext(Guid playerGuid, TargetSelectionResult selection)
        {
            var ctx = new EffectContext
            {
                SourceGuid = playerGuid,
                TargetGuid = playerGuid,
                lastResult = true,
                SelectionResult = selection,
            };

            // Sin seleccion explicita, self-target al tile del jugador — es lo que
            // esperan los efectos con SlotState = Self.
            if (selection == null
                && ServiceLocator.TryGetService<IGridManager>(out var grid)
                && grid != null
                && grid.TryGetPosition(playerGuid, out var coord))
            {
                ctx.SelectionResult = new TargetSelectionResult
                {
                    WasCompleted = true,
                    SelectedTargets = new List<TargetRef> { TargetRef.At(coord) },
                };
            }

            return ctx;
        }

        private static PreConditionContext BuildPreCtx(EffectContext ctx)
        {
            return new PreConditionContext
            {
                OwnerGuid = ctx.SourceGuid,
                OpponentGuid = ctx.TargetGuid,
                Entity = ctx.SourceEntity,
            };
        }
    }
}
