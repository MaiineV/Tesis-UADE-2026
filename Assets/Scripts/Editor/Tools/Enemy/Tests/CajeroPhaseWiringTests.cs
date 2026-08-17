using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Threat;
using Rollgeon.Combos;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.EditorTools;
using Rollgeon.Entities;
using Rollgeon.Feedback;
using Rollgeon.PreConditions.Concretes;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>
    /// Valida el wiring del árbol de <b>El Cajero</b> (piso 2) construido por
    /// <see cref="CajeroAssetBuilder"/>, <b>en memoria</b> — sin cargar el <c>.asset</c>.
    /// </summary>
    /// <remarks>
    /// Deliberadamente contra el builder y no contra el asset: los seis jefes nuevos se autoran en
    /// ramas paralelas y un test que dependa del <c>.asset</c> falla por reimports, deserialización
    /// vieja o merges de YAML en vez de por diseño roto. El asset lo genera el mismo builder que se
    /// testea acá, así que lo que se afirma es la fuente de verdad.
    /// <para>
    /// Lo que se cuida es el patrón de fase que ya rompió una vez (Sunken Grand): gates
    /// <b>antes</b> del ataque, todo lo que puede devolver Failed aislado en
    /// <c>Selector[acción, Wait]</c>, y <c>Once</c> sólo alrededor del one-shot real.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class CajeroPhaseWiringTests
    {
        private const float PercentTolerance = 0.0001f;

        private AINode_Sequence _root;

        [SetUp]
        public void SetUp()
        {
            _root = CajeroAssetBuilder.BuildAIRoot();
            Assert.IsNotNull(_root, "El builder tiene que devolver un Sequence raíz.");
        }

        // ---- Forma del turno ---------------------------------------------

        [Test]
        public void Root_StartsByDetonatingLastTurnsColumn()
        {
            Assert.IsInstanceOf<AINode_ExecuteTelegraph>(_root.Children[0],
                "El turno arranca resolviendo el telegráfico del turno anterior.");
        }

        [Test]
        public void Root_HasTheStepsOfTheSheet()
        {
            Assert.AreEqual(7, _root.Children.Count,
                "Detona → arqueo → Comisiones → arma el peaje → ataca (marca o dispara) → suelta → se corre.");
            Assert.IsNotNull(FindNode<AINode_TelegraphMarkGoldScaled>(), "Falta la columna que engorda.");
            Assert.IsNotNull(FindNode<AINode_CashierRangedShot>(), "Falta el disparo de los turnos sin columna.");
            Assert.IsNotNull(FindNode<AINode_CashierCounterToll>(), "Falta el peaje del mostrador.");
            Assert.IsNotNull(FindNode<AINode_CashierDropChips>(), "Faltan las fichas.");
            Assert.IsNotNull(FindNode<AINode_KeepDistance>(), "Falta el repliegue al otro lado del mostrador.");
            Assert.IsNotNull(FindNode<AINode_CashierAudit>(), "Falta el arqueo de caja.");
            Assert.IsNotNull(FindNode<AINode_SpawnReinforcements>(), "Faltan las Comisiones del 50%.");
        }

        [Test]
        public void Boss_HasNoMelee_AndItsDirectDamageIsTheSheetShot()
        {
            Assert.IsEmpty(Descendants(_root).OfType<AINode_Behavior>().ToList(),
                "El Cajero no pelea cuerpo a cuerpo: se repliega y cobra a distancia.");
            Assert.IsEmpty(Descendants(_root).OfType<AINode_TelegraphMark>().ToList(),
                "La columna tiene que salir del nodo escalado por oro, no de un TelegraphMark plano " +
                "con daño fijo (sería el jefe sin su mecánica).");

            // La ficha le dio un ataque directo — el disparo de los turnos sin columna — porque la
            // columna sola se esquivaba con un paso. Es el único daño suyo que no pasa por el área.
            var shot = FindNode<AINode_CashierRangedShot>();
            Assert.AreEqual(12, shot.Damage, "El disparo pega 12 fijos, no escala con el oro.");
            Assert.AreEqual(4, shot.Range,
                "Alcance 4: pegarle exige distancia 1, y distancia 1 tiene que estar adentro.");
        }

        // ---- Gate de fase -------------------------------------------------

        [Test]
        public void AuditGate_TriggersAtFiftyPercentHp()
        {
            var gate = FindGateAtPercent<AINode_CashierAudit>(0.5f);

            Assert.IsNotNull(gate, "No hay gate de HP al 50% — el arqueo nunca dispararía.");
            Assert.IsNotNull(gate.Else, "El gate necesita Else (un If sin rama devuelve Failed y aborta el turno).");
            Assert.IsInstanceOf<AINode_Wait>(gate.Else);
        }

        [Test]
        public void AuditGate_RunsBeforeTheAttack()
        {
            int gateIdx = IndexOfGateAtPercent<AINode_CashierAudit>(0.5f);
            int attackIdx = _root.Children.FindIndex(c =>
                Descendants(c).Any(n => n is AINode_TelegraphMarkGoldScaled));

            Assert.Greater(gateIdx, -1);
            Assert.Greater(attackIdx, gateIdx,
                "Las fases van antes del ataque: en el path no-coroutine un Running del ataque " +
                "aborta la secuencia y el arqueo no se cobraría nunca.");
        }

        [Test]
        public void AuditGate_IsLatchedOnce_AndThenAnnouncesPhaseTwo()
        {
            var gate = FindGateAtPercent<AINode_CashierAudit>(0.5f);
            var once = gate.Then as AINode_Once;

            Assert.IsNotNull(once, "El arqueo es un one-shot: sin Once se cobraría el 40% todos los turnos.");
            var sequence = once.Child as AINode_Sequence;
            Assert.IsNotNull(sequence, "Once → Sequence[Audit, ApplyStatModifier].");
            Assert.IsInstanceOf<AINode_CashierAudit>(sequence.Children[0], "Primero cobra…");
            var phase = sequence.Children[1] as AINode_ApplyStatModifier;
            Assert.IsNotNull(phase, "…y después anuncia la fase.");
            Assert.AreEqual(2, phase.PhaseIndex);
            Assert.IsTrue(phase.EmitPhaseChangedEvent, "Sin el evento la Fase 2 no tiene feedback.");
            Assert.AreEqual(0, phase.AttackDelta,
                "El daño del Cajero lo decide el oro, no la fase: ningún delta de Attack.");
            Assert.AreEqual(0, phase.SpeedDelta);
        }

        [Test]
        public void Once_WrapsOnlyThePhaseGates_SoChipsAndColumnKeepRunning()
        {
            var latches = Descendants(_root).OfType<AINode_Once>().ToList();

            Assert.AreEqual(2, latches.Count,
                "Los one-shots del jefe son dos y sólo dos: el arqueo y las Comisiones.");
            Assert.AreEqual(1, latches.Count(l => Descendants(l).OfType<AINode_CashierAudit>().Any()),
                "Falta el Once del arqueo.");
            Assert.AreEqual(1, latches.Count(l => Descendants(l).OfType<AINode_SpawnReinforcements>().Any()),
                "Falta el Once de las Comisiones.");

            foreach (var latch in latches)
            {
                Assert.IsEmpty(Descendants(latch).OfType<AINode_CashierDropChips>().ToList(),
                    "Las fichas se sueltan todos los turnos en que le peguen — un Once las latchearía.");
                Assert.IsEmpty(Descendants(latch).OfType<AINode_TelegraphMarkGoldScaled>().ToList(),
                    "La columna se marca todos los turnos pares — un Once la apagaría después del primero.");
            }
        }

        // ---- Las Comisiones -----------------------------------------------

        [Test]
        public void CritterGate_SpawnsTwoOfThemAtFiftyPercentHp()
        {
            var gate = FindGateAtPercent<AINode_SpawnReinforcements>(0.5f);

            Assert.IsNotNull(gate, "No hay gate de HP al 50% para las Comisiones.");
            Assert.IsInstanceOf<AINode_Wait>(gate.Else,
                "El gate necesita Else: un If sin rama devuelve Failed y aborta el turno.");

            var spawn = FindNode<AINode_SpawnReinforcements>();
            Assert.AreEqual(2, spawn.Count, "Dos bichos, los que pidió el diseño.");
            Assert.AreEqual(CajeroAssetBuilder.CritterCount, spawn.Count,
                "El nodo tiene que salir cableado desde la constante de la ficha, no de su default.");
        }

        /// <summary>
        /// Sin <see cref="AINode_Once"/> el nodo se auto-gatea y repone la oleada cada vez que la
        /// matan (así lo usa La Generala para su mesa de dados). Acá eso sería una pelea que no
        /// termina: el jefe se cura hasta 30 en el arqueo del mismo umbral.
        /// </summary>
        [Test]
        public void CritterGate_IsLatchedOnce_SoTheWaveNeverRespawns()
        {
            var gate = FindGateAtPercent<AINode_SpawnReinforcements>(0.5f);
            var once = gate.Then as AINode_Once;

            Assert.IsNotNull(once, "Sin Once las Comisiones se repondrían para siempre.");
            Assert.IsInstanceOf<AINode_SpawnReinforcements>(once.Child,
                "El Once envuelve el spawn y nada más.");
        }

        /// <summary>
        /// Comparten umbral con el arqueo a propósito —cruzar la mitad es UN momento de la pelea—
        /// pero no comparten latch: <c>AINode_SpawnReinforcements</c> devuelve Failed cuando la sala
        /// no tiene tiles de borde libres, y un Failed adentro del Sequence del arqueo impediría que
        /// su <c>Once</c> latcheara. El turno siguiente el arqueo volvería a cobrar el 40% del oro y
        /// a curar hasta 30.
        /// </summary>
        [Test]
        public void CritterGate_HasItsOwnLatch_SoAFailedSpawnCannotRechargeTheAudit()
        {
            var auditLatch = FindGateAtPercent<AINode_CashierAudit>(0.5f).Then as AINode_Once;
            var critterLatch = FindGateAtPercent<AINode_SpawnReinforcements>(0.5f).Then as AINode_Once;

            Assert.IsNotNull(auditLatch);
            Assert.IsNotNull(critterLatch);
            Assert.AreNotSame(auditLatch, critterLatch, "Son dos gates y dos latches, no uno.");
            Assert.IsEmpty(Descendants(auditLatch).OfType<AINode_SpawnReinforcements>().ToList(),
                "El spawn no puede colgar del Once del arqueo: su Failed dejaría el arqueo sin latchear.");
        }

        [Test]
        public void CritterGate_RunsBeforeTheAttack_LikeEveryOtherPhaseGate()
        {
            int gateIdx = IndexOfGateAtPercent<AINode_SpawnReinforcements>(0.5f);
            int attackIdx = _root.Children.FindIndex(c =>
                Descendants(c).Any(n => n is AINode_TelegraphMarkGoldScaled));

            Assert.Greater(gateIdx, -1);
            Assert.Greater(attackIdx, gateIdx,
                "Las fases van antes del ataque: en el path no-coroutine un Running del ataque " +
                "aborta la secuencia y las Comisiones no saldrían nunca.");
        }

        [Test]
        public void CritterGate_AnimatesTheSummon_SoTheyDoNotAppearOutOfNowhere()
        {
            var spawn = FindNode<AINode_SpawnReinforcements>();

            Assert.AreEqual(BossFeedbackIds.CajeroMeleeAnim, spawn.SpawnFeedbackId,
                "Es el trigger 'Attack', el único no-idle de AnimCon_GeneralDirector. Sin gesto, " +
                "dos bichos se materializan con el jefe quieto y no se leen como cosa suya.");
        }

        [Test]
        public void CritterGate_TakesTheEnemyDataHandedToTheBuilder()
        {
            var critter = ScriptableObject.CreateInstance<EnemyDataSO>();
            critter.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var root = CajeroAssetBuilder.BuildAIRoot(chip: null, critter: critter);
                var spawn = Descendants(root).OfType<AINode_SpawnReinforcements>().First();

                Assert.AreSame(critter, spawn.EnemyToSpawn,
                    "El MenuItem crea el ED_Min_Comision y lo inyecta acá; en null el nodo devuelve " +
                    "Failed todos los turnos y no sale nada.");
            }
            finally
            {
                Object.DestroyImmediate(critter);
            }
        }

        // ---- La ficha de la Comisión ---------------------------------------

        [Test]
        public void CritterData_IsSmallWeakAndWorthNoGold()
        {
            var critter = ScriptableObject.CreateInstance<EnemyDataSO>();
            critter.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                CajeroAssetBuilder.PopulateCritterData(critter);

                Assert.AreEqual("minion.cajero_comision", critter.EntityId);
                Assert.AreEqual("Comisión", critter.DisplayName);
                Assert.AreEqual(18, critter.BaseHP,
                    "Muere de un golpe de la mediana del piso 2 (24): sacárselos de encima cuesta " +
                    "un golpe cada uno, y ese es todo el precio.");
                Assert.Less(critter.BaseHP, CajeroAssetBuilder.BaseHP / 4,
                    "Es un bicho, no un segundo jefe.");
                Assert.AreEqual(6, critter.BaseAttack, "Mordisco flojo: los dos juntos pegan 12.");
                Assert.Less(2 * critter.BaseAttack, CajeroAssetBuilder.CounterTollDamage + 1,
                    "Los dos juntos no pueden pegar más que el peaje: son un impuesto por dejarlos " +
                    "vivos, no la amenaza principal.");
                Assert.AreEqual(1, critter.BaseAttackRange, "Muerden pegados.");
            }
            finally
            {
                Object.DestroyImmediate(critter);
            }
        }

        /// <summary>
        /// El daño de la columna del Cajero escala con el oro que el jugador lleva encima (ver
        /// <c>BuildGoldTiers</c>): una Comisión que pague al morir le sube el escalón al jefe, o sea
        /// que matarlas haría la pelea <b>más</b> difícil.
        /// </summary>
        [Test]
        public void CritterData_DropsNoGold_BecauseGoldIsWhatFeedsHisColumn()
        {
            var critter = ScriptableObject.CreateInstance<EnemyDataSO>();
            critter.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                CajeroAssetBuilder.PopulateCritterData(critter);

                Assert.AreEqual(0, critter.MinGoldDrop);
                Assert.AreEqual(0, critter.MaxGoldDrop);
            }
            finally
            {
                Object.DestroyImmediate(critter);
            }
        }

        /// <summary>
        /// Sin árbol propio el spawn cae al <c>BasicEnemyAI</c>, que le pega al jugador desde
        /// cualquier distancia y sin moverse: un impuesto inesquivable en vez de un bicho que se
        /// puede kitear o matar antes de que llegue.
        /// </summary>
        /// <summary>
        /// El refuerzo del Cajero es el ranged común del juego, no la Comisión. La Comisión mordía a
        /// distancia 1 con la malla del GeneralDirector —la misma que usa el ranged común—, así que en
        /// pantalla se leía como el enemigo ranged andando mal, no como un bicho distinto.
        /// </summary>
        /// <remarks>
        /// Las funciones que autoran la Comisión siguen vivas y con tests (parkeada, no borrada), así
        /// que sin este test nada impide que el árbol del jefe vuelva a apuntarle sin que se note.
        /// </remarks>
        [Test]
        public void Reinforcements_AreTheGameGenericRangedEnemy_NotTheComision()
        {
            Assert.AreEqual("Assets/Rollgeon/Enemies/ED_RangedEnemy.asset",
                CajeroAssetBuilder.ReinforcementAssetPath,
                "El refuerzo tiene que ser el ranged común: mismo look y mismo kit que el resto.");
            Assert.AreNotEqual(CajeroAssetBuilder.CritterAssetPath,
                CajeroAssetBuilder.ReinforcementAssetPath,
                "Si el refuerzo vuelve a ser la Comisión, vuelve el melee disfrazado de ranged.");
        }

        [Test]
        public void CritterAI_BitesFirstAndFliesAfter_SoArrivingDoesNotEatItsAttack()
        {
            var root = CajeroAssetBuilder.BuildCritterAIRoot();

            Assert.IsNotNull(root, "La Comisión necesita árbol propio: sin él cae al BasicEnemyAI.");
            Assert.AreEqual(2, root.Children.Count, "Muerde y vuela, nada más.");

            int biteIdx = root.Children.FindIndex(c =>
                Descendants(c).Any(n => n is AINode_CashierRangedShot));
            int moveIdx = root.Children.FindIndex(c =>
                Descendants(c).Any(n => n is AINode_Move));

            Assert.Greater(biteIdx, -1, "Falta el mordisco.");
            Assert.Greater(moveIdx, biteIdx,
                "AINode_Move devuelve Running cuando se mueve, y un Running corta el Sequence: con " +
                "el orden invertido, el turno en que llega al jugador se le comería el mordisco.");
        }

        [Test]
        public void CritterAI_BiteIsMelee_NotTheBossRangedShot()
        {
            var bite = Descendants(CajeroAssetBuilder.BuildCritterAIRoot())
                .OfType<AINode_CashierRangedShot>().First();

            Assert.AreEqual(1, bite.Range,
                "Range 1 es lo que convierte el disparo del jefe en un mordisco. Con más, la " +
                "Comisión pega sin acercarse y deja de poder esquivarse caminando.");
            Assert.AreEqual(6, bite.Damage);
            Assert.AreEqual(CajeroAssetBuilder.CritterDamage, bite.Damage,
                "Cableado desde la constante de la ficha, no del default de 12 del nodo.");
        }

        [Test]
        public void CritterAI_EveryStepIsIsolated_SoABenignFailedDoesNotEatItsTurn()
        {
            var root = CajeroAssetBuilder.BuildCritterAIRoot();

            // El mordisco falla con el jugador lejos y el vuelo falla cuando ya está pegada: los dos
            // Failed son normales y ninguno tiene que abortar el turno del bicho.
            foreach (var child in root.Children)
            {
                var selector = child as AINode_Selector;
                Assert.IsNotNull(selector, "Cada paso de la Comisión va en Selector[acción, Wait].");
                Assert.IsTrue(selector.Children.Any(c => c is AINode_Wait),
                    "El Selector sin Wait de fallback devuelve Failed igual.");
            }
        }

        // ---- Aislamiento de fallos ---------------------------------------

        [Test]
        public void EveryFallibleChild_IsIsolatedInSelectorWithWaitFallback()
        {
            // Todos los hijos salvo ExecuteTelegraph (que siempre sucede) pueden devolver Failed:
            // KeepDistance cuando ya está lejos, DropChips cuando no le pegaron, la columna con área
            // vacía, el disparo con el jugador fuera de rango, el peaje sin jugador en contexto, y
            // el gate cuando su rama falla. Suelto en el Sequence, cualquiera de esos aborta el
            // turno entero — el bug que dejó quieto al Sunken Grand.
            for (int i = 1; i < _root.Children.Count; i++)
            {
                var selector = _root.Children[i] as AINode_Selector;
                Assert.IsNotNull(selector,
                    $"El hijo {i} del Sequence raíz no está envuelto en Selector: su Failed abortaría el turno.");
                Assert.IsTrue(selector.Children.Any(c => c is AINode_Wait),
                    $"El Selector del hijo {i} no tiene Wait de fallback — devolvería Failed igual.");
            }
        }

        [Test]
        public void KeepDistance_IsNeverLooseInTheRootSequence()
        {
            var wrapper = _root.Children.OfType<AINode_Selector>()
                .FirstOrDefault(s => s.Children.Any(c => c is AINode_KeepDistance));

            Assert.IsNotNull(wrapper,
                "KeepDistance suelto en el Sequence raíz: su Failed benigno ('ya estoy lejos') " +
                "abortaría el arqueo y la fase 2.");
            Assert.IsTrue(wrapper.Children.Any(c => c is AINode_Wait));
        }

        // ---- La columna que engorda ---------------------------------------

        /// <summary>
        /// Los umbrales son 40/120 y no los 80/250 de la primera pasada: el jugador llega al piso 2
        /// con ~65-70 de oro, así que con 80/250 la columna vivía clavada en el escalón pobre y el
        /// jefe medía 0% de vida perdida en la mediana de 3000 peleas simuladas. Con 40/120, 65 de
        /// oro ya paga el escalón medio.
        /// </summary>
        [Test]
        public void Column_ScalesWithGold_AtFortyAndOneTwenty()
        {
            var column = FindNode<AINode_TelegraphMarkGoldScaled>();

            Assert.AreEqual(ThreatShape.ColumnAroundSelf, column.Shape,
                "Es una columna, no una fila — y anclada en el jefe: centrada en el jugador la recta " +
                "lo perseguía y se esquivaba con un paso al costado.");
            Assert.IsTrue(ThreatAreaShape.AnchorsOnSelf(column.Shape),
                "Si la shape deja de anclarse en el jefe, la recta vuelve a salir del jugador.");
            Assert.IsTrue(column.ApplyBribeStepDown, "El soborno tiene que poder bajarle un escalón.");
            Assert.AreEqual(3, column.Tiers.Count, "Tres escalones: pobre, medio y rico.");

            var ranked = column.Tiers.OrderBy(t => t.MinGold).ToList();
            Assert.AreEqual(0, ranked[0].MinGold);
            Assert.AreEqual(1, ranked[0].ColumnSize);
            Assert.AreEqual(14, ranked[0].Damage);

            Assert.AreEqual(40, ranked[1].MinGold,
                "El escalón medio arranca en 40: con el oro real de entrada al piso 2 tiene que " +
                "ser el default, no el premio.");
            Assert.AreEqual(3, ranked[1].ColumnSize);
            Assert.AreEqual(28, ranked[1].Damage);

            Assert.AreEqual(120, ranked[2].MinGold,
                "El escalón rico queda a una tanda de fichas de distancia, no a una run entera.");
            Assert.AreEqual(3, ranked[2].ColumnSize);
            Assert.AreEqual(35, ranked[2].Damage);
        }

        [Test]
        public void Column_NeverExceedsFloorTwoDamageCeiling()
        {
            var column = FindNode<AINode_TelegraphMarkGoldScaled>();

            foreach (var tier in column.Tiers)
            {
                Assert.LessOrEqual(tier.Damage, 35,
                    $"El escalón desde {tier.MinGold} de oro pega {tier.Damage} — el techo de piso 2 es 35.");
            }
        }

        // ---- El peaje -----------------------------------------------------

        [Test]
        public void Toll_ChargesTheSheetTwenty()
        {
            var toll = FindNode<AINode_CashierCounterToll>();

            Assert.AreEqual(CajeroAssetBuilder.CounterTollDamage, toll.Damage,
                "El nodo tiene que salir cableado desde la constante de la ficha, no de su default.");
            Assert.AreEqual(20, toll.Damage,
                "Sin peaje, elegir abertura no cuesta nada y el mostrador es decorado. A 10 el " +
                "peaje salía más barato que replegarse y convenía comerlo; a 20 quedarse del lado " +
                "de él vuelve a ser una decisión.");
            Assert.Less(toll.Damage, 35,
                "El peaje es el precio de una posición, no su ataque: tiene que quedar por debajo " +
                "del techo de daño por golpe del piso 2.");
        }

        /// <summary>
        /// El jefe no puede leer el terreno (los blockers son agujeros en el NavGraph, no props
        /// tipados), así que la fila del mostrador va autorada. Este cruce contra el plano que
        /// hornea <see cref="BossRoomBuilder"/> es lo único que impide que mover el mostrador deje
        /// el peaje cobrando sobre una fila vacía — que no rompe nada, sólo deja de cobrar.
        /// </summary>
        [Test]
        public void Toll_UsesTheRowTheRoomBuilderBakesTheCounterOn()
        {
            var plan = BossRoomBuilder.Plans.FirstOrDefault(p => p.BossName == "Cajero");
            Assert.IsNotNull(plan, "No hay plano de sala del Cajero en BossRoomBuilder.Plans.");

            var counterRows = plan.BlockerPlanCells
                .Select(cell => BossRoomBuilder.PlanToRoom(cell).Y)
                .Distinct()
                .ToList();

            Assert.AreEqual(1, counterRows.Count,
                "El mostrador es una fila sola: si el plano bloquea más de una, 'el lado' deja de " +
                "estar definido por un solo número y el peaje necesita otra regla.");
            Assert.AreEqual(counterRows[0], CajeroAssetBuilder.CounterRow,
                "La fila autorada en la ficha no es la fila donde la sala pone el mostrador.");

            int bossRow = BossRoomBuilder.PlanToRoom(plan.BossPlanCell).Y;
            Assert.AreNotEqual(CajeroAssetBuilder.CounterRow, bossRow,
                "El jefe spawnea dentro del mostrador: sin lado propio no hay lado que compartir " +
                "y el peaje no cobraría nunca.");
        }

        [Test]
        public void Toll_IsArmedBeforeTheAttack_SoARunningCannotSkipIt()
        {
            int tollIdx = _root.Children.FindIndex(c =>
                Descendants(c).Any(n => n is AINode_CashierCounterToll));
            int attackIdx = _root.Children.FindIndex(c =>
                Descendants(c).Any(n => n is AINode_TelegraphMarkGoldScaled));

            Assert.Greater(tollIdx, -1);
            Assert.Greater(attackIdx, tollIdx,
                "El peaje arma el cobro del cierre de turno del jugador: en el path no-coroutine " +
                "un Running del ataque lo dejaría sin armar justo en los turnos en que el jefe actuó.");
        }

        // ---- Fichas -------------------------------------------------------

        [Test]
        public void Chips_DropAfterTheColumn_SoTheyLandInsideIt()
        {
            int columnIdx = _root.Children.FindIndex(c =>
                Descendants(c).Any(n => n is AINode_TelegraphMarkGoldScaled));
            int chipsIdx = _root.Children.FindIndex(c =>
                Descendants(c).Any(n => n is AINode_CashierDropChips));

            Assert.Greater(chipsIdx, columnIdx,
                "La ficha cae dentro de la columna recién marcada: el nodo lee el área pendiente, " +
                "así que tiene que correr después de marcarla.");
        }

        [Test]
        public void Chips_UseTheSheetNumbers()
        {
            var chips = FindNode<AINode_CashierDropChips>();

            Assert.AreEqual(CajeroAssetBuilder.ChipCount, chips.Count, "Dos fichas cuando le pegaron.");
            Assert.AreEqual(CajeroAssetBuilder.ChipMinCount, chips.MinCount,
                "Y una garantizada cuando no: sin piso, la ficha pedía turno de columna Y golpe " +
                "recibido Y casilla libre, y en el playtest se vio una sola moneda en toda la pelea.");
            Assert.AreEqual(6, chips.MinValue);
            Assert.AreEqual(9, chips.MaxValue);
            Assert.AreEqual(2, chips.MinDistanceFromPlayer, "A 2-3 casillas: agarrarla cuesta el movimiento.");
            Assert.AreEqual(3, chips.MaxDistanceFromPlayer);
            Assert.IsTrue(chips.RequireDamageTaken,
                "El bonus por lastimarlo sigue: el piso lo complementa, no lo reemplaza.");
            Assert.Greater(chips.Count, chips.MinCount,
                "Pegarle tiene que pagar más que no pegarle, o el personaje deja de leerse.");
        }

        [Test]
        public void CounterToll_LeavesAFreeRound_SoTheBossCanBeApproached()
        {
            var toll = FindNode<AINode_CashierCounterToll>();

            // Cobrando todas las rondas el peaje deja de ser el precio de una posición: pegarle exige
            // distancia 1, y distancia 1 está de su lado, así que acercarse costaba 20 por ronda para
            // siempre — con el disparo castigándote por quedarte lejos, la tenaza no tenía salida.
            Assert.AreEqual(CajeroAssetBuilder.CounterTollEveryNRounds, toll.ChargesEveryNRounds);
            Assert.GreaterOrEqual(toll.ChargesEveryNRounds, 2,
                "Sin ronda franca la respuesta correcta a este jefe es no entrar nunca a su lado.");
            Assert.AreEqual(CajeroAssetBuilder.CounterTollDamage, toll.Damage);
            Assert.AreEqual(CajeroAssetBuilder.CounterRow, toll.CounterRow);
        }

        [Test]
        public void ChipHazard_LastsLongEnoughForThePlayerToStepOnIt()
        {
            // El guard que le faltaba al Cajero y sí tienen el Anotador, el Croupier y la Generala.
            // La duración se descuenta una vez por wrap de ronda y la ficha nace en el turno del jefe,
            // con el turno del jugador de esa ronda ya jugado (CNF-006): DurationRounds = D deja D-1
            // turnos pisables. Con 1 la moneda aparecía y expiraba sin que el jugador pudiera nunca
            // levantarla.
            Assert.GreaterOrEqual(CajeroAssetBuilder.ChipDurationRounds - 1, 2,
                $"Con DurationRounds = {CajeroAssetBuilder.ChipDurationRounds} la ficha vive " +
                $"{CajeroAssetBuilder.ChipDurationRounds - 1} turnos pisables del jugador. Hacen falta " +
                "dos: la ficha cae DENTRO de la columna marcada, así que el primer turno para " +
                "levantarla es el mismo en el que pararse ahí cobra el golpe. Con uno solo, agarrarla " +
                "exigía que el único paso disponible fuera exactamente ése.");
        }

        [Test]
        public void Chips_TakeTheHazardDefinitionHandedToTheBuilder()
        {
            var definition = ScriptableObject.CreateInstance<HazardDefinitionSO>();
            definition.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var root = CajeroAssetBuilder.BuildAIRoot(definition);
                var chips = Descendants(root).OfType<AINode_CashierDropChips>().First();

                Assert.AreSame(definition, chips.Chip,
                    "El MenuItem crea el HazardDefinitionSO de la ficha y lo inyecta acá.");
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        // ---- Arqueo -------------------------------------------------------

        [Test]
        public void Audit_UsesTheSheetNumbers()
        {
            var audit = FindNode<AINode_CashierAudit>();

            Assert.AreEqual(0.4f, audit.TaxPercent, PercentTolerance, "Guarda el 40% del oro.");
            Assert.AreEqual(30, audit.MaxHeal, "Cura hasta +30 de vida.");
            Assert.AreEqual(2, audit.ChipValueMultiplierAfterAudit, "Después del arqueo las fichas valen el doble.");
        }

        // ---- Repliegue ----------------------------------------------------

        [Test]
        public void KeepDistance_KitesToFourTiles()
        {
            var keep = FindNode<AINode_KeepDistance>();

            Assert.IsNotNull(keep.IdealDistance);
            Assert.AreEqual(4, keep.IdealDistance.Read(null), "Se repliega a distancia 4.");
            Assert.IsNotNull(keep.MaxSteps);
            Assert.AreEqual(3, keep.MaxSteps.Read(null));
        }

        // ---- EnemyDataSO --------------------------------------------------

        [Test]
        public void PopulateEnemyData_WritesTheSheet()
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                CajeroAssetBuilder.PopulateEnemyData(data);

                Assert.AreEqual("boss.cashier", data.EntityId);
                Assert.AreEqual("El Cajero", data.DisplayName);
                Assert.AreEqual(170, data.BaseHP,
                    "Piso 2: ~7 turnos con el golpe base del piso (mediana 24). Lo que se cura " +
                    "en el arqueo es presupuesto aparte.");
                Assert.AreEqual(30, data.BaseAttack);
                Assert.AreEqual(30, data.MinGoldDrop, "Drop de piso 2: 30-60.");
                Assert.AreEqual(60, data.MaxGoldDrop);
                Assert.AreEqual(ComboId.FullHouse, data.WeaknessComboId,
                    "Debilidad combo.full ⇒ el id canónico del full house.");
                Assert.AreEqual(1.5f, data.WeaknessMultiplierOverride, PercentTolerance);
                Assert.IsInstanceOf<AINode_Sequence>(data.AIRoot);
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void PopulateEnemyData_TakesTheVisualPrefabAndPortraitHandedToIt()
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.hideFlags = HideFlags.HideAndDontSave;
            var visual = new GameObject("PF_Boss_Cajero") { hideFlags = HideFlags.HideAndDontSave };
            var portrait = NewPortrait();
            try
            {
                CajeroAssetBuilder.PopulateEnemyData(data, visual, chip: null, portrait: portrait);

                Assert.AreSame(visual, data.VisualPrefab,
                    "El MenuItem construye el wrapper y lo inyecta acá.");
                Assert.AreSame(portrait, data.Portrait,
                    "Sin retrato, la cola de turnos y la barra de jefe caen a su visual default.");
            }
            finally
            {
                Object.DestroyImmediate(data);
                Object.DestroyImmediate(visual);
                DestroyPortrait(portrait);
            }
        }

        [Test]
        public void PopulateEnemyData_DoesNotClearTheVisualsWhenCalledWithoutThem()
        {
            // El builder se re-corre para refrescar números; si nulease el visual, cada rebuild dejaría
            // al jefe sin cuerpo y sin cara hasta que alguien lo notara en un playtest.
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.hideFlags = HideFlags.HideAndDontSave;
            var visual = new GameObject("PF_Boss_Cajero") { hideFlags = HideFlags.HideAndDontSave };
            var portrait = NewPortrait();
            try
            {
                data.VisualPrefab = visual;
                data.Portrait = portrait;

                CajeroAssetBuilder.PopulateEnemyData(data);

                Assert.AreSame(visual, data.VisualPrefab);
                Assert.AreSame(portrait, data.Portrait);
            }
            finally
            {
                Object.DestroyImmediate(data);
                Object.DestroyImmediate(visual);
                DestroyPortrait(portrait);
            }
        }

        [Test]
        public void PopulateEnemyData_IsIdempotent_AndBuildsAFreshTreeEachTime()
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                CajeroAssetBuilder.PopulateEnemyData(data);
                var first = data.AIRoot;
                CajeroAssetBuilder.PopulateEnemyData(data);
                var second = data.AIRoot as AINode_Sequence;

                Assert.IsNotNull(second);
                Assert.AreEqual(7, second.Children.Count, "Re-ejecutar el builder no acumula hijos.");
                Assert.AreNotSame(first, second,
                    "Cada build es un árbol nuevo: nodos compartidos arrastrarían estado runtime.");
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        // ---- Helpers ------------------------------------------------------

        /// <summary>Sprite in-memory de 4×4: alcanza para afirmar la asignación del retrato sin
        /// tocar el AssetDatabase ni reimportar la textura compartida del pack de símbolos.</summary>
        private static Sprite NewPortrait()
        {
            var texture = new Texture2D(4, 4) { hideFlags = HideFlags.HideAndDontSave };
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static void DestroyPortrait(Sprite portrait)
        {
            if (portrait == null) return;

            var texture = portrait.texture;
            Object.DestroyImmediate(portrait);
            if (texture != null) Object.DestroyImmediate(texture);
        }

        private T FindNode<T>() where T : class
        {
            var node = Descendants(_root).OfType<T>().FirstOrDefault();
            Assert.IsNotNull(node, $"No se encontró ningún {typeof(T).Name} en el árbol.");
            return node;
        }

        /// <summary>Devuelve el <see cref="AINode_If"/> de un hijo del Sequence raíz, ya venga
        /// suelto o envuelto en el <see cref="AINode_Selector"/> de aislamiento de fallos.</summary>
        private static AINode_If Unwrap(AIDecisionNode child)
        {
            if (child is AINode_If direct) return direct;
            if (child is AINode_Selector sel && sel.Children != null)
                return sel.Children.OfType<AINode_If>().FirstOrDefault();
            return null;
        }

        /// <summary>
        /// El gate de HP del hijo raíz que además contiene un <typeparamref name="T"/> en su
        /// subárbol. El tipo es lo que desambigua: el arqueo y las Comisiones comparten umbral
        /// (50%) a propósito —cruzar la mitad es UN momento de la pelea— así que buscar sólo por
        /// porcentaje devolvería el primero de los dos y los tests del otro pasarían por accidente.
        /// </summary>
        private AINode_If FindGateAtPercent<T>(float percent) where T : class
        {
            return _root.Children.Select(Unwrap).FirstOrDefault(g =>
                g != null
                && g.Conditions != null
                && g.Conditions.OfType<PcOwnerHpBelow>()
                    .Any(p => Mathf.Abs(p.Percent - percent) < PercentTolerance)
                && Descendants(g).OfType<T>().Any());
        }

        private int IndexOfGateAtPercent<T>(float percent) where T : class
        {
            var gate = FindGateAtPercent<T>(percent);
            if (gate == null) return -1;
            return _root.Children.FindIndex(c => ReferenceEquals(Unwrap(c), gate));
        }

        /// <summary>Tree-walker por reflexión: todo lo alcanzable desde <paramref name="root"/>, sin
        /// descender en <see cref="Object"/> (no arrastra assets referenciados). Copiado de
        /// <c>SunkenGrandPhaseWiringTests</c> — vive en otro assembly, no se puede compartir.</summary>
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
