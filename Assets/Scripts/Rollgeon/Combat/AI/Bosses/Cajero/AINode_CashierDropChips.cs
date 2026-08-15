using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Cashier;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// "Suelta": si al Cajero le pegaron desde su turno anterior, tira <see cref="Count"/> ficha(s)
    /// de <see cref="MinValue"/>-<see cref="MaxValue"/> de oro <b>dentro de la columna que acaba de
    /// marcar</b>, a <see cref="MinDistanceFromPlayer"/>-<see cref="MaxDistanceFromPlayer"/> casillas
    /// del jugador. La ficha dura lo que su <see cref="HazardDefinitionSO.DurationRounds"/>: se
    /// agarra ya o rueda de vuelta a la caja.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>La ficha es un hazard, no un pickup nuevo.</b> No existen pickups por tile, pero
    /// <c>IHazardService</c> ya sabe hacer exactamente esto: un hazard con
    /// <c>Trigger = OnEnter</c>, <c>Damage = 0</c>, <c>ConsumeOnTrigger = true</c> y
    /// <c>DurationRounds = 1</c> se dispara cuando el jugador pisa la casilla (escaneando todo el
    /// path, no sólo el destino), se consume y expira solo. El pago lo hace
    /// <c>CashierLedgerService</c> escuchando <c>OnHazardTriggered</c>. Cero sistemas nuevos.
    /// </para>
    /// <para>
    /// <b>Va después del nodo de marca</b> en el Sequence: lee el área pendiente de
    /// <c>IThreatenedAreaService</c>, que es justamente la columna de este turno. La plata cae donde
    /// va a caer el hacha — eso es el diseño, no un bug.
    /// </para>
    /// <para>
    /// <b>Devuelve Failed cuando no hay nada que soltar</b> (no le pegaron, no hay columna, no hay
    /// casilla válida). Es un Failed benigno: en el árbol va en <c>Selector[DropChips, Wait]</c>,
    /// como el KeepDistance — suelto en el Sequence le abortaría el turno al jefe.
    /// </para>
    /// <para>
    /// <b>Corre todos los turnos, pero sólo paga en los de columna.</b> El jefe alterna marcar y
    /// disparar, así que la mitad de los ticks encuentran el área vacía y salen por Failed sin
    /// tocar el flag de daño. El golpe que el jugador metió en un turno de disparo se cobra en el
    /// turno de columna siguiente: se pierde el timing, nunca la ficha.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_CashierDropChips : AIActionNode
    {
        [Tooltip("Definición del hazard-ficha (OnEnter, Damage 0, ConsumeOnTrigger, DurationRounds 1).")]
        public HazardDefinitionSO Chip;

        [Tooltip("Fichas a soltar por turno. Ficha del jefe: 1 por golpe recibido.")]
        [MinValue(1)]
        public int Count = 1;

        [Tooltip("Valor mínimo en oro de una ficha (antes del multiplicador post-arqueo).")]
        [MinValue(0)]
        public int MinValue = 6;

        [Tooltip("Valor máximo en oro de una ficha, inclusive (antes del multiplicador post-arqueo).")]
        [MinValue(0)]
        public int MaxValue = 9;

        [Tooltip("Distancia Manhattan mínima al jugador. Nunca cae debajo de sus pies.")]
        [MinValue(1)]
        public int MinDistanceFromPlayer = 2;

        [Tooltip("Distancia Manhattan máxima al jugador. Si no hay casilla en la banda, cae en la " +
                 "más cercana que respete el mínimo.")]
        [MinValue(1)]
        public int MaxDistanceFromPlayer = 3;

        [Tooltip("Si está activo, sólo suelta ficha en turnos en que el jefe recibió daño.")]
        public bool RequireDamageTaken = true;

        public override string NodeName => $"Cashier Drop Chips ({Count} × {MinValue}-{MaxValue}g)";

        public override AIResult Tick(AIContext context)
        {
            if (context == null || context.SelfGuid == Guid.Empty) return AIResult.Failed;
            if (Chip == null) return AIResult.Failed;

            var grid = context.Grid;
            if (grid == null) return AIResult.Failed;
            if (!grid.TryGetPosition(context.PlayerGuid, out var playerCoord)) return AIResult.Failed;

            // Se crea acá aunque todavía no vaya a soltar nada: es el nodo del Cajero que corre
            // todos los turnos, y el reloj del rastrillo necesita al servicio escuchando rondas
            // desde el principio, no recién cuando el jugador le pegue.
            var ledger = CashierLedgerService.ResolveOrCreate();

            if (!ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat) || threat == null)
                return AIResult.Failed;
            if (!ServiceLocator.TryGetService<IHazardService>(out var hazards) || hazards == null)
            {
                Debug.LogWarning("[AINode_CashierDropChips] IHazardService no registrado — sin él la " +
                                 "ficha no existe como casilla. Agregá HazardServiceBootstrap.");
                return AIResult.Failed;
            }

            // El flag se consume DESPUÉS de saber que hay columna: en los turnos de disparo el
            // jefe no marca nada, y consumirlo antes se comía el golpe que el jugador acababa de
            // pagar con su turno — una de cada dos fichas desaparecía sin caer al piso.
            var column = threat.GetPendingTiles(context.SelfGuid);
            if (column == null || column.Count == 0) return AIResult.Failed;

            if (RequireDamageTaken && !ledger.ConsumeDamageTaken(context.SelfGuid)) return AIResult.Failed;

            var used = new HashSet<GridCoord>();
            int dropped = 0;
            var rng = context.Rng ?? new System.Random();

            for (int i = 0; i < Count; i++)
            {
                if (!TryPickTile(grid, column, playerCoord, used, rng, out var coord)) break;
                used.Add(coord);

                var instanceId = hazards.Activate(Chip, new[] { coord });
                if (instanceId == Guid.Empty) continue;

                ledger.RegisterChip(instanceId, RollValue(rng, ledger.ChipValueMultiplier), context.SelfGuid);
                dropped++;
            }

            return dropped > 0 ? AIResult.Succeeded : AIResult.Failed;
        }

        /// <summary>
        /// Valor de una ficha: tirada inclusiva en [<see cref="MinValue"/>, <see cref="MaxValue"/>]
        /// escalada por el multiplicador del arqueo. Se congela al soltarla (no al cobrarla) para
        /// que un arqueo posterior no revalúe fichas ya en el piso.
        /// </summary>
        private int RollValue(System.Random rng, int multiplier)
        {
            int min = Mathf.Min(MinValue, MaxValue);
            int max = Mathf.Max(MinValue, MaxValue);
            int roll = min == max ? min : rng.Next(min, max + 1);
            return roll * (multiplier < 1 ? 1 : multiplier);
        }

        /// <summary>
        /// Elige casilla dentro de la columna: primero la banda [Min, Max] de distancia al jugador;
        /// si está vacía, la casilla libre más cercana que igual respete el mínimo (mejor una ficha
        /// un poco más lejos que un turno sin pagar). Nunca elige la casilla del jugador ni una
        /// ocupada.
        /// </summary>
        private bool TryPickTile(
            IGridManager grid,
            IReadOnlyCollection<GridCoord> column,
            GridCoord playerCoord,
            HashSet<GridCoord> used,
            System.Random rng,
            out GridCoord picked)
        {
            picked = default;

            var inBand = new List<GridCoord>();
            var fallback = new List<GridCoord>();

            foreach (var coord in column)
            {
                if (used.Contains(coord)) continue;
                if (!grid.InBounds(coord) || !grid.IsFree(coord)) continue;

                int dist = coord.Manhattan(playerCoord);
                if (dist < MinDistanceFromPlayer) continue;

                if (dist <= MaxDistanceFromPlayer) inBand.Add(coord);
                else fallback.Add(coord);
            }

            var pool = inBand.Count > 0 ? inBand : fallback;
            if (pool.Count == 0) return false;

            // Orden estable antes de tirar el dado: los sets de tiles no garantizan orden de
            // iteración, y sin esto el mismo seed elegiría casillas distintas entre corridas.
            pool.Sort(CompareCoord);

            if (pool == fallback)
            {
                picked = pool[0];
                int best = picked.Manhattan(playerCoord);
                foreach (var coord in pool)
                {
                    int dist = coord.Manhattan(playerCoord);
                    if (dist >= best) continue;
                    best = dist;
                    picked = coord;
                }
                return true;
            }

            picked = pool[rng.Next(pool.Count)];
            return true;
        }

        private static int CompareCoord(GridCoord a, GridCoord b)
        {
            int c = a.X.CompareTo(b.X);
            return c != 0 ? c : a.Y.CompareTo(b.Y);
        }
    }
}
