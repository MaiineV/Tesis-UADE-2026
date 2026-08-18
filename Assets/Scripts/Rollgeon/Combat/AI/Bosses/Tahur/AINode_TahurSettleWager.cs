using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Tahur
{
    /// <summary>
    /// "Liquida" del Tahúr: lee la mano que el jugador jugó, la mide contra el canto de la ronda
    /// pasada, mueve las fichas del pozo y marca el Castigo con la forma que dice cuánto faltó.
    /// Ficha de diseño "El Tahúr" (piso 3).
    /// </summary>
    /// <remarks>
    /// Los resultados posibles están en <see cref="TahurSettleOutcome"/>. El Castigo se marca en el
    /// mismo <see cref="IThreatenedAreaService"/> de siempre, así que lo detona el
    /// <c>AINode_ExecuteTelegraph</c> estándar el turno siguiente. Puede devolver
    /// <see cref="AIResult.Failed"/> ⇒ <b>va envuelto en</b> <c>Selector[SettleWager, Wait]</c>, o el
    /// turno entero se aborta.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_TahurSettleWager : AIActionNode
    {
        [Title("El pozo")]
        [Tooltip("Daño del Castigo por cantidad de fichas (index 0 = 1 ficha). Calibrado por " +
                 "simulación el 12/08: 26/32/38/42/45. El último valor es el techo del piso 3.")]
        public List<int> PotDamageTable = new List<int> { 26, 32, 38, 42, 45 };

        [Tooltip("Techo duro de daño por golpe del piso 3. El Castigo nunca pega más que esto.")]
        [MinValue(0)]
        public int DamageCeiling = 45;

        [Tooltip("Pago por ficha al cobrar el pozo (armando exacto dentro de La Mesa).")]
        [MinValue(0)]
        public int PayoutPerChip = 12;

        [Tooltip("Techo de fichas del pozo (la banca). Con la tabla de 5 entradas, 5.")]
        [MinValue(1)]
        public int MaxChips = 5;

        [Tooltip("El rastrillo: fichas que el pozo sube por ronda, solo, desde la fase 1. Es lo que " +
                 "convierte 'no jugar' en una cuenta regresiva — con 0 el pozo sólo se mueve cuando " +
                 "el jugador falla y renunciar al pozo vuelve a ser una postura estable.")]
        [MinValue(0)]
        public int RakeChipsPerRound = 1;

        [Title("Cómo se mueven las fichas")]
        [Tooltip("Fichas que suma armar una mano peor (o ninguna).")]
        [MinValue(0)]
        public int MissChipGain = 1;

        [Tooltip("Fichas que suma la codicia (armar una mano mejor que el canto).")]
        [MinValue(0)]
        public int GreedChipGain = 2;

        [Tooltip("Fichas que suma que te lea en fase 2 (armar exactamente el canto invertido).")]
        [MinValue(0)]
        public int ReadChipGain = 2;

        [Title("Formas del Castigo")]
        [Tooltip("Forma por distancia al canto: index 0 = faltó 1 escalón, index 1 = faltaron 2, etc. " +
                 "Distancias mayores usan la última entrada.")]
        [ListDrawerSettings(ShowFoldout = false)]
        public List<TahurPunishmentShape> MissShapes = new List<TahurPunishmentShape>
        {
            new TahurPunishmentShape { Shape = ThreatShape.Column, Size = 1 },
            new TahurPunishmentShape { Shape = ThreatShape.Row, Size = 1 },
            new TahurPunishmentShape { Shape = ThreatShape.Column, Size = 3 },
            new TahurPunishmentShape { Shape = ThreatShape.ScatteredSquares, Size = 2, Count = 4 },
        };

        [Tooltip("Forma del Castigo de la codicia — y del 'te leyó' de la fase 2.")]
        public TahurPunishmentShape GreedShape = new TahurPunishmentShape
        {
            Shape = ThreatShape.ScatteredSquares, Size = 2, Count = 6,
        };

        [Title("Daño")]
        [Tooltip("Tipo de ataque del Castigo al detonar el turno siguiente.")]
        public AttackKind PunishmentKind = AttackKind.BasicAttack;

        [Tooltip("Tipo de ataque del cobro del pozo. ScriptedAbility: no es el golpe del jugador, " +
                 "es el pozo pagando — no debe engancharse a weakness ni a bonos de combo.")]
        public AttackKind PayoutKind = AttackKind.ScriptedAbility;

        public override string NodeName => "Tahúr — Settle Wager (liquida y mueve el pozo)";

        // -----------------------------------------------------------------

        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Failed;

            var wager = TahurWagerService.ResolveOrCreate();
            wager.MaxChips = MaxChips;
            wager.PayoutPerChip = PayoutPerChip;
            wager.BeginBossTurn();

            // Sólo mientras no se volteó la carta: a partir del volteo el valor lo fija
            // AINode_TahurFlipCard y este nodo no puede pisárselo.
            if (!wager.CallInverted) wager.RakeChipsPerRound = RakeChipsPerRound;

            // Antes de liquidar: el Castigo que se marque esta ronda ya cuenta la ficha del rastrillo.
            if (wager.RakeChipsPerRound > 0) wager.AddChips(wager.RakeChipsPerRound);

            // El canto pendiente se armó con las reglas de antes del volteo: la primera liquidación
            // tras invertir el cartel no puede castigar por un puzzle que cambió a mitad de camino.
            if (wager.ConsumeGrace())
            {
                wager.ConsumePlayedHand();
                wager.ReportOutcome(TahurSettleOutcome.Grace, false);
                return AIResult.Succeeded;
            }

            string played = wager.LastPlayedComboId;
            bool playedByPlayer = context.PlayerGuid == Guid.Empty || wager.LastPlayedBy == context.PlayerGuid;
            wager.ConsumePlayedHand();
            if (!playedByPlayer) played = string.Empty;

            var ladder = TahurHandLadder.FromContext(context);
            if (!ladder.IsValid)
            {
                Debug.LogWarning("[AINode_TahurSettleWager] Sin ContractSheet del jugador — " +
                                 "no hay escalera de manos que liquidar.");
                return AIResult.Failed;
            }

            // Primera ronda del combate: todavía no cantó nada, no hay nada que liquidar.
            if (wager.CalledRank <= 0)
            {
                wager.ReportOutcome(TahurSettleOutcome.None, false);
                return AIResult.Succeeded;
            }

            int playedRank = ladder.RankOf(played);

            // LEE (fase 2): armar el canto es el peor resultado — te leyó.
            if (wager.CallInverted && playedRank == wager.CalledRank)
                return MarkPunishment(context, wager, ReadChipGain, GreedShape, TahurSettleOutcome.Read);

            int distance = playedRank - wager.TargetRank;

            if (distance == 0) return SettleExact(context, wager);
            if (distance > 0) return MarkPunishment(context, wager, GreedChipGain, GreedShape, TahurSettleOutcome.Greed);
            return MarkPunishment(context, wager, MissChipGain, ShapeForShortfall(-distance), TahurSettleOutcome.Miss);
        }

        // -----------------------------------------------------------------
        // Resultados
        // -----------------------------------------------------------------

        /// <remarks>
        /// El pozo no pega: paga, y paga contra el jefe. El único daño que el jugador recibe del
        /// Tahúr son el Castigo y el poke.
        /// </remarks>
        private AIResult SettleExact(AIContext context, ITahurWagerService wager)
        {
            wager.ReportOutcome(TahurSettleOutcome.Exact, false);

            var grid = context.Grid;
            if (grid == null || !grid.TryGetPosition(context.PlayerGuid, out var playerCoord))
                return AIResult.Succeeded;

            // Cobrar exige estar en La Mesa: armar exacto desde afuera es contención sin cobro.
            if (!wager.IsOnTable(playerCoord)) return AIResult.Succeeded;

            int payout = wager.Chips * PayoutPerChip;
            if (payout > 0 && context.DamagePipeline != null)
            {
                context.DamagePipeline.Resolve(new DamageContext
                {
                    SourceId = context.PlayerGuid,
                    TargetId = context.SelfGuid,
                    BaseDamage = payout,
                    Kind = PayoutKind,
                });
            }

            // El cobro vacía el pozo hasta su piso: 0 en fase 1, 1 con el rastrillo encendido.
            wager.SetChips(wager.ChipsFloor);
            return AIResult.Succeeded;
        }

        private AIResult MarkPunishment(
            AIContext context, ITahurWagerService wager, int chipGain,
            TahurPunishmentShape shape, TahurSettleOutcome outcome)
        {
            wager.AddChips(chipGain);

            var grid = context.Grid;
            if (grid == null) return AIResult.Failed;
            if (!grid.TryGetPosition(context.PlayerGuid, out var playerCoord)) return AIResult.Failed;
            if (shape == null) return AIResult.Failed;

            var tiles = shape.Compute(grid, playerCoord, context.Rng);
            if (tiles.Count == 0)
            {
                Debug.LogWarning($"[AINode_TahurSettleWager] Castigo vacío (forma={shape.Label}) — " +
                                 "¿grafo sin bounds? No se marca nada.");
                return AIResult.Failed;
            }

            if (!ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat) || threat == null)
            {
                Debug.LogError("[AINode_TahurSettleWager] IThreatenedAreaService no registrado. " +
                               "Agrega ThreatenedAreaServiceBootstrap a ServiceBootstrap.ExtraServices.");
                return AIResult.Failed;
            }

            int damage = PunishmentDamageForChips(wager.Chips);
            threat.Mark(context.SelfGuid, tiles, damage, PunishmentKind);
            ThreatTelegraphOverlay.ResolveOrCreate().Show(context.SelfGuid, tiles);

            wager.ReportOutcome(outcome, true);
            return AIResult.Succeeded;
        }

        // -----------------------------------------------------------------
        // Tabla del pozo
        // -----------------------------------------------------------------

        /// <summary>
        /// Daño del Castigo para un pozo de <paramref name="chips"/> fichas. Fichas por encima de la
        /// tabla usan la última entrada, y nunca supera <see cref="DamageCeiling"/>.
        /// </summary>
        public int PunishmentDamageForChips(int chips)
        {
            if (PotDamageTable == null || PotDamageTable.Count == 0) return 0;
            int index = Mathf.Clamp(chips, 1, PotDamageTable.Count) - 1;
            return Mathf.Clamp(PotDamageTable[index], 0, DamageCeiling);
        }

        /// <summary>Forma del Castigo según cuántos escalones le faltaron al jugador.</summary>
        public TahurPunishmentShape ShapeForShortfall(int shortfall)
        {
            if (MissShapes == null || MissShapes.Count == 0) return GreedShape;
            int index = Mathf.Clamp(shortfall, 1, MissShapes.Count) - 1;
            return MissShapes[index] ?? GreedShape;
        }
    }
}
