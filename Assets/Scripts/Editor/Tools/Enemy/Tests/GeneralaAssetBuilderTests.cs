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
using Rollgeon.Tiles;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>Árbol de La Generala armado en memoria: el wiring se valida sin depender de que el
    /// <c>[MenuItem]</c> ya se haya corrido en el proyecto.</summary>
    [TestFixture]
    public class GeneralaAssetBuilderTests
    {
        private RoomObjectDefinitionSO _dice;
        private SpecialTileDefinitionSO _electric;
        private AINode_Sequence _root;

        [SetUp]
        public void SetUp()
        {
            _dice = ScriptableObject.CreateInstance<RoomObjectDefinitionSO>();
            _electric = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            _root = GeneralaAssetBuilder.BuildAIRoot(_dice, _electric);
        }

        [TearDown]
        public void TearDown()
        {
            if (_dice != null) UnityEngine.Object.DestroyImmediate(_dice);
            if (_electric != null) UnityEngine.Object.DestroyImmediate(_electric);
        }

        [Test]
        public void Root_OpensTheTurnByLightingTheRingItMarkedLastTurn()
        {
            var ignite = Descendants(_root.Children[0]).OfType<AINode_IgniteArea>().FirstOrDefault();

            Assert.IsNotNull(ignite, "El primer hijo tiene que prender el anillo marcado el turno pasado.");
            Assert.AreSame(_electric, ignite.Definition, "El anillo tiene que plantar SU piso electrico.");
            Assert.AreEqual(GeneralaAssetBuilder.RingChannelId, ignite.ChannelId,
                "Sin el canal del ciclo la ignicion leeria el default, que es el que consume " +
                "AINode_ExecuteTelegraph.");
            Assert.AreEqual(GeneralaAssetBuilder.RingDurationRounds, ignite.DurationRounds);

            Assert.AreEqual(1, Descendants(_root).OfType<AINode_IgniteArea>().Count(),
                "Una sola ignicion: dos prenderian dos anillos por turno.");
        }

        [Test]
        public void Root_MarksTheRing_AfterLightingThePreviousOne()
        {
            int igniteIdx = _root.Children.FindIndex(c => Descendants(c).Any(n => n is AINode_IgniteArea));
            int markIdx = _root.Children.FindIndex(c => Descendants(c).Any(n => n is AINode_Alternate));

            // Al reves la marca nueva le comeria el canal a la ignicion y el anillo no cobraria nunca.
            Assert.Greater(igniteIdx, -1, "No se encontro la ignicion del anillo.");
            Assert.Greater(markIdx, igniteIdx, "La marca del anillo siguiente va DESPUES de prender el anterior.");
        }

        [Test]
        public void Root_TicksThePhaseGate_BeforeTheAttack()
        {
            int phaseIdx = _root.Children.FindIndex(c => Descendants(c).Any(n => n is AINode_SetHandReroll));
            int markIdx = _root.Children.FindIndex(c => Descendants(c).Any(n => n is AINode_Alternate));

            Assert.Greater(phaseIdx, -1, "No se encontro el gate de Fase 2.");
            Assert.Greater(markIdx, phaseIdx, "El gate de fase tiene que ir antes del ataque.");
        }

        [Test]
        public void Table_SpawnsFiveDice_FromTheRoomObjectDefinition()
        {
            var spawn = Descendants(_root).OfType<AINode_SpawnRoomObjects>().FirstOrDefault();

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
            var spawn = Descendants(_root).OfType<AINode_SpawnRoomObjects>().First();

            Assert.AreEqual(AINode_SpawnRoomObjects.Placement.DoorFronts, spawn.Pattern,
                "La mesa tiene que repartirse por la sala, no apilarse pegada a ella.");
        }

        [Test]
        public void DiceDefinition_CarriesTheTableNumbers_AndStaysOutOfTheTurnQueue()
        {
            // La reposición y el HP viven en la definición, no en el nodo.
            var table = ScriptableObject.CreateInstance<RoomObjectDefinitionSO>();
            table.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                GeneralaAssetBuilder.PopulateDiceDefinition(table, null);

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
            var table = ScriptableObject.CreateInstance<RoomObjectDefinitionSO>();
            table.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                GeneralaAssetBuilder.PopulateDiceDefinition(table, null);

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
            var spawn = Descendants(_root).OfType<AINode_SpawnRoomObjects>().First();

            // Es el único uso que tiene esa animación del rig (ver BossFeedbackInstaller).
            Assert.AreEqual(BossFeedbackIds.GeneralaSummonAnim, spawn.SpawnFeedbackId);
        }

        [Test]
        public void Table_IsNotWrappedInOnce_SoTheTableRefillsEveryTurn()
        {
            // El spawn se auto-gatea y necesita tickear cada turno para correr los
            // relojes de reposición.
            var owner = _root.Children.FirstOrDefault(c =>
                Descendants(c).Any(n => n is AINode_SpawnRoomObjects));

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
            typeof(AINode_IgniteArea),               // primer turno: no hay marca pendiente
            typeof(AINode_RotateBlock),              // sin IContractModifierService ni IComboLogService
            typeof(AINode_Move),                     // "ya estoy en la banda", la mayoría de sus turnos
        };

        [Test]
        public void RiskyNodes_AreIsolatedInSelectorsWithAWaitFallback()
        {
            // Un Failed suelto en el Sequence raíz le cancela al jefe el resto del turno.
            var risky = _root.Children
                .OfType<AINode_Selector>()
                .Where(s => Descendants(s).Any(n => RiskyNodeTypes.Contains(n.GetType())))
                .ToList();

            // Uno por nodo: compartir Selector saltearía al segundo en vez de aislarlo.
            Assert.AreEqual(RiskyNodeTypes.Length, risky.Count,
                "Cada nodo que puede devolver Failed va en su propio Selector de aislamiento: la " +
                "ignicion del anillo, la mesa, el setup de fase, el cubilete, la regla de la mano " +
                "repetida y el reposicionamiento.");
            foreach (var selector in risky)
            {
                Assert.IsTrue(selector.Children.Any(c => c is AINode_Wait),
                    "El Selector de aislamiento necesita un Wait de fallback, si no aborta igual.");
            }
        }

        [Test]
        public void RingCycle_MarksTheThreeRings_FromTheOutsideIn()
        {
            var beats = RingBeats();

            Assert.AreEqual(ThreatAreaShape.ConcentricRingCount, beats.Count,
                "El ciclo tiene que tener un tiempo por anillo.");
            for (int i = 0; i < beats.Count; i++)
            {
                Assert.AreEqual(ThreatShape.ConcentricRing, beats[i].Shape,
                    "Los anillos van centrados en la SALA: cualquier otra shape se corre con ella.");
                Assert.AreEqual(i + 1, beats[i].Size,
                    "El indice del anillo viaja en Size, de afuera (1) hacia adentro, y el orden del " +
                    "Alternate ES el orden del ciclo.");
            }
        }

        [Test]
        public void RingCycle_HitsForThirtyFive_OnEveryBeat()
        {
            foreach (var beat in RingBeats())
            {
                Assert.AreEqual(GeneralaAssetBuilder.RingDamage, beat.Damage,
                    "Los tres anillos pegan lo mismo: el chico no es mas barato de esquivar.");
                Assert.AreEqual(AttackKind.Environmental, beat.Kind,
                    "El piso no es un golpe suyo: si fuera BasicAttack le entraria su propio ataque base.");
            }
        }

        [Test]
        public void RingCycle_SharesTheChannelWithTheIgnition_SoTheMarkIsTheOneThatLights()
        {
            var ignite = Descendants(_root).OfType<AINode_IgniteArea>().First();

            foreach (var beat in RingBeats())
                Assert.AreEqual(ignite.ChannelId, beat.ChannelId,
                    "Marca e ignicion tienen que compartir canal: con canales distintos el anillo se " +
                    "pinta y no cobra nunca.");
        }

        [Test]
        public void RingCycle_RidesAnAlternate_SoItLoopsWithoutARoundCounter()
        {
            // PcRoundNumber solo sabe 'multiplo de N': no puede expresar 'resto 1 de 3'.
            var alternate = Descendants(_root).OfType<AINode_Alternate>().ToList();

            Assert.AreEqual(1, alternate.Count, "Un solo Alternate: dos desincronizarian el ciclo.");
            Assert.AreEqual(ThreatAreaShape.ConcentricRingCount, alternate[0].Children.Count);
        }

        [Test]
        public void RingCycle_IsNotIsolated_SoAFailedMarkIsLoud()
        {
            // Aislarlo esconderia el turno en que la sala no tiene bounds y el anillo sale vacio:
            // el jefe pasaria turnos sin atacar y el arbol diria que todo salio bien.
            var owner = _root.Children.FirstOrDefault(c => c is AINode_Alternate);

            Assert.IsNotNull(owner, "El ciclo del anillo tiene que colgar directo del Sequence raiz.");
        }

        [Test]
        public void CupSlam_HitsForTwelve_AtOneTileInManhattan()
        {
            var cup = FindCupSlam();

            // Mismo alcance con el que el jugador le pega a ella: si le llegás, te llega.
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
            var branch = FindCupBranch();

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
            var branch = FindCupBranch();

            Assert.IsFalse(Descendants(branch).OfType<AINode_AuxTelegraph>().Any(),
                "El cubilete no ocupa canal de aviso: cobra en el acto.");
            Assert.IsFalse(Descendants(branch).OfType<AINode_TelegraphMark>().Any(),
                "Ni marca área — el único aviso es la distancia, que el jugador controla entera.");
        }

        [Test]
        public void RepeatBan_ForbidsExactlyTheLastComboScored()
        {
            var ban = Descendants(_root).OfType<AINode_RotateBlock>().FirstOrDefault();

            Assert.IsNotNull(ban, "Falta la regla de la mano repetida.");
            Assert.AreEqual(AINode_RotateBlock.BlockTarget.Combo, ban.Target,
                "Modo Dice bloquearía dados de la build — eso es del jefe del piso 1.");
            Assert.AreEqual(1, ban.Count, "Uno solo: el último. Dos serían dos manos prohibidas.");
            Assert.AreEqual(GeneralaAssetBuilder.RepeatBanWindow, ban.Count);
        }

        [Test]
        public void RepeatBan_IsPromulgatedAtTheEndOfHerTurn_SoThePlayerSeesItBeforeRolling()
        {
            int markIdx = _root.Children.FindIndex(c => Descendants(c).Any(n => n is AINode_Alternate));
            int banIdx = _root.Children.FindIndex(c => Descendants(c).Any(n => n is AINode_RotateBlock));

            // La fila sale tachada en el Contrato ANTES de que el jugador comprometa dados.
            Assert.Greater(banIdx, -1, "No se encontró la regla en el Sequence raíz.");
            Assert.Greater(banIdx, markIdx, "La regla se promulga al cierre del turno, no al abrirlo.");
        }

        [Test]
        public void Reposition_ChasesOnALeash_InsteadOfFleeing()
        {
            var move = Descendants(_root).OfType<AINode_Move>().FirstOrDefault();

            // La correa: cierra distancia hasta DesiredRange y devuelve Failed (se queda
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
            Assert.Greater(GeneralaAssetBuilder.RepositionRange, GeneralaAssetBuilder.CupSlamRange,
                "Su banda tiene que quedar FUERA del alcance del cubilete: si se pegara sola, el " +
                "peaje de acercarse dejaría de elegirlo el jugador.");
        }

        [Test]
        public void Reposition_GoesLast_SoTheCupResolvesFromWhereSheStood()
        {
            var last = _root.Children.Last();
            Assert.IsTrue(Descendants(last).OfType<AINode_Move>().Any(),
                "El reposicionamiento tiene que ser el último hijo del Sequence raíz.");

            Assert.AreEqual(1, Descendants(_root).OfType<AINode_Move>().Count(),
                "Un solo nodo de movimiento: dos serían dos desplazamientos por turno.");
            Assert.IsFalse(Descendants(_root).OfType<AINode_KeepDistance>().Any(),
                "KeepDistance sólo kitea — convivir con Move duplicaría la lógica de distancia.");
        }

        [Test]
        public void Damage_NeverExceedsTheFloorThreeCeiling()
        {
            // Techo de daño por golpe del piso 3.
            const int floorThreeCeiling = 45;

            foreach (var mark in Descendants(_root).OfType<AINode_TelegraphMark>())
                Assert.LessOrEqual(mark.Damage, floorThreeCeiling,
                    $"Un TelegraphMark ({mark.Shape}) pega {mark.Damage}, sobre el techo del piso 3.");

            // El cubilete no se avisa: entra sí o sí por estar parado al lado.
            foreach (var cup in Descendants(_root).OfType<AINode_GeneralaCupSlam>())
                Assert.LessOrEqual(cup.Damage, floorThreeCeiling,
                    $"El cubilete pega {cup.Damage}, sobre el techo del piso 3.");
        }

        [Test]
        public void PhaseTwo_AtFiftyPercent_GivesRerollAndAdoptsTheWeakness_Once()
        {
            var gate = Descendants(_root)
                .OfType<AINode_If>()
                .FirstOrDefault(g => g.Conditions != null && g.Conditions.OfType<PcOwnerHpBelow>()
                    .Any(p => Mathf.Abs(p.Percent - GeneralaAssetBuilder.Phase2HpThreshold) < 0.0001f));

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

        [Test]
        public void PopulateEnemyData_WritesTheSpecdStatsAndIdentity()
        {
            var boss = ScriptableObject.CreateInstance<EnemyDataSO>();
            try
            {
                GeneralaAssetBuilder.PopulateEnemyData(boss, _dice, null);

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
            var dice = ScriptableObject.CreateInstance<EnemyDataSO>();
            try
            {
                GeneralaAssetBuilder.PopulateDiceData(dice, null);

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
            var boss = ScriptableObject.CreateInstance<EnemyDataSO>();
            var visual = new GameObject("PF_Boss_Generala_Probe");
            var portrait = MakeSprite();
            try
            {
                GeneralaAssetBuilder.PopulateEnemyData(boss, _dice, visual, portrait);

                // Sin VisualPrefab, EntityVisualService loguea error y no spawnea nada.
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
            // El builder es re-ejecutable: una corrida sin arte no puede borrar el wiring.
            var boss = ScriptableObject.CreateInstance<EnemyDataSO>();
            var visual = new GameObject("PF_Boss_Generala_Probe");
            var portrait = MakeSprite();
            try
            {
                GeneralaAssetBuilder.PopulateEnemyData(boss, _dice, visual, portrait);

                GeneralaAssetBuilder.PopulateEnemyData(boss, _dice, null, null);

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
            var dice = ScriptableObject.CreateInstance<EnemyDataSO>();
            var visual = new GameObject("PF_Obj_DadoCasa_Probe");
            var portrait = MakeSprite();
            try
            {
                GeneralaAssetBuilder.PopulateDiceData(dice, visual, portrait);

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

        /// <summary>Los tiempos del ciclo, en el orden en que el Alternate los rota.</summary>
        private List<AINode_TelegraphMark> RingBeats()
        {
            var alternate = Descendants(_root).OfType<AINode_Alternate>().FirstOrDefault();
            Assert.IsNotNull(alternate, "No se encontró el ciclo del anillo.");

            var beats = alternate.Children.OfType<AINode_TelegraphMark>().ToList();
            Assert.AreEqual(alternate.Children.Count, beats.Count,
                "Todos los tiempos del ciclo tienen que ser marcas: uno que no lo sea es un turno mudo.");
            return beats;
        }

        /// <summary>Tree-walker por reflexión.</summary>
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
