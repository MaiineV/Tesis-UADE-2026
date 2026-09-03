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
            var tiles = CajeroCoinScatter.PickTiles(grid, hazards, rng, Count, MinSeparation);
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

        private int RollValue(System.Random rng)
        {
            int min = Mathf.Min(MinValue, MaxValue);
            int max = Mathf.Max(MinValue, MaxValue);
            return min == max ? min : rng.Next(min, max + 1);
        }
    }
}
