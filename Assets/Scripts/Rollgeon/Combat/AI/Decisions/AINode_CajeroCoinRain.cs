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
    /// La sala suelta plata: <see cref="Count"/> monedas repartidas por la sala cada
    /// <see cref="EveryNRounds"/> rondas. Es el reloj de la pelea del Cajero — juntarlas obliga al
    /// jugador a caminar la sala con él persiguiéndolo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Vive en el árbol del jefe aunque las monedas sean de la sala: es el único tick por ronda que
    /// hay disponible, y colgarlo de un servicio propio duplicaría el reloj.
    /// </para>
    /// <para>
    /// El vencimiento no es de este nodo: cada moneda nace permanente y la expira
    /// <see cref="AINode_CajeroCoinVault"/>, que es el único que puede distinguir una cobrada de
    /// una vencida. Su Failed (todavía no toca tanda) es benigno y va en
    /// <c>Selector[CoinRain, Wait]</c>.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_CajeroCoinRain : AIActionNode
    {
        [Tooltip("Definición del hazard-moneda. Sin ella el nodo no hace nada.")]
        public HazardDefinitionSO Coin;

        [Tooltip("Monedas por tanda.")]
        [MinValue(0)]
        public int Count = 4;

        [Tooltip("Rondas entre tandas. 1 = todas las rondas.")]
        [MinValue(1)]
        public int EveryNRounds = 3;

        [Tooltip("Valor mínimo en oro de una moneda.")]
        [MinValue(0)]
        public int MinValue = 6;

        [Tooltip("Valor máximo en oro de una moneda, inclusive.")]
        [MinValue(0)]
        public int MaxValue = 9;

        [Tooltip("Distancia Chebyshev mínima entre dos monedas de la misma tanda. 0 = pueden caer " +
                 "pegadas. Con separación las monedas se leen como puntos a visitar y no como un montón.")]
        [MinValue(0)]
        public int MinSeparation = 2;

        // Estado de pelea. NonSerialized: vive sólo en la copia runtime del árbol, nunca en el
        // asset, así que una pelea nueva arranca con la primera tanda pendiente.
        [NonSerialized] private int _nextRound;
        [NonSerialized] private bool _rained;

        public override string NodeName =>
            $"Cajero — Monedas de sala ({Count} cada {EveryNRounds} rondas)";

        public override AIResult Tick(AIContext context)
        {
            if (context == null || Coin == null || Count <= 0) return AIResult.Failed;

            var grid = context.Grid;
            if (grid?.Graph == null) return AIResult.Failed;

            // La primera tanda cae en el primer turno del jefe: la pelea abre con plata en el piso
            // para que la primera decisión del jugador sea si va a buscarla.
            if (_rained && context.RoundIndex < _nextRound) return AIResult.Failed;

            if (!ServiceLocator.TryGetService<IHazardService>(out var hazards) || hazards == null)
            {
                Debug.LogWarning("[AINode_CajeroCoinRain] IHazardService no registrado — sin él la " +
                                 "moneda no existe como casilla. Agregá HazardServiceBootstrap.");
                return AIResult.Failed;
            }

            var rng = context.Rng ?? new System.Random();
            var tiles = PickTiles(grid, hazards, rng);
            if (tiles.Count == 0) return AIResult.Failed;

            var ledger = CashierLedgerService.ResolveOrCreate();

            int dropped = 0;
            foreach (var coord in tiles)
            {
                var instanceId = hazards.Activate(Coin, new[] { coord });
                if (instanceId == Guid.Empty) continue;

                ledger.RegisterChip(instanceId, RollValue(rng), context.SelfGuid);
                dropped++;
            }

            // El reloj sólo avanza si de verdad cayó algo: una sala sin casilla libre reintenta el
            // próximo turno en vez de saltarse la tanda entera.
            if (dropped == 0) return AIResult.Failed;

            _rained = true;
            _nextRound = context.RoundIndex + EveryNRounds;
            return AIResult.Succeeded;
        }

        /// <summary>
        /// Casillas de la tanda: libres, sin moneda encima, y repartidas. El peligro NO se filtra —
        /// una moneda sobre pinchos es contenido, y levantarla ahí cuesta lo que cuesta.
        /// </summary>
        private List<GridCoord> PickTiles(IGridManager grid, IHazardService hazards, System.Random rng)
        {
            var pool = new List<GridCoord>();
            foreach (var coord in grid.Graph.AllCoords())
            {
                // IsFree cubre al jugador y al jefe: una moneda debajo de alguien no se puede
                // levantar (la casilla dispara al ENTRAR, y ya está parado ahí).
                if (!grid.IsFree(coord)) continue;

                // Dos monedas apiladas se cobran las dos con un solo paso: los triggers de hazard
                // disparan una vez POR INSTANCIA y nada valida el solape. La tanda perdería un punto
                // al que ir sin que se note. Los pinchos son casilla especial, no hazard, así que
                // esto no bloquea la moneda sobre pinchos.
                if (hazards.TryGetHazardAt(coord, out _)) continue;

                pool.Add(coord);
            }

            // Orden estable antes de tirar el dado: el grafo no garantiza orden de iteración, y sin
            // esto el mismo seed elegiría casillas distintas entre corridas.
            pool.Sort(CompareCoord);
            Shuffle(pool, rng);

            var picked = new List<GridCoord>(Count);

            // Dos pasadas: la primera respeta la separación mínima, la segunda rellena si la sala
            // no tiene lugar para tanta distancia.
            for (int pass = 0; pass < 2 && picked.Count < Count; pass++)
            {
                int separation = pass == 0 ? MinSeparation : 0;
                foreach (var coord in pool)
                {
                    if (picked.Count >= Count) break;
                    if (picked.Contains(coord)) continue;
                    if (!IsFarEnough(coord, picked, separation)) continue;
                    picked.Add(coord);
                }
            }

            return picked;
        }

        private static bool IsFarEnough(GridCoord coord, List<GridCoord> picked, int separation)
        {
            if (separation <= 0) return true;
            foreach (var other in picked)
                if (coord.Chebyshev(other) < separation) return false;
            return true;
        }

        private static void Shuffle(List<GridCoord> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static int CompareCoord(GridCoord a, GridCoord b)
        {
            int c = a.X.CompareTo(b.X);
            return c != 0 ? c : a.Y.CompareTo(b.Y);
        }

        private int RollValue(System.Random rng)
        {
            int min = Mathf.Min(MinValue, MaxValue);
            int max = Mathf.Max(MinValue, MaxValue);
            return min == max ? min : rng.Next(min, max + 1);
        }
    }
}
