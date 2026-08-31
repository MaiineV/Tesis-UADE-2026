using System;
using System.Collections.Generic;
using System.Linq;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Chests;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Rooms;
using Rollgeon.Combat.Status;
using Rollgeon.Grid;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.Tiles.Forced;
using UnityEngine;

namespace Rollgeon.Combat.Skills.Push
{
    /// <inheritdoc cref="IClassSkillPushResolver"/>
    public sealed class ClassSkillPushResolver : IClassSkillPushResolver, IPreloadableService
    {
        /// <summary>
        /// Eslabones máximos de una cadena. Muy por debajo del <c>ChainBudget</c> del motor de
        /// casillas: una fila de 16 enemigos en línea ya es un caso de laboratorio.
        /// </summary>
        public const int MaxChainDepth = 16;

        /// <summary>Después de ForcedMovement (81).</summary>
        public int Priority => 82;

        public void Register()
        {
            ServiceLocator.AddService<IClassSkillPushResolver>(this, ServiceScope.Global);
            ServiceLocator.AddService<ClassSkillPushResolver>(this, ServiceScope.Global);
        }

        /// <inheritdoc />
        public PushOutcome Resolve(Guid pusher, Guid target, int distance, int collisionDamage, int stunTurns = 1)
        {
            var outcome = new PushOutcome();
            if (pusher == Guid.Empty || target == Guid.Empty || distance <= 0) return outcome;

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return outcome;
            if (!grid.TryGetPosition(pusher, out var pusherCoord)) return outcome;
            if (!grid.TryGetPosition(target, out var targetCoord)) return outcome;

            if (!ServiceLocator.TryGetService<IForcedMovementService>(out var forced) || forced == null)
            {
                Debug.LogWarning("[ClassSkillPushResolver] IForcedMovementService no registrado — el empuje no " +
                                 "mueve a nadie. Agregá ForcedMovementServiceBootstrap a ExtraServices.");
                return outcome;
            }

            // Con Range 1 y métrica Manhattan el objetivo está ortogonalmente pegado, así que el
            // delta es un cardinal exacto (mismo razonamiento que AINode_CajeroShove). Con un
            // target multi-celda el ANCLA puede quedar en diagonal: la dirección se toma
            // contra la celda del rectángulo más cercana al pusher (para 1×1 es la misma).
            var nearestCell = targetCoord;
            int nearestDist = int.MaxValue;
            foreach (var cell in grid.OccupiedCells(target))
            {
                int d = pusherCoord.Manhattan(cell);
                if (d < nearestDist) { nearestDist = d; nearestCell = cell; }
            }
            outcome.Direction = CardinalExtensions.FromDelta(pusherCoord, nearestCell);

            var visited = new HashSet<Guid>();
            PushChain(outcome, grid, forced, pusher, target, outcome.Direction, distance, collisionDamage,
                Math.Max(1, stunTurns), visited, depth: 0);

            if (outcome.Hops.Count > 0)
                Debug.Log($"[ClassSkillPushResolver] {outcome.Direction} x{distance}: " +
                          string.Join(" | ", outcome.Hops));

            return outcome;
        }

        private void PushChain(PushOutcome outcome, IGridManager grid, IForcedMovementService forced,
            Guid pusher, Guid entity, Cardinal dir, int distance, int collisionDamage, int stunTurns,
            HashSet<Guid> visited, int depth)
        {
            if (distance <= 0 || depth >= MaxChainDepth) return;
            // Una unidad se empuja una sola vez por resolución: corta loops de portal A↔B.
            if (!visited.Add(entity)) return;

            if (!grid.TryGetPosition(entity, out var from))
            {
                outcome.Hops.Add(new PushHop(entity, default, default, distance, 0, PushHopStop.NotOnGrid));
                return;
            }

            var move = forced.Push(entity, dir, distance, pusher);

            if (move.TargetDied || move.StoppedBy == ForcedMoveStop.Death)
            {
                // Murió por el camino (pinchos, fuego): no hay contra qué chocar.
                outcome.Hops.Add(new PushHop(entity, from, move.FinalCoord, distance, move.TilesTraveled,
                    PushHopStop.Died, pushedDied: true));
                return;
            }

            if (move.StoppedBy == ForcedMoveStop.PortalBlocked)
            {
                outcome.Hops.Add(new PushHop(entity, from, move.FinalCoord, distance, move.TilesTraveled,
                    PushHopStop.PortalBlocked));
                return;
            }

            if (move.StoppedBy != ForcedMoveStop.Obstacle)
            {
                outcome.Hops.Add(new PushHop(entity, from, move.FinalCoord, distance, move.TilesTraveled,
                    PushHopStop.Completed));
                return;
            }

            // TilesTraveled incluye deslizamientos y pasos de portal: un slide que se pasa deja
            // el remanente en 0 y la cadena termina después del choque.
            int remaining = Math.Max(0, distance - move.TilesTraveled);
            var blocker = move.BlockerGuid;

            switch (Classify(blocker, pusher))
            {
                case BlockerKind.Wall:
                case BlockerKind.NonBreakable:
                {
                    bool stunned = Stun(entity, stunTurns);
                    outcome.Hops.Add(new PushHop(entity, from, move.FinalCoord, distance, move.TilesTraveled,
                        blocker == Guid.Empty ? PushHopStop.Wall : PushHopStop.NonBreakableProp,
                        blocker, pushedStunned: stunned));
                    return;
                }

                case BlockerKind.Breakable:
                {
                    int dmg = Deal(pusher, entity, collisionDamage, out bool pushedDied);
                    bool broke = Break(pusher, blocker, out bool blockerDied);
                    // El empujado NO avanza a la celda liberada: frena contra el obstáculo.
                    outcome.Hops.Add(new PushHop(entity, from, move.FinalCoord, distance, move.TilesTraveled,
                        PushHopStop.BreakableObstacle, blocker, dmg, 0,
                        pushedDied: pushedDied, blockerBroken: broke, blockerDied: blockerDied));
                    return;
                }

                case BlockerKind.Enemy:
                {
                    int dmgPushed = Deal(pusher, entity, collisionDamage, out bool pushedDied);
                    int dmgBlocker = Deal(pusher, blocker, collisionDamage, out bool blockerDied);
                    outcome.Hops.Add(new PushHop(entity, from, move.FinalCoord, distance, move.TilesTraveled,
                        PushHopStop.Enemy, blocker, dmgPushed, dmgBlocker,
                        pushedDied: pushedDied, blockerDied: blockerDied));

                    // La muerte del empujado no frena el impulso que ya transfirió; la del
                    // bloqueador sí (ya no está en la grilla).
                    if (!blockerDied && remaining > 0)
                        PushChain(outcome, grid, forced, pusher, blocker, dir, remaining, collisionDamage,
                            stunTurns, visited, depth + 1);
                    return;
                }
            }
        }

        // ------------------------------------------------------------------
        // Clasificación del bloqueador
        // ------------------------------------------------------------------

        private enum BlockerKind { Wall, NonBreakable, Breakable, Enemy }

        private static BlockerKind Classify(Guid blocker, Guid pusher)
        {
            // Celda no transitable sin ocupante: pared o borde de la sala.
            if (blocker == Guid.Empty) return BlockerKind.Wall;

            // El jugador nunca es objetivo, pero un portal puede devolver la cadena hacia él:
            // se comporta como algo sólido, sin daño.
            if (blocker == pusher) return BlockerKind.NonBreakable;

            // Los cofres TIENEN Health (para el mimic) — hay que descartarlos antes del chequeo
            // de vida, si no se los trataría como enemigos empujables.
            if (ServiceLocator.TryGetService<IChestRegistry>(out var chests) && chests != null
                && chests.IsChest(blocker))
                return BlockerKind.NonBreakable;

            // Objetos de sala (bombas del Croupier, dados de La Generala): el único obstáculo
            // rompible. EntityQueryService los clasifica como Enemies, así que la fuente de
            // verdad es el registro del cleanup service.
            if (ServiceLocator.TryGetService<IRoomObjectCleanupService>(out var roomObjects) && roomObjects != null
                && roomObjects.Tracked.Contains(blocker))
                return BlockerKind.Breakable;

            // Enemigo = entidad registrada con vida. IsRegistered primero: los guids sintéticos
            // de PropTileBlocker no existen en el AttributesManager y GetAttribute loguea.
            if (ServiceLocator.TryGetService<AttributesManager>(out var attrs) && attrs != null
                && attrs.IsRegistered(blocker))
            {
                var hp = attrs.GetAttribute<Health>(blocker);
                if (hp != null && hp.Value > 0) return BlockerKind.Enemy;
            }

            return BlockerKind.NonBreakable;
        }

        // ------------------------------------------------------------------
        // Efectos del choque
        // ------------------------------------------------------------------

        private static bool Stun(Guid entity, int turns)
        {
            if (!ServiceLocator.TryGetService<IStunService>(out var stun) || stun == null)
            {
                Debug.LogWarning("[ClassSkillPushResolver] IStunService no registrado — el choque contra pared " +
                                 "no hace perder el turno.");
                return false;
            }
            stun.ApplyStun(entity, turns);
            return true;
        }

        /// <summary>
        /// Daño de choque por el pipeline normal. <c>Environmental</c>: sin debilidad ni bonos
        /// planos de ataque (el empuje no es un golpe de combo); <c>SourceId = pusher</c> conserva
        /// el crédito de la kill para el jugador.
        /// </summary>
        private static int Deal(Guid pusher, Guid target, int amount, out bool died)
        {
            died = false;
            if (amount <= 0 || target == Guid.Empty) return 0;
            if (!ServiceLocator.TryGetService<IDamagePipeline>(out var pipeline) || pipeline == null)
            {
                Debug.LogWarning("[ClassSkillPushResolver] IDamagePipeline no registrado — el choque no cobra.");
                return 0;
            }

            var ctx = pipeline.Resolve(new DamageContext
            {
                SourceId = pusher,
                TargetId = target,
                BaseDamage = amount,
                Kind = AttackKind.Environmental,
            });
            died = ctx.WasLethal;
            return ctx.FinalDamage;
        }

        /// <summary>
        /// Rompe el obstáculo con una resolución letal por el pipeline (vida + escudo). Así el
        /// <c>CombatDeathWatcher</c> lo desregistra de la grilla en el acto, corre su secuencia de
        /// muerte y el <c>CollectBroken</c> del dueño ve una muerte normal (hazard al romperse).
        /// </summary>
        private static bool Break(Guid pusher, Guid obstacle, out bool died)
        {
            died = false;
            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null) return false;
            if (!ServiceLocator.TryGetService<IDamagePipeline>(out var pipeline) || pipeline == null) return false;
            if (!attrs.IsRegistered(obstacle)) return false;

            var hp = attrs.GetAttribute<Health>(obstacle);
            if (hp == null || hp.Value <= 0) return false;
            int shield = attrs.GetAttribute<Shield>(obstacle)?.Value ?? 0;

            var ctx = pipeline.Resolve(new DamageContext
            {
                SourceId = pusher,
                TargetId = obstacle,
                BaseDamage = hp.Value + shield,
                Kind = AttackKind.Environmental,
            });

            if (!ctx.WasLethal)
            {
                // Algún IIncomingDamageMultiplierProvider recortó el golpe: segunda pasada con lo
                // que quedó. Si tampoco alcanza, el obstáculo sobrevive y se reporta.
                var rest = attrs.IsRegistered(obstacle) ? attrs.GetAttribute<Health>(obstacle)?.Value ?? 0 : 0;
                if (rest > 0)
                    ctx = pipeline.Resolve(new DamageContext
                    {
                        SourceId = pusher,
                        TargetId = obstacle,
                        BaseDamage = rest,
                        Kind = AttackKind.Environmental,
                    });
            }

            died = ctx.WasLethal;
            return ctx.WasLethal;
        }
    }
}
