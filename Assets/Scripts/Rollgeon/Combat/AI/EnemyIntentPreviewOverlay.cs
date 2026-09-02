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
    /// Pinta en la grilla lo que un enemigo tiene en curso y lo que va a hacer, mientras el mouse
    /// está encima de él.
    /// </summary>
    /// <remarks>
    /// El dibujo sale del hover y no del turno del enemigo: su turno dura segundos y ahí nadie
    /// lee. Lo que pinta no se re-simula nunca — son las áreas que el enemigo ya dejó
    /// comprometidas, la casilla del jugador para un ataque a distancia, y el alcance del arma
    /// leído de los gates que el árbol ya declara (<see cref="EnemyAttackReach"/>): estático,
    /// sin contar su movimiento.
    /// </remarks>
    public sealed class EnemyIntentPreviewOverlay
    {
        /// <summary>Canal de lo que ya está puesto.</summary>
        private const string StandingChannel = "hover.now";

        /// <summary>Canal de lo que viene en el próximo turno.</summary>
        private const string NextChannel = "hover.next";

        /// <summary>Canal del alcance del arma — hasta dónde llega, no dónde va a caer.</summary>
        private const string RangeChannel = "hover.range";

        private static EnemyIntentPreviewOverlay s_instance;

        private readonly List<AIIntent> _standing = new();
        private readonly List<AIIntent> _next = new();
        private readonly HashSet<GridCoord> _standingCells = new();
        private readonly HashSet<GridCoord> _nextCells = new();
        private readonly HashSet<GridCoord> _reachCells = new();

        private Guid _painted;
        private Guid _paintedSubject;
        private bool _hasSubject;

        /// <summary>
        /// Público como el de <see cref="ThreatTelegraphOverlay"/>: producción entra por
        /// <see cref="ResolveOrCreate"/>, y quien necesite una instancia limpia la construye.
        /// </summary>
        public EnemyIntentPreviewOverlay()
        {
            EventManager.Subscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.Subscribe(EventName.OnCombatEnd, HandleScopeEnded);
            EventManager.Subscribe(EventName.OnRunEnd, HandleScopeEnded);
        }

        public static EnemyIntentPreviewOverlay ResolveOrCreate()
            => s_instance ??= new EnemyIntentPreviewOverlay();

        public static Guid StandingSource(Guid enemyId)
            => AINode_AuxTelegraph.ChannelGuid(enemyId, StandingChannel);

        public static Guid NextSource(Guid enemyId)
            => AINode_AuxTelegraph.ChannelGuid(enemyId, NextChannel);

        public static Guid RangeSource(Guid enemyId)
            => AINode_AuxTelegraph.ChannelGuid(enemyId, RangeChannel);

        /// <summary>
        /// Pinta lo que es del enemigo mismo: sus marcas y su próximo ataque. Lo que generan sus
        /// objetos (las bombas) no entra — cada uno tiene su propio hover.
        /// </summary>
        public void Show(Guid enemyId) => Paint(enemyId, Guid.Empty, hasSubject: false);

        /// <summary>
        /// Pinta sólo lo que sale de <paramref name="subjectGuid"/> — la cruz de una bomba y no
        /// las de sus tres hermanas.
        /// </summary>
        public void ShowForSubject(Guid enemyId, Guid subjectGuid)
            => Paint(enemyId, subjectGuid, hasSubject: true);

        public void Clear()
        {
            if (_painted == Guid.Empty) return;

            var overlay = ThreatTelegraphOverlay.ResolveOrCreate();
            overlay?.Clear(StandingSource(_painted));
            overlay?.Clear(NextSource(_painted));
            overlay?.Clear(RangeSource(_painted));
            _painted = Guid.Empty;
        }

        private void Paint(Guid enemyId, Guid subjectGuid, bool hasSubject)
        {
            // El mouse pudo saltar de un enemigo a otro sin pasar por el vacío.
            Clear();
            if (enemyId == Guid.Empty) return;

            _paintedSubject = subjectGuid;
            _hasSubject = hasSubject;

            if (!ServiceLocator.TryGetService<IEnemyIntentService>(out var intents) || intents == null)
                return;
            if (!intents.TryRead(enemyId, _standing, _next)) return;

            CollectCells(_standing, _standingCells, enemyId, subjectGuid, hasSubject);
            CollectCells(_next, _nextCells, enemyId, subjectGuid, hasSubject);

            // Sin esto, una celda que está en las dos listas se lleva DOS quads apilados y se lee
            // como una casilla distinta. Un quad es un solo aviso, así que en el empate se pierde
            // uno de los dos: gana lo que ya está puesto, que es lo seguro — el próximo ataque
            // todavía puede caer en otro lado, la marca que el jefe ya congeló no.
            _nextCells.ExceptWith(_standingCells);

            // El alcance es del arma del enemigo, no de sus objetos: el hover de una bomba
            // cuenta su cruz y nada más. Y de los tres avisos es el menos urgente, así que en
            // el empate por una celda pierde contra los otros dos.
            _reachCells.Clear();
            if (!hasSubject) intents.TryReadReach(enemyId, _reachCells);
            _reachCells.ExceptWith(_standingCells);
            _reachCells.ExceptWith(_nextCells);

            var overlay = ThreatTelegraphOverlay.ResolveOrCreate();
            if (overlay == null) return;

            if (_standingCells.Count > 0)
                overlay.Show(StandingSource(enemyId), _standingCells, ThreatOverlayState.Marked);
            if (_nextCells.Count > 0)
                overlay.Show(NextSource(enemyId), _nextCells, ThreatOverlayState.Incoming);
            if (_reachCells.Count > 0)
                overlay.Show(RangeSource(enemyId), _reachCells, ThreatOverlayState.Incoming,
                             ThreatTelegraphOverlay.ReachTint);

            _painted = enemyId;
        }

        private static void CollectCells(List<AIIntent> intents, HashSet<GridCoord> into,
                                         Guid enemyId, Guid subjectGuid, bool hasSubject)
        {
            into.Clear();
            foreach (var intent in intents)
            {
                if (hasSubject)
                {
                    if (intent.SubjectGuid != subjectGuid) continue;
                }
                // Mismo reparto que la card NEXT TURN: sobre el cuerpo se lee lo del enemigo, y lo
                // que sale de sus objetos (las cruces de sus bombas) lo cuenta el hover del objeto.
                else if (intent.SubjectGuid != Guid.Empty && intent.SubjectGuid != enemyId)
                {
                    continue;
                }

                foreach (var coord in intent.Tiles) into.Add(coord);
            }
        }

        // Con el mouse quieto sobre el jefe, su turno dejaría en pantalla una predicción que ya
        // caducó. Se apaga mientras le toca a él y vuelve cuando el turno es del jugador otra vez.
        private void HandleTurnStarted(params object[] args)
        {
            if (args == null || args.Length == 0 || !(args[0] is Guid acting)) return;

            var playerGuid = ServiceLocator.TryGetService<IPlayerService>(out var players)
                ? players?.PlayerGuid ?? Guid.Empty
                : Guid.Empty;

            if (acting != playerGuid)
            {
                var hovered = _painted;
                Clear();
                _painted = Guid.Empty;
                _pendingRepaint = hovered;
                return;
            }

            if (_pendingRepaint == Guid.Empty) return;
            var target = _pendingRepaint;
            _pendingRepaint = Guid.Empty;
            Paint(target, _paintedSubject, _hasSubject);
        }

        private Guid _pendingRepaint;

        private void HandleScopeEnded(params object[] args)
        {
            Clear();
            _pendingRepaint = Guid.Empty;
        }
    }
}
