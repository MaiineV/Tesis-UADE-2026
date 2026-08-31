using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Patterns;
using Rollgeon.ActionRolls;
using Rollgeon.Combos;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Entities.Visuals;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Patterns;
using Rollgeon.Phase;
using Rollgeon.Player;
using Rollgeon.PreConditions;
using UnityEngine;

namespace Rollgeon.Exploration
{
    public sealed class ExplorationBehaviorService : IExplorationBehaviorService, IDisposable
    {
        private enum State { Inactive, Idle, Selecting, Rolling }

        private State _state = State.Inactive;
        private HeroActionBehavior _pendingBehavior;

        // Casillas "frente a puerta" de la selección de movimiento en curso → dirección.
        // Si el user clickea una de estas, se cruza de sala en vez de moverse.
        private System.Collections.Generic.Dictionary<GridCoord, DoorDirection> _doorTiles;

        // Casillas frente a la puerta de SALIDA de piso (#158). Pisarlas dispara la
        // transición al siguiente piso en vez de cruzar a una sala vecina.
        private System.Collections.Generic.HashSet<GridCoord> _exitTiles;

        // Latch anti-encadenado (bug de puerta encadenada): true desde que se dispara un
        // cruce de sala/piso hasta que la transición efectivamente resuelve. Mientras esté
        // activo, ArmMovement no re-arma la selección y OnSelectionCompleted ignora
        // cualquier resultado que llegue en el medio — sin esto, un evento duplicado (o
        // el propio click que originó el cruce) podía encadenar un segundo cruce antes de
        // que el primero terminara.
        private bool _crossingDoor;

        private EventManager.EventReceiver _onPhaseEnter;
        private EventManager.EventReceiver _onPhaseExit;
        private EventManager.EventReceiver _onRoomEntered;
        private EventManager.EventReceiver _onTutorialActionUnlocked;

        public bool IsActive => _state != State.Inactive;

        private ExplorationBehaviorService()
        {
            _onPhaseEnter = OnPhaseEnter;
            _onPhaseExit = OnPhaseExit;
            _onRoomEntered = OnRoomEntered;
            _onTutorialActionUnlocked = OnTutorialActionUnlocked;

            EventManager.Subscribe(EventName.OnPhaseEnter, _onPhaseEnter);
            EventManager.Subscribe(EventName.OnPhaseExit, _onPhaseExit);
            EventManager.Subscribe(EventName.OnRoomEntered, _onRoomEntered);
            EventManager.Subscribe(EventName.OnTutorialActionUnlocked, _onTutorialActionUnlocked);
        }

        // BUG-068: el gate del tutorial corta el re-armado del movimiento, pero nada lo
        // re-armaba al desbloquear — el jugador quedaba sin click-to-move hasta la próxima
        // transición de sala. El evento dispara tanto en Lock como en Unlock (es la señal
        // genérica de "recomputá"); si Movement sigue lockeado, OnBehaviorSelected lo
        // ignora vía el gate y esto degrada a no-op. Fuera del tutorial el evento no se
        // emite.
        private void OnTutorialActionUnlocked(params object[] args)
        {
            if (args == null || args.Length < 1 || args[0] is not HeroBehaviorSlot slot) return;
            if (slot != HeroBehaviorSlot.Movement) return;
            CoroutineHost.Run(ArmMovementNextFrame());
        }

        public static ExplorationBehaviorService CreateAndRegister()
        {
            var service = new ExplorationBehaviorService();
            ServiceLocator.AddService<IExplorationBehaviorService>(service, ServiceScope.Run);
            return service;
        }

        public void OnBehaviorSelected(int slot)
        {
            Debug.LogWarning($"[ExplorationBehaviorService] OnBehaviorSelected(slot={slot}) — _state={_state}");

            // BUG-019: backstop del gate del tutorial. Esta ruta no pasa por
            // TurnManager.CanExecute (arma selección directo), así
            // que sin este chequeo una acción lockeada podía ejecutarse igual vía
            // hotkey/ArmMovement. Primero de todo: un click en acción lockeada no
            // debe cancelar un flow en curso. Fuera del tutorial el service no está
            // registrado y esto degrada a no-op.
            if (ServiceLocator.TryGetService<Rollgeon.Tutorial.ITutorialActionGateService>(out var tutorialGate)
                && tutorialGate != null && tutorialGate.IsSlotLocked((HeroBehaviorSlot)slot))
            {
                Debug.Log($"[ExplorationBehaviorService] Slot {(HeroBehaviorSlot)slot} lockeado por el tutorial — click ignorado.");
                return;
            }

            // Si hay un flow anterior colgado (Selecting / Rolling sin terminar), lo
            // cancelamos automaticamente antes de procesar el nuevo click. Asi el user
            // puede clickear otro boton para "abandonar" la accion en curso.
            if (_state != State.Idle)
            {
                Debug.LogWarning($"[ExplorationBehaviorService] _state={_state} → cancelando flow anterior y procesando click nuevo.");
                ForceCancelInProgress();
            }

            if (_state != State.Idle)
            {
                Debug.LogWarning($"[ExplorationBehaviorService] No pude resetear _state a Idle (queda {_state}) — abortando click.");
                return;
            }

            if (!ServiceLocator.TryGetService<IPlayerService>(out var playerService))
            {
                Debug.LogWarning("[ExplorationBehaviorService] IPlayerService not registered.");
                return;
            }

            var hero = playerService.CurrentHero;
            if (hero == null) return;

            var playerGuid = playerService.PlayerGuid;
            var behaviors = hero.GetBehaviorsForPhase(GamePhase.Exploration);

            // Buscar por slot (no por list index) — la lista de exploration filtra
            // los slots que no aplican (ej. BaseAttack, ClassSkill), por lo que
            // los índices no se alinean con HeroBehaviorSlot. Buscar por slot
            // garantiza que el botón de Pass Door no termine disparando Healing.
            HeroActionBehavior behavior = null;
            for (int i = 0; i < behaviors.Count; i++)
            {
                if (behaviors[i] != null && (int)behaviors[i].Slot == slot)
                {
                    behavior = behaviors[i];
                    break;
                }
            }

            if (behavior == null)
            {
                Debug.LogWarning($"[ExplorationBehaviorService] No behavior at slot {slot} for Exploration.");
                return;
            }

            var preCtx = new PreConditionContext
            {
                OwnerGuid = playerGuid,
                Entity = new Entity { Guid = playerGuid },
            };

            if (behavior.ShowConditions != null && behavior.ShowConditions.Count > 0
                && !behavior.ShouldShow(preCtx))
            {
                Debug.LogWarning($"[ExplorationBehaviorService] '{behavior.ActionName}' ShowConditions failed — behavior no ejecutado.");
                return;
            }

            // Si algun effect del behavior implementa IActionRollEffect, va por el flow
            // de tirada (el spec decide si cobra — en exploración no cobra: el pool de
            // rolls no existe fuera de combate y las acciones de exploración son gratis).
            bool ownsCostViaActionRoll = TryFindActionRollEffect(behavior, out var rollEffect);

            if (ownsCostViaActionRoll && rollEffect.TryGetRollSpec(playerGuid, out var rollSpec))
            {
                StartActionRoll(behavior, playerGuid, playerService, rollSpec);
                return;
            }

            if (behavior.HasEffectsWithSelectionAt(SelectionTiming.BeforeRoll))
            {
                BeginSelection(behavior, playerGuid);
                return;
            }

            ExecuteBehavior(behavior, playerGuid, null, null);
            // Acción instantánea (ej. Heal): re-armar el movimiento para no quedar sin
            // click-to-move tras usarla. Movement nunca cae acá (siempre va por selección).
            ArmMovement();
        }

        public void CancelSelection()
        {
            if (_state != State.Selecting) return;

            if (ServiceLocator.TryGetService<ISelectionController>(out var controller))
            {
                controller.OnSelectionCompleted -= OnSelectionCompleted;
                controller.CancelSelection();
            }

            _pendingBehavior = null;
            _doorTiles = null;
            _exitTiles = null;
            _state = State.Idle;
        }

        // Cancela cualquier flow en curso y deja _state en Idle. Llamado cuando el
        // user clickea otra accion mientras hay una en progreso — comportamiento
        // tipico de UX: el ultimo click "manda" y abandona lo anterior.
        private void ForceCancelInProgress()
        {
            switch (_state)
            {
                case State.Selecting:
                    Debug.LogWarning("[ExplorationBehaviorService] ForceCancel: cancelando Selecting.");
                    CancelSelection();
                    break;
                case State.Rolling:
                    Debug.LogWarning("[ExplorationBehaviorService] ForceCancel: cancelando Rolling (action roll).");
                    if (ServiceLocator.TryGetService<IActionRollService>(out var rs)
                        && rs != null && rs.IsActive)
                    {
                        rs.Cancel(); // el callback resuelve outcome.Cancelled=true → _state=Idle
                    }
                    // Safety: si el cancel del roll service no logro resetear (callback
                    // no se invoco), forzamos Idle directamente.
                    if (_state == State.Rolling)
                    {
                        _pendingBehavior = null;
                        _state = State.Idle;
                    }
                    break;
            }
        }

        public void Dispose()
        {
            CancelSelection();

            if (_onPhaseEnter != null)
            {
                EventManager.UnSubscribe(EventName.OnPhaseEnter, _onPhaseEnter);
                _onPhaseEnter = null;
            }
            if (_onPhaseExit != null)
            {
                EventManager.UnSubscribe(EventName.OnPhaseExit, _onPhaseExit);
                _onPhaseExit = null;
            }
            if (_onRoomEntered != null)
            {
                EventManager.UnSubscribe(EventName.OnRoomEntered, _onRoomEntered);
                _onRoomEntered = null;
            }
            if (_onTutorialActionUnlocked != null)
            {
                EventManager.UnSubscribe(EventName.OnTutorialActionUnlocked, _onTutorialActionUnlocked);
                _onTutorialActionUnlocked = null;
            }

            _state = State.Inactive;
        }

        // En Exploración el modo movimiento está SIEMPRE activo: en vez de apretar el
        // botón de Movement, el player se mueve clickeando una casilla. Esto re-arma la
        // selección de movimiento (mismo flujo que el botón) cada vez que el player
        // queda idle en Exploración. Si hay otra acción en curso (Selecting/Rolling) NO
        // la pisa — el guard de _state evita el ForceCancel de OnBehaviorSelected.
        private void ArmMovement()
        {
            // Mientras estemos cruzando de sala/piso no hay que re-armar el movimiento —
            // la selección se arma de nuevo cuando el cruce termine (ver OnRoomEntered).
            if (_crossingDoor) return;
            if (_state != State.Idle) return;
            if (!ServiceLocator.TryGetService<IPhaseService>(out var phase)
                || phase.CurrentBase != GamePhase.Exploration)
                return;

            OnBehaviorSelected((int)HeroBehaviorSlot.Movement);
        }

        // Arma el movimiento un frame después para darle tiempo al TileRendererRegistrar
        // (que también escucha OnRoomEntered) a registrar los renderers de la sala antes
        // de pintar el rango — si no, el highlight saltea tiles recién cargados.
        private IEnumerator ArmMovementNextFrame()
        {
            yield return null;
            ArmMovement();
        }

        // Re-arma el movimiento recién cuando el pawn termina de caminar, para que el
        // rango se recalcule desde la casilla de destino y no mientras se mueve.
        private IEnumerator RearmMovementAfterArrival(Guid playerGuid)
        {
            if (ServiceLocator.TryGetService<IEntityVisualService>(out var visuals) && visuals != null)
            {
                var wait = visuals.WaitForMoveComplete(playerGuid);
                if (wait != null) yield return wait;
            }
            ArmMovement();
        }

        private static bool TryFindActionRollEffect(HeroActionBehavior behavior,
            out IActionRollEffect rollEffect)
        {
            rollEffect = null;
            if (behavior?.Effects == null) return false;
            foreach (var group in behavior.Effects)
            {
                if (group?.Effects == null) continue;
                foreach (var eff in group.Effects)
                {
                    if (eff is IActionRollEffect candidate)
                    {
                        rollEffect = candidate;
                        return true;
                    }
                }
            }
            return false;
        }

        private void StartActionRoll(HeroActionBehavior behavior, Guid playerGuid,
            IPlayerService playerService, ActionRollSpec spec)
        {
            if (!ServiceLocator.TryGetService<IActionRollService>(out var rollService)
                || rollService == null)
            {
                Debug.LogError("[ExplorationBehaviorService] IActionRollService no registrado — " +
                               "no se puede ejecutar la accion con tirada.");
                return;
            }

            var bag = playerService.DiceBag;
            if (bag == null || bag.Dice == null || bag.Dice.Count == 0)
            {
                Debug.LogError("[ExplorationBehaviorService] PlayerService.DiceBag null o vacio — " +
                               "no se puede tirar para la accion.");
                return;
            }

            _pendingBehavior = behavior;
            _state = State.Rolling;
            Debug.LogWarning($"[ExplorationBehaviorService] StartActionRoll → behavior='{behavior.ActionName}' _state=Rolling");

            rollService.StartFlow(spec, playerGuid, bag, outcome =>
            {
                Debug.LogWarning($"[ExplorationBehaviorService] outcome callback: cancelled={outcome.Cancelled} " +
                                 $"passed={outcome.PassedThreshold} effective={outcome.EffectiveTotal} " +
                                 $"hasCombo={outcome.HasCombo} _state→Idle");
                var resolvedBehavior = _pendingBehavior;
                _pendingBehavior = null;
                _state = State.Idle;

                if (outcome.Cancelled || resolvedBehavior == null) return;

                // Detección REAL del service (ComboId + ContributingIndices sobre el subset
                // holdeado) — los effects N×M (EffHeal) leen tabla y Σcaras; EffForceDoor
                // sigue usando ActionRollEffectiveTotal, siempre seteado.
                ExecuteBehavior(resolvedBehavior, playerGuid, null, outcome.FinalRoll,
                    outcome.Combo, outcome.EffectiveTotal,
                    outcome.HeldDice, outcome.HeldDiceOriginalIndices);
                // Acción con tirada terminada (ej. Force Door) — re-armar movimiento.
                ArmMovement();
            });
        }

        private void BeginSelection(HeroActionBehavior behavior, Guid playerGuid)
        {
            var targetSettings = behavior.Effects?
                .Where(g => g?.Effects != null)
                .SelectMany(g => g.Effects)
                .FirstOrDefault(e => e != null && e.RequiresSelectionAt(SelectionTiming.BeforeRoll))
                ?.GetSelection();

            if (targetSettings == null)
            {
                Debug.LogWarning("[ExplorationBehaviorService] No SelectionSettings found — executing directly.");
                ExecuteBehavior(behavior, playerGuid, null, null);
                return;
            }

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid)
                || !grid.TryGetPosition(playerGuid, out var ownerPos))
            {
                Debug.LogWarning("[ExplorationBehaviorService] Cannot resolve player position.");
                return;
            }

            if (targetSettings.SlotState == SlotState.Self)
            {
                var selfResult = new TargetSelectionResult
                {
                    WasCompleted = true,
                    SelectedTargets = new System.Collections.Generic.List<TargetRef>
                        { TargetRef.At(ownerPos) },
                };
                ExecuteBehavior(behavior, playerGuid, selfResult, null);
                ArmMovement();
                return;
            }

            if (targetSettings.AutoResolve)
            {
                var autoResult = targetSettings.AutoResolveTargets(ownerPos, playerGuid);
                ExecuteBehavior(behavior, playerGuid, autoResult, null);
                ArmMovement();
                return;
            }

            var validTargets = targetSettings.ResolveValidTiles(ownerPos, playerGuid);
            if (validTargets.Count == 0)
            {
                Debug.Log("[ExplorationBehaviorService] No valid targets for selection.");
                return;
            }

            if (!ServiceLocator.TryGetService<ISelectionController>(out var controller))
            {
                Debug.LogWarning("[ExplorationBehaviorService] ISelectionController not registered.");
                return;
            }

            // Pass-door por selección de casilla: durante el movimiento, las casillas
            // frente a puertas abiertas se ofrecen como targets extra (pintadas en rojo).
            // Seleccionarlas cruza a la sala vecina. Solo aplica al slot de Movement.
            var doorTiles = ResolveDoorTiles(behavior, grid, validTargets);

            _pendingBehavior = behavior;
            _state = State.Selecting;
            Debug.LogWarning($"[ExplorationBehaviorService] BeginSelection: behavior='{behavior.ActionName}' " +
                             $"validTargets={validTargets.Count} doorTiles={(doorTiles?.Count ?? 0)} → _state=Selecting (esperando click del user).");

            // Movement está siempre armado en Exploración → no pintamos ningún tinte de
            // piso (ni rango de fondo, ni path en hover, ni destino "selected"): en
            // permanencia molesta. Click-to-move limpio; solo las puertas se pintan.
            bool isMovement = behavior.IsBaseBehavior && behavior.Slot == HeroBehaviorSlot.Movement;

            controller.OnSelectionCompleted += OnSelectionCompleted;
            controller.BeginSelection(new SelectionRequest
            {
                Settings = targetSettings,
                ValidTargets = validTargets,
                OwnerGuid = playerGuid,
                HighlightStyle = "move",
                DoorTiles = doorTiles,
                SuppressRangeHighlight = isMovement,
                SuppressPathPreview = isMovement,
            });
        }

        // Calcula las casillas frente a puerta para una selección de movimiento y las
        // agrega a validTargets (clickeables aunque caigan fuera del rango). Guarda el
        // mapa casilla→dirección en _doorTiles para resolver el cruce al completarse.
        // Devuelve null si el behavior no es Movement o no hay puertas atravesables.
        private System.Collections.Generic.HashSet<GridCoord> ResolveDoorTiles(
            HeroActionBehavior behavior, IGridManager grid, System.Collections.Generic.List<TargetRef> validTargets)
        {
            _doorTiles = null;
            _exitTiles = null;

            if (!behavior.IsBaseBehavior || behavior.Slot != HeroBehaviorSlot.Movement) return null;
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon) || dungeon == null) return null;

            var fronts = DoorTileQuery.GetOpenDoorFrontTiles(dungeon, grid);
            var exitFronts = DoorTileQuery.GetOpenExitDoorFrontTiles(dungeon, grid);
            if (fronts.Count == 0 && exitFronts.Count == 0) return null;

            if (fronts.Count > 0) _doorTiles = fronts;
            if (exitFronts.Count > 0) _exitTiles = exitFronts;

            // Las dos clases de casilla (cruce de sala + salida de piso) se ofrecen como
            // targets extra con el mismo highlight "door"; el destino se distingue al confirmar.
            var coords = new System.Collections.Generic.HashSet<GridCoord>(fronts.Keys);
            coords.UnionWith(exitFronts);

            foreach (var coord in coords)
            {
                bool already = false;
                for (int i = 0; i < validTargets.Count; i++)
                {
                    if (validTargets[i] != null && validTargets[i].Coord == coord) { already = true; break; }
                }
                if (!already) validTargets.Add(TargetRef.At(coord));
            }

            return coords;
        }

        private void OnSelectionCompleted(TargetSelectionResult result)
        {
            // Latch anti-encadenado: si ya hay un cruce de sala/piso en vuelo, cualquier
            // resultado que llegue en el medio (evento duplicado, click en cola) se
            // ignora sin tocar nada — ver comentario del campo _crossingDoor.
            if (_crossingDoor) return;

            if (ServiceLocator.TryGetService<ISelectionController>(out var controller))
                controller.OnSelectionCompleted -= OnSelectionCompleted;

            var behavior = _pendingBehavior;
            var doorTiles = _doorTiles;
            var exitTiles = _exitTiles;
            _pendingBehavior = null;
            _doorTiles = null;
            _exitTiles = null;
            _state = State.Idle;

            if (behavior == null || !result.WasCompleted) return;

            if (!ServiceLocator.TryGetService<IPlayerService>(out var playerService)) return;
            var playerGuid = playerService.PlayerGuid;

            var picked = result.FirstSelectedCoord;

            // Inicializado afuera: el compilador no puede correlacionar hasDoorDir con la
            // asignación del out dentro de la cadena de &&.
            DoorDirection doorDir = default;
            bool hasDoorDir = picked.HasValue && doorTiles != null
                && doorTiles.TryGetValue(picked.Value, out doorDir);
            bool isExitPick = !hasDoorDir && picked.HasValue && exitTiles != null
                && exitTiles.Contains(picked.Value);

            bool standingHere = picked.HasValue
                && ServiceLocator.TryGetService<IGridManager>(out var gridForGuard)
                && gridForGuard.TryGetPosition(playerGuid, out var currentPos)
                && picked.Value == currentPos;

            // Clickear la casilla en la que ya estás parado (y que no es una puerta) es
            // no-op.
            if (standingHere && !hasDoorDir && !isExitPick)
            {
                // Next-frame y no inline: acá seguimos dentro del callback de Complete()
                // del SelectionController — re-armar la selección en el mismo stack se
                // pisaría con su propio cleanup.
                Debug.Log("[ExplorationBehaviorService] Click en la casilla propia — no-op, re-armar movimiento.");
                CoroutineHost.Run(ArmMovementNextFrame());
                return;
            }

            // El spawn al entrar a una sala deja al player YA PARADO sobre la casilla
            // frente-a-puerta por la que entró (ResolveSpawnCoord). Ese era el bug
            // original: click ahí "caminaba" cero pasos (MovementService no-op, sin
            // animación) y ExecuteBehavior + el cruce salían en el mismo frame,
            // encadenando dos salas con un solo click. Acá saltamos directo al cruce
            // (sin pasar por ExecuteBehavior, que no tiene nada que mover) y el latch de
            // _crossingDoor evita que un segundo evento repita el salto.
            if (standingHere && hasDoorDir)
            {
                Debug.Log($"[ExplorationBehaviorService] Click en la puerta bajo los pies (dir={doorDir}) — cruzar directo, sin caminar.");
                _crossingDoor = true;
                CoroutineHost.Run(CrossDoorAfterArrival(playerGuid, doorDir));
                return;
            }

            if (standingHere && isExitPick)
            {
                Debug.Log("[ExplorationBehaviorService] Click en la puerta de salida bajo los pies — transicionar directo, sin caminar.");
                _crossingDoor = true;
                CoroutineHost.Run(ExitFloorAfterArrival(playerGuid));
                return;
            }

            // Caso normal: el player no está parado en la casilla elegida — camina hasta
            // ahí.
            ExecuteBehavior(behavior, playerGuid, result, null);

            // Si esa casilla es una "frente a puerta", cruzar a la sala vecina recién
            // cuando el pawn termine de caminar hasta ahí (no de forma instantánea).
            if (hasDoorDir)
            {
                Debug.Log($"[ExplorationBehaviorService] Casilla frente a puerta dir={doorDir} seleccionada — caminar y cruzar al llegar.");
                _crossingDoor = true;
                CoroutineHost.Run(CrossDoorAfterArrival(playerGuid, doorDir));
                // El re-armado lo dispara OnRoomEntered al cargar la sala vecina.
            }
            // Si es la casilla frente a la puerta de SALIDA, transicionar de piso al llegar (#158).
            else if (isExitPick)
            {
                Debug.Log("[ExplorationBehaviorService] Casilla frente a puerta de salida seleccionada — caminar y transicionar de piso.");
                _crossingDoor = true;
                CoroutineHost.Run(ExitFloorAfterArrival(playerGuid));
                // El re-armado lo dispara la primera sala del piso siguiente (OnRoomEntered).
            }
            else
            {
                // Movimiento normal dentro de la misma sala: al terminar de caminar,
                // re-armar el modo movimiento para seguir clickeando casillas sin botón.
                CoroutineHost.Run(RearmMovementAfterArrival(playerGuid));
            }
        }

        // Espera a que el pawn del player termine de caminar y recién ahí cruza la puerta.
        // Corre en el CoroutineHost porque este servicio es una clase plana. Si el pawn
        // ya estaba en la casilla (sin animación) cruza inmediatamente. No-static: toca
        // _crossingDoor en los caminos que abortan sin transición (ver comentarios abajo)
        // — el camino feliz lo limpia OnRoomEntered, disparado sincrónicamente desde
        // dentro de EnterRoomByDoor.
        private IEnumerator CrossDoorAfterArrival(Guid playerGuid, DoorDirection dir)
        {
            // Capturado ANTES del wait: si la sala cambia mientras esperamos (otra
            // transición disparada en el medio), cruzar "a ciegas" con esta dirección
            // podría llevar a una sala que ya no es la vecina correcta.
            Guid? originRoomId = null;
            if (ServiceLocator.TryGetService<IDungeonService>(out var dungeonAtStart) && dungeonAtStart != null)
                originRoomId = dungeonAtStart.CurrentRoomInstance?.InstanceId;

            if (ServiceLocator.TryGetService<IEntityVisualService>(out var visuals) && visuals != null)
            {
                var wait = visuals.WaitForMoveComplete(playerGuid);
                if (wait != null) yield return wait;
            }

            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon) || dungeon == null)
            {
                // Sin IDungeonService no hay cómo cruzar — no dejar el latch pegado ni el
                // movimiento muerto.
                _crossingDoor = false;
                ArmMovement();
                yield break;
            }

            if (originRoomId.HasValue && dungeon.CurrentRoomInstance?.InstanceId != originRoomId.Value)
            {
                Debug.LogWarning($"[ExplorationBehaviorService] Sala cambió durante el wait de cruce (dir={dir}) — abortando cruce, re-armando movimiento.");
                _crossingDoor = false;
                ArmMovement();
                yield break;
            }

            Debug.Log($"[ExplorationBehaviorService] Player llegó a la casilla frente a puerta dir={dir} — EnterRoomByDoor.");
            if (!dungeon.EnterRoomByDoor(dir))
            {
                // No pudo cruzar (ej. la puerta quedó lockeada durante el wait) — no va a
                // llegar un OnRoomEntered que limpie el latch, así que lo hacemos acá para
                // no dejar el movimiento muerto.
                Debug.LogWarning($"[ExplorationBehaviorService] EnterRoomByDoor(dir={dir}) no transicionó — re-armando movimiento.");
                _crossingDoor = false;
                ArmMovement();
            }
            // Si transicionó, EnterRoomByDoor disparó OnRoomEntered sincrónicamente, que
            // ya limpió el latch y re-armó el movimiento (ver OnRoomEntered).
        }

        // Espera a que el pawn llegue a la casilla de salida y dispara la transición de
        // piso (#158). FloorProgressionService consume OnFloorExitRequested.
        private IEnumerator ExitFloorAfterArrival(Guid playerGuid)
        {
            if (ServiceLocator.TryGetService<IEntityVisualService>(out var visuals) && visuals != null)
            {
                var wait = visuals.WaitForMoveComplete(playerGuid);
                if (wait != null) yield return wait;
            }

            if (ServiceLocator.TryGetService<IDungeonService>(out var dungeon) && dungeon != null
                && dungeon.CurrentRoomInstance != null)
            {
                var roomId = dungeon.CurrentRoomInstance.InstanceId;
                Debug.Log("[ExplorationBehaviorService] Player llegó a la puerta de salida — OnFloorExitRequested.");
                EventManager.Trigger(EventName.OnFloorExitRequested, roomId);
                // El re-armado (y la limpieza del latch) lo dispara OnRoomEntered cuando
                // carga la primera sala del piso siguiente.
            }
            else
            {
                // No se pudo resolver la sala actual — no hay a quién pedirle la salida.
                // No dejar el latch pegado ni el movimiento muerto.
                Debug.LogWarning("[ExplorationBehaviorService] ExitFloorAfterArrival sin sala actual — re-armando movimiento.");
                _crossingDoor = false;
                ArmMovement();
            }
        }

        private void ExecuteBehavior(HeroActionBehavior behavior, Guid playerGuid,
            TargetSelectionResult selectionResult, IReadOnlyList<int> diceResult,
            ComboDetectionResult? matchedCombo = null,
            int? actionRollEffectiveTotal = null,
            IReadOnlyList<int> keptDice = null,
            IReadOnlyList<int> keptDiceOriginalIndices = null)
        {
            var ctx = new HeroBehaviorContext
            {
                SourceEntity = new Entity { Guid = playerGuid },
                SelectionResult = selectionResult,
                DiceResult = diceResult,
                MatchedComboResult = matchedCombo,
                ActionRollEffectiveTotal = actionRollEffectiveTotal,
                KeptDice = keptDice,
                KeptDiceOriginalIndices = keptDiceOriginalIndices,
            };

            behavior.Execute(ctx);
        }

        private void OnPhaseEnter(params object[] args)
        {
            if (args == null || args.Length == 0) return;
            if ((GamePhase)args[0] == GamePhase.Exploration)
            {
                Debug.Log("[ExplorationBehaviorService] OnPhaseEnter(Exploration) — _state cambia a Idle.");
                _state = State.Idle;
                // Defensivo: un cruce nunca debería seguir "en vuelo" al re-entrar a
                // Exploración (ej. volviendo de combate), pero si quedó pegado por algún
                // camino no cubierto, un arranque fresco no debe heredarlo.
                _crossingDoor = false;
                // Entrar a Exploración (inicio de run, nuevo piso, o volver de combate)
                // arma el modo movimiento permanente.
                CoroutineHost.Run(ArmMovementNextFrame());
            }
        }

        // Entrar a una sala nueva (cruzar puerta, nuevo piso) re-arma el movimiento. El
        // frame de delay deja que el TileRendererRegistrar registre los tiles primero, y
        // el guard de fase en ArmMovement evita armar si la sala disparó combate.
        private void OnRoomEntered(params object[] args)
        {
            // El cruce (si lo había) ya resolvió — soltamos el latch ACÁ, antes de
            // encolar el re-armado next-frame, para que el primer click en la sala nueva
            // ya funcione. Es sincrónico y anterior al re-armado a propósito: si
            // EnterRoomByDoor dispara este evento dos veces en el mismo frame (no debería,
            // pero es barato cubrirlo), la segunda lo encuentra ya en false y solo repite
            // un ArmMovementNextFrame inocuo — ArmMovement es idempotente vía el guard de
            // _state.
            _crossingDoor = false;
            CoroutineHost.Run(ArmMovementNextFrame());
        }

        private void OnPhaseExit(params object[] args)
        {
            if (args == null || args.Length == 0) return;
            if ((GamePhase)args[0] == GamePhase.Exploration)
            {
                CancelSelection();
                if (_state == State.Rolling
                    && ServiceLocator.TryGetService<IActionRollService>(out var rollService)
                    && rollService != null
                    && rollService.IsActive)
                {
                    rollService.Cancel();
                }
                _pendingBehavior = null;
                _state = State.Inactive;
                // Teardown: si la fase se corta a mitad de un cruce (ej. una sala dispara
                // combate en el instante en que el player la pisa), no dejar el latch
                // pegado para la próxima vez que entremos a Exploración.
                _crossingDoor = false;
            }
        }
    }
}
