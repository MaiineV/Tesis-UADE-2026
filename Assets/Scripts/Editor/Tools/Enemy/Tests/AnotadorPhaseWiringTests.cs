using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Combat.Threat;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Entities;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>
    /// Wiring del árbol de El Anotador (piso 2) validado <b>en memoria</b> vía
    /// <see cref="AnotadorAssetBuilder"/>: gates, fallbacks, números y el orden
    /// <c>tacha → repliegue → estela → marca</c> que pide la ficha.
    /// </summary>
    /// <remarks>
    /// Mismo objetivo que <c>SunkenGrandPhaseWiringTests</c> —que un merge no se lleve puesta la
    /// estructura de fases— pero sin cargar el <c>.asset</c>: el builder es la fuente de verdad del
    /// árbol, así que testearlo cubre también al asset que genera y no depende de un import.
    /// </remarks>
    [TestFixture]
    public class AnotadorPhaseWiringTests
    {
        /// <summary>Techo de daño por golpe del piso 2 — ninguna marca puede pasarlo.</summary>
        private const int Floor2DamageCeiling = 35;

        private const float PercentTolerance = 0.0001f;

        private AINode_Sequence _root;
        private HazardDefinitionSO _ice;

        [SetUp]
        public void SetUp()
        {
            _ice = ScriptableObject.CreateInstance<HazardDefinitionSO>();
            _ice.hideFlags = HideFlags.HideAndDontSave;
            AnotadorAssetBuilder.ConfigureIceHazard(_ice);

            _root = AnotadorAssetBuilder.BuildAIRoot(_ice);
            Assert.IsNotNull(_root, "El builder debería devolver un AINode_Sequence como raíz.");
        }

        [TearDown]
        public void TearDown()
        {
            if (_ice != null) Object.DestroyImmediate(_ice);
            _ice = null;
            _root = null;
        }

        // ======================================================================
        // Estructura del turno
        // ======================================================================

        [Test]
        public void Root_HasTheEightChildrenOfTheDesignSheet_InOrder()
        {
            Assert.AreEqual(8, _root.Children.Count,
                "La ficha define 7 pasos; el octavo es el Execute del canal del lápiz.");

            Assert.IsInstanceOf<AINode_ExecuteTelegraph>(_root.Children[0],
                "El telegráfico del turno pasado se resuelve SIEMPRE primero.");
            var pencilExecute = _root.Children[1] as AINode_AuxTelegraph;
            Assert.IsNotNull(pencilExecute,
                "El cobro del lápiz (canal secundario) va arriba, al lado del Execute principal.");
            Assert.AreEqual(AINode_AuxTelegraph.TelegraphStep.Execute, pencilExecute.Step);
            Assert.AreEqual(AnotadorAssetBuilder.PencilChannelId, pencilExecute.ChannelId,
                "Mark y Execute tienen que compartir canal, o el aviso nunca se cobra.");
            Assert.IsNotNull(Child<AINode_ShiftComboToNeighbor>(_root.Children[2]),
                "La 'tacha' (corrimiento de la hoja) es efecto de inicio de turno.");
            Assert.IsNotNull(Child<AINode_KeepDistance>(_root.Children[3]), "Falta el repliegue.");
            Assert.IsNotNull(Child<AINode_IceTrail>(_root.Children[4]), "Falta la estela helada.");
        }

        [Test]
        public void IceTrail_ComesRightAfterTheRetreat_SoItFreezesWhatHeJustWalked()
        {
            int retreatIdx = IndexOf<AINode_KeepDistance>();
            int trailIdx = IndexOf<AINode_IceTrail>();

            Assert.Greater(retreatIdx, -1, "No se encontró el nodo de repliegue.");
            Assert.Greater(trailIdx, -1, "No se encontró el nodo de estela.");
            Assert.AreEqual(retreatIdx + 1, trailIdx,
                "La estela lee el path del movimiento que acaba de ocurrir: tiene que ir " +
                "inmediatamente después del repliegue.");
        }

        [Test]
        public void Marks_ComeAfterTheRetreat_SoThePencilRingLandsOnHisFinalTile()
        {
            int retreatIdx = IndexOf<AINode_KeepDistance>();
            var markIndices = MarkIndices();

            CollectionAssert.IsNotEmpty(markIndices, "El boss no marca nada — perdió su único ataque.");
            foreach (var idx in markIndices)
            {
                Assert.Greater(idx, retreatIdx,
                    "Una marca quedó antes del repliegue: el anillo del lápiz telegrafiaría " +
                    "dónde el boss ya no está.");
            }
        }

        /// <summary>
        /// El bug que dejó quieto al Sunken Grand: <see cref="AINode_KeepDistance"/> devuelve
        /// <c>Failed</c> en el caso benigno "ya estoy a distancia ideal", que en esta pelea es la
        /// mayoría de los turnos (solo se mueve si lo tienen a 3 o menos). Suelto en el Sequence, ese
        /// Failed le come la marca de fila — su único ataque.
        /// </summary>
        [Test]
        public void EveryFailableChild_IsWrappedInSelectorWithWaitFallback()
        {
            foreach (var child in _root.Children)
            {
                if (child is AINode_ExecuteTelegraph) continue; // contrato del nodo: siempre Succeeded.
                // El Execute del canal del lápiz también es siempre-Succeeded (y debe quedar fuera
                // de todo gate para que el aviso pendiente se cobre aunque no se marque de nuevo).
                if (child is AINode_AuxTelegraph aux
                    && aux.Step == AINode_AuxTelegraph.TelegraphStep.Execute) continue;

                var selector = child as AINode_Selector;
                Assert.IsNotNull(selector,
                    $"El hijo {child.GetType().Name} está suelto en el Sequence raíz: si devuelve " +
                    "Failed aborta el turno entero del boss.");

                bool hasFallback = selector.Children.Any(c => c is AINode_Wait)
                                   || selector.Children.Any(c => c is AINode_TelegraphMark);
                Assert.IsTrue(hasFallback,
                    "El Selector no tiene fallback que pueda suceder (Wait, o la marca de fila) — " +
                    "devolvería Failed igual y abortaría el turno.");
            }
        }

        // ======================================================================
        // Marcas: una sola grande por turno
        // ======================================================================

        [Test]
        public void RowAndColumn_ShareOneSelector_SoTheyCanNeverBothMarkInTheSameTurn()
        {
            var selector = _root.Children.OfType<AINode_Selector>().FirstOrDefault(s =>
                Descendants(s).OfType<AINode_TelegraphMark>().Any(m => m.Shape == ThreatShape.Column));

            Assert.IsNotNull(selector, "No se encontró la marca de columna de fase 2.");

            var row = selector.Children.OfType<AINode_TelegraphMark>()
                .FirstOrDefault(m => m.Shape == ThreatShape.Row);
            Assert.IsNotNull(row,
                "La fila debería ser el fallback del MISMO Selector que la columna: fila (30) + " +
                "columna (32) el mismo turno son 62 sobre 100 de vida y rompen el techo del piso.");

            Assert.AreEqual(AnotadorAssetBuilder.RowDamage, row.Damage);
            Assert.AreEqual(AnotadorAssetBuilder.MarkSize, row.Size, "Row Size 1 = la línea del jugador.");
        }

        [Test]
        public void ColumnMark_IsGatedByPhase2AndEvenRound()
        {
            var gate = Gates().FirstOrDefault(g =>
                Descendants(g.Then).OfType<AINode_TelegraphMark>().Any(m => m.Shape == ThreatShape.Column));

            Assert.IsNotNull(gate, "La columna no está detrás de un If.");

            var hpGate = gate.Conditions.OfType<PcOwnerHpBelow>().FirstOrDefault();
            Assert.IsNotNull(hpGate, "La columna debería exigir fase 2 (HP < 35%).");
            Assert.AreEqual(AnotadorAssetBuilder.Phase2HpThreshold, hpGate.Percent, PercentTolerance);

            var parity = gate.Conditions.OfType<PcRoundNumber>().FirstOrDefault();
            Assert.IsNotNull(parity, "La columna debería exigir ronda par.");
            Assert.AreEqual(PcRoundNumber.CompareMode.Multiple, parity.Mode);
            Assert.AreEqual(2, parity.Value, "Ronda par = múltiplo de 2.");

            var column = Descendants(gate.Then).OfType<AINode_TelegraphMark>()
                .First(m => m.Shape == ThreatShape.Column);
            Assert.AreEqual(AnotadorAssetBuilder.ColumnDamage, column.Damage);
            Assert.AreEqual(AnotadorAssetBuilder.MarkSize, column.Size);
        }

        [Test]
        public void Pencil_IsSquareAroundSelf_OnOddRoundsOnly_AndUngatedByRange()
        {
            var gate = Gates().FirstOrDefault(g =>
                Descendants(g.Then).OfType<AINode_AuxTelegraph>()
                    .Any(m => m.Step == AINode_AuxTelegraph.TelegraphStep.Mark
                              && m.Shape == ThreatShape.SquareAroundSelf));

            Assert.IsNotNull(gate, "Falta el lápiz (SquareAroundSelf, canal secundario).");

            // Ronda impar = NOT(múltiplo de 2). PcRoundNumber no tiene negación propia.
            var not = gate.Conditions.OfType<PCComposite>().FirstOrDefault(c => c.Mode == CompositeMode.Not);
            Assert.IsNotNull(not, "El lápiz debería estar gateado por NOT(ronda múltiplo de 2).");
            var parity = not.Children.OfType<PcRoundNumber>().FirstOrDefault();
            Assert.IsNotNull(parity, "El NOT del lápiz no envuelve un PcRoundNumber.");
            Assert.AreEqual(PcRoundNumber.CompareMode.Multiple, parity.Mode);
            Assert.AreEqual(2, parity.Value);

            // El anillo ES la adyacencia: un gate de rango lo volvería redundante y podría
            // saltearlo justo cuando el jugador está pegado.
            Assert.IsEmpty(gate.Conditions.OfType<PcTargetInRange>().ToList(),
                "El lápiz no lleva gate de rango — el anillo de 3×3 ya es el peaje de acercarse.");

            var pencil = Descendants(gate.Then).OfType<AINode_AuxTelegraph>()
                .First(m => m.Shape == ThreatShape.SquareAroundSelf);
            Assert.AreEqual(AnotadorAssetBuilder.PencilDamage, pencil.Damage, "El lápiz pega 12.");
            Assert.AreEqual(AnotadorAssetBuilder.MarkSize, pencil.Size, "Size 1 ⇒ anillo 3×3.");
            Assert.AreEqual(AnotadorAssetBuilder.PencilChannelId, pencil.ChannelId,
                "El lápiz marca por su canal: bajo el SelfGuid pisaría la marca de la fila y el " +
                "camino derecho pagaría 12 en vez de 42.");
        }

        /// <summary>
        /// Tres marcas pueden convivir en el piso de esta pelea y cada una cobra distinto: fila (30),
        /// estela (stun) y lápiz (12). Con el lápiz en el violeta default del nodo, "12 de daño" y
        /// "perdés el turno" se decidirían a ojo.
        /// </summary>
        [Test]
        public void Pencil_IsAuthoredWithItsOwnOverlayTint()
        {
            var pencil = Descendants(_root).OfType<AINode_AuxTelegraph>()
                .First(m => m.Step == AINode_AuxTelegraph.TelegraphStep.Mark);

            Assert.AreEqual(AnotadorAssetBuilder.PencilOverlayTint, pencil.OverlayTint,
                "El anillo del lápiz tiene que salir en el grafito autorado, no en el default del nodo.");
            Assert.AreNotEqual(_ice.EffectiveOverlayTint, pencil.OverlayTint,
                "Lápiz y estela no pueden compartir color: uno pega 12 y el otro te saca el turno.");
        }

        [Test]
        public void NoSingleMark_BreaksTheFloor2DamageCeiling()
        {
            foreach (var mark in Descendants(_root).OfType<AINode_TelegraphMark>())
            {
                Assert.LessOrEqual(mark.Damage, Floor2DamageCeiling,
                    $"La marca {mark.Shape} pega {mark.Damage} — el techo de piso 2 es {Floor2DamageCeiling}.");
            }
            foreach (var mark in Descendants(_root).OfType<AINode_AuxTelegraph>()
                         .Where(m => m.Step == AINode_AuxTelegraph.TelegraphStep.Mark))
            {
                Assert.LessOrEqual(mark.Damage, Floor2DamageCeiling,
                    $"La marca aux {mark.Shape} pega {mark.Damage} — el techo de piso 2 es {Floor2DamageCeiling}.");
            }
        }

        // ======================================================================
        // Repliegue y estela
        // ======================================================================

        [Test]
        public void Retreat_KeepsDistanceFour_WithThreeSteps()
        {
            var retreat = Child<AINode_KeepDistance>(_root.Children[3]);

            Assert.IsNotNull(retreat);
            Assert.AreEqual(AnotadorAssetBuilder.IdealDistance, ReadConstant(retreat.IdealDistance),
                "Ideal 4: solo se repliega si lo tienen a 3 casillas o menos.");
            Assert.AreEqual(AnotadorAssetBuilder.RetreatSteps, ReadConstant(retreat.MaxSteps),
                "3 pasos de repliegue ⇒ la estela nunca pasa de 3 casillas.");
        }

        [Test]
        public void IceTrail_FreezesUpToThreeTiles_AndStunsForOneTurn()
        {
            var trail = Child<AINode_IceTrail>(_root.Children[4]);

            Assert.IsNotNull(trail);
            Assert.AreEqual(3, trail.MaxTiles, "La estela es de 1 a 3 casillas.");
            Assert.AreEqual(1, trail.StunTurns, "Pisarla cuesta 1 turno.");
            Assert.IsTrue(trail.ReplacePreviousTrail, "Una sola estela viva por vez.");
            Assert.AreSame(_ice, trail.Hazard, "El nodo tiene que apuntar a la definición del hielo.");
        }

        [Test]
        public void IceHazard_IsOnEnter_ZeroDamage_MeltsOnStep_AndSurvivesOnePlayerTurn()
        {
            Assert.AreEqual(HazardTriggerMode.OnEnter, _ice.Trigger,
                "La estela cobra al PISAR, no por ciclo ni al terminar el turno encima.");
            Assert.AreEqual(0, _ice.Damage,
                "La estela no hace daño: cobra en turnos. El stun lo aplica el binder.");
            Assert.IsTrue(_ice.ConsumeOnTrigger,
                "La casilla pisada se derrite — sin eso dos estelas seguidas encadenan stuns.");

            // 2 y no 1: la duración se descuenta en el wrap de ronda y el jugador tiene forzado el
            // primer turno de cada ronda, así que con 1 la estela muere antes de que pueda pisarla.
            Assert.AreEqual(2, _ice.DurationRounds,
                "Con DurationRounds=1 la estela expira en el arranque de la ronda siguiente, " +
                "antes del turno del jugador: sería inalcanzable.");

            Assert.Greater(_ice.EffectiveOverlayTint.b, _ice.EffectiveOverlayTint.r,
                "El overlay de la estela tiene que ser celeste, no el naranja del telegraph.");
            Assert.Greater(_ice.EffectiveOverlayTint.a, 0f, "Un tint transparente pintaría quads invisibles.");
        }

        /// <summary>
        /// El burst es decoración: la estela tiene que quedar jugable si el prefab de VFX no está
        /// construido (o si el builder corre sin él).
        /// </summary>
        [Test]
        public void IceHazard_TriggerVfx_IsOptional()
        {
            Assert.IsNull(_ice.TriggerVfxPrefab,
                "Sin prefab pasado, ConfigureIceHazard no debería inventar uno.");
            Assert.Greater(_ice.TriggerVfxLifetime, 0f,
                "Los VFX del proyecto no se autodestruyen (stopAction = None): sin lifetime, cada " +
                "pisada dejaría un ParticleSystem colgado en la escena.");
        }

        [Test]
        public void IceHazard_TakesTheBurstWhenTheBuilderHasOne()
        {
            var burst = new GameObject("VFX_IceBurst_Fake");
            try
            {
                AnotadorAssetBuilder.ConfigureIceHazard(_ice, burst);

                Assert.AreSame(burst, _ice.TriggerVfxPrefab,
                    "El hazard tiene que quedar apuntando al burst que arma el builder.");
                Assert.AreEqual(0, _ice.Damage,
                    "El VFX no cambia el contrato: la estela sigue cobrando en turnos, no en HP.");
            }
            finally
            {
                Object.DestroyImmediate(burst);
            }
        }

        // ======================================================================
        // Tacha (corrimiento) y fase 2
        // ======================================================================

        [Test]
        public void Shift_RunsOneComboPerTurn_TwoAndPermanentInPhase2()
        {
            var shift = Child<AINode_ShiftComboToNeighbor>(_root.Children[2]);

            Assert.IsNotNull(shift);
            Assert.AreEqual(1, shift.ShiftsPerTurnPhase1, "Fase 1: 1 corrimiento por turno.");
            Assert.AreEqual(2, shift.ShiftsPerTurnPhase2, "Fase 2: 2 corrimientos por turno.");
            Assert.AreEqual(0.35f, shift.Phase2HpThreshold, PercentTolerance, "Fase 2 al 35% de HP.");
            Assert.IsTrue(shift.RevertPreviousShifts, "Fase 1: el corrimiento dura 1 turno.");
            Assert.IsTrue(shift.Phase2ShiftsArePermanent,
                "Fase 2: deja de devolverlos — se acumulan hasta el final del combate.");
            CollectionAssert.Contains(shift.ImmuneComboIds, AnotadorAssetBuilder.WeaknessComboId,
                "Generala es inmune al corrimiento: es la debilidad y la única mano que no " +
                "depende de la tabla.");
        }

        [Test]
        public void Phase2Gate_FiresOnceAt35Percent()
        {
            var gate = Gates().FirstOrDefault(g =>
                Descendants(g.Then).OfType<AINode_ApplyStatModifier>().Any());

            Assert.IsNotNull(gate, "Falta el gate de fase 2.");
            var hpGate = gate.Conditions.OfType<PcOwnerHpBelow>().FirstOrDefault();
            Assert.IsNotNull(hpGate);
            Assert.AreEqual(0.35f, hpGate.Percent, PercentTolerance);

            Assert.IsInstanceOf<AINode_Once>(gate.Then,
                "El setup de fase va envuelto en Once: sin el latch se re-aplicaría cada turno.");

            var phase = Descendants(gate.Then).OfType<AINode_ApplyStatModifier>().First();
            Assert.AreEqual(2, phase.PhaseIndex);
            Assert.IsTrue(phase.EmitPhaseChangedEvent, "Sin el evento no hay feedback de 'muestra la manga'.");
            Assert.AreEqual(0, phase.AttackDelta,
                "La fase 2 no sube el daño por golpe: lo que cambia es el eje de esquiva.");
        }

        // ======================================================================
        // EnemyDataSO
        // ======================================================================

        [Test]
        public void PopulateEnemyData_WritesTheDesignSheetNumbers()
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                AnotadorAssetBuilder.PopulateEnemyData(data, _ice, null);

                Assert.AreEqual("boss.scorekeeper", data.EntityId);
                Assert.AreEqual(190, data.BaseHP);
                Assert.AreEqual(30, data.BaseAttack);
                Assert.AreEqual("combo.generala", data.WeaknessComboId);
                Assert.AreEqual(1.5f, data.WeaknessMultiplierOverride, PercentTolerance);

                // Oro de jefe de piso 2 (mismo rango que el Jefe de Seguridad).
                Assert.AreEqual(30, data.MinGoldDrop);
                Assert.AreEqual(60, data.MaxGoldDrop);

                Assert.IsInstanceOf<AINode_Sequence>(data.AIRoot, "El asset tiene que llevar el árbol inline.");
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        /// <summary>
        /// Arte y retrato entran por parámetro, con guarda de null: correr el builder en una copia del
        /// repo a la que le falte el prefab no debería <b>borrar</b> el arte que el asset ya tiene.
        /// </summary>
        [Test]
        public void PopulateEnemyData_AssignsArtAndPortrait_ButNeverClearsThem()
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.hideFlags = HideFlags.HideAndDontSave;
            var visual = new GameObject("PF_Boss_Anotador_Fake");
            // La textura se guarda aparte: Sprite.Create no la adopta, y una Texture2D sin destruir
            // la reporta el detector de leaks de EditMode.
            var texture = new Texture2D(4, 4);
            var portrait = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
            try
            {
                AnotadorAssetBuilder.PopulateEnemyData(data, _ice, visual, portrait);

                Assert.AreSame(visual, data.VisualPrefab);
                Assert.AreSame(portrait, data.Portrait,
                    "El retrato es el set de 6 dados: sin él el frame del jefe sale vacío.");

                // Segunda corrida sin arte (el caso "el prefab no está en el disco").
                AnotadorAssetBuilder.PopulateEnemyData(data, _ice, null, null);

                Assert.AreSame(visual, data.VisualPrefab, "Un null no debería pisar el arte asignado.");
                Assert.AreSame(portrait, data.Portrait, "Un null no debería pisar el retrato asignado.");
            }
            finally
            {
                Object.DestroyImmediate(portrait);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(visual);
                Object.DestroyImmediate(data);
            }
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        /// <summary>Hijo de tipo <typeparamref name="T"/> de un <c>Selector[node, Wait]</c>.</summary>
        private static T Child<T>(AIDecisionNode wrapper) where T : AIDecisionNode
        {
            if (wrapper is T direct) return direct;
            return (wrapper as AINode_Selector)?.Children.OfType<T>().FirstOrDefault();
        }

        private int IndexOf<T>() where T : AIDecisionNode
            => _root.Children.FindIndex(c => Child<T>(c) != null);

        private List<int> MarkIndices()
        {
            var indices = new List<int>();
            for (int i = 0; i < _root.Children.Count; i++)
            {
                bool marks = Descendants(_root.Children[i]).OfType<AINode_TelegraphMark>().Any()
                             || Descendants(_root.Children[i]).OfType<AINode_AuxTelegraph>()
                                 .Any(m => m.Step == AINode_AuxTelegraph.TelegraphStep.Mark);
                if (marks) indices.Add(i);
            }
            return indices;
        }

        private IEnumerable<AINode_If> Gates() => Descendants(_root).OfType<AINode_If>();

        private static int ReadConstant(AIIntReader reader)
        {
            var constant = reader as AIConstantInt;
            Assert.IsNotNull(constant, "Se esperaba un AIConstantInt (valor literal del inspector).");
            return constant.Value;
        }

        /// <summary>
        /// Walker explícito del árbol (Sequence/Selector/If/Once). Se enumera por tipo conocido en
        /// vez de por reflexión: son los cuatro composites que el builder usa, y así el test rompe si
        /// alguien mete un composite nuevo sin actualizarlo, en vez de recorrerlo por accidente.
        /// </summary>
        private static IEnumerable<object> Descendants(object node)
        {
            if (node == null) yield break;
            yield return node;

            switch (node)
            {
                case AINode_Sequence sequence:
                    foreach (var child in sequence.Children ?? new List<AIDecisionNode>())
                    foreach (var d in Descendants(child)) yield return d;
                    break;
                case AINode_Selector selector:
                    foreach (var child in selector.Children ?? new List<AIDecisionNode>())
                    foreach (var d in Descendants(child)) yield return d;
                    break;
                case AINode_If gate:
                    foreach (var d in Descendants(gate.Then)) yield return d;
                    foreach (var d in Descendants(gate.Else)) yield return d;
                    break;
                case AINode_Once once:
                    foreach (var d in Descendants(once.Child)) yield return d;
                    break;
            }
        }
    }
}
