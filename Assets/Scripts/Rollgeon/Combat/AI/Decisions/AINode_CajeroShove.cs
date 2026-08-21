using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Cashier;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Rollgeon.Tiles.Forced;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// El empujón del Cajero: pega <see cref="AINode_RangedShot.Damage"/> y manda al jugador
    /// <see cref="PushTiles"/> casillas en línea recta hacia el lado opuesto al suyo, dejando
    /// <see cref="CoinCount"/> monedas tiradas a lo largo del tumbo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hereda de <see cref="AINode_RangedShot"/> con <c>Range = 1</c>: el gate de rango, el daño,
    /// el giro hacia el jugador y la secuencia de presentación ya viven ahí y no dependen de que el
    /// golpe sea a distancia. Es el mismo idiom que el mordisco de la Comisión — el daño de
    /// <c>EffDealDamage</c> es privado y un builder no puede autorarlo.
    /// </para>
    /// <para>
    /// El tumbo no lo camina este nodo: lo delega en <see cref="IForcedMovementService"/>, que es
    /// quien frena en seco contra una pared o un blocker, cobra las casillas atravesadas (los
    /// pinchos que cruce) y levanta las monedas que ya estuvieran en el recorrido.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_CajeroShove : AINode_RangedShot
    {
        [Tooltip("Casillas del tumbo. Frena antes si choca una pared o un blocker.")]
        [MinValue(0)]
        public int PushTiles = 3;

        [Tooltip("Definición del hazard-moneda que queda en el piso. Sin ella el empujón sólo pega y tira.")]
        public HazardDefinitionSO Coin;

        [Tooltip("Monedas que se le caen al jugador a lo largo del tumbo.")]
        [MinValue(0)]
        public int CoinCount = 2;

        [Tooltip("Valor mínimo en oro de una moneda del tumbo.")]
        [MinValue(0)]
        public int CoinMinValue = 6;

        [Tooltip("Valor máximo en oro de una moneda del tumbo, inclusive.")]
        [MinValue(0)]
        public int CoinMaxValue = 9;

        public override string NodeName => $"Cajero — Empujón ({Damage} y {PushTiles} casillas)";

        public override AIResult Tick(AIContext context)
        {
            var result = base.Tick(context);
            if (result != AIResult.Succeeded) return result;

            Shove(context);
            return AIResult.Succeeded;
        }

        /// <remarks>
        /// El tumbo va DESPUÉS del clip: la base retiene el turno hasta el frame del golpe, y
        /// tumbarlo antes dejaría al jugador llegando a destino sin que nada explique por qué salió
        /// volando.
        /// </remarks>
        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            var result = AIResult.Failed;
            var blow = base.TickCoroutine(context, r => result = r);
            while (blow.MoveNext()) yield return blow.Current;

            if (result == AIResult.Succeeded) Shove(context);
            onResult?.Invoke(result);
        }

        // ---- el tumbo -----------------------------------------------------

        private void Shove(AIContext context)
        {
            if (PushTiles <= 0) return;

            var grid = context.Grid;
            if (grid == null) return;
            if (!grid.TryGetPosition(context.SelfGuid, out var selfCoord)) return;
            if (!grid.TryGetPosition(context.PlayerGuid, out var origin)) return;

            if (!ServiceLocator.TryGetService<IForcedMovementService>(out var forced) || forced == null)
            {
                Debug.LogWarning("[AINode_CajeroShove] IForcedMovementService no registrado — el " +
                                 "empujón pega pero no tira. Agregá ForcedMovementServiceBootstrap.");
                return;
            }

            // Con Range 1 y métrica Manhattan el jugador está siempre ortogonalmente pegado, así que
            // el delta es un cardinal exacto: "el lado opuesto al suyo" no necesita desempate.
            var away = CardinalExtensions.FromDelta(selfCoord, origin);
            var result = forced.Push(context.PlayerGuid, away, PushTiles, context.SelfGuid);

            // Sobre un cadáver no se tira plata: si los pinchos del tumbo lo mataron, la pelea
            // terminó y las monedas serían pickups que nadie va a levantar.
            if (result.TargetDied) return;

            DropCoins(context, grid, origin, away, result.TilesTraveled);
        }

        private void DropCoins(
            AIContext context, IGridManager grid, GridCoord origin, Cardinal away, int traveled)
        {
            if (Coin == null || CoinCount <= 0) return;

            if (!ServiceLocator.TryGetService<IHazardService>(out var hazards) || hazards == null)
            {
                Debug.LogWarning("[AINode_CajeroShove] IHazardService no registrado — sin él la " +
                                 "moneda no existe como casilla. Agregá HazardServiceBootstrap.");
                return;
            }

            var ledger = CashierLedgerService.ResolveOrCreate();
            var rng = context.Rng ?? new System.Random();

            int dropped = 0;
            foreach (var coord in TumbleTiles(origin, away, traveled, PushTiles))
            {
                if (dropped >= CoinCount) break;
                if (!grid.InBounds(coord) || !grid.IsFree(coord)) continue;

                var instanceId = hazards.Activate(Coin, new[] { coord });
                if (instanceId == Guid.Empty) continue;

                ledger.RegisterChip(instanceId, RollValue(rng), context.SelfGuid);
                dropped++;
            }
        }

        /// <summary>
        /// Casillas del tumbo donde puede quedar plata, en orden de preferencia: las intermedias
        /// (1..<paramref name="traveled"/>−1) y, como último recurso, la de partida.
        /// </summary>
        /// <remarks>
        /// <para>
        /// La casilla FINAL nunca entra: el jugador termina parado ahí y la moneda dispara al
        /// entrar, así que sería plata que no se puede levantar sin salir y volver. Con un tumbo
        /// completo de 3 las dos intermedias ya alcanzan y la de partida no se usa; existe para el
        /// tumbo corto contra una caja fuerte, donde si no el empujón no le sacaría nada.
        /// </para>
        /// <para>
        /// Las monedas se colocan DESPUÉS de que el empuje resolvió: el servicio de movimiento
        /// forzado camina cada paso de verdad, así que una moneda puesta antes sobre la línea del
        /// tumbo se la levantaría el propio tumbo.
        /// </para>
        /// <para>
        /// El recorrido se reconstruye a mano porque <c>ForcedMoveResult</c> devuelve la celda final
        /// y el conteo de pasos, no el camino. Va clampeado a <paramref name="pushTiles"/>: si una
        /// continuación de casilla (Hielo, Portal) siguió empujando, esos pasos extra ya no están
        /// sobre esta línea recta y adivinarlos pondría monedas en casillas por las que el jugador
        /// nunca pasó.
        /// </para>
        /// </remarks>
        private static IEnumerable<GridCoord> TumbleTiles(
            GridCoord origin, Cardinal away, int traveled, int pushTiles)
        {
            int straight = Mathf.Min(traveled, pushTiles);
            for (int i = 1; i < straight; i++) yield return Offset(origin, away, i);
            yield return origin;
        }

        private static GridCoord Offset(GridCoord from, Cardinal dir, int steps)
        {
            var coord = from;
            for (int i = 0; i < steps; i++) coord = dir.Step(coord);
            return coord;
        }

        private int RollValue(System.Random rng)
        {
            int min = Mathf.Min(CoinMinValue, CoinMaxValue);
            int max = Mathf.Max(CoinMinValue, CoinMaxValue);
            return min == max ? min : rng.Next(min, max + 1);
        }
    }
}
