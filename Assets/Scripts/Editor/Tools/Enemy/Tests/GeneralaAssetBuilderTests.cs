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
    /// Tests del árbol de La Generala construido <b>en memoria</b> por
    /// <see cref="GeneralaAssetBuilder"/> — sin tocar el <see cref="UnityEditor.AssetDatabase"/>, así
    /// el wiring se valida aunque el <c>[MenuItem]</c> todavía no se haya corrido en el proyecto.
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
            // Assert — la mano de la ronda pasada es lo único que hay para cobrar al abrir el
            // turno: desde que el cubilete es melee directo no queda un segundo aviso en cola.
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

            // Assert — es lo que reparte la mesa en dos precios distintos: cuatro dados en los
            // marcos de puerta cuestan caminar bajo persecución, y el quinto —pegado a ella— cuesta
            // el cubilete. Con RingAroundSelf los cinco caían pegados a ella y las puertas no
            // costaban nada; con AINode_SpawnReinforcements caían en PickEdgeSpawnTiles —el
            // perímetro de la sala, separados 3— y ninguna se tocaba.
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

                // Cada ranura vacía corre su propio reloj y el dado vuelve a SU casilla. Como oleada
                // de refuerzos había que romper los cinco para que volviera alguno.
                Assert.IsTrue(table.Respawns);
                Assert.AreEqual(GeneralaAssetBuilder.TableRefillTurns, table.RespawnDelayTurns);

                Assert.IsTrue(table.HideFromTurnQueue,
                    "Como EnemyDataSO los cinco dados ocupaban cinco slots seguidos de iniciativa " +
                    "con retrato propio para tickear un Wait. La mesa es mobiliario.");
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

                // Assert — es lo único que la mesa hace y el jugador puede ver: sus otros dos efectos
                // (bloquear el paso, borrarle una categoría) no aparecen en pantalla, así que romper
                // dados parecía una pérdida de turnos.
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

            // Assert — sin el gesto los dados aparecen de la nada mientras ella sigue en idle. Es
            // además el único uso que tiene esa animación del rig (ver BossFeedbackInstaller).
            Assert.AreEqual(BossFeedbackIds.GeneralaSummonAnim, spawn.SpawnFeedbackId);
        }

        [Test]
        public void Table_IsNotWrappedInOnce_SoTheHandComesBack()
        {
            // Arrange — AINode_SpawnRoomObjects se auto-gatea y necesita tickear cada turno para
            // correr los relojes de reposición; envuelto en Once queda latcheado tras el primer
            // spawn y ningún dado vuelve nunca.
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

            // Assert — uno por nodo riesgoso: si dos compartieran Selector, el Failed del primero
            // saltearía al segundo en vez de aislarlo.
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
            // Assert — sin esto, la Generala recién cantada marcaría el mismo turno y se perdería
            // la ronda extra de aviso.
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

            // Assert — el alcance es el mismo con el que el jugador le pega a ella (Base Attack:
            // Range 1, Manhattan), así que la regla se lee de una: si le llegás, te llega.
            Assert.AreEqual(12, GeneralaAssetBuilder.CupSlamDamage,
                "Bajó de 18 a 12: ahora persigue (ver RepositionRange), así que llega más seguido " +
                "y el peaje por golpe tiene que bajar — el número vive en el builder, no en el nodo.");
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

            // Assert — mientras fue un área avisada en rondas impares había una ronda franca para
            // romperle dados gratis. Ahora el único gate es la distancia, y esa la elige el jugador.
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

            // Assert — un aviso acá significaría que el cubilete volvió a ser un área que se marca
            // un turno y se cobra al siguiente, o sea dos golpes por una sola tirada.
            Assert.IsFalse(Descendants(branch).OfType<AINode_AuxTelegraph>().Any(),
                "El cubilete no ocupa canal de aviso: cobra en el acto.");
            Assert.IsFalse(Descendants(branch).OfType<AINode_TelegraphMark>().Any(),
                "Ni marca área — el único aviso es la distancia, que el jugador controla entera.");
        }

        // ======================================================================
        // La escarcha
        // ======================================================================

        [Test]
        public void Frost_FreezesTheRingAroundTheTable_LeavingTheAdjacentTilesFree()
        {
            // Act
            var frost = Descendants(_root).OfType<AINode_GeneralaFrostRing>().FirstOrDefault();

            // Assert — radio 2 = el BORDE del 5×5. Las cuatro casillas pegadas a ella quedan
            // libres, que son desde donde el jugador le rompe los dados: con radio 1 el anillo las
            // tapaba y desarmarle la mesa dejaba de ser posible.
            Assert.IsNotNull(frost, "La Generala no congela nada.");
            Assert.AreEqual(GeneralaAssetBuilder.FrostRingRadius, frost.Radius);
            Assert.AreEqual(2, GeneralaAssetBuilder.FrostRingRadius,
                "El anillo tiene que dejar libre el anillo de distancia 1.");
            Assert.AreSame(_frost, frost.Hazard, "El anillo tiene que usar SU definición de hielo.");
            Assert.AreEqual(1, frost.StunTurns, "La ficha pide 1 turno de congelamiento.");
            Assert.IsTrue(frost.ReplacePreviousRing,
                "Dos anillos vivos duplicarían overlays y dejarían medio mapa helado.");
        }

        [Test]
        public void Frost_FallsOnEvenRoundsOnly_SoThereIsARoundToBreakDice()
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
                "Rondas pares: la impar es la ventana franca para entrar a la mesa.");
        }

        [Test]
        public void Frost_PaysInTurnsAndNotInHp_BecauseTheFloorCeilingIsAlreadyFull()
        {
            // Arrange — su turno ya puede sumar la mano detonada (45) + el cubilete (12) = 57,
            // contra un techo de 45 por golpe y ≤65 anunciado. No queda presupuesto para un
            // tercer golpe: la escarcha cobra el turno, no HP.
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
                Assert.AreEqual(2, definition.DurationRounds,
                    "'Dura 1 turno' se autora como 2: la duración se descuenta en el wrap de ronda " +
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
            // Assert — la estela del piso 2 dura 3 rondas pisables a propósito (tapar corredores);
            // la escarcha dura 1. Compartir asset obligaría a elegir, y el jefe del piso 2 no es
            // de este trabajo.
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

            // Assert — modo Combo: ClearAll + ForbidCombo sobre los últimos N del ComboLog. Con
            // N = 1 es literalmente "no repitas la mano de la ronda pasada".
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

            // Assert — el jefe computa al cerrar su turno y el jugador lo lee al abrir el suyo: la
            // fila sale tachada en el Contrato ANTES de que comprometa los dados.
            Assert.Greater(banIdx, -1, "No se encontró la regla en el Sequence raíz.");
            Assert.Greater(banIdx, rollIdx, "La regla se promulga al cierre del turno, no al abrirlo.");
        }

        // ======================================================================
        // El reposicionamiento
        // ======================================================================

        [Test]
        public void Reposition_ChasesWithATwoTileLeash()
        {
            // Act
            var move = Descendants(_root).OfType<AINode_Move>().FirstOrDefault();

            // Assert — el pedido es que la persiga sin plantarse nunca en melee. AINode_Move con
            // Retreat = false cierra distancia hasta DesiredRange y devuelve Failed (se queda
            // quieta) cuando ya está más cerca: eso es la correa.
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
            // Assert — el invariante del que depende toda la correa: si alguna vez coincidieran,
            // perseguir hasta el borde de la correa la dejaría pegada al jugador y el cubilete
            // dejaría de ser una elección para pasar a ser un impuesto por turno.
            Assert.Greater(GeneralaAssetBuilder.RepositionRange, GeneralaAssetBuilder.CupSlamRange,
                "Su banda tiene que quedar FUERA del alcance del cubilete: si se pegara sola, el " +
                "peaje de acercarse dejaría de elegirlo el jugador.");
        }

        [Test]
        public void Reposition_GoesLast_SoTheCupAndTheFrostResolveFromWhereSheRolled()
        {
            // Assert — moverse antes le cambiaría el centro al anillo y la distancia al cubilete
            // respecto de lo que el jugador vio cuando decidió dónde pararse.
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

            // El cubilete no se avisa, así que su daño es el que más caro sale sostener: entra sí
            // o sí por estar parado al lado.
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

                // Assert — el dado tiene visual propio: con el del jefe no se leería como dado.
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
