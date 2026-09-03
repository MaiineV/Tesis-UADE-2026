using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Entities;
using Rollgeon.Grid;

namespace Rollgeon.Combat.Auras
{
    /// <summary>
    /// Auras defensivas declaradas en <see cref="EnemyDataSO"/> (Guardian del GDD: "aliados a
    /// ≤2 casillas reciben +defensa"). Pull on-demand, espejo de la Fortaleza: en cada golpe,
    /// si algún portador registrado sigue en la grilla (vivo — <c>CombatDeathWatcher</c>
    /// desregistra al morir), es ALIADO del target y está a ≤ radio (Manhattan rect-a-rect),
    /// el pipeline descuenta su reducción. Cero bookkeeping: el aura se apaga sola al morir
    /// o alejarse el portador.
    /// </summary>
    /// <remarks>
    /// Sin stacking: con varias auras alcanzando al target aplica la MAYOR. El portador no se
    /// protege a sí mismo (la ficha dice "aliados"). Global, y olvida portadores al terminar
    /// la pelea/run — mismo lifecycle que <c>RoomObjectArmorService</c>.
    /// </remarks>
    public sealed class EnemyAuraService : IIncomingFlatDamageReducerProvider, IDisposable
    {
        private readonly struct Aura
        {
            public readonly int Radius;
            public readonly int FlatReduction;
            public Aura(int radius, int flatReduction) { Radius = radius; FlatReduction = flatReduction; }
        }

        private readonly Dictionary<Guid, Aura> _auras = new Dictionary<Guid, Aura>();

        private EventManager.EventReceiver _onScopeEnded;
        private bool _disposed;

        public EnemyAuraService()
        {
            _onScopeEnded = ForgetAllExternal;
            EventManager.Subscribe(EventName.OnCombatEnd, _onScopeEnded);
            EventManager.Subscribe(EventName.OnRunEnd, _onScopeEnded);
        }

        /// <summary>Devuelve el registrado o crea y registra uno nuevo (Global).</summary>
        public static EnemyAuraService ResolveOrCreate()
        {
            if (ServiceLocator.TryGetService<IIncomingFlatDamageReducerProvider>(out var existing)
                && existing is EnemyAuraService aura)
            {
                return aura;
            }

            var created = new EnemyAuraService();
            ServiceLocator.AddService<IIncomingFlatDamageReducerProvider>(created, ServiceScope.Global);
            return created;
        }

        /// <summary>Registra (o actualiza) el aura del portador. Radio o reducción ≤ 0 la borra.</summary>
        public void Register(Guid ownerGuid, int radius, int flatReduction)
        {
            if (ownerGuid == Guid.Empty) return;
            if (radius <= 0 || flatReduction <= 0)
            {
                _auras.Remove(ownerGuid);
                return;
            }
            _auras[ownerGuid] = new Aura(radius, flatReduction);
        }

        public void Unregister(Guid ownerGuid) => _auras.Remove(ownerGuid);

        /// <inheritdoc />
        public int GetFlatReduction(DamageContext ctx)
        {
            if (ctx == null || ctx.TargetId == Guid.Empty) return 0;
            return BestReductionFor(ctx.TargetId);
        }

        /// <summary>
        /// Consulta pull, sin depender de que llegue un golpe: la MEJOR reducción activa que
        /// protege a <paramref name="allyGuid"/> ahora mismo. Mismo alcance/criterio que
        /// <see cref="GetFlatReduction"/> — pensado para UI que necesita mostrar "¿estoy
        /// protegido, por cuánto?" en cualquier momento (ej. un badge junto a la vida), no sólo
        /// en el instante del hit.
        /// </summary>
        public bool TryGetActiveReduction(Guid allyGuid, out int amount)
        {
            amount = BestReductionFor(allyGuid);
            return amount > 0;
        }

        /// <summary>Escaneo compartido: la mayor <c>FlatReduction</c> de un portador vivo, aliado
        /// de <paramref name="targetGuid"/> (no el propio portador) y a ≤ <c>Radius</c> rect-a-rect.</summary>
        private int BestReductionFor(Guid targetGuid)
        {
            if (_auras.Count == 0 || targetGuid == Guid.Empty) return 0;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return 0;
            if (!ServiceLocator.TryGetService<IEntityQueryService>(out var query) || query == null) return 0;
            if (!grid.TryGetPosition(targetGuid, out var targetAnchor)) return 0;

            var targetFp = grid.GetFootprint(targetGuid);
            int best = 0;
            foreach (var pair in _auras)
            {
                var owner = pair.Key;
                if (owner == targetGuid) continue; // la ficha protege ALIADOS, no al portador
                if (pair.Value.FlatReduction <= best) continue;
                // Portador fuera de la grilla = muerto (CombatDeathWatcher lo desregistra
                // sincrónicamente en el golpe letal): el aura se apaga sola.
                if (!grid.TryGetPosition(owner, out var ownerAnchor)) continue;
                if ((query.GetRelationship(owner, targetGuid) & Effects.Selection.EntityFilterMask.Allies) == 0)
                    continue;

                int dist = GridFootprint.ManhattanDistance(
                    ownerAnchor, grid.GetFootprint(owner), targetAnchor, targetFp);
                if (dist > pair.Value.Radius) continue;

                best = pair.Value.FlatReduction;
            }
            return best;
        }

        private void ForgetAllExternal(params object[] args) => _auras.Clear();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            EventManager.UnSubscribe(EventName.OnCombatEnd, _onScopeEnded);
            EventManager.UnSubscribe(EventName.OnRunEnd, _onScopeEnded);
            _auras.Clear();
        }
    }
}
