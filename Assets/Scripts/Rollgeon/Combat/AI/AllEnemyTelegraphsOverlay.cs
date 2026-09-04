using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Rollgeon.Player;

namespace Rollgeon.Combat.AI
{
    /// <summary>
    /// Pinta SIEMPRE (durante el turno del jugador) los telegraphs de TODOS los enemigos
    /// del combate: lo que ya dejaron marcado (rayado) y lo que viene el próximo turno
    /// (sólido). Pedido de playtest 04/09: leer el paño de un vistazo, sin hoverear
    /// enemigo por enemigo. Invierte la regla Mewgenics original de "marca y no pinta"
    /// de <see cref="AINode_TelegraphMark"/>.
    /// </summary>
    /// <remarks>
    /// Hermano de <see cref="AllEnemyRangesOverlay"/> (que sigue siendo el toggle ALT,
    /// solo para el ALCANCE — el aviso menos urgente y el más ruidoso). Mismo patrón:
    /// canales propios por enemigo para no pisar los quads del hover individual
    /// (<see cref="EnemyIntentPreviewOverlay"/> — el hover sobre un enemigo APILA su
    /// pintado encima y funciona como highlight), pintado suspendido durante turnos
    /// enemigos y repintado al volver el del jugador.
    /// </remarks>
    public sealed class AllEnemyTelegraphsOverlay
    {
        private const string StandingChannel = "always.now";
        private const string NextChannel = "always.next";
        private const string ReachChannel = "always.reach";

        private static AllEnemyTelegraphsOverlay s_instance;

        private readonly List<AIIntent> _standing = new();
        private readonly List<AIIntent> _next = new();
        private readonly HashSet<GridCoord> _standingCells = new();
        private readonly HashSet<GridCoord> _nextCells = new();
        private readonly List<Guid> _painted = new();

        private bool _bound;

        public static AllEnemyTelegraphsOverlay ResolveOrCreate()
            => s_instance ??= new AllEnemyTelegraphsOverlay();

        public static Guid StandingSource(Guid enemyId)
            => AINode_AuxTelegraph.ChannelGuid(enemyId, StandingChannel);

        public static Guid NextSource(Guid enemyId)
            => AINode_AuxTelegraph.ChannelGuid(enemyId, NextChannel);

        public static Guid ReachSource(Guid enemyId)
            => AINode_AuxTelegraph.ChannelGuid(enemyId, ReachChannel);

        /// <summary>Lo llama <c>CombatHUDView.BindAll</c>, junto al overlay de ALT.</summary>
        public void Bind()
        {
            if (_bound) Unbind();

            EventManager.Subscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.Subscribe(EventName.OnCombatEnd, HandleScopeEnded);
            EventManager.Subscribe(EventName.OnRunEnd, HandleScopeEnded);
            EventManager.Subscribe(EventName.OnEntityDestroyed, HandleEntityDestroyed);

            _bound = true;
            Repaint();
        }

        public void Unbind()
        {
            if (!_bound) return;

            EventManager.UnSubscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.UnSubscribe(EventName.OnCombatEnd, HandleScopeEnded);
            EventManager.UnSubscribe(EventName.OnRunEnd, HandleScopeEnded);
            EventManager.UnSubscribe(EventName.OnEntityDestroyed, HandleEntityDestroyed);

            ClearPainted();
            _bound = false;
        }

        /// <summary>Repinta contra el orden de turnos actual.</summary>
        public void Repaint()
        {
            if (!ServiceLocator.TryGetService<TurnOrderService>(out var turnOrder) || turnOrder == null)
                return;
            Repaint(turnOrder.OrderForRound);
        }

        /// <summary>Seam de EditMode, mismo criterio que <see cref="AllEnemyRangesOverlay"/>.</summary>
        internal void Repaint(IReadOnlyList<Guid> order)
        {
            ClearPainted();
            if (!_bound || order == null) return;

            if (!ServiceLocator.TryGetService<IEnemyIntentService>(out var intents) || intents == null)
                return;

            var overlay = ThreatTelegraphOverlay.ResolveOrCreate();
            if (overlay == null) return;

            // La casilla actual del jugador: un intent cuyos tiles son SOLO esa casilla
            // no es un telegraph — es el marcador "te va a pegar a vos" del melee/disparo
            // genérico, que se mueve con vos. Pintado siempre, dejaba tu propio tile
            // marcado por cada Guardian de la sala (bug de playtest 04/09).
            var playerCell = ResolvePlayerCell();

            foreach (var id in order)
            {
                if (!intents.TryRead(id, _standing, _next)) continue;

                // Todo lo del enemigo Y de sus objetos (las cruces de sus bombas): esta
                // vista es el paño completo, no el reparto por hover del preview.
                CollectCells(_standing, _standingCells);
                CollectCells(_next, _nextCells, skipPlayerTracker: true, playerCell);

                // Mismo dedupe que el preview: la marca congelada le gana a la predicción.
                _nextCells.ExceptWith(_standingCells);

                bool any = false;
                if (_standingCells.Count > 0)
                {
                    overlay.Show(StandingSource(id), _standingCells, ThreatOverlayState.Marked);
                    any = true;
                }
                if (_nextCells.Count > 0)
                {
                    overlay.Show(NextSource(id), _nextCells, ThreatOverlayState.Incoming);
                    any = true;
                }

                // Sin área comprometida ni anunciada NO se pinta nada: el rango "por si
                // acaso" de todos los enemigos todo el tiempo era ruido (playtest 04/09)
                // — el alcance sigue siendo territorio de ALT y del hover individual.

                if (any) _painted.Add(id);
            }
        }

        private static void CollectCells(List<AIIntent> intents, HashSet<GridCoord> into,
                                         bool skipPlayerTracker = false, GridCoord? playerCell = null)
        {
            into.Clear();
            foreach (var intent in intents)
            {
                if (skipPlayerTracker && playerCell.HasValue && TracksPlayerOnly(intent, playerCell.Value))
                    continue;
                foreach (var coord in intent.Tiles)
                    into.Add(coord);
            }
        }

        // "Solo la casilla del jugador" = marcador de objetivo, no un área comprometida.
        private static bool TracksPlayerOnly(in AIIntent intent, GridCoord playerCell)
        {
            bool anyTile = false;
            foreach (var coord in intent.Tiles)
            {
                anyTile = true;
                if (coord != playerCell) return false;
            }
            return anyTile;
        }

        private static GridCoord? ResolvePlayerCell()
        {
            if (!ServiceLocator.TryGetService<IPlayerService>(out var players) || players == null)
                return null;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null)
                return null;
            return grid.TryGetPosition(players.PlayerGuid, out var cell) ? cell : (GridCoord?)null;
        }

        private void ClearPainted()
        {
            if (_painted.Count == 0) return;

            var overlay = ThreatTelegraphOverlay.ResolveOrCreate();
            if (overlay != null)
            {
                foreach (var id in _painted)
                {
                    overlay.Clear(StandingSource(id));
                    overlay.Clear(NextSource(id));
                    overlay.Clear(ReachSource(id));
                }
            }
            _painted.Clear();
        }

        // Durante el turno enemigo la foto está a medio revelar (mismo motivo que el
        // preview del hover): se apaga y se repinta fresca al volver el turno del jugador.
        private void HandleTurnStarted(params object[] args)
        {
            if (args == null || args.Length == 0 || !(args[0] is Guid acting)) return;

            var playerGuid = ServiceLocator.TryGetService<IPlayerService>(out var players)
                ? players?.PlayerGuid ?? Guid.Empty
                : Guid.Empty;

            if (acting == playerGuid) Repaint();
            else ClearPainted();
        }

        private void HandleEntityDestroyed(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (!_painted.Remove(guid)) return;

            var overlay = ThreatTelegraphOverlay.ResolveOrCreate();
            overlay?.Clear(StandingSource(guid));
            overlay?.Clear(NextSource(guid));
            overlay?.Clear(ReachSource(guid));
        }

        private void HandleScopeEnded(params object[] args) => ClearPainted();
    }
}
