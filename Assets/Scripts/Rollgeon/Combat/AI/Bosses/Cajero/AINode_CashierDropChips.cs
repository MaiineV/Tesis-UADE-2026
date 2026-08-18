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
    /// "Suelta": en cada turno de columna el Cajero tira fichas de oro dentro de la columna que acaba
    /// de marcar, a <see cref="MinDistanceFromPlayer"/>-<see cref="MaxDistanceFromPlayer"/> del
    /// jugador: <see cref="Count"/> si le pegaron desde su turno anterior, <see cref="MinCount"/> si no.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Off-by-one de <c>DurationRounds</c>:</b> se descuenta en el <c>OnTurnQueueBuilt</c> de la
    /// ronda siguiente y la ficha nace con el turno del jugador de esa ronda ya jugado (CNF-006).
    /// Con <c>1</c> expira antes de que él pueda pisarla; "dura un turno del jugador" se autora
    /// como <c>2</c>.
    /// </para>
    /// <para>
    /// Va <b>después</b> del nodo de marca en el Sequence: lee el área pendiente de
    /// <c>IThreatenedAreaService</c>, que es la columna de este turno. Su Failed es benigno y debe
    /// ir en <c>Selector[DropChips, Wait]</c> — suelto en el Sequence abortaría el turno del jefe.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_CashierDropChips : AIActionNode
    {
        [Tooltip("Definición del hazard-ficha (OnEnter, Damage 0, ConsumeOnTrigger, DurationRounds 2 " +
                 "= dura un turno del jugador; con 1 la moneda expira antes de que él pueda pisarla).")]
        public HazardDefinitionSO Chip;

        [Tooltip("Fichas a soltar en un turno de columna en el que el jefe recibió daño.")]
        [MinValue(1)]
        public int Count = 1;

        [Tooltip("Fichas mínimas por turno de columna, incluso si no le pegaron. El jefe alterna " +
                 "marcar y disparar, así que esto es una ficha cada dos turnos suyos. 0 = sólo " +
                 "suelta cuando le pegan.")]
        [MinValue(0)]
        public int MinCount;

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

        public override string NodeName =>
            $"Cashier Drop Chips ({MinCount}→{Count} × {MinValue}-{MaxValue}g)";

        public override AIResult Tick(AIContext context)
        {
            if (context == null || context.SelfGuid == Guid.Empty) return AIResult.Failed;
            if (Chip == null) return AIResult.Failed;

            var grid = context.Grid;
            if (grid == null) return AIResult.Failed;
            if (!grid.TryGetPosition(context.PlayerGuid, out var playerCoord)) return AIResult.Failed;

            // Se crea acá aunque todavía no suelte nada: es el nodo que corre todos los turnos, y
            // el reloj del rastrillo necesita al servicio escuchando rondas desde el principio.
            var ledger = CashierLedgerService.ResolveOrCreate();

            if (!ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat) || threat == null)
                return AIResult.Failed;
            if (!ServiceLocator.TryGetService<IHazardService>(out var hazards) || hazards == null)
            {
                Debug.LogWarning("[AINode_CashierDropChips] IHazardService no registrado — sin él la " +
                                 "ficha no existe como casilla. Agregá HazardServiceBootstrap.");
                return AIResult.Failed;
            }

            // El flag se consume DESPUÉS de saber que hay columna: en los turnos de disparo el jefe
            // no marca nada, y consumirlo antes se comería el golpe que el jugador ya pagó.
            var column = threat.GetPendingTiles(context.SelfGuid);
            if (column == null || column.Count == 0) return AIResult.Failed;

            // El flag se consume igual cuando hay piso: es destructivo y no se puede pre-chequear, y
            // dejarlo puesto haría que el próximo turno de columna cobrara un golpe que ya se pagó.
            bool paid = !RequireDamageTaken || ledger.ConsumeDamageTaken(context.SelfGuid);
            int toDrop = paid ? Count : MinCount;
            if (toDrop <= 0) return AIResult.Failed;

            var used = new HashSet<GridCoord>();
            int dropped = 0;
            var rng = context.Rng ?? new System.Random();

            for (int i = 0; i < toDrop; i++)
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
        /// si está vacía, la casilla libre más cercana que igual respete el mínimo. Nunca elige la
        /// casilla del jugador ni una ocupada.
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
