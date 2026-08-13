using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Rollgeon.Combat.AI.Bosses.Tahur;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Threat;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Entities;
using Rollgeon.PreConditions.Concretes;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>
    /// Valida el árbol y los stats del Tahúr <b>en memoria</b>, vía
    /// <see cref="TahurAssetBuilder"/> — sin cargar el <c>.asset</c>.
    /// </summary>
    /// <remarks>
    /// Contra el asset, un merge desprolijo o un builder no corrido dan un rojo confuso; contra el
    /// builder, el rojo dice exactamente qué número o qué cable se movió. El asset se regenera con
    /// <c>Tools/Rollgeon/Bosses/Build Tahur</c>, que usa este mismo código.
    /// </remarks>
    [TestFixture]
    public class TahurWiringTests
    {
        private AINode_Sequence _root;

        [SetUp]
        public void SetUp()
        {
            _root = TahurAssetBuilder.BuildAIRoot();
            Assert.IsNotNull(_root, "BuildAIRoot devolvió null.");
        }

        // -----------------------------------------------------------------
        // Stats
        // -----------------------------------------------------------------

        [Test]
        public void EnemyData_CarriesTheCalibratedStats()
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            try
            {
                TahurAssetBuilder.PopulateEnemyData(data);

                Assert.AreEqual("boss.tahur", data.EntityId);
                Assert.AreEqual(290, data.BaseHP,
                    "HP calibrado el 12/08 por simulación: 250 → 290. No tocar sin re-simular.");
                Assert.AreEqual(40, data.BaseAttack);
                Assert.AreEqual(60, data.MinGoldDrop, "Gold drop de jefe de piso 3: 60-80.");
                Assert.AreEqual(80, data.MaxGoldDrop);
                Assert.IsTrue(string.IsNullOrEmpty(data.WeaknessComboId),
                    "El Tahúr no tiene debilidad a propósito: un ×1,5 encima de su ×2 de codicia " +
                    "apilaría dos multiplicadores.");
                Assert.IsInstanceOf<AINode_Sequence>(data.AIRoot);
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        // -----------------------------------------------------------------
        // Orden del turno
        // -----------------------------------------------------------------

        [Test]
        public void Turn_ResolvesTheMarkedPunishmentFirst()
        {
            Assert.IsInstanceOf<AINode_ExecuteTelegraph>(_root.Children[0],
                "El primer hijo debe ser ExecuteTelegraph: el Castigo de la ronda pasada se cobra " +
                "al abrir el turno, antes de marcar uno nuevo (si no, se pisarían en el mismo guid).");
        }

        [Test]
        public void Turn_SettlesThePot_BeforeAttackingAndBeforeCalling()
        {
            int settleIdx = IndexOfSubtreeWith<AINode_TahurSettleWager>();
            int pokeIdx = IndexOfSubtreeWith<AINode_TahurPoke>();
            int callIdx = IndexOfSubtreeWith<AINode_TahurCallHand>();
            int moveIdx = IndexOfSubtreeWith<AINode_Move>();
            int tableIdx = IndexOfSubtreeWith<AINode_TahurMarkTable>();

            Assert.Greater(settleIdx, 0, "No se encontró el nodo de liquidación.");
            Assert.Less(settleIdx, pokeIdx,
                "El pozo se mueve ANTES del poke: la rama del poke depende de que la liquidación " +
                "haya dejado la ronda limpia.");
            Assert.Less(settleIdx, callIdx,
                "Liquida el canto viejo antes de cantar el nuevo — al revés se mide contra el canto " +
                "equivocado.");
            Assert.Less(callIdx, moveIdx, "El canto es previo al movimiento (el orden de la ficha).");
            Assert.Less(moveIdx, tableIdx,
                "La Mesa se pinta DESPUÉS de moverse: si se pintara antes, quedaría donde el jefe " +
                "ya no está y la ronda perfecta no pediría ningún paso.");
        }

        [Test]
        public void Turn_TicksThePhaseGate_BeforeTheSettle()
        {
            int flipIdx = IndexOfSubtreeWith<AINode_TahurFlipCard>();
            int settleIdx = IndexOfSubtreeWith<AINode_TahurSettleWager>();

            Assert.Greater(flipIdx, -1, "No hay gate de volteo de carta en el árbol.");
            Assert.Less(flipIdx, settleIdx,
                "El gate de fase va antes de la acción: en el path no-coroutine un Running/Failed " +
                "posterior lo dejaría sin tickear.");
        }

        [Test]
        public void Turn_IsolatesEveryFailableChild_WithAWaitFallback()
        {
            for (int i = 0; i < _root.Children.Count; i++)
            {
                var child = _root.Children[i];
                if (child is AINode_ExecuteTelegraph) continue; // no es un gate: siempre Succeeded.

                var selector = child as AINode_Selector;
                Assert.IsNotNull(selector,
                    $"El hijo [{i}] ({child.NodeName}) está suelto en el Sequence raíz: si devuelve " +
                    "Failed el jefe pierde el resto del turno. Envolverlo en Selector[nodo, Wait].");
                Assert.IsTrue(selector.Children.Any(c => c is AINode_Wait),
                    $"El Selector del hijo [{i}] no tiene AINode_Wait de fallback — devolvería " +
                    "Failed igual y abortaría el turno.");
            }
        }

        // -----------------------------------------------------------------
        // Números del pozo
        // -----------------------------------------------------------------

        [Test]
        public void Settle_CarriesTheCalibratedPotTable()
        {
            var settle = FindFirst<AINode_TahurSettleWager>();

            Assert.AreEqual(new[] { 26, 32, 38, 42, 45 }, settle.PotDamageTable.ToArray(),
                "Tabla del pozo calibrada el 12/08 — no tocar sin re-simular.");
            Assert.AreEqual(45, settle.DamageCeiling,
                "Techo de daño por golpe del piso 3. El Castigo máximo es EXACTAMENTE 45.");
            Assert.AreEqual(12, settle.PayoutPerChip, "Cobrar paga 12 × fichas.");
            Assert.AreEqual(5, settle.MaxChips, "La banca: el pozo tope es 5 fichas.");
            Assert.AreEqual(1, settle.MissChipGain);
            Assert.AreEqual(2, settle.GreedChipGain, "La codicia mueve el pozo dos fichas.");
        }

        [Test]
        public void Settle_NeverExceedsTheFloorCeiling()
        {
            var settle = FindFirst<AINode_TahurSettleWager>();

            for (int chips = 1; chips <= 20; chips++)
            {
                Assert.LessOrEqual(settle.PunishmentDamageForChips(chips), 45,
                    $"El Castigo con {chips} fichas pasó el techo de 45 del piso 3.");
            }
        }

        [Test]
        public void Settle_ShapeTellsHowMuchTheHandFellShort()
        {
            var settle = FindFirst<AINode_TahurSettleWager>();

            Assert.AreEqual(4, settle.MissShapes.Count,
                "Cuatro formas de fallo: Column 1 → Row 1 → Column 3 → Scattered 4×2.");
            AssertShape(settle.ShapeForShortfall(1), ThreatShape.Column, 1, null);
            AssertShape(settle.ShapeForShortfall(2), ThreatShape.Row, 1, null);
            AssertShape(settle.ShapeForShortfall(3), ThreatShape.Column, 3, null);
            AssertShape(settle.ShapeForShortfall(4), ThreatShape.ScatteredSquares, 2, 4);
            AssertShape(settle.ShapeForShortfall(9), ThreatShape.ScatteredSquares, 2, 4,
                "Faltar más de 4 escalones usa la última forma, no una quinta inexistente.");
            AssertShape(settle.GreedShape, ThreatShape.ScatteredSquares, 2, 6,
                "La codicia usa la forma más ancha: 6 cuadrados de 2×2.");
        }

        // -----------------------------------------------------------------
        // Poke, canto, mesa, fase
        // -----------------------------------------------------------------

        [Test]
        public void Poke_IsGatedByACleanRound_AndByMeleeRange()
        {
            var gate = _root.Children.Select(Unwrap).FirstOrDefault(g =>
                g != null && Descendants(g.Then).OfType<AINode_TahurPoke>().Any());

            Assert.IsNotNull(gate, "El poke no está detrás de un If — pegaría todas las rondas.");
            Assert.IsTrue(gate.Conditions.OfType<PcTahurCleanRound>().Any(),
                "Falta PcTahurCleanRound: el poke y el Castigo no pueden resolver la misma ronda " +
                "(12 + 45 rompe el techo de 45).");
            var inRange = gate.Conditions.OfType<PcTargetInRange>().FirstOrDefault();
            Assert.IsNotNull(inRange, "Falta PcTargetInRange: el poke es melee.");
            Assert.AreEqual(1, inRange.Range);

            var poke = FindFirst<AINode_TahurPoke>();
            Assert.AreEqual(12, poke.Damage, "Poke de la ficha v2: 12.");
            Assert.IsTrue(poke.RequireCleanRound,
                "El nodo se auto-gatea aunque el If ya lo gatee — un rewire olvidadizo no puede " +
                "convertirlo en un golpe de 57.");
        }

        [Test]
        public void Call_UsesTheSixStepValve()
        {
            var call = FindFirst<AINode_TahurCallHand>();

            Assert.AreEqual(1, call.MinRank);
            Assert.AreEqual(6, call.MaxRank, "Escalones 1-6.");
            Assert.AreEqual(5, call.HighRankThreshold);
            Assert.IsTrue(call.AvoidConsecutiveHighCalls, "Nunca dos cantos ≥5 seguidos.");
            Assert.IsTrue(call.ForbidCalledHand,
                "Armar el canto tiene que hacer 0 (R03): cobrar cuesta el ataque, no la vida.");
            Assert.AreEqual(2f, call.GreedMultiplier, "El ×2 de la codicia es la R01.");
        }

        [Test]
        public void Table_IsAThreeByThreeAroundSelf_InItsOwnColour()
        {
            var table = FindFirst<AINode_TahurMarkTable>();

            Assert.AreEqual(1, table.Size, "La Mesa es SquareAroundSelf 1 ⇒ 3×3.");
            Assert.Greater(table.Tint.b, table.Tint.r,
                "La Mesa va en cian: con el naranja del Castigo el jefe es ilegible por construcción.");
        }

        [Test]
        public void PhaseGate_FlipsTheCardOnceAtFortyPercent()
        {
            var gate = _root.Children.Select(Unwrap).FirstOrDefault(g =>
                g != null && Descendants(g.Then).OfType<AINode_TahurFlipCard>().Any());

            Assert.IsNotNull(gate, "No hay gate de HP para el volteo de la carta.");
            var hp = gate.Conditions.OfType<PcOwnerHpBelow>().FirstOrDefault();
            Assert.IsNotNull(hp, "El gate del volteo no mira el HP del jefe.");
            Assert.AreEqual(0.40f, hp.Percent, 0.0001f, "El volteo entra al 40% de HP.");
            Assert.IsInstanceOf<AINode_Once>(gate.Then,
                "El volteo es one-shot: sin Once se re-aplicaría cada turno bajo el umbral y la " +
                "gracia se regalaría en loop.");

            var flip = FindFirst<AINode_TahurFlipCard>();
            Assert.AreEqual(1, flip.RakeChipsPerRound, "El rastrillo suma 1 ficha por ronda.");
            Assert.AreEqual(1, flip.ChipsFloorAfterFlip, "Cobrar deja el pozo en 1, nunca en 0.");
            Assert.IsTrue(flip.GraceOnFirstSettle, "La primera liquidación tras el volteo es de gracia.");
        }

        [Test]
        public void Tree_NeverSpawnsReinforcements()
        {
            Assert.IsEmpty(Descendants(_root).OfType<AINode_SpawnReinforcements>().ToList(),
                "El ×2 de la codicia es un modificador global del Contrato: con refuerzos en la " +
                "sala aplicaría también contra ellos.");
        }

        [Test]
        public void Tree_ClosesDistance_AndNeverKites()
        {
            var move = FindFirst<AINode_Move>();
            Assert.IsFalse(move.Retreat, "El Tahúr nunca kitea: el que acorta es él.");
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private static void AssertShape(
            TahurPunishmentShape shape, ThreatShape expected, int size, int? count, string because = null)
        {
            Assert.IsNotNull(shape, because);
            Assert.AreEqual(expected, shape.Shape, because);
            Assert.AreEqual(size, shape.Size, because);
            if (count.HasValue) Assert.AreEqual(count.Value, shape.Count, because);
        }

        private T FindFirst<T>() where T : class
        {
            var found = Descendants(_root).OfType<T>().FirstOrDefault();
            Assert.IsNotNull(found, $"No se encontró un {typeof(T).Name} en el árbol.");
            return found;
        }

        /// <summary>Índice del hijo del Sequence raíz cuyo subárbol contiene un <typeparamref name="T"/>.</summary>
        private int IndexOfSubtreeWith<T>() where T : class
            => _root.Children.FindIndex(c => Descendants(c).OfType<T>().Any());

        /// <summary>El <see cref="AINode_If"/> de un hijo, venga suelto o dentro del Selector de aislamiento.</summary>
        private static AINode_If Unwrap(AIDecisionNode child)
        {
            if (child is AINode_If direct) return direct;
            if (child is AINode_Selector sel && sel.Children != null)
                return sel.Children.OfType<AINode_If>().FirstOrDefault();
            return null;
        }

        /// <summary>Tree-walker por reflexión — mismo patrón que <c>SunkenGrandPhaseWiringTests</c>.</summary>
        private static List<object> Descendants(object root)
        {
            var all = new List<object>();
            var visited = new HashSet<object>(RefComparer.Instance);

            void Walk(object o)
            {
                if (o == null || o is string || o is Object) return;

                var type = o.GetType();
                if (type.IsPrimitive || type.IsEnum) return;
                if (!type.IsValueType && !visited.Add(o)) return;

                all.Add(o);

                if (o is IEnumerable enumerable)
                {
                    foreach (var item in enumerable) Walk(item);
                    return;
                }

                if (!(type.Namespace ?? string.Empty).StartsWith("Rollgeon")) return;

                foreach (var field in type.GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    object value;
                    try { value = field.GetValue(o); }
                    catch { continue; }
                    Walk(value);
                }
            }

            Walk(root);
            return all;
        }

        private sealed class RefComparer : IEqualityComparer<object>
        {
            public static readonly RefComparer Instance = new RefComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
