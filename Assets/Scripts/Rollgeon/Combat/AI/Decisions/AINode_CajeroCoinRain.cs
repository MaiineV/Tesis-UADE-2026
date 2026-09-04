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
    /// Las monedas que le saltan al Cajero cuando pega: <see cref="Count"/> repartidas al azar por
    /// la sala. Es lo que obliga al jugador a caminar la sala con él persiguiéndolo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sin reloj propio: cae cuando el golpe cae, colgado del paso de ataque. Un tick por ronda
    /// soltaba plata en turnos en los que el jefe no había hecho nada, y el jugador la juntaba
    /// mientras lo esquivaba.
    /// </para>
    /// <para>
    /// El vencimiento no es de este nodo: cada moneda nace permanente y la expira
    /// <see cref="AINode_CajeroCoinVault"/>, que es el único que puede distinguir una cobrada de
    /// una vencida. Su Failed (sala sin casilla libre) es benigno y va en
    /// <c>Selector[CoinRain, Wait]</c>.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_CajeroCoinRain : AIActionNode
    {
        [Tooltip("Definición del hazard-moneda. Sin ella el nodo no hace nada.")]
        public HazardDefinitionSO Coin;

        [Tooltip("Monedas que suelta el golpe.")]
        [MinValue(0)]
        public int Count = 3;

        [Tooltip("Valor mínimo en oro de una moneda.")]
        [MinValue(0)]
        public int MinValue = 6;

        [Tooltip("Valor máximo en oro de una moneda, inclusive.")]
        [MinValue(0)]
        public int MaxValue = 9;

        [Tooltip("Distancia Chebyshev mínima entre dos monedas del mismo golpe. 0 = pueden caer " +
                 "pegadas. Con separación las monedas se leen como puntos a visitar y no como un montón.")]
        [MinValue(0)]
        public int MinSeparation = 2;

        public override string NodeName => $"Cajero — Monedas del golpe ({Count} por la sala)";

        public override AIResult Tick(AIContext context)
        {
            if (context == null || Coin == null || Count <= 0) return AIResult.Failed;

            var grid = context.Grid;
            if (grid?.Graph == null) return AIResult.Failed;

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
                var instanceId = hazards.Activate(Coin, new[] { coord }, context.SelfGuid);
                if (instanceId == Guid.Empty) continue;

                ledger.RegisterChip(instanceId, RollValue(rng), context.SelfGuid);
                dropped++;
            }

            return dropped == 0 ? AIResult.Failed : AIResult.Succeeded;
        }

        private int RollValue(System.Random rng)
        {
            int min = Mathf.Min(MinValue, MaxValue);
            int max = Mathf.Max(MinValue, MaxValue);
            return min == max ? min : rng.Next(min, max + 1);
        }
    }
}
