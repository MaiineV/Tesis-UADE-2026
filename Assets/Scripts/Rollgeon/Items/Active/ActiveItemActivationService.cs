using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Rolls;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Effects.Selection;
using Rollgeon.Grid;
using Rollgeon.Items.Active.Choice;
using Rollgeon.Items.Active.Targeting;
using Rollgeon.Player;
using Rollgeon.PreConditions;
using UnityEngine;

namespace Rollgeon.Items.Active
{
    /// <inheritdoc cref="IActiveItemActivationService"/>
    public sealed class ActiveItemActivationService : IActiveItemActivationService, IDisposable
    {
        private const string LogPrefix = "[ActiveItemActivationService] ";

        private static readonly Cardinal[] DirectionValues =
        {
            Cardinal.North, Cardinal.East, Cardinal.South, Cardinal.West,
        };

        private readonly IEquippedActiveItemService _equipped;
        private readonly IActiveItemDieRoller _roller;
        private readonly EventManager.EventReceiver _onCombatEndHandler;
        private readonly EventManager.EventReceiver _onTurnFinishedHandler;

        // Ventana de decision (aceptar / re-tirar). El item y el target se congelan al
        // confirmar: un equip o un cambio de grilla a mitad de la ventana no la corrompe.
        private ItemSO _pendingItem;
        private TargetSelectionResult _pendingSelection;
        private Guid _pendingGuid;
        private int _pendingRawRoll;
        private int _pendingRerolls;
        private bool _hasPending;

        // Ventana de eleccion post-tirada (§A5). Se abre DESPUES de OnResolved, cuando un
        // efecto de banda pidio elegir entre N tiles (Probability Drive cara 4).
        private ActiveItemChoiceRequest _pendingChoice;
        private Guid _choiceOwnerGuid;

        public ActiveItemActivationService(IEquippedActiveItemService equipped, IActiveItemDieRoller roller)
        {
            _equipped = equipped;
            _roller = roller ?? new ActiveItemDieRoller();

            // Si el combate se cierra con la decision abierta, la tirada se descarta sin
            // correr efectos: OnCombatEnd llega despues del teardown del combate y
            // ejecutar la banda ahi pegaria sobre una sala ya desarmada. El roll pagado
            // no se devuelve — la regla de "nunca reembolsar" es la misma del reroll de
            // ataque/defensa. La eleccion pendiente (si la hay) se descarta en silencio:
            // ya no hay sala donde aplicar OnChosen/OnAbandoned.
            _onCombatEndHandler = _ =>
            {
                DiscardPending();
                DiscardChoiceSilently();
            };
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEndHandler);

            // Fin del turno del jugador con una eleccion abierta: se abandona (el efecto
            // ya definio que hacer sin eleccion — normalmente al azar). El roll ya esta
            // pagado, este es el unico gate que le queda a la ventana.
            _onTurnFinishedHandler = args => HandleTurnFinished(args);
            EventManager.Subscribe(EventName.OnTurnFinished, _onTurnFinishedHandler);
        }

        public void Dispose()
        {
            EventManager.UnSubscribe(EventName.OnCombatEnd, _onCombatEndHandler);
            EventManager.UnSubscribe(EventName.OnTurnFinished, _onTurnFinishedHandler);
            DetachChoiceSelection();
        }

        public event Action<ActiveItemActivationResult> OnResolved;
        public event Action<ActiveItemPendingRoll> OnRollPending;
        public event Action OnSelectionStarted;
        public event Action OnSelectionCancelled;
        public event Action OnChoicePending;
        public event Action OnChoiceResolved;

        public bool IsSelecting { get; private set; }

        public bool IsAwaitingDecision => _hasPending;

        public bool IsAwaitingChoice => _pendingChoice != null;

        public ActiveItemPendingRoll? Pending => _hasPending
            ? new ActiveItemPendingRoll(_pendingItem, _pendingRawRoll, _pendingRerolls)
            : (ActiveItemPendingRoll?)null;

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

            var item = _equipped.Current;
            var playerGuid = ResolvePlayerGuid();

            // Targeting por direccion (§A4): si algun efecto de banda lo implementa, el
            // flujo de seleccion es el de las 4 direcciones cardinales, no el de
            // SelectionSettings normal.
            var directionEffect = FindDirectionEffect(item);
            if (directionEffect != null)
                return BeginDirectionActivation(item, directionEffect, playerGuid);

            var settings = ResolveSelectionSettings(item);

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

            var validTargets = ApplyTargetFilters(item, settings.ResolveValidTiles(ownerPos, playerGuid), playerGuid);
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

        /// <summary>
        /// Flujo de targeting por direccion (§A4): proxies = las 4 casillas adyacentes
        /// cuya trayectoria sea valida (in-bounds, no vacia), pintadas como underlay la
        /// union de todas las trayectorias. El hover recalcula la trayectoria de la
        /// cardinal hacia la casilla hovered para el preview de camino.
        /// </summary>
        private bool BeginDirectionActivation(ItemSO item, IDirectionTargetedEffect directionEffect, Guid playerGuid)
        {
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid)
                || grid == null
                || !grid.TryGetPosition(playerGuid, out var origin))
            {
                Debug.LogWarning(LogPrefix + "item de direccion sin grilla — activacion rechazada.");
                return false;
            }

            var validTargets = new List<TargetRef>();
            var rangeTiles = new HashSet<GridCoord>();
            foreach (var dir in DirectionValues)
            {
                var trajectory = directionEffect.PreviewTrajectory(playerGuid, origin, dir);
                if (trajectory == null || trajectory.Count == 0) continue;

                validTargets.Add(TargetRef.At(dir.Step(origin)));
                foreach (var coord in trajectory) rangeTiles.Add(coord);
            }

            if (validTargets.Count == 0) return false;

            if (!ServiceLocator.TryGetService<ISelectionController>(out var controller) || controller == null)
            {
                Debug.LogWarning(LogPrefix + "ISelectionController no registrado — no se puede pedir direccion.");
                return false;
            }

            IsSelecting = true;
            controller.OnSelectionCompleted += HandleSelectionCompleted;
            controller.BeginSelection(new SelectionRequest
            {
                // Both: la casilla adyacente puede tener un enemigo (se clickea como
                // direccion, no como target puntual) o estar libre.
                Settings = new SelectionSettings { SlotState = SlotState.Both, AutoAccept = true, SelectionCount = 1 },
                ValidTargets = validTargets,
                OwnerGuid = playerGuid,
                HighlightStyle = "attack",
                RangeTiles = rangeTiles,
                RangeHighlightStyle = "range",
                HoverPreviewStyle = "path",
                HoverPreview = coord =>
                    directionEffect.PreviewTrajectory(playerGuid, origin, CardinalExtensions.FromDelta(origin, coord)),
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

            // Con una tirada esperando aceptar/re-tirar, o una eleccion post-tirada
            // esperando un tile, no se abre otra activacion: la ventana se resuelve
            // primero. Va antes que el chequeo de rolls porque el motivo real del
            // bloqueo es la decision pendiente, no el pool.
            if (_hasPending || IsAwaitingChoice) return ActiveItemBlock.AwaitingDecision;

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
        // Confirmacion (§22): cobrar → tirar → decidir (aceptar / re-tirar)
        // ======================================================================

        public ActiveItemPendingRoll? Confirm(TargetSelectionResult selection)
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

            // La tirada queda pendiente de decision: el activo se re-tira como ataque y
            // defensa, solo que con un dado. Los efectos corren recien en AcceptRoll.
            _pendingItem = item;
            _pendingSelection = selection;
            _pendingGuid = playerGuid;
            _pendingRawRoll = _roller.Roll(item.ActiveDie);
            _pendingRerolls = 0;
            _hasPending = true;

            var pending = new ActiveItemPendingRoll(item, _pendingRawRoll, 0);
            OnRollPending?.Invoke(pending);
            return pending;
        }

        public bool CanRequestReroll
            => _hasPending
               && ServiceLocator.TryGetService<IRollPoolService>(out IRollPoolService rolls)
               && rolls != null
               && rolls.GetCurrent(_pendingGuid) >= RollCost;

        public bool RequestReroll()
        {
            if (!_hasPending)
            {
                Debug.LogWarning(LogPrefix + "RequestReroll sin tirada pendiente — ignorado.");
                return false;
            }

            // Mismo contrato que el reroll de combate: cada re-tirada cuesta 1 roll y con
            // el pool en 0 no pasa nada — la cara vigente queda y la salida es aceptar.
            // El boton deberia estar apagado via CanRequestReroll; esto es el guard.
            ServiceLocator.TryGetService<IRollPoolService>(out IRollPoolService rolls);
            if (rolls == null || !rolls.TrySpendRolls(_pendingGuid, RollCost))
            {
                Debug.Log(LogPrefix + "reroll bloqueado — pool vacio. La cara vigente queda.");
                return false;
            }

            _pendingRawRoll = _roller.Roll(_pendingItem.ActiveDie);
            _pendingRerolls++;

            OnRollPending?.Invoke(new ActiveItemPendingRoll(_pendingItem, _pendingRawRoll, _pendingRerolls));
            return true;
        }

        public ActiveItemActivationResult? AcceptRoll()
        {
            if (!_hasPending) return null;

            var item = _pendingItem;
            var selection = _pendingSelection;
            var playerGuid = _pendingGuid;
            int rawRoll = _pendingRawRoll;

            // La ventana se cierra ANTES de correr efectos: si un efecto dispara un
            // refresh del HUD, este ya no tiene que ver una decision abierta.
            DiscardPending();

            // §14, orden de operaciones: el encantamiento ajusta el resultado crudo y
            // RECIEN despues se determina la banda. Al reves, el ajuste no cambiaria
            // nada. Corre sobre la cara aceptada, no sobre cada reroll intermedio: un
            // uso limitado no se gasta en tiradas que el jugador descarto.
            int roll = ApplyEnchantment(rawRoll, item.ActiveDie.MaxFace());

            // Resolucion completa (Feature#0085): cara, banda, estructura y magnitud —
            // por item, no por dado: Precision, Control y Binary tienen mecanismo propio.
            var resolution = ActiveItemBands.ResolveRoll(rawRoll, roll, item);

            var choiceHost = new ChoiceCollector();
            var ctx = BuildContext(playerGuid, selection, item, resolution, choiceHost);
            var effects = item.GetEffectsFor(resolution);

            // El roll ya se cobro: que la cadena de efectos corte no lo devuelve. Lo que
            // se reporta es si corrio entera, para el feedback.
            bool ok = effects.TryExecute(ctx, BuildPreCtx(ctx));

            var result = new ActiveItemActivationResult(item, roll, resolution.Band, ok, rawRoll, resolution);
            OnResolved?.Invoke(result);

            // §A5: un efecto de banda pidio elegir entre N tiles. La ventana se abre
            // DESPUES de OnResolved — la activacion ya se reporto resuelta, la eleccion
            // es una fase aparte encima.
            if (choiceHost.Request != null)
            {
                _choiceOwnerGuid = playerGuid;
                OpenChoice(choiceHost.Request);
            }

            return result;
        }

        private void DiscardPending()
        {
            _hasPending = false;
            _pendingItem = null;
            _pendingSelection = null;
            _pendingGuid = Guid.Empty;
            _pendingRawRoll = 0;
            _pendingRerolls = 0;
        }

        /// <summary>
        /// Ajuste del encantamiento sobre el resultado crudo, si hay uno y le quedan usos.
        /// </summary>
        /// <remarks>
        /// El clamp a <c>[1, faces]</c> es innegociable: el GDD prohibe que un
        /// encantamiento saque el resultado del rango del dado. El tope propio del
        /// modifier (ej. "máximo 5") ya deberia hacerlo, pero no se confia en la
        /// autoria — un item mal configurado no puede romper la regla del sistema.
        /// </remarks>
        private int ApplyEnchantment(int rawRoll, int faces)
        {
            var ench = _equipped.Enchantment;
            if (ench?.Modifier == null) return rawRoll;
            if (ench.IsLimited && _equipped.EnchantmentUsesLeft <= 0) return rawRoll;
            if (!ench.Modifier.AppliesTo(rawRoll, faces)) return rawRoll;

            int adjusted = ench.Modifier.Apply(rawRoll, faces);
            if (adjusted < 1) adjusted = 1;
            if (adjusted > faces) adjusted = faces;

            // Un ajuste que no cambia nada no gasta uso: seria regalar el limite.
            if (adjusted != rawRoll) _equipped.ConsumeEnchantmentUse();

            return adjusted;
        }

        // ======================================================================
        // Eleccion post-tirada (§A5)
        // ======================================================================

        /// <summary>
        /// Recolecta el (unico) pedido de eleccion de la activacion en curso. Se pasa
        /// como <see cref="ActiveItemRollTriggerContext.Choices"/> — el efecto llama
        /// <see cref="RequestChoice"/> de forma sincronica dentro de <c>TryExecute</c> y
        /// el servicio recien actua despues, cuando ya sabe si corrio entera.
        /// </summary>
        private sealed class ChoiceCollector : IActiveItemChoiceHost
        {
            public ActiveItemChoiceRequest Request { get; private set; }

            public bool RequestChoice(ActiveItemChoiceRequest request)
            {
                if (request == null || request.Options == null || request.Options.Count == 0) return false;

                if (Request != null)
                {
                    Debug.LogWarning(LogPrefix + "ya hay un pedido de eleccion en esta activacion — se ignora el extra.");
                    return false;
                }

                Request = request;
                return true;
            }
        }

        private void OpenChoice(ActiveItemChoiceRequest request)
        {
            // Una sola opcion: nada que elegir, se resuelve directo (ej. Probability
            // Drive con menos de 3 tiles seguras disponibles en radio 4).
            if (request.Options.Count == 1)
            {
                request.OnChosen?.Invoke(request.Options[0]);
                return;
            }

            if (!ServiceLocator.TryGetService<ISelectionController>(out var controller) || controller == null)
            {
                Debug.LogWarning(LogPrefix + "ISelectionController no registrado — la eleccion se abandona.");
                request.OnAbandoned?.Invoke();
                return;
            }

            _pendingChoice = request;

            var validTargets = new List<TargetRef>();
            foreach (var coord in request.Options) validTargets.Add(TargetRef.At(coord));

            controller.OnSelectionCompleted += HandleChoiceSelectionCompleted;
            controller.BeginSelection(new SelectionRequest
            {
                Settings = new SelectionSettings
                {
                    SlotState = SlotState.Empty,
                    IsGlobal = true,
                    AutoAccept = true,
                    SelectionCount = 1,
                },
                ValidTargets = validTargets,
                OwnerGuid = _choiceOwnerGuid,
                HighlightStyle = string.IsNullOrEmpty(request.HighlightStyle) ? "range" : request.HighlightStyle,
            });

            OnChoicePending?.Invoke();
        }

        private void HandleChoiceSelectionCompleted(TargetSelectionResult result)
        {
            var request = _pendingChoice;
            if (request == null) return;

            _pendingChoice = null;
            DetachChoiceSelection();

            if (result != null && result.WasCompleted && result.FirstSelectedCoord.HasValue)
                request.OnChosen?.Invoke(result.FirstSelectedCoord.Value);
            else
                request.OnAbandoned?.Invoke();

            OnChoiceResolved?.Invoke();
        }

        private void DetachChoiceSelection()
        {
            if (ServiceLocator.TryGetService<ISelectionController>(out var controller) && controller != null)
                controller.OnSelectionCompleted -= HandleChoiceSelectionCompleted;
        }

        /// <summary>Fin de turno con una eleccion abierta: se abandona (§A5).</summary>
        private void HandleTurnFinished(object[] args)
        {
            if (_pendingChoice == null) return;
            if (args != null && args.Length >= 1 && args[0] is Guid guid && guid != _choiceOwnerGuid) return;

            var request = _pendingChoice;
            _pendingChoice = null;
            DetachChoiceSelection();
            if (ServiceLocator.TryGetService<ISelectionController>(out var controller) && controller != null)
                controller.CancelSelection();

            request.OnAbandoned?.Invoke();
            OnChoiceResolved?.Invoke();
        }

        /// <summary>Fin de combate con una eleccion abierta: descarte silencioso, sin callbacks.</summary>
        private void DiscardChoiceSilently()
        {
            if (_pendingChoice == null) return;
            _pendingChoice = null;
            DetachChoiceSelection();
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
        /// Un item que activa directo (sin seleccion) no tiene nada que validar. Items de
        /// direccion (§A4) validan que exista al menos una cardinal con trayectoria.
        /// </summary>
        private static bool HasAnyValidTarget(ItemSO item, Guid playerGuid)
        {
            var directionEffect = FindDirectionEffect(item);
            if (directionEffect != null) return HasAnyValidDirection(directionEffect, playerGuid);

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

            var valid = ApplyTargetFilters(item, settings.ResolveValidTiles(ownerPos, playerGuid), playerGuid);
            return valid != null && valid.Count > 0;
        }

        private static bool HasAnyValidDirection(IDirectionTargetedEffect directionEffect, Guid playerGuid)
        {
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid)
                || grid == null
                || !grid.TryGetPosition(playerGuid, out var origin))
            {
                return true;
            }

            foreach (var dir in DirectionValues)
            {
                var trajectory = directionEffect.PreviewTrajectory(playerGuid, origin, dir);
                if (trajectory != null && trajectory.Count > 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Primer <c>SelectionSettings</c> que pida seleccion entre los efectos de las
        /// bandas. Se mira en todas porque el target se elige <b>antes</b> de tirar: en
        /// ese momento todavia no se sabe que banda va a salir.
        /// </summary>
        public static SelectionSettings ResolveSelectionSettings(ItemSO item)
        {
            foreach (var eff in EnumerateAllBandEffects(item))
                if (eff.HasSelectionRequirement()) return eff.GetSelection();
            return null;
        }

        /// <summary>Primer efecto de banda que se dirige por direccion, o null.</summary>
        private static IDirectionTargetedEffect FindDirectionEffect(ItemSO item)
        {
            foreach (var eff in EnumerateAllBandEffects(item))
                if (eff is IDirectionTargetedEffect dir) return dir;
            return null;
        }

        /// <summary>Todos los efectos de banda que restringen targets validos (§A4/Bottle'o Thunder).</summary>
        private static List<IActiveItemTargetFilter> FindTargetFilters(ItemSO item)
        {
            var list = new List<IActiveItemTargetFilter>();
            foreach (var eff in EnumerateAllBandEffects(item))
                if (eff is IActiveItemTargetFilter filter) list.Add(filter);
            return list;
        }

        /// <summary>
        /// Intersecta <paramref name="validTargets"/> con cada <see cref="IActiveItemTargetFilter"/>
        /// de banda. No-op si el item no tiene ninguno. NOTA: no cubre el camino de
        /// <c>SelectionSettings.AutoResolve</c> — ninguno de los items autorados hoy
        /// combina AutoResolve con un target filter.
        /// </summary>
        private static List<TargetRef> ApplyTargetFilters(ItemSO item, List<TargetRef> validTargets, Guid playerGuid)
        {
            if (validTargets == null || validTargets.Count == 0) return validTargets;

            var filters = FindTargetFilters(item);
            if (filters.Count == 0) return validTargets;

            var result = new List<TargetRef>();
            foreach (var target in validTargets)
            {
                bool ok = true;
                foreach (var filter in filters)
                {
                    if (!filter.IsValidTarget(playerGuid, target.Coord)) { ok = false; break; }
                }
                if (ok) result.Add(target);
            }
            return result;
        }

        private static IEnumerable<IEffect> EnumerateAllBandEffects(ItemSO item)
        {
            if (item == null) yield break;

            foreach (var group in EnumerateBandGroups(item))
            {
                if (group?.Effects == null) continue;
                for (int i = 0; i < group.Effects.Count; i++)
                {
                    var eff = group.Effects[i];
                    if (eff != null) yield return eff;
                }
            }
        }

        private static IEnumerable<EffectData> EnumerateBandGroups(ItemSO item)
        {
            yield return item.OnNegativeBand;
            yield return item.OnMixedBand;
            yield return item.OnPositiveBand;
        }

        private static EffectContext BuildContext(Guid playerGuid, TargetSelectionResult selection,
            ItemSO item, ActiveItemRollResolution resolution, IActiveItemChoiceHost choices)
        {
            var ctx = new EffectContext
            {
                SourceGuid = playerGuid,
                TargetGuid = playerGuid,
                lastResult = true,
                SelectionResult = selection,
                SourceItemId = item?.ItemId,
            };

            // Sin seleccion explicita, self-target al tile del jugador — es lo que
            // esperan los efectos con SlotState = Self.
            bool hasGrid = ServiceLocator.TryGetService<IGridManager>(out var grid) && grid != null;
            GridCoord origin = default;
            bool hasOrigin = hasGrid && grid.TryGetPosition(playerGuid, out origin);

            if (selection == null && hasOrigin)
            {
                ctx.SelectionResult = new TargetSelectionResult
                {
                    WasCompleted = true,
                    SelectedTargets = new List<TargetRef> { TargetRef.At(origin) },
                };
            }

            // §A4: si el item resuelve por direccion, la direccion elegida se deriva del
            // origen del jugador y el proxy seleccionado — el efecto recomputa la
            // trayectoria real al resolver (la cara decide la distancia).
            Cardinal? direction = null;
            if (hasOrigin && FindDirectionEffect(item) != null
                && ctx.SelectionResult != null && ctx.SelectionResult.FirstSelectedCoord.HasValue)
            {
                direction = CardinalExtensions.FromDelta(origin, ctx.SelectionResult.FirstSelectedCoord.Value);
            }

            ctx.TriggerContext = new ActiveItemRollTriggerContext
            {
                Item = item,
                Face = resolution.Face,
                RawFace = resolution.RawFace,
                Faces = resolution.Faces,
                Band = resolution.Band,
                Structure = resolution.Structure,
                Magnitude = resolution.Magnitude,
                Magnitude01 = resolution.Magnitude01,
                Direction = direction,
                Origin = hasOrigin ? origin : default,
                Choices = choices,
            };

            return ctx;
        }

        private static PreConditionContext BuildPreCtx(EffectContext ctx)
        {
            return new PreConditionContext
            {
                OwnerGuid = ctx.SourceGuid,
                OpponentGuid = ctx.TargetGuid,
                Entity = ctx.SourceEntity,
                Effect = ctx,
            };
        }
    }
}
