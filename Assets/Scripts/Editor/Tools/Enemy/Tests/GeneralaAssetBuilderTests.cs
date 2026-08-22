using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Bosses.Generala;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Combat.BossHand;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Rooms;
using Rollgeon.Combat.Threat;
using Rollgeon.Combos;
using Rollgeon.Feedback;
using Rollgeon.Combos.Concretes;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Entities;
using Rollgeon.Grid;
using Rollgeon.PreConditions.Concretes;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>
    /// Árbol de La Generala armado en memoria: el wiring se valida sin depender de que el
    /// <c>[MenuItem]</c> ya se haya corrido en el proyecto.
    /// </summary>
    [TestFixture]
    public class GeneralaAssetBuilderTests
    {
        private RoomObjectDefinitionSO _dice;
        private HazardDefinitionSO _frost;
        private AINode_Sequence _root;

        [SetUp]
        public void SetUp()
        {
            _dice = ScriptableObject.CreateInstance<RoomObjectDefinitionSO>();
            _frost = ScriptableObject.CreateInstance<HazardDefinitionSO>();
            _root = GeneralaAssetBuilder.BuildAIRoot(_dice, _frost);
        }

        [TearDown]
        public void TearDown()
        {
            if (_dice != null) UnityEngine.Object.DestroyImmediate(_dice);
            if (_frost != null) UnityEngine.Object.DestroyImmediate(_frost);
        }

        // ======================================================================
        // Orden del turno
        // ======================================================================

        [Test]
        public void Root_OpensTheTurnByDetonatingTheHand_TheOnlyThingSheLeavesPending()
        {
            // Assert
            Assert.IsInstanceOf<AINode_ExecuteTelegraph>(_root.Children[0],
                "El primer hijo tiene que detonar la mano de la ronda pasada.");

            Assert.AreEqual(1, Descendants(_root).OfType<AINode_ExecuteTelegraph>().Count(),
                "Un solo Execute: un segundo aviso pendiente serían dos golpes por una tirada.");
        }

        [Test]
        public void Root_TicksThePhaseGate_BeforeRollingTheHand()
        {
            // Arrange
            int phaseIdx = _root.Children.FindIndex(c => Descendants(c).Any(n => n is AINode_SetHandReroll));
            int rollIdx = _root.Children.FindIndex(c => Descendants(c).Any(n => n is AINode_RollHand));

            // Assert — si el gate quedara después, el reroll de Fase 2 recién aplicaría un turno tarde.
            Assert.Greater(phaseIdx, -1, "No se encontró el gate de Fase 2.");
            Assert.Greater(rollIdx, phaseIdx, "El gate de fase tiene que ir antes de la tirada.");
        }

        [Test]
        public void Root_RefillsTheTable_BeforeRollingTheHand()
        {
            // Arrange — la mano se arma con los dados vivos, así que la mesa se repone antes.
            int spawnIdx = _root.Children.FindIndex(c =>
                Descendants(c).Any(n => n is AINode_SpawnRoomObjects));
            int rollIdx = _root.Children.FindIndex(c => Descendants(c).Any(n => n is AINode_RollHand));

            // Assert
            Assert.Greater(spawnIdx, -1, "No se encontró el spawn de la mesa.");
            Assert.Greater(rollIdx, spawnIdx);
        }

        // ======================================================================
        // La mesa
        // ======================================================================

        [Test]
        public void Table_SpawnsFiveDice_FromTheRoomObjectDefinition()
        {
            // Act
            var spawn = Descendants(_root).OfType<AINode_SpawnRoomObjects>().FirstOrDefault();

            // Assert
            Assert.IsNotNull(spawn);
            Assert.AreSame(_dice, spawn.Definition, "La mesa tiene que spawnear los dados de la casa.");
            Assert.AreEqual(GeneralaAssetBuilder.HandSize, spawn.Count);
            CollectionAssert.IsEmpty(Descendants(_root).OfType<AINode_SpawnReinforcements>().ToList(),
                "Sus dados dejaron de ser refuerzos: como EnemyDataSO arrastraban retrato de enemigo, " +
                "barra de enemigo y un slot en la cola de turnos — nada de eso lo pide el diseño.");
        }

        [Test]
        public void Table_SpreadsAcrossTheDoorFronts_NotRingedAroundHer()
        {
            // Act
            var spawn = Descendants(_root).OfType<AINode_SpawnRoomObjects>().First();

            // Assert
            Assert.AreEqual(AINode_SpawnRoomObjects.Placement.DoorFronts, spawn.Pattern,
                "La mesa tiene que repartirse por la sala, no apilarse pegada a ella.");
        }

        [Test]
        public void DiceDefinition_CarriesTheTableNumbers_AndStaysOutOfTheTurnQueue()
        {
            // Arrange — la reposición y el HP viven en la definición, no en el nodo.
            var table = ScriptableObject.CreateInstance<RoomObjectDefinitionSO>();
            table.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                // Act
                GeneralaAssetBuilder.PopulateDiceDefinition(table, null);

                // Assert
                Assert.AreEqual(GeneralaAssetBuilder.DiceRoomObjectId, table.Id);
                Assert.AreEqual(GeneralaAssetBuilder.DiceHp, table.Hp,
                    "Romper un dado tiene que costar un golpe entero.");
                Assert.IsTrue(table.Blocks, "Sus dados SON las paredes de la sala.");

                Assert.IsFalse(table.Respawns,
                    "La mesa es un recurso que se gasta, no una noria: el dado roto se queda roto.");
                Assert.AreEqual(GeneralaAssetBuilder.TableRefillTurns, table.RespawnDelayTurns);
                Assert.Less(GeneralaAssetBuilder.TableRefillTurns, 0,
                    "RoomObjectDefinitionSO.Respawns es 'RespawnDelayTurns >= 0' — el 0 es 'vuelve " +
                    "enseguida', no 'no vuelve'. Sólo un negativo apaga la reposición.");

                Assert.IsTrue(table.HideFromTurnQueue,
                    "Sin esto los cinco dados ocupan cinco slots seguidos de iniciativa con " +
                    "retrato propio para tickear un Wait. La mesa es mobiliario.");
                Assert.IsNull(table.OnDeathHazard,
                    "Romper un dado tiene que ser puro premio: algo en su casilla lo volvería una " +
                    "decisión con costo, y romperlos es la jugada que la pelea quiere premiar.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void DiceDefinition_ArmorsHer_AndTheFiveSharesAddUpToTheSheet()
        {
            // Arrange
            var table = ScriptableObject.CreateInstance<RoomObjectDefinitionSO>();
            table.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                // Act
                GeneralaAssetBuilder.PopulateDiceDefinition(table, null);

                // Assert
                Assert.IsTrue(table.GrantsOwnerArmor);
                Assert.AreEqual(GeneralaAssetBuilder.TableArmorPerDie,
                    table.OwnerDamageReductionPerObject, 0.0001f);

                Assert.AreEqual(GeneralaAssetBuilder.TableArmorMax,
                    GeneralaAssetBuilder.TableArmorPerDie * GeneralaAssetBuilder.HandSize, 0.0001f,
                    "Los cinco dados juntos tienen que dar la reducción de la ficha. Un literal " +
                    "suelto (0.15 con cinco dados = 75%) se desfasaría sin que nadie lo note.");

                Assert.Less(GeneralaAssetBuilder.TableArmorMax, 1f,
                    "Una reducción del 100% no es una mecánica dura, es una pelea que no termina.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void Table_KeepsHerRefillGesture()
        {
            // Act
            var spawn = Descendants(_root).OfType<AINode_SpawnRoomObjects>().First();

            // Assert — es el único uso que tiene esa animación del rig (ver BossFeedbackInstaller).
            Assert.AreEqual(BossFeedbackIds.GeneralaSummonAnim, spawn.SpawnFeedbackId);
        }

        [Test]
        public void Table_IsNotWrappedInOnce_SoTheHandComesBack()
        {
            // Arrange — el spawn se auto-gatea y necesita tickear cada turno para correr los
            // relojes de reposición.
            var owner = _root.Children.FirstOrDefault(c =>
                Descendants(c).Any(n => n is AINode_SpawnRoomObjects));

            // Assert
            Assert.IsNotNull(owner);
            Assert.IsFalse(Descendants(owner).Any(n => n is AINode_Once),
                "El spawn de la mesa no puede ir dentro de un Once — rompe la reposición.");
        }

        /// <summary>Los nodos del árbol que devuelven Failed en su caso benigno.</summary>
        private static readonly Type[] RiskyNodeTypes =
        {
            typeof(AINode_SpawnRoomObjects),         // sin casillas válidas para el anillo
            typeof(AINode_SetHandReroll),            // el gate de fase, sin ComboLog ni registry
            typeof(AINode_GeneralaCupSlam),          // con el jugador lejos — media pelea
            typeof(AINode_GeneralaFrostRing),        // en ronda impar, y sin IHazardService
            typeof(AINode_RotateBlock),              // sin IContractModifierService ni IComboLogService
            typeof(AINode_Move),                     // "ya estoy en la banda", la mayoría de sus turnos
        };

        [Test]
        public void RiskyNodes_AreIsolatedInSelectorsWithAWaitFallback()
        {
            // Arrange — un Failed suelto en el Sequence raíz le cancela al jefe el resto del turno.
            var risky = _root.Children
                .OfType<AINode_Selector>()
                .Where(s => Descendants(s).Any(n => RiskyNodeTypes.Contains(n.GetType())))
                .ToList();

            // Assert — uno por nodo: compartir Selector saltearía al segundo en vez de aislarlo.
            Assert.AreEqual(RiskyNodeTypes.Length, risky.Count,
                "Cada nodo que puede devolver Failed va en su propio Selector de aislamiento: la " +
                "mesa, el setup de fase, el cubilete, la escarcha, la regla de la mano repetida y " +
                "el reposicionamiento.");
            foreach (var selector in risky)
            {
                Assert.IsTrue(selector.Children.Any(c => c is AINode_Wait),
                    "El Selector de aislamiento necesita un Wait de fallback, si no aborta igual.");
            }
        }

        // ======================================================================
        // La tabla combo → telegraph
        // ======================================================================

        [Test]
        public void HandTable_MapsEveryCategoryToTheSpecdShapeAndDamage()
        {
            // Act + Assert — la ficha, mano por mano.
            AssertHandBranch(Rollgeon.Combos.ComboId.Generala, ThreatShape.ScatteredSquares,
                GeneralaAssetBuilder.GeneralaDamage, size: 3, count: 8);
            AssertHandBranch(Rollgeon.Combos.ComboId.Poker, ThreatShape.SquareAroundPlayer,
                GeneralaAssetBuilder.PokerDamage, size: 2);
            AssertHandBranch(Rollgeon.Combos.ComboId.FullHouse, ThreatShape.ScatteredSquares,
                GeneralaAssetBuilder.FullHouseDamage, size: 3, count: 2);
            AssertHandBranch(Rollgeon.Combos.ComboId.Straight, ThreatShape.DirectionalBand,
                GeneralaAssetBuilder.LadderDamage, size: 1, depth: 4);
            AssertHandBranch(Rollgeon.Combos.ComboId.Par, ThreatShape.DirectionalBand,
                GeneralaAssetBuilder.PairDamage, size: 1, depth: 3);
        }

        [Test]
        public void HandTable_HasABustBranch_ThatHurtsLessThanAPair()
        {
            // Act
            var bust = HandBranches()
                .FirstOrDefault(b => b.pc.Match == PcBossHandCombo.HandMatch.NoCombo);

            // Assert
            Assert.IsNotNull(bust.mark, "Falta la rama de bust: fallar del todo también pega.");
            Assert.AreEqual(ThreatShape.DirectionalBand, bust.mark.Shape,
                "El bust también sale de ella, no de una fila centrada en el jugador.");
            Assert.AreEqual(0, bust.mark.Size,
                "Size = 0 en DirectionalBand es una línea de 1 sola casilla: el bust es el slash más flaco.");
            Assert.AreEqual(3, bust.mark.Depth,
                "Depth explícito: con Shape = Row este campo no se leía, y sin escribirlo acá " +
                "queda en el default de 2 en vez de la profundidad autorada.");
            Assert.AreEqual(GeneralaAssetBuilder.BustDamage, bust.mark.Damage);
            Assert.Less(GeneralaAssetBuilder.BustDamage, GeneralaAssetBuilder.PairDamage,
                "El bust tiene que doler menos que un Par.");
        }

        [Test]
        public void HandTable_EveryBranchRequiresAnArmedHand()
        {
            // Assert — sin mano armada se perdería la ronda extra de aviso.
            foreach (var branch in HandBranches())
                Assert.IsTrue(branch.pc.RequireArmed,
                    $"La rama '{branch.pc.ConditionName}' marca sin exigir mano armada.");
        }

        [Test]
        public void HandTable_EndsInAWait_SoTheCalledHandTurnDoesNotAbortTheSequence()
        {
            // Arrange
            var table = FindHandTable();

            // Assert
            Assert.IsInstanceOf<AINode_Wait>(table.Children.Last(),
                "El turno en que la mano solo se canta no matchea ninguna rama: hace falta el Wait.");
        }

        // ======================================================================
        // El cubilete
        // ======================================================================

        [Test]
        public void CupSlam_HitsForTwelve_AtOneTileInManhattan()
        {
            // Act
            var cup = FindCupSlam();

            // Assert — mismo alcance con el que el jugador le pega a ella: si le llegás, te llega.
            Assert.AreEqual(12, GeneralaAssetBuilder.CupSlamDamage,
                "El mazazo cambió de daño: persigue (ver RepositionRange), así que llega seguido y " +
                "el peaje por golpe tiene que ser bajo. El número vive en el builder, no en el nodo.");
            Assert.AreEqual(GeneralaAssetBuilder.CupSlamDamage, cup.Damage);
            Assert.AreEqual(1, cup.Range, "Range 1: solo cobra a quien esté pegado.");
            Assert.AreEqual(DistanceMetric.Manhattan, cup.Metric,
                "Chebyshev le sumaría las diagonales, desde donde el jugador no puede atacarla.");
            Assert.AreEqual(AttackKind.BasicAttack, cup.Kind);
        }

        [Test]
        public void CupSlam_FallsOnEveryRoll_WithoutARoundParityGate()
        {
            // Arrange
            var branch = FindCupBranch();

            // Assert
            Assert.AreEqual(1, Descendants(_root).OfType<AINode_GeneralaCupSlam>().Count(),
                "Un solo cubilete en el árbol: dos nodos serían dos golpes por tirada.");
            Assert.IsFalse(Descendants(branch).OfType<PcRoundNumber>().Any(),
                "Nada del cubilete puede colgar del número de ronda.");
            Assert.IsFalse(Descendants(branch).OfType<AINode_If>().Any(),
                "Ni de una condición: el cubilete se auto-gatea por distancia adentro del nodo.");
        }

        [Test]
        public void CupSlam_AnnouncesNothing_TheOnlyWarningIsTheDistance()
        {
            // Arrange
            var branch = FindCupBranch();

            // Assert
            Assert.IsFalse(Descendants(branch).OfType<AINode_AuxTelegraph>().Any(),
                "El cubilete no ocupa canal de aviso: cobra en el acto.");
            Assert.IsFalse(Descendants(branch).OfType<AINode_TelegraphMark>().Any(),
                "Ni marca área — el único aviso es la distancia, que el jugador controla entera.");
        }

        // ======================================================================
        // La escarcha
        // ======================================================================

        [Test]
        public void Frost_FreezesExactlyTheTilesAdjacentToHer_AndNothingWider()
        {
            // Act
            var frost = Descendants(_root).OfType<AINode_GeneralaFrostRing>().FirstOrDefault();

            // Assert — radio 1 macizo = el 3×3 donde vive el quinto dado y desde donde se cobra
            // el cubilete.
            Assert.IsNotNull(frost, "La Generala no congela nada.");
            Assert.AreEqual(GeneralaAssetBuilder.FrostRingRadius, frost.Radius);
            Assert.AreEqual(1, GeneralaAssetBuilder.FrostRingRadius,
                "El candado cierra el anillo pegado a ella, no un cuarto de la sala.");
            Assert.IsTrue(frost.Solid,
                "Maciza: como borde hueco el centro no hacía nada y se veía como un bug dibujado.");
            Assert.AreSame(_frost, frost.Hazard, "El anillo tiene que usar SU definición de hielo.");
            Assert.AreEqual(1, frost.StunTurns, "La ficha pide 1 turno de congelamiento.");
            Assert.IsTrue(frost.ReplacePreviousRing,
                "Dos anillos vivos duplicarían overlays y dejarían medio mapa helado.");
        }

        /// <summary>El hielo ocupa <c>DurationRounds - 1</c> rondas — de ahí el ajuste de la cuenta.</summary>
        [Test]
        public void Frost_LeavesAFreeRound_BecauseTheCadenceOutlastsTheIce()
        {
            int iceRounds = GeneralaAssetBuilder.FrostDurationRounds - 1;

            Assert.Greater(GeneralaAssetBuilder.FrostParityDivisor, iceRounds,
                $"El hielo ocupa {iceRounds} ronda(s) y cae cada " +
                $"{GeneralaAssetBuilder.FrostParityDivisor}: sin cadencia mayor que la duración, la " +
                "escarcha nueva entra antes de que se derrita la anterior y romperle el dado caro " +
                "—la única jugada que baja su armadura— se vuelve imposible.");
        }

        [Test]
        public void Frost_FallsOnACadenceOfRounds_SoThereIsARoundToBreakDice()
        {
            // Arrange
            var gate = _root.Children
                .OfType<AINode_Selector>()
                .SelectMany(s => s.Children.OfType<AINode_If>())
                .FirstOrDefault(i => Descendants(i).OfType<AINode_GeneralaFrostRing>().Any());

            // Assert
            Assert.IsNotNull(gate, "La escarcha tiene que colgar de un gate de paridad de ronda.");

            var parity = gate.Conditions.OfType<PcRoundNumber>().FirstOrDefault();
            Assert.IsNotNull(parity, "Sin gate de ronda el hielo se repone antes de derretirse.");
            Assert.AreEqual(PcRoundNumber.CompareMode.Multiple, parity.Mode);
            Assert.AreEqual(GeneralaAssetBuilder.FrostParityDivisor, parity.Value,
                "La cadencia del gate y la constante tienen que ser el mismo número: la ronda franca " +
                "sale de esa cuenta (ver Frost_LeavesAFreeRound_BecauseTheCadenceOutlastsTheIce).");
        }

        [Test]
        public void Frost_PaysInTurnsAndNotInHp_BecauseTheFloorCeilingIsAlreadyFull()
        {
            // Arrange — la mano detonada (45) + el cubilete (12) ya llenan el techo del piso.
            var definition = ScriptableObject.CreateInstance<HazardDefinitionSO>();
            try
            {
                // Act
                GeneralaAssetBuilder.ConfigureFrostHazard(definition);

                // Assert
                Assert.AreEqual(0, definition.Damage, "El hielo no puede cobrar HP: el techo ya está lleno.");
                Assert.AreEqual(HazardTriggerMode.OnEnter, definition.Trigger,
                    "Cobra al CRUZARLO — quedarse adentro o afuera del anillo no cuesta nada.");
                Assert.IsTrue(definition.ConsumeOnTrigger,
                    "La casilla pisada se derrite: sin eso el mismo anillo encadena stuns.");
                Assert.AreEqual(3, definition.DurationRounds,
                    "'Dura 2 turnos' se autora como 3: la duración se descuenta en el wrap de ronda " +
                    "y la escarcha nace con el turno del jugador de esa ronda ya jugado.");
                Assert.AreNotEqual(AnotadorAssetBuilder.IceHazardSourceId, definition.SourceId,
                    "Dos hazards con el mismo source id se pisan el estado.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Frost_UsesItsOwnDefinition_AndDoesNotRetuneTheAnotadorsTrail()
        {
            // Assert
            Assert.AreNotEqual(AnotadorAssetBuilder.IceHazardAssetPath,
                GeneralaAssetBuilder.FrostHazardAssetPath,
                "La Generala tiene que tener su propio HazardDefinitionSO.");
            Assert.AreNotEqual(AnotadorAssetBuilder.TrailDurationRounds,
                GeneralaAssetBuilder.FrostDurationRounds,
                "Si las dos duraciones coincidieran, el asset propio no tendría razón de existir.");
        }

        // ======================================================================
        // La regla de la mano repetida
        // ======================================================================

        [Test]
        public void RepeatBan_ForbidsExactlyTheLastComboScored()
        {
            // Act
            var ban = Descendants(_root).OfType<AINode_RotateBlock>().FirstOrDefault();

            // Assert
            Assert.IsNotNull(ban, "Falta la regla de la mano repetida.");
            Assert.AreEqual(AINode_RotateBlock.BlockTarget.Combo, ban.Target,
                "Modo Dice bloquearía dados de la build — eso es del jefe del piso 1.");
            Assert.AreEqual(1, ban.Count, "Uno solo: el último. Dos serían dos manos prohibidas.");
            Assert.AreEqual(GeneralaAssetBuilder.RepeatBanWindow, ban.Count);
        }

        [Test]
        public void RepeatBan_IsPromulgatedAtTheEndOfHerTurn_SoThePlayerSeesItBeforeRolling()
        {
            // Arrange
            int rollIdx = _root.Children.FindIndex(c => Descendants(c).Any(n => n is AINode_RollHand));
            int banIdx = _root.Children.FindIndex(c => Descendants(c).Any(n => n is AINode_RotateBlock));

            // Assert — la fila sale tachada en el Contrato ANTES de que el jugador comprometa dados.
            Assert.Greater(banIdx, -1, "No se encontró la regla en el Sequence raíz.");
            Assert.Greater(banIdx, rollIdx, "La regla se promulga al cierre del turno, no al abrirlo.");
        }

        // ======================================================================
        // El reposicionamiento
        // ======================================================================

        [Test]
        public void Reposition_ChasesOnALeash_InsteadOfFleeing()
        {
            // Act
            var move = Descendants(_root).OfType<AINode_Move>().FirstOrDefault();

            // Assert — la correa: cierra distancia hasta DesiredRange y devuelve Failed (se queda
            // quieta) cuando ya está más cerca.
            Assert.IsNotNull(move, "La Generala no se mueve: sin este nodo es una estatua.");
            Assert.IsFalse(move.Retreat,
                "Retreat = true la haría huir otra vez — con la correa ella persigue, y quedarse " +
                "fuera del alcance del cubilete es lo que mantiene ese golpe como una elección del " +
                "jugador y no un impuesto que ella cobra sola.");
            Assert.IsInstanceOf<AIConstantInt>(move.DesiredRange);
            Assert.AreEqual(GeneralaAssetBuilder.RepositionRange, ((AIConstantInt)move.DesiredRange).Value);
            Assert.AreEqual(GeneralaAssetBuilder.RepositionSteps, ((AIConstantInt)move.MaxSteps).Value);
        }

        [Test]
        public void RepositionRange_StaysStrictlyOutsideCupSlamRange_SoSheNeverParksInMelee()
        {
            // Assert
            Assert.Greater(GeneralaAssetBuilder.RepositionRange, GeneralaAssetBuilder.CupSlamRange,
                "Su banda tiene que quedar FUERA del alcance del cubilete: si se pegara sola, el " +
                "peaje de acercarse dejaría de elegirlo el jugador.");
        }

        [Test]
        public void RepositionRange_StaysStrictlyOutsideTheFrostRing_SoTheIceIsNeverForced()
        {
            // Assert
            Assert.Greater(GeneralaAssetBuilder.RepositionRange, GeneralaAssetBuilder.FrostRingRadius,
                "La correa tiene que dejarla FUERA de su propio anillo de escarcha: si frena adentro, " +
                "el hielo pasa de ser el precio de acercarse a ser un impuesto por ronda.");
        }

        [Test]
        public void Reposition_GoesLast_SoTheCupAndTheFrostResolveFromWhereSheRolled()
        {
            // Assert
            var last = _root.Children.Last();
            Assert.IsTrue(Descendants(last).OfType<AINode_Move>().Any(),
                "El reposicionamiento tiene que ser el último hijo del Sequence raíz.");

            Assert.AreEqual(1, Descendants(_root).OfType<AINode_Move>().Count(),
                "Un solo nodo de movimiento: dos serían dos desplazamientos por turno.");
            Assert.IsFalse(Descendants(_root).OfType<AINode_KeepDistance>().Any(),
                "KeepDistance sólo kitea — convivir con Move duplicaría la lógica de distancia.");
        }

        // ======================================================================
        // Techo de daño
        // ======================================================================

        [Test]
        public void Damage_NeverExceedsTheFloorThreeCeiling()
        {
            // Arrange — techo de daño por golpe del piso 3.
            const int floorThreeCeiling = 45;

            // Act + Assert
            foreach (var mark in Descendants(_root).OfType<AINode_TelegraphMark>())
                Assert.LessOrEqual(mark.Damage, floorThreeCeiling,
                    $"Un TelegraphMark ({mark.Shape}) pega {mark.Damage}, sobre el techo del piso 3.");

            // El cubilete no se avisa: entra sí o sí por estar parado al lado.
            foreach (var cup in Descendants(_root).OfType<AINode_GeneralaCupSlam>())
                Assert.LessOrEqual(cup.Damage, floorThreeCeiling,
                    $"El cubilete pega {cup.Damage}, sobre el techo del piso 3.");
        }

        // ======================================================================
        // Fase 2
        // ======================================================================

        [Test]
        public void PhaseTwo_AtFiftyPercent_GivesRerollAndAdoptsTheWeakness_Once()
        {
            // Act
            var gate = Descendants(_root)
                .OfType<AINode_If>()
                .FirstOrDefault(g => g.Conditions != null && g.Conditions.OfType<PcOwnerHpBelow>()
                    .Any(p => Mathf.Abs(p.Percent - GeneralaAssetBuilder.Phase2HpThreshold) < 0.0001f));

            // Assert
            Assert.IsNotNull(gate, "No hay gate de HP al 50%.");
            Assert.IsInstanceOf<AINode_Once>(gate.Then, "El setup de fase corre una sola vez.");

            var reroll = Descendants(gate.Then).OfType<AINode_SetHandReroll>().FirstOrDefault();
            Assert.IsNotNull(reroll, "Fase 2 tiene que darle reroll.");
            Assert.AreEqual(1, reroll.RerollsPerRound, "Un reroll por tirada, como el del jugador.");

            var adopt = Descendants(gate.Then).OfType<AINode_AdoptWeakness>().FirstOrDefault();
            Assert.IsNotNull(adopt, "Fase 2 tiene que copiarle la debilidad al jugador.");
            Assert.AreEqual(GeneralaAssetBuilder.WeaknessMultiplier, adopt.MultiplierOverride, 0.0001f);

            var phase = Descendants(gate.Then).OfType<AINode_ApplyStatModifier>().FirstOrDefault();
            Assert.IsNotNull(phase);
            Assert.AreEqual(2, phase.PhaseIndex);
            Assert.IsTrue(phase.EmitPhaseChangedEvent, "El feedback de Fase 2 se engancha a este evento.");
        }

        // ======================================================================
        // Data del SO
        // ======================================================================

        [Test]
        public void PopulateEnemyData_WritesTheSpecdStatsAndIdentity()
        {
            // Arrange
            var boss = ScriptableObject.CreateInstance<EnemyDataSO>();
            try
            {
                // Act
                GeneralaAssetBuilder.PopulateEnemyData(boss, _dice, null);

                // Assert
                Assert.AreEqual(GeneralaAssetBuilder.BossEntityId, boss.EntityId);
                Assert.AreEqual("La Generala", boss.DisplayName);
                Assert.AreEqual(GeneralaAssetBuilder.BossHp, boss.BaseHP);
                Assert.AreEqual(GeneralaAssetBuilder.BossAttack, boss.BaseAttack);
                Assert.AreEqual(Rollgeon.Combos.ComboId.Generala, boss.WeaknessComboId);
                Assert.AreEqual(GeneralaAssetBuilder.WeaknessMultiplier, boss.WeaknessMultiplierOverride, 0.0001f);
                Assert.AreEqual(60, boss.MinGoldDrop, "Oro de jefe de piso 3.");
                Assert.AreEqual(80, boss.MaxGoldDrop);
                Assert.IsInstanceOf<AINode_Sequence>(boss.AIRoot);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(boss);
            }
        }

        [Test]
        public void PopulateDiceData_MakesObjectsThatDoNotAttack_WithTheSpecdHp()
        {
            // Arrange
            var dice = ScriptableObject.CreateInstance<EnemyDataSO>();
            try
            {
                // Act
                GeneralaAssetBuilder.PopulateDiceData(dice, null);

                // Assert
                Assert.AreEqual(GeneralaAssetBuilder.DiceEntityId, dice.EntityId);
                Assert.AreEqual(GeneralaAssetBuilder.DiceHp, dice.BaseHP);
                Assert.That(GeneralaAssetBuilder.DiceHp, Is.InRange(40, 50),
                    "La ficha pide dados de 40-50 HP: menos y la mesa se desarma de cualquier roce.");
                Assert.AreEqual(0, dice.BaseAttack, "Los dados no pegan: todo el daño entra por la mano.");
                Assert.AreEqual(0, dice.MaxGoldDrop, "Romper un dado paga en categorías, no en oro.");
                Assert.IsInstanceOf<AINode_Wait>(dice.AIRoot,
                    "Sin AIRoot el spawn cae al BasicEnemyAI y el dado atacaría.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dice);
            }
        }

        [Test]
        public void PopulateEnemyData_AssignsTheVisualPrefabAndThePortrait()
        {
            // Arrange
            var boss = ScriptableObject.CreateInstance<EnemyDataSO>();
            var visual = new GameObject("PF_Boss_Generala_Probe");
            var portrait = MakeSprite();
            try
            {
                // Act
                GeneralaAssetBuilder.PopulateEnemyData(boss, _dice, visual, portrait);

                // Assert — sin VisualPrefab, EntityVisualService loguea error y no spawnea nada.
                Assert.AreSame(visual, boss.VisualPrefab);
                Assert.AreSame(portrait, boss.Portrait,
                    "El retrato alimenta la cola de turnos y la BossBar por el mismo campo.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(boss);
                UnityEngine.Object.DestroyImmediate(visual);
                DestroySprite(portrait);
            }
        }

        [Test]
        public void PopulateData_KeepsTheExistingVisual_WhenNothingIsPassed()
        {
            // Arrange — el builder es re-ejecutable: una corrida sin arte no puede borrar el wiring.
            var boss = ScriptableObject.CreateInstance<EnemyDataSO>();
            var visual = new GameObject("PF_Boss_Generala_Probe");
            var portrait = MakeSprite();
            try
            {
                GeneralaAssetBuilder.PopulateEnemyData(boss, _dice, visual, portrait);

                // Act
                GeneralaAssetBuilder.PopulateEnemyData(boss, _dice, null, null);

                // Assert
                Assert.AreSame(visual, boss.VisualPrefab);
                Assert.AreSame(portrait, boss.Portrait);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(boss);
                UnityEngine.Object.DestroyImmediate(visual);
                DestroySprite(portrait);
            }
        }

        [Test]
        public void PopulateDiceData_AssignsItsOwnVisualPrefabAndPortrait()
        {
            // Arrange
            var dice = ScriptableObject.CreateInstance<EnemyDataSO>();
            var visual = new GameObject("PF_Obj_DadoCasa_Probe");
            var portrait = MakeSprite();
            try
            {
                // Act
                GeneralaAssetBuilder.PopulateDiceData(dice, visual, portrait);

                // Assert
                Assert.AreSame(visual, dice.VisualPrefab);
                Assert.AreSame(portrait, dice.Portrait);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dice);
                UnityEngine.Object.DestroyImmediate(visual);
                DestroySprite(portrait);
            }
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        private static Sprite MakeSprite()
        {
            var texture = new Texture2D(4, 4);
            return Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
        }

        private static void DestroySprite(Sprite sprite)
        {
            if (sprite == null) return;
            var texture = sprite.texture;
            UnityEngine.Object.DestroyImmediate(sprite);
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
        }

        private AINode_Selector FindHandTable()
        {
            var table = _root.Children.OfType<AINode_Selector>()
                .FirstOrDefault(s => s.Children.OfType<AINode_If>()
                    .Any(i => i.Conditions.OfType<PcBossHandCombo>().Any()));
            Assert.IsNotNull(table, "No se encontró la tabla combo → telegraph.");
            return table;
        }

        private AINode_GeneralaCupSlam FindCupSlam()
        {
            var cup = Descendants(_root).OfType<AINode_GeneralaCupSlam>().FirstOrDefault();
            Assert.IsNotNull(cup, "No se encontró el cubilete en el árbol.");
            return cup;
        }

        /// <summary>Hijo del Sequence raíz que cuelga del cubilete — su Selector de aislamiento.</summary>
        private AIDecisionNode FindCupBranch()
        {
            var branch = _root.Children.FirstOrDefault(c =>
                Descendants(c).Any(n => n is AINode_GeneralaCupSlam));
            Assert.IsNotNull(branch, "No se encontró la rama del cubilete en el Sequence raíz.");
            return branch;
        }

        private List<(PcBossHandCombo pc, AINode_TelegraphMark mark)> HandBranches()
        {
            var result = new List<(PcBossHandCombo pc, AINode_TelegraphMark mark)>();
            foreach (var branch in FindHandTable().Children.OfType<AINode_If>())
            {
                var pc = branch.Conditions.OfType<PcBossHandCombo>().FirstOrDefault();
                var mark = Descendants(branch.Then).OfType<AINode_TelegraphMark>().FirstOrDefault();
                if (pc != null && mark != null) result.Add((pc, mark));
            }
            return result;
        }

        private void AssertHandBranch(
            string comboId, ThreatShape shape, int damage, int size, int count = -1, int depth = -1)
        {
            var branch = HandBranches().FirstOrDefault(b =>
                b.pc.Match == PcBossHandCombo.HandMatch.Combo &&
                string.Equals(b.pc.ComboId, comboId, StringComparison.Ordinal));

            Assert.IsNotNull(branch.mark, $"Falta la rama de '{comboId}'.");
            Assert.AreEqual(shape, branch.mark.Shape, $"Shape equivocada para '{comboId}'.");
            Assert.AreEqual(damage, branch.mark.Damage, $"Daño equivocado para '{comboId}'.");
            Assert.AreEqual(size, branch.mark.Size, $"Size equivocado para '{comboId}'.");
            if (count >= 0)
                Assert.AreEqual(count, branch.mark.Count, $"Cantidad de cuadrados equivocada para '{comboId}'.");
            if (depth >= 0)
                Assert.AreEqual(depth, branch.mark.Depth,
                    $"Depth equivocada para '{comboId}': con Shape = Row el campo no se leía, y " +
                    "copiarlo tal cual a DirectionalBand deja un slash de 6 casillas en vez de la " +
                    "profundidad autorada.");
        }

        /// <summary>Tree-walker por reflexión (mismo helper que SunkenGrandPhaseWiringTests).</summary>
        private static List<object> Descendants(object root)
        {
            var all = new List<object>();
            var visited = new HashSet<object>(RefComparer.Instance);

            void Walk(object o)
            {
                if (o == null || o is string || o is UnityEngine.Object) return;

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
