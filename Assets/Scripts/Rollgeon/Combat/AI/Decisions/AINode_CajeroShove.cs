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
    /// El empujón del Cajero: pega <see cref="AINode_RangedShot.Damage"/>, manda al jugador
    /// <see cref="PushTiles"/> casillas en línea recta hacia el lado opuesto al suyo, y le cobra
    /// <see cref="TaxPercent"/> del oro que lleve encima —nunca menos de <see cref="TaxMinimum"/>—
    /// dejando <see cref="RefundPercent"/> de lo cobrado tirado en <see cref="CoinCount"/> monedas
    /// repartidas al azar por la sala.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hereda de <see cref="AINode_RangedShot"/> con <c>Range = 1</c>: el gate de rango, el daño,
    /// el giro hacia el jugador y la secuencia de presentación ya viven ahí y no dependen de que el
    /// golpe sea a distancia.
    /// </para>
    /// <para>
    /// El tumbo no lo camina este nodo: lo delega en <see cref="IForcedMovementService"/>, que es
    /// quien frena en seco contra una pared o un blocker, cobra las casillas atravesadas (los
    /// pinchos que cruce) y levanta las monedas que ya estuvieran en el recorrido — las de este
    /// empujón caen después, y en otra parte.
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

        [Tooltip("Monedas que se le caen al jugador, repartidas al azar por la sala.")]
        [MinValue(0)]
        public int CoinCount = 2;

        [Tooltip("Fracción del oro del jugador que le cobra cada empujón (0..1).")]
        [PropertyRange(0f, 1f)]
        public float TaxPercent = 0.10f;

        [Tooltip("Piso del cobro. Sin él un jugador con poco oro sale gratis del empujón.")]
        [MinValue(0)]
        public int TaxMinimum = 10;

        [Tooltip("Fracción de lo cobrado que vuelve al piso repartida entre las monedas (0..1). El resto se lo queda él.")]
        [PropertyRange(0f, 1f)]
        public float RefundPercent = 0.70f;

        [Tooltip("Distancia Chebyshev mínima entre las monedas que deja. Dos pegadas son un solo viaje.")]
        [MinValue(0)]
        public int CoinMinSeparation = 2;

        public override string NodeName =>
            $"Cajero — Empujón ({Damage}, {PushTiles} casillas y {TaxPercent:P0} del oro)";

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

            DropCoins(context, grid);
        }

        private void DropCoins(AIContext context, IGridManager grid)
        {
            if (Coin == null || CoinCount <= 0) return;

            var ledger = CashierLedgerService.ResolveOrCreate();

            // El cobro va primero y manda: lo que cae al piso es plata del jugador, no plata que
            // aparece. Si sale seco no hay nada que tirar y el empujón se queda en golpe y tumbo.
            //
            // refundOnDeath: false — lo que no se levanta del piso se pierde, y punto. En la caja
            // volvería entero al matarlo, y entre eso y las monedas el jugador saldría ganando.
            int taken = ledger.CollectTax(
                context.SelfGuid, TaxPercent, TaxMinimum, refundOnDeath: false);
            if (taken <= 0) return;

            int refund = Mathf.FloorToInt(taken * RefundPercent);
            if (refund <= 0) return;

            if (!ServiceLocator.TryGetService<IHazardService>(out var hazards) || hazards == null)
            {
                Debug.LogWarning("[AINode_CajeroShove] IHazardService no registrado — sin él la " +
                                 "moneda no existe como casilla. Agregá HazardServiceBootstrap.");
                return;
            }

            // Repartidas por la sala y no sobre el tumbo: la plata tiene que quedar lejos para que
            // ir a buscarla sea una decisión. En el recorrido quedaba a un paso, y recuperarla no
            // costaba el desvío que la mecánica cobra.
            //
            // Corre DESPUÉS de que el empuje resolvió, así IsFree ve al jugador en su casilla final
            // y no le deja una moneda debajo — la casilla dispara al ENTRAR, y ya está parado ahí.
            var cells = CajeroCoinScatter.PickTiles(
                grid, hazards, context.Rng, CoinCount, CoinMinSeparation);

            // El reparto se calcula sobre las que salieron: una moneda sin casilla dejaría su parte
            // del reembolso en el aire, y el jugador ya pagó.
            if (cells.Count == 0) return;

            int placed = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                int share = SplitShare(refund, cells.Count, i);
                if (share <= 0) continue;

                var instanceId = hazards.Activate(Coin, new[] { cells[i] });
                if (instanceId == Guid.Empty) continue;

                ledger.RegisterChip(instanceId, share, context.SelfGuid);
                placed++;
            }

            if (placed == 0)
                Debug.LogWarning($"[AINode_CajeroShove] Cobró {taken} de oro y no pudo dejar " +
                                 "ninguna moneda en el piso.");
        }

        /// <summary>
        /// Reparte <paramref name="total"/> entre <paramref name="parts"/> monedas sin perder
        /// monedas por el redondeo: las primeras <c>total % parts</c> se llevan una de más.
        /// </summary>
        private static int SplitShare(int total, int parts, int index)
        {
            if (parts <= 0) return 0;
            return total / parts + (index < total % parts ? 1 : 0);
        }

    }
}
