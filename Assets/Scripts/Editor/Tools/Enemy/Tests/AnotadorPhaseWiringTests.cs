using System;
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
// Alias explícito: `using System` (lo pide Func<> de los matchers) haría ambiguo a `Object`.
using Object = UnityEngine.Object;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>Wiring del árbol de El Anotador en memoria: contra el builder y no contra el
    /// <c>.asset</c>, que depende de un import.</summary>
    [TestFixture]
    public class AnotadorPhaseWiringTests
    {
        /// <summary>Techo de daño por golpe del piso 2 — ningún ataque puede pasarlo.</summary>
        private const int Floor2DamageCeiling = 35;

        private const float PercentTolerance = 0.0001f;

        /// <summary>Casillas de estela que pide la ficha ("3 rondas, hasta 4 casillas").</summary>
        private const int SheetTrailTiles = 4;

        /// <summary>Rondas de estela pisables que pide la ficha.</summary>
        private const int SheetTrailRounds = 3;

        /// <summary>Ancho de la columna de fase 2 según la ficha ("Columna de 3").</summary>
        private const int SheetPhase2ColumnWidth = 3;

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

        [Test]
        public void Root_HasTheSevenChildrenOfTheDesignSheet_InOrder()
        {
            Assert.AreEqual(7, _root.Children.Count,
                "La ficha define 7 pasos: detona → tacha → lápiz → repliegue → estela → marca → fase 2.");

            Assert.IsInstanceOf<AINode_ExecuteTelegraph>(_root.Children[0],
                "El telegráfico del turno pasado se resuelve SIEMPRE primero.");
            Assert.IsNotNull(Child<AINode_ShiftComboToNeighbor>(_root.Children[1]),
                "La 'tacha' (corrimiento de la hoja) es efecto de inicio de turno.");
            CollectionAssert.IsNotEmpty(Descendants(_root.Children[2]).OfType<AINode_AnotadorPencil>().ToList(),
                "Falta el lápiz. Ya no es un telegraph de canal auxiliar: es un golpe melee directo.");
            Assert.IsNotNull(Child<AINode_KeepDistance>(_root.Children[3]), "Falta el repliegue.");
            Assert.IsNotNull(Child<AINode_IceTrail>(_root.Children[4]), "Falta la estela helada.");
            CollectionAssert.IsNotEmpty(Descendants(_root.Children[5]).OfType<AINode_TelegraphMark>().ToList(),
                "Falta la marca de eje — el único ataque grande del jefe.");
            CollectionAssert.IsNotEmpty(Descendants(_root.Children[6]).OfType<AINode_ApplyStatModifier>().ToList(),
                "Falta el setup de fase 2.");
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
        public void Pencil_ComesBeforeTheRetreat_SoItChargesTheTileThePlayerChose()
        {
            int pencilIdx = IndexOf<AINode_AnotadorPencil>();
            int retreatIdx = IndexOf<AINode_KeepDistance>();

            Assert.Greater(pencilIdx, -1, "No se encontró el lápiz en el Sequence raíz.");
            Assert.Less(pencilIdx, retreatIdx,
                "El lápiz quedó después del repliegue: el boss ya se fue a distancia 4 y el peaje " +
                "de acercarse no se cobraría nunca.");
        }

        [Test]
        public void Marks_ComeAfterTheRetreat_SoTheStripeIsReadOnTheFinalBoard()
        {
            int retreatIdx = IndexOf<AINode_KeepDistance>();
            var markIndices = MarkIndices();

            CollectionAssert.IsNotEmpty(markIndices, "El boss no marca nada — perdió su único ataque.");
            foreach (var idx in markIndices)
            {
                Assert.Greater(idx, retreatIdx,
                    "Una marca quedó antes del repliegue: la franja se pintaría sobre un tablero " +
                    "que el propio turno del jefe todavía va a mover.");
            }
        }

        /// <summary><see cref="AINode_KeepDistance"/> y el lápiz devuelven <c>Failed</c> en su caso
        /// benigno, que acá es la mayoría de los turnos: sueltos, le comen la marca de fila.</summary>
        [Test]
        public void EveryFailableChild_IsWrappedInSelectorWithWaitFallback()
        {
            foreach (var child in _root.Children)
            {
                if (child is AINode_ExecuteTelegraph) continue; // contrato del nodo: siempre Succeeded.

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

        [Test]
        public void RowAndColumn_ShareOneSelector_SoTheyCanNeverBothMarkInTheSameTurn()
        {
            var selector = _root.Children.OfType<AINode_Selector>().FirstOrDefault(s =>
                Descendants(s).OfType<AINode_TelegraphMark>().Any(m => m.Shape == ThreatShape.Column));

            Assert.IsNotNull(selector, "No se encontró la marca de columna.");

            var row = selector.Children.OfType<AINode_TelegraphMark>()
                .FirstOrDefault(m => m.Shape == ThreatShape.Row);
            Assert.IsNotNull(row,
                "La fila debería ser el fallback del MISMO Selector que la columna: fila (30) + " +
                "columna (32) el mismo turno son 62 sobre 100 de vida y rompen el techo del piso.");

            Assert.AreEqual(AnotadorAssetBuilder.RowDamage, row.Damage);
            Assert.AreEqual(AnotadorAssetBuilder.MarkSize, row.Size, "Row Size 1 = la línea del jugador.");
        }

        [Test]
        public void ColumnMark_AlternatesByRoundParity_WithNoPhaseGate()
        {
            var guards = GuardsOf(IsColumnOfWidth(AnotadorAssetBuilder.MarkSize));
            Assert.IsNotNull(guards, "No se encontró la marca de columna de una casilla.");

            var parity = guards.OfType<PcRoundNumber>().FirstOrDefault();
            Assert.IsNotNull(parity, "La columna debería exigir ronda par.");
            Assert.AreEqual(PcRoundNumber.CompareMode.Multiple, parity.Mode);
            Assert.AreEqual(2, parity.Value, "Ronda par = múltiplo de 2.");

            CollectionAssert.IsEmpty(guards.OfType<PcOwnerHpBelow>().ToList(),
                "La alternancia volvió a estar detrás de un gate de HP: hasta el 35% el jefe " +
                "amenazaría un solo eje y la diagonal eterna vuelve a ganar la pelea.");

            var column = Find<AINode_TelegraphMark>(IsColumnOfWidth(AnotadorAssetBuilder.MarkSize));
            Assert.AreEqual(AnotadorAssetBuilder.ColumnDamage, column.Damage);
        }

        [Test]
        public void RowMark_IsTheUngatedFallback_SoOneAxisIsAlwaysThreatened()
        {
            var guards = GuardsOf(n => n is AINode_TelegraphMark m && m.Shape == ThreatShape.Row);

            Assert.IsNotNull(guards, "No se encontró la marca de fila.");
            CollectionAssert.IsEmpty(guards,
                "La fila quedó detrás de una condición: si esa condición falla en una ronda impar " +
                "el jefe se queda sin ataque ese turno.");
        }

        /// <summary>El gate de HP va <b>adentro</b> del de paridad: envolviéndolo, la columna vuelve a ser
        /// un ataque de fase 2 y muere la alternancia desde la ronda 1.</summary>
        [Test]
        public void Phase2_WidensTheColumnToThree_WithoutRegatingTheAlternation()
        {
            var wide = Find<AINode_TelegraphMark>(IsColumnOfWidth(SheetPhase2ColumnWidth));
            Assert.IsNotNull(wide,
                $"No hay columna de Size {SheetPhase2ColumnWidth}: la fase 2 corre los corrimientos " +
                "de la planilla pero deja el eje igual de ancho que en fase 1.");
            Assert.AreEqual(AnotadorAssetBuilder.Phase2ColumnSize, wide.Size);
            Assert.AreEqual(AnotadorAssetBuilder.ColumnDamage, wide.Damage,
                "La fase 2 ensancha la columna, no la hace pegar más: el techo del piso no se toca.");

            var guards = GuardsOf(IsColumnOfWidth(SheetPhase2ColumnWidth));
            var hpGate = guards.OfType<PcOwnerHpBelow>().FirstOrDefault();
            Assert.IsNotNull(hpGate, "La columna ancha tiene que exigir fase 2 (HP < 35%).");
            Assert.AreEqual(AnotadorAssetBuilder.Phase2HpThreshold, hpGate.Percent, PercentTolerance);

            var parity = guards.OfType<PcRoundNumber>().FirstOrDefault();
            Assert.IsNotNull(parity,
                "La columna ancha se salteó la paridad: en fase 2 amenazaría la columna todas las " +
                "rondas y la alternancia dejaría de ser predecible.");
            Assert.AreEqual(PcRoundNumber.CompareMode.Multiple, parity.Mode);
            Assert.AreEqual(2, parity.Value);
        }

        [Test]
        public void NoSingleAttack_BreaksTheFloor2DamageCeiling()
        {
            foreach (var mark in Descendants(_root).OfType<AINode_TelegraphMark>())
            {
                Assert.LessOrEqual(mark.Damage, Floor2DamageCeiling,
                    $"La marca {mark.Shape} pega {mark.Damage} — el techo de piso 2 es {Floor2DamageCeiling}.");
            }

            // El lápiz cobra directo (sin telegraph), así que también entra en el techo.
            foreach (var pencil in Descendants(_root).OfType<AINode_AnotadorPencil>())
            {
                Assert.LessOrEqual(pencil.Damage, Floor2DamageCeiling,
                    $"El lápiz pega {pencil.Damage} — el techo de piso 2 es {Floor2DamageCeiling}.");
            }
        }

        [Test]
        public void Pencil_IsDirectMelee_ForTwelve_AtRangeOne()
        {
            var pencil = Find<AINode_AnotadorPencil>(_ => true);

            Assert.IsNotNull(pencil, "Falta el lápiz en el árbol.");
            Assert.AreEqual(AnotadorAssetBuilder.PencilDamage, pencil.Damage, "El lápiz pega 12.");
            Assert.AreEqual(AnotadorAssetBuilder.PencilRange, pencil.Range,
                "Rango 1: el peaje es de la casilla desde la que el jugador le pega, no de acercarse.");
            Assert.AreEqual(DistanceMetric.Manhattan, pencil.Metric,
                "El rango del jugador se mide en Manhattan: en Chebyshev el lápiz cobraría la " +
                "diagonal, desde donde nadie le puede pegar.");
        }

        [Test]
        public void Pencil_SharesTheOddRoundParityOfTheRow_AndCarriesNoRangeGate()
        {
            var guards = GuardsOf(n => n is AINode_AnotadorPencil);
            Assert.IsNotNull(guards, "Falta el lápiz en el árbol.");

            // Ronda impar = NOT(múltiplo de 2). PcRoundNumber no tiene negación propia.
            var not = guards.OfType<PCComposite>().FirstOrDefault(c => c.Mode == CompositeMode.Not);
            Assert.IsNotNull(not, "El lápiz debería estar gateado por NOT(ronda múltiplo de 2).");
            var parity = not.Children.OfType<PcRoundNumber>().FirstOrDefault();
            Assert.IsNotNull(parity, "El NOT del lápiz no envuelve un PcRoundNumber.");
            Assert.AreEqual(PcRoundNumber.CompareMode.Multiple, parity.Mode);
            Assert.AreEqual(2, parity.Value);

            CollectionAssert.IsEmpty(guards.OfType<PcTargetInRange>().ToList(),
                "El lápiz no lleva gate de rango: el nodo ya falla solo cuando el jugador está " +
                "lejos, y dos fuentes de alcance se desincronizan.");
            CollectionAssert.IsEmpty(guards.OfType<PcOwnerHpBelow>().ToList(),
                "El lápiz cobra desde la fase 1: es el peaje de la casilla de melee toda la pelea.");
        }

        [Test]
        public void Pencil_PaintsNoOverlay_SoTheFloorKeepsTwoColors()
        {
            CollectionAssert.IsEmpty(Descendants(_root).OfType<AINode_AuxTelegraph>().ToList(),
                "Volvió a haber un canal de telegraph auxiliar: el lápiz es un golpe directo y un " +
                "tercer overlay le saca legibilidad a los dos que sí se esquivan moviéndose.");

            var shapes = Descendants(_root).OfType<AINode_TelegraphMark>()
                .Select(m => m.Shape).Distinct().ToList();
            CollectionAssert.AreEquivalent(new[] { ThreatShape.Row, ThreatShape.Column }, shapes,
                "El jefe marca formas que la ficha no tiene: las únicas áreas de esta pelea son la " +
                "fila y la columna.");
        }

        [Test]
        public void Retreat_KeepsDistanceFour_WithFourSteps()
        {
            var retreat = Find<AINode_KeepDistance>(_ => true);

            Assert.IsNotNull(retreat);
            Assert.AreEqual(AnotadorAssetBuilder.IdealDistance, ReadConstant(retreat.IdealDistance),
                "Ideal 4: solo se repliega si lo tienen a 3 casillas o menos.");
            Assert.AreEqual(AnotadorAssetBuilder.RetreatSteps, ReadConstant(retreat.MaxSteps),
                "4 pasos de repliegue: es el tope real de casillas que la estela puede congelar.");
        }

        [Test]
        public void IceTrail_FreezesUpToFourTiles_AndStunsForOneTurn()
        {
            var trail = Find<AINode_IceTrail>(_ => true);

            Assert.IsNotNull(trail);
            Assert.AreEqual(SheetTrailTiles, trail.MaxTiles, "La ficha pide estela de hasta 4 casillas.");
            Assert.AreEqual(1, trail.StunTurns, "Pisarla cuesta 1 turno.");
            Assert.IsTrue(trail.ReplacePreviousTrail, "Una sola estela viva por vez.");
            Assert.AreSame(_ice, trail.Hazard, "El nodo tiene que apuntar a la definición del hielo.");
        }

        [Test]
        public void IceTrail_NeverAsksForMoreTilesThanTheRetreatWalks()
        {
            var trail = Find<AINode_IceTrail>(_ => true);
            var retreat = Find<AINode_KeepDistance>(_ => true);

            Assert.IsNotNull(trail, "Falta la estela helada.");
            Assert.IsNotNull(retreat, "Falta el repliegue.");
            Assert.LessOrEqual(trail.MaxTiles, ReadConstant(retreat.MaxSteps),
                $"MaxTiles {trail.MaxTiles} contra un repliegue de " +
                $"{ReadConstant(retreat.MaxSteps)} pasos: el tope de arriba nunca se alcanza.");
        }

        [Test]
        public void IceHazard_IsOnEnter_ZeroDamage_MeltsOnStep_AndLastsThreePlayerRounds()
        {
            Assert.AreEqual(HazardTriggerMode.OnEnter, _ice.Trigger,
                "La estela cobra al PISAR, no por ciclo ni al terminar el turno encima.");
            Assert.AreEqual(0, _ice.Damage,
                "La estela no hace daño: cobra en turnos. El stun lo aplica el binder.");
            Assert.IsTrue(_ice.ConsumeOnTrigger,
                "La casilla pisada se derrite — sin eso dos estelas seguidas encadenan stuns.");

            // La duración se descuenta en el wrap de ronda y la estela nace con el turno del jugador
            // ya jugado (CNF-006): DurationRounds = D deja D-1 rondas pisables.
            Assert.AreEqual(SheetTrailRounds + 1, _ice.DurationRounds,
                $"Con DurationRounds = {_ice.DurationRounds} la estela vive " +
                $"{_ice.DurationRounds - 1} rondas pisables y la ficha pide {SheetTrailRounds}: " +
                "el hielo se derrite antes de tapar el corredor.");

            Assert.Greater(_ice.EffectiveOverlayTint.b, _ice.EffectiveOverlayTint.r,
                "El overlay de la estela tiene que ser celeste, no el naranja del telegraph.");
            Assert.Greater(_ice.EffectiveOverlayTint.a, 0f, "Un tint transparente pintaría quads invisibles.");
        }

        /// <summary>El burst es decoración: la estela queda jugable sin el prefab de VFX.</summary>
        [Test]
        public void IceHazard_TriggerVfx_IsOptional()
        {
            Assert.IsNull(_ice.TriggerVfxPrefab,
                "Sin prefab pasado, ConfigureIceHazard no debería inventar uno.");
            Assert.AreEqual(AnotadorAssetBuilder.TrailBurstLifetime, _ice.TriggerVfxLifetime,
                PercentTolerance,
                "Los VFX del proyecto no se autodestruyen (stopAction = None): sin un lifetime " +
                "autorado, cada pisada dejaría un ParticleSystem colgado en la escena.");
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

        [Test]
        public void Shift_RunsOneComboPerTurn_TwoAndPermanentInPhase2()
        {
            var shift = Find<AINode_ShiftComboToNeighbor>(_ => true);

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

        [Test]
        public void EveryPhase2Gate_UsesTheSameHpThreshold()
        {
            var thresholds = Descendants(_root).OfType<AINode_If>()
                .SelectMany(g => g.Conditions ?? new List<BasePreCondition>())
                .OfType<PcOwnerHpBelow>()
                .Select(c => c.Percent)
                .Distinct()
                .ToList();

            CollectionAssert.IsNotEmpty(thresholds, "No quedó ningún gate de fase 2 en el árbol.");
            Assert.AreEqual(1, thresholds.Count,
                $"Hay {thresholds.Count} umbrales de fase distintos en el árbol: " +
                string.Join(", ", thresholds));
            Assert.AreEqual(AnotadorAssetBuilder.Phase2HpThreshold, thresholds[0], PercentTolerance);
        }

        [Test]
        public void PopulateEnemyData_WritesTheDesignSheetNumbers()
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                AnotadorAssetBuilder.PopulateEnemyData(data, _ice, null);

                Assert.AreEqual("boss.scorekeeper", data.EntityId);
                Assert.AreEqual(170, data.BaseHP,
                    "Piso 2: ~7 turnos con el golpe base del piso (mediana 24), suficiente para " +
                    "que la alternancia fila/columna se lea como patrón.");
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

        [Test]
        public void PopulateEnemyData_AssignsArtAndPortrait_ButNeverClearsThem()
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.hideFlags = HideFlags.HideAndDontSave;
            var visual = new GameObject("PF_Boss_Anotador_Fake");
            // La textura se guarda aparte: Sprite.Create no la adopta y el detector de leaks la ve.
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

        private static Func<object, bool> IsColumnOfWidth(int size) =>
            node => node is AINode_TelegraphMark mark
                    && mark.Shape == ThreatShape.Column
                    && mark.Size == size;

        private static T Child<T>(AIDecisionNode wrapper) where T : AIDecisionNode
        {
            if (wrapper is T direct) return direct;
            return (wrapper as AINode_Selector)?.Children.OfType<T>().FirstOrDefault();
        }

        /// <summary>Primer nodo de tipo <typeparamref name="T"/> que cumple <paramref name="match"/>.
        /// Por tipo y no por índice: reordenar el turno rompe el test de orden y no los de números.</summary>
        private T Find<T>(Func<object, bool> match) where T : class =>
            Descendants(_root).OfType<T>().FirstOrDefault(n => match(n));

        private int IndexOf<T>() where T : AIDecisionNode
            => _root.Children.FindIndex(c => Descendants(c).OfType<T>().Any());

        private List<int> MarkIndices()
        {
            var indices = new List<int>();
            for (int i = 0; i < _root.Children.Count; i++)
            {
                if (Descendants(_root.Children[i]).OfType<AINode_TelegraphMark>().Any()) indices.Add(i);
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

        /// <summary>Condiciones de <b>todos</b> los <see cref="AINode_If"/> del camino, o <c>null</c> si no
        /// hay nodo: un ancestro puede sumar un gate, así que mirar el gate suelto no alcanza.</summary>
        private List<BasePreCondition> GuardsOf(Func<object, bool> match)
        {
            var guards = new List<BasePreCondition>();
            return Walk(_root, match, guards) ? guards : null;
        }

        private static bool Walk(object node, Func<object, bool> match, List<BasePreCondition> guards)
        {
            if (node == null) return false;
            if (match(node)) return true;

            switch (node)
            {
                case AINode_Sequence sequence:
                    return WalkChildren(sequence.Children, match, guards);
                case AINode_Selector selector:
                    return WalkChildren(selector.Children, match, guards);
                case AINode_If gate:
                {
                    int depth = guards.Count;
                    if (gate.Conditions != null) guards.AddRange(gate.Conditions);
                    if (Walk(gate.Then, match, guards)) return true;

                    // El Else corre con las condiciones en FALSO: arrastrarlas mentiría sobre el gateo.
                    guards.RemoveRange(depth, guards.Count - depth);
                    return Walk(gate.Else, match, guards);
                }
                case AINode_Once once:
                    return Walk(once.Child, match, guards);
                default:
                    return false;
            }
        }

        private static bool WalkChildren(
            List<AIDecisionNode> children, Func<object, bool> match, List<BasePreCondition> guards)
        {
            if (children == null) return false;
            foreach (var child in children)
                if (Walk(child, match, guards)) return true;
            return false;
        }

        /// <summary>Walker explícito (Sequence/Selector/If/Once) y no por reflexión: un composite
        /// nuevo rompe el test en vez de recorrerse por accidente.</summary>
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
