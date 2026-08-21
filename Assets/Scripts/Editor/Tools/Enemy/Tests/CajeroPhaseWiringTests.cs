using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Combos;
using Rollgeon.Dungeon.Components;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.EditorTools;
using Rollgeon.Entities;
using Rollgeon.Feedback;
using Rollgeon.Grid;
using Rollgeon.PreConditions.Concretes;
using Rollgeon.Tiles;
using Rollgeon.Tiles.Authoring;
using UnityEditor;
using UnityEngine;
// Explícito porque el archivo importa UnityEditor y UnityEngine juntos, igual que
// CroupierVisualWiringTests: sin el alias, `Object` queda ambiguo.
using Object = UnityEngine.Object;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>
    /// Wiring del árbol de <b>El Cajero</b> (piso 2) <b>en memoria</b>: contra el builder y no
    /// contra el <c>.asset</c>, que falla por reimports en vez de por diseño roto.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El jefe es melee puro y persecución: se te pega, alterna mandoble y empujón, y la sala suelta
    /// monedas que él se cobra si no las levantás. Lo que se cuida acá es el <b>orden</b> del turno
    /// (de ahí cuelga la mitad del diseño), la alternancia estricta de los dos golpes, que los tres
    /// consumidores de monedas hablen de la misma definición, y el <b>terreno</b>: la mitad de la
    /// pelea es la sala, así que el plano que la construye se verifica acá y no en la ficha suelta.
    /// </para>
    /// <para>
    /// <b>Lo que ya no está.</b> El mostrador, el peaje, la columna escalada por oro, el arqueo de
    /// caja y el disparo a distancia salieron con el rediseño. Sus nodos siguen existiendo en
    /// runtime (y con tests propios en <c>Combat/AI/Tests</c>), pero el árbol del Cajero no los monta
    /// más, así que los asserts que los buscaban se fueron con ellos: un test verde sobre una
    /// mecánica que no existe es peor que no tenerlo.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class CajeroPhaseWiringTests
    {
        private const float PercentTolerance = 0.0001f;

        /// <summary>Los cinco pasos del turno, en orden. Ver <c>CajeroAssetBuilder.BuildAIRoot</c>.</summary>
        private const int RootSteps = 5;

        /// <summary>
        /// Una definición de casilla especial que <b>ya existe en disco</b>, para los tests del paso
        /// que escribe placements. No es la del Cajero a propósito: <c>Tile_Spikes_Cajero</c> la crea
        /// su propio menú, así que usarla ataría estos tests a que alguien lo haya corrido.
        /// </summary>
        private const string SharedSpikeTilePath = "Assets/Rollgeon/Tiles/Tile_Spikes.asset";

        private AINode_Sequence _root;

        [SetUp]
        public void SetUp()
        {
            _root = CajeroAssetBuilder.BuildAIRoot();
            Assert.IsNotNull(_root, "El builder tiene que devolver un Sequence raíz.");
        }

        // ---- Forma del turno ---------------------------------------------

        [Test]
        public void Root_HasTheStepsOfTheSheet()
        {
            Assert.AreEqual(RootSteps, _root.Children.Count,
                "Comisiones → ataca (mandoble o empujón) → monedas de la sala → caja → persigue.");

            Assert.IsNotNull(FindNode<AINode_SpawnReinforcements>(), "Faltan las Comisiones del 50%.");
            Assert.IsNotNull(FindNode<AINode_Alternate>(), "Falta el ciclo de los dos golpes.");
            Assert.IsNotNull(FindNode<AINode_CajeroShove>(), "Falta el empujón.");
            Assert.IsNotNull(FindNode<AINode_CajeroCoinRain>(), "Faltan las monedas de la sala.");
            Assert.IsNotNull(FindNode<AINode_CajeroCoinVault>(), "Falta la caja: nada vence las monedas.");
            Assert.IsNotNull(FindNode<AINode_Move>(), "Falta la persecución.");
        }

        /// <summary>
        /// Melee puro: el disparo a distancia y el peaje del mostrador salieron con el rediseño, y
        /// volver a montarlos le devuelve al jefe un daño que no exige acercarse — que es justo lo
        /// que la pelea pide.
        /// </summary>
        [Test]
        public void Boss_HasNoRangedAttackAndNoCounter()
        {
            Assert.IsEmpty(Descendants(_root).OfType<AINode_CashierRangedShot>().ToList(),
                "Volvió el disparo del diseño viejo: con daño a distancia el jugador ya no tiene por " +
                "qué elegir entre pegarle y juntar monedas.");
            Assert.IsEmpty(Descendants(_root).OfType<AINode_CashierCounterToll>().ToList(),
                "Volvió el peaje del mostrador, y la sala nueva no tiene mostrador que cruzar.");
            Assert.IsEmpty(Descendants(_root).OfType<AINode_TelegraphMarkGoldScaled>().ToList(),
                "Volvió la columna escalada por oro: el oro dejó de ser la unidad de daño del jefe.");
            Assert.IsEmpty(Descendants(_root).OfType<AINode_CashierAudit>().ToList(),
                "Volvió el arqueo: la curación del jefe ahora sale de las monedas vencidas, con techo.");
            Assert.IsEmpty(Descendants(_root).OfType<AINode_KeepDistance>().ToList(),
                "Volvió el repliegue. Es melee puro: alejarse le deja el turno sin ataque posible.");
            Assert.IsEmpty(Descendants(_root).OfType<AINode_Behavior>().ToList(),
                "Un AINode_Behavior traería su propio alcance y su propio número de daño, aparte de " +
                "los dos golpes del ciclo.");
        }

        [Test]
        public void PhaseGate_RunsBeforeTheAttack()
        {
            int gateIdx = IndexOfGateAtPercent<AINode_SpawnReinforcements>(CajeroAssetBuilder.CritterHpThreshold);
            int attackIdx = IndexOfStepWith<AINode_Alternate>();

            Assert.Greater(gateIdx, -1, "No hay gate de HP para las Comisiones.");
            Assert.Greater(attackIdx, gateIdx,
                "El gate de fase va antes del ataque: en el path no-coroutine un Running del ataque " +
                "aborta la secuencia y las Comisiones no saldrían nunca.");
        }

        /// <summary>
        /// La caja descubre las monedas barriendo las instancias vivas, así que tiene que correr
        /// <b>después</b> de todo lo que suelta monedas: adelante, cada moneda soltada este turno
        /// viviría una ronda de más.
        /// </summary>
        [Test]
        public void CoinVault_RunsAfterEverythingThatDropsCoins()
        {
            int vaultIdx = IndexOfStepWith<AINode_CajeroCoinVault>();
            int rainIdx = IndexOfStepWith<AINode_CajeroCoinRain>();
            int shoveIdx = IndexOfStepWith<AINode_CajeroShove>();

            Assert.Greater(vaultIdx, -1, "No hay caja en el árbol.");
            Assert.Greater(vaultIdx, rainIdx, "La caja quedó antes de la lluvia de monedas.");
            Assert.Greater(vaultIdx, shoveIdx, "La caja quedó antes del empujón, que también tira monedas.");
        }

        /// <summary>
        /// <see cref="AINode_Move"/> devuelve <c>Running</c> cuando camina, y un Running trunca el
        /// Sequence en el path no-coroutine: nada que tenga que correr todos los turnos puede quedar
        /// detrás. En particular la caja — con el movimiento en el medio, las monedas dejarían de
        /// vencerse justo en los turnos en que persigue, que son la mayoría.
        /// </summary>
        [Test]
        public void Chase_IsTheLastStep_BecauseARunningTruncatesTheSequence()
        {
            Assert.AreEqual(_root.Children.Count - 1, IndexOfStepWith<AINode_Move>(),
                "La persecución dejó de ser el último paso del turno.");
        }

        [Test]
        public void EveryChild_IsIsolatedInSelectorWithWaitFallback()
        {
            // Los cinco tienen un Failed benigno: no cruzó el umbral, el jugador está lejos, no toca
            // ronda de monedas, ninguna venció, ya está pegado. Suelto en el Sequence, cualquiera de
            // esos le aborta el turno entero.
            for (int i = 0; i < _root.Children.Count; i++)
            {
                var selector = _root.Children[i] as AINode_Selector;
                Assert.IsNotNull(selector,
                    $"El hijo {i} del Sequence raíz no está envuelto en Selector: su Failed abortaría el turno.");
                Assert.IsTrue(selector.Children.Any(c => c is AINode_Wait),
                    $"El Selector del hijo {i} no tiene Wait de fallback — devolvería Failed igual.");
            }
        }

        // ---- El ciclo de los dos golpes -----------------------------------

        /// <summary>
        /// Mandoble, empujón, mandoble, empujón. El orden importa: el índice del
        /// <see cref="AINode_Alternate"/> arranca en 0, así que la pelea abre con el golpe que no se
        /// puede evitar de ninguna manera estando a su alcance.
        /// </summary>
        [Test]
        public void AttackCycle_AlternatesTheHeavyBlowAndTheShove_InThatOrder()
        {
            var cycle = Alternate();

            Assert.AreEqual(2, cycle.Children.Count, "Son dos golpes y sólo dos.");

            var first = Unwrap<AINode_RangedShot>(cycle.Children[0]);
            var second = Unwrap<AINode_RangedShot>(cycle.Children[1]);

            Assert.IsNotNull(first, "El primer tiempo del ciclo no es un golpe.");
            Assert.IsNotNull(second, "El segundo tiempo del ciclo no es un golpe.");
            Assert.IsNotInstanceOf<AINode_CajeroShove>(first,
                "La pelea abre con el empujón. El mandoble va primero porque es el golpe que no se " +
                "puede prevenir: el empujón es el que el jugador puede preparar eligiendo desde qué " +
                "casilla atacarlo, y para eso tiene que haber visto uno antes.");
            Assert.IsInstanceOf<AINode_CajeroShove>(second, "El segundo tiempo tiene que ser el empujón.");
        }

        /// <summary>
        /// El gate de rango va <b>por fuera</b> del <c>Alternate</c>, y eso es lo único que hace que
        /// la alternancia que el jugador ve sea estricta.
        /// </summary>
        /// <remarks>
        /// <see cref="AINode_Alternate"/> avanza el índice ANTES de tickear al hijo y no lo devuelve
        /// si el hijo falla. Con los dos golpes auto-gateados por su propio <c>Range</c> y nada
        /// afuera, cada turno que el jefe pasa caminando quemaría un slot del ciclo: el jugador
        /// contaría mandoble-empujón y le llegaría mandoble-mandoble. Con el <c>If</c> afuera, el
        /// índice sólo se mueve en los turnos en que de verdad pega.
        /// </remarks>
        [Test]
        public void AttackCycle_IsGatedByRangeFromOutsideTheAlternate()
        {
            var gate = _root.Children.Select(Unwrap<AINode_If>)
                .FirstOrDefault(g => g != null && g.Then is AINode_Alternate);

            Assert.IsNotNull(gate,
                "El Alternate dejó de colgar de un If: sin gate afuera, los turnos de caminata le " +
                "queman slots al ciclo y la alternancia deja de ser estricta.");
            Assert.IsInstanceOf<AINode_Wait>(gate.Else,
                "El gate necesita Else: un If sin rama devuelve Failed y aborta el turno.");

            var range = gate.Conditions.OfType<PcTargetInRange>().SingleOrDefault();
            Assert.IsNotNull(range, "El gate del ataque no condiciona por rango.");
            Assert.AreEqual(CajeroAssetBuilder.MeleeRange, range.Range,
                "El gate y los golpes tienen que pedir el MISMO alcance: con el gate más ancho, el " +
                "ciclo avanza en turnos en que el golpe de adentro falla por rango.");
            Assert.AreEqual(DistanceMetric.Manhattan, range.Metric,
                "Misma métrica que los golpes, o la casilla que abre el gate no es la casilla desde " +
                "la que pega.");
        }

        [Test]
        public void HeavyBlow_IsHisFloorDamage_AtContactRange()
        {
            var heavy = Descendants(Alternate()).OfType<AINode_RangedShot>()
                .Single(n => !(n is AINode_CajeroShove));

            Assert.AreEqual(CajeroAssetBuilder.HeavyDamage, heavy.Damage,
                "Cableado desde la constante de la ficha, no del default del nodo.");
            Assert.AreEqual(CajeroAssetBuilder.BaseAttack, heavy.Damage,
                "El mandoble ES su ataque base: dos números distintos para el mismo golpe se " +
                "desincronizan, y el que ve el tooltip es el stat.");
            Assert.AreEqual(CajeroAssetBuilder.MeleeRange, heavy.Range,
                "Range 1 es lo que lo hace melee. Con más, pega sin acercarse y la persecución " +
                "deja de ser la pelea.");
            Assert.AreEqual(DistanceMetric.Manhattan, heavy.Metric);
            Assert.AreEqual(AttackKind.BasicAttack, heavy.Kind);

            // Sin ids el golpe cobra y no se ve: PlayShot sale por `steps.Count == 0` y lo único en
            // pantalla es el jugador perdiendo vida.
            Assert.AreEqual(BossFeedbackIds.CajeroMeleeAnim, heavy.AnimFeedbackId);
            Assert.AreEqual(BossFeedbackIds.CajeroImpactVfx, heavy.ImpactVfxFeedbackId);
            Assert.AreEqual(BossFeedbackIds.CajeroImpactFeel, heavy.ImpactFeelFeedbackId);
        }

        /// <summary>
        /// El empujón pega <b>menos</b> que el mandoble a propósito: lo que cobra de verdad es el
        /// tumbo contra los pinchos. Si pegara más, elegir la casilla desde la que atacarlo dejaría
        /// de ser una decisión y sería sólo el turno malo.
        /// </summary>
        [Test]
        public void Shove_HitsForLessThanTheHeavyBlow_AndThrowsHimAcrossTheSheetsTiles()
        {
            var shove = FindNode<AINode_CajeroShove>();

            Assert.AreEqual(CajeroAssetBuilder.ShoveDamage, shove.Damage,
                "Cableado desde la constante de la ficha, no del default del nodo.");
            Assert.Less(shove.Damage, CajeroAssetBuilder.HeavyDamage,
                "El empujón pasó a pegar igual o más que el mandoble: el precio del empujón son los " +
                "pinchos que cruzás, y con el golpe directo más alto el tumbo deja de ser el punto.");
            Assert.AreEqual(CajeroAssetBuilder.ShovePushTiles, shove.PushTiles);
            Assert.AreEqual(CajeroAssetBuilder.MeleeRange, shove.Range);
            Assert.AreEqual(DistanceMetric.Manhattan, shove.Metric,
                "Manhattan y Range 1 son lo que garantiza que el jugador esté ortogonalmente pegado: " +
                "con Chebyshev la diagonal entra en rango y 'el lado opuesto al suyo' deja de ser un " +
                "cardinal exacto.");

            Assert.AreEqual(CajeroAssetBuilder.ChipCount, shove.CoinCount,
                "Las monedas del tumbo salen de la ficha, no del default del nodo.");
            Assert.AreEqual(CajeroAssetBuilder.ChipMinValue, shove.CoinMinValue);
            Assert.AreEqual(CajeroAssetBuilder.ChipMaxValue, shove.CoinMaxValue);

            Assert.AreEqual(BossFeedbackIds.CajeroMeleeAnim, shove.AnimFeedbackId);
            Assert.AreEqual(BossFeedbackIds.CajeroImpactVfx, shove.ImpactVfxFeedbackId);
            Assert.AreEqual(BossFeedbackIds.CajeroImpactFeel, shove.ImpactFeelFeedbackId);
        }

        // ---- Las monedas --------------------------------------------------

        [Test]
        public void CoinRain_UsesTheSheetNumbers()
        {
            var rain = FindNode<AINode_CajeroCoinRain>();

            Assert.AreEqual(CajeroAssetBuilder.CoinsPerRain, rain.Count);
            Assert.AreEqual(CajeroAssetBuilder.CoinRainEveryNRounds, rain.EveryNRounds);
            Assert.AreEqual(CajeroAssetBuilder.ChipMinValue, rain.MinValue);
            Assert.AreEqual(CajeroAssetBuilder.ChipMaxValue, rain.MaxValue);
            Assert.AreEqual(CajeroAssetBuilder.CoinRainMinSeparation, rain.MinSeparation);
            Assert.Greater(rain.MinSeparation, 1,
                "\"Repartidas por la sala\" es media mecánica: cada moneda tiene que ser un punto al " +
                "que ir, y cuatro pegadas son un solo viaje.");
        }

        [Test]
        public void CoinVault_CarriesTheClockAndTheHealCeilingFromTheSheet()
        {
            var vault = FindNode<AINode_CajeroCoinVault>();

            Assert.AreEqual(CajeroAssetBuilder.ChipDurationRounds, vault.LifetimeRounds,
                "El reloj de la moneda vive en este nodo, no en el DurationRounds del hazard: el " +
                "servicio expira igual una moneda levantada y una vencida, y sólo la segunda cura.");
            Assert.AreEqual(CajeroAssetBuilder.HealPerExpiredCoin, vault.HealPerCoin);
            Assert.AreEqual(CajeroAssetBuilder.MaxHealPerFight, vault.MaxHealPerFight);

            // El techo es lo que hace que juntar monedas sea la jugada ganadora en vez de una carrera
            // imposible. Sin techo, cada tanda que se le escapa le devuelve HealPerCoin × CoinsPerRain
            // (48) cada CoinRainEveryNRounds rondas y la pelea no cierra.
            Assert.Greater(vault.MaxHealPerFight, 0,
                "Techo 0 apaga la curación entera: las monedas dejan de tener consecuencia y la " +
                "mecánica central de la sala se queda sin apuesta.");
            Assert.Less(vault.MaxHealPerFight, CajeroAssetBuilder.BaseHP,
                "El techo de curación llegó a valer una vida entera: la pelea puede no terminar.");
        }

        /// <summary>
        /// Los tres consumidores de monedas tienen que hablar de la <b>misma</b> definición. La caja
        /// reconoce lo que hay en el piso comparando <c>info.Definition == Coin</c>: con una
        /// definición distinta no falla nada, simplemente ninguna moneda vence nunca y el jefe no se
        /// cura jamás.
        /// </summary>
        [Test]
        public void EveryCoinNode_TakesTheSameHazardDefinitionHandedToTheBuilder()
        {
            var definition = ScriptableObject.CreateInstance<HazardDefinitionSO>();
            definition.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var root = CajeroAssetBuilder.BuildAIRoot(definition);

                Assert.AreSame(definition, Descendants(root).OfType<AINode_CajeroShove>().Single().Coin,
                    "El empujón tira monedas de otra definición.");
                Assert.AreSame(definition, Descendants(root).OfType<AINode_CajeroCoinRain>().Single().Coin,
                    "La lluvia suelta monedas de otra definición.");
                Assert.AreSame(definition, Descendants(root).OfType<AINode_CajeroCoinVault>().Single().Coin,
                    "La caja vigila otra definición: nada vence y el jefe no se cura nunca.");
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        // ---- El terreno de la sala ----------------------------------------

        /// <summary>
        /// <b>El plano del Cajero en <c>BossRoomBuilder</c>, no el array de esta ficha.</b> Los diez
        /// pinchos y las seis cajas fuertes viven acá porque la regla que los gobierna es del jefe,
        /// pero lo que se construye es el plano: si el plano no los lee, el array es un dibujo que
        /// nadie mira y la sala sale pelada.
        /// </summary>
        /// <remarks>
        /// <para>
        /// La versión anterior de estos tests cruzaba <c>SpikePlanCells</c> contra sí mismo y contra
        /// los bordes del plano, así que pasaba en verde con la sala shippeada sin un solo pincho.
        /// Todo lo que se compara acá cruza <b>dos archivos</b>: la ficha del jefe y el plano de la
        /// sala.
        /// </para>
        /// <para>
        /// Lo que estos tests <b>no</b> pueden ver es el prefab ya escrito: <c>Boss_Room_Cajero</c> lo
        /// reescribe el menú <c>Rollgeon/Bosses/Build Boss Room/Cajero</c>, y hasta que alguien lo
        /// corra el plano y el prefab dicen cosas distintas. Un assert sobre el prefab quedaría rojo
        /// por trabajo pendiente y no por diseño roto, que es justo lo que este archivo evita.
        /// </para>
        /// </remarks>
        [Test]
        public void CajeroRoomPlan_CarriesTheTenSpikesAndTheSixSafeBoxes()
        {
            var plan = CajeroRoomPlan();

            Assert.AreSame(CajeroAssetBuilder.SafeBoxPlanCells, plan.BlockerPlanCells,
                "Los blockers del plano tienen que ser EL MISMO array de la ficha, no otro con las " +
                "mismas celdas: dos copias se desincronizan en el primer ajuste y la que se " +
                "construye es la del plano. Las cajas fuertes son lo único que bloquea y lo único " +
                "que frena un empujón en seco — con otro dibujo, el tumbo de 3 casillas no tiene " +
                "contra qué parar.");
            Assert.AreEqual(6, plan.BlockerPlanCells.Length, "Son seis cajas fuertes.");

            Assert.IsNotNull(plan.SpecialTiles, "El plano no autora ninguna casilla especial.");
            Assert.AreEqual(1, plan.SpecialTiles.Length,
                "Los pinchos van agrupados en UNA entrada: el layout de un tipo de casilla es una " +
                "sola decisión de diseño (los diez se leen juntos: ninguno toca a otro).");

            var group = plan.SpecialTiles[0];
            Assert.AreEqual(CajeroAssetBuilder.SpikeTilePath, group.DefinitionAssetPath,
                "El plano coloca otra definición de casilla. La de la ficha pega SpikeDamage y trae " +
                "el costo virtual de pathing; Tile_Spikes, la genérica, no hace ni una ni la otra.");
            Assert.AreSame(CajeroAssetBuilder.SpikePlanCells, group.PlanCells,
                "Los pinchos del plano tienen que ser EL MISMO array de la ficha. Con una copia, la " +
                "regla que estos tests verifican —ninguno toca a otro— vale sobre el array de la " +
                "ficha mientras la sala se construye con el otro dibujo: es exactamente así como se " +
                "shippeó una sala sin un solo pincho y con los tests en verde.");
            Assert.AreEqual(CajeroAssetBuilder.SpikeCount, group.PlanCells.Length,
                "La cuenta de la ficha y lo que el plano coloca dejaron de coincidir.");
            Assert.AreEqual(10, group.PlanCells.Length,
                "Son diez. El número se ancla acá aparte de SpikeCount para que moverlo sea una " +
                "decisión y no el efecto colateral de editar el array.");
        }

        /// <summary>
        /// El jefe arranca en el <b>centro exacto</b>. Nada parte la sala al medio, así que él no
        /// tiene lado: la primera decisión de la pelea es por qué esquina entra el jugador, y para
        /// que las cuatro sean equivalentes él tiene que estar equidistante de todas.
        /// </summary>
        [Test]
        public void CajeroRoomPlan_StartsHimOnTheExactCentreOfTheRoom()
        {
            var plan = CajeroRoomPlan();

            Assert.AreEqual(new Vector2Int(BossRoomBuilder.PlanWidth / 2, BossRoomBuilder.PlanHeight / 2),
                plan.BossPlanCell,
                "La casilla del jefe dejó de ser el centro del plano.");
            Assert.AreEqual(new GridCoord(0, 0), BossRoomBuilder.PlanToRoom(plan.BossPlanCell),
                "El centro del plano dejó de caer en el (0,0) de la sala. Es la cuenta que hace que " +
                "'centro' quiera decir lo mismo en el dibujo y en la grilla.");
        }

        /// <summary>
        /// La regla de los pinchos: <b>ninguno toca a otro, ni en diagonal</b>. Dos pegados forman
        /// una pared que el tumbo no puede cruzar sin cobrar doble, y la sala pasa de tener trampas
        /// a tener zonas prohibidas.
        /// </summary>
        /// <remarks>
        /// Se verifica sobre las celdas que el <b>plano</b> coloca, no sobre el array de la ficha:
        /// la regla vale sobre lo que se construye.
        /// </remarks>
        [Test]
        public void PlannedSpikes_NeverTouchEachOther_NotEvenDiagonally()
        {
            var cells = PlannedSpikeCells();

            for (int i = 0; i < cells.Length; i++)
            {
                for (int j = i + 1; j < cells.Length; j++)
                {
                    int dx = Mathf.Abs(cells[i].x - cells[j].x);
                    int dy = Mathf.Abs(cells[i].y - cells[j].y);
                    Assert.Greater(Mathf.Max(dx, dy), 1,
                        $"Los pinchos de {cells[i]} y {cells[j]} se tocan (Chebyshev " +
                        $"{Mathf.Max(dx, dy)}). Van sueltos: pegados dejan de ser casillas que " +
                        "esquivás y pasan a ser una pared de daño.");
                }
            }
        }

        /// <summary>
        /// Pinchos, cajas fuertes y la casilla del jefe: tres cosas que no pueden compartir celda, y
        /// las tres tienen que caer dentro de la sala.
        /// </summary>
        /// <remarks>
        /// Un pincho debajo de una caja fuerte no existe para el jugador (la caja bloquea, así que
        /// nadie lo pisa) y uno debajo del jefe le cobra a él en su propio spawn. Fuera del plano,
        /// <c>PlanToRoom</c> los manda a una casilla que la sala no tiene: el placement queda escrito
        /// y el layout se ve completo.
        /// </remarks>
        [Test]
        public void PlannedTerrain_NeverStacksTwoThingsOnOneCell_AndFitsInsideTheRoom()
        {
            var plan = CajeroRoomPlan();
            var spikes = PlannedSpikeCells();
            var boxes = plan.BlockerPlanCells;

            foreach (var box in boxes)
            {
                Assert.IsFalse(spikes.Contains(box),
                    $"{box} es caja fuerte y pincho a la vez: la caja bloquea, así que ese pincho " +
                    "no se puede pisar y no existe para el jugador.");
            }

            CollectionAssert.DoesNotContain(spikes, plan.BossPlanCell,
                "Un pincho quedó debajo del spawn del jefe: le cobra a él antes de que la pelea " +
                "empiece y la casilla desde la que pelea deja de ser piso limpio.");
            CollectionAssert.DoesNotContain(boxes, plan.BossPlanCell,
                "Una caja fuerte quedó sobre la casilla del jefe: el bake la deja no caminable y el " +
                "jefe aparece dentro de un mueble.");

            Assert.AreEqual(spikes.Length, spikes.Distinct().Count(),
                "Hay pinchos repetidos. Dos placements en la misma coord cobran los DOS: los " +
                "triggers disparan una vez por instancia y Place no valida el solape.");
            Assert.AreEqual(boxes.Length, boxes.Distinct().Count(), "Hay cajas fuertes repetidas.");

            // Más ajustado que los bordes del plano, y a propósito: la ficha pide que el borde y las
            // cuatro esquinas queden limpios. El jugador entra por una esquina al azar, así que algo
            // ahí lo recibe pisando un pincho o lo mete dentro de un mueble; y contra la pared un
            // blocker no encarece ningún camino, sólo se come piso.
            foreach (var cell in spikes.Concat(boxes))
            {
                Assert.IsTrue(
                    cell.x >= 1 && cell.x <= BossRoomBuilder.PlanWidth - 2
                    && cell.y >= 1 && cell.y <= BossRoomBuilder.PlanHeight - 2,
                    $"{cell} está en el borde del plano {BossRoomBuilder.PlanWidth}×" +
                    $"{BossRoomBuilder.PlanHeight}. El borde y las cuatro esquinas van limpios: el " +
                    "jugador entra por una esquina al azar. Y fuera del plano es peor — PlanToRoom la " +
                    "manda a una casilla que la sala no tiene y el placement falla en silencio.");
            }
        }

        /// <summary>
        /// El paso que escribe de verdad: las celdas del plano terminan en
        /// <c>RoomLayout.SpecialTilePlacements</c> —la lista <b>permanente</b>— traducidas con
        /// <c>PlanToRoom</c>, y sin borrar lo que la sala base ya traía.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Corre con un plano sintético y una definición que ya existe en el proyecto, no con el del
        /// Cajero: <c>Tile_Spikes_Cajero</c> lo crea el menú del jefe y todavía no está en disco, así
        /// que un test contra el plano real fallaría por trabajo pendiente en vez de por el
        /// mecanismo. Lo que se prueba es el mecanismo.
        /// </para>
        /// <para>
        /// La lista importa: <c>SpecialTilePlacements</c> es la permanente y <c>SpecialTileSlots</c>
        /// rolea el tipo. En estos planos la posición y el tipo son <b>los dos</b> autoría — un pincho
        /// dibujado en una casilla exacta no puede salir "fuego" en la mitad de las runs.
        /// </para>
        /// <para>
        /// Y agrega en vez de reemplazar: cada corrida parte de la sala base, así que lo que ya está
        /// en la lista es autoría de la sala compartida del piso y vaciarla la borraría en la derivada.
        /// </para>
        /// </remarks>
        [Test]
        public void ApplySpecialTiles_WritesThePlanIntoTheLayoutsPermanentList()
        {
            var definition = LoadSharedSpikeTile();

            var host = new GameObject("Room_Test") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var layout = host.AddComponent<RoomLayout>();
                var fromTheBaseRoom = new SpecialTilePlacement
                {
                    Definition = definition,
                    Coord = new GridCoord(4, 4),
                };
                layout.SpecialTilePlacements = new List<SpecialTilePlacement> { fromTheBaseRoom };

                var failures = ApplySpecialTiles(
                    layout, new Vector2Int(2, 1), new Vector2Int(6, 2));

                Assert.IsEmpty(failures, "El paso reportó problemas con un plano válido.");
                Assert.AreEqual(3, layout.SpecialTilePlacements.Count,
                    "Los placements no se agregaron a la lista permanente, o se reemplazó lo que la " +
                    "sala base ya traía.");
                Assert.AreSame(fromTheBaseRoom, layout.SpecialTilePlacements[0],
                    "Se borró el placement que venía de la sala base: en la sala derivada eso " +
                    "desaparece contenido que autoró el piso entero.");

                var written = layout.SpecialTilePlacements.Skip(1).ToList();
                CollectionAssert.AreEqual(
                    new[] { BossRoomBuilder.PlanToRoom(new Vector2Int(2, 1)),
                            BossRoomBuilder.PlanToRoom(new Vector2Int(6, 2)) },
                    written.Select(p => p.Coord).ToList(),
                    "Las celdas del plano no se tradujeron con PlanToRoom: el dibujo tiene el origen " +
                    "arriba-izquierda y la sala el centro en (0,0), así que sin traducir caen " +
                    "corridas media sala.");
                foreach (var placement in written)
                {
                    Assert.AreSame(definition, placement.Definition,
                        "El placement quedó sin la definición cargada del path: una casilla especial " +
                        "en null es piso pelado con un placement escrito al lado.");
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// Dos placements en la misma coord <b>cobran los dos</b>: los triggers disparan una vez por
        /// instancia y <c>Place</c> no valida el solape. Una celda ya ocupada se saltea con finding.
        /// </summary>
        [Test]
        public void ApplySpecialTiles_RefusesToStackTwoTilesOnOneCell()
        {
            LoadSharedSpikeTile();

            var host = new GameObject("Room_Test") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var layout = host.AddComponent<RoomLayout>();
                layout.SpecialTilePlacements = new List<SpecialTilePlacement>();

                var repeated = new Vector2Int(2, 1);
                var failures = ApplySpecialTiles(layout, repeated, repeated);

                Assert.AreEqual(1, layout.SpecialTilePlacements.Count,
                    "Se apilaron dos casillas especiales en una coord: esa celda cobra el doble y " +
                    "dibuja dos visuales encimados, sin que nada lo diga.");
                Assert.IsNotEmpty(failures,
                    "La celda repetida se salteó en silencio. El plano pidió diez pinchos y la sala " +
                    "salió con nueve: eso tiene que llegar al log del build.");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// El daño virtual es lo que convierte un pincho armado en <b>intransitable</b> para el
        /// pathing en vez de en caro. La cuenta es la que documenta la ficha, y no es libre: apunta a
        /// que <c>daño / HP</c> dé exactamente 1.
        /// </summary>
        /// <remarks>
        /// <c>AIPathPlanner.ComputeHazardPenalty</c> es <c>ceil(daño / HP × 10 × Caution)</c> y el
        /// costo de casilla es <c>1 + penalty</c>. Con los 14 reales sobre 350 el penalty da 1 y la
        /// casilla cuesta 2: rodea un desvío de un paso y se come el pincho si el desvío es de dos.
        /// Con el virtual sumado el penalty llega a 10 y la casilla cuesta 11 — más que cualquier
        /// desvío alcanzable dentro de un movimiento de <c>ChaseSteps</c>. <b>No es daño</b>: el
        /// filtro de supervivencia sólo mira los 14 reales, así que "empujado se los come igual"
        /// sigue en pie.
        /// </remarks>
        [Test]
        public void SpikeVirtualDamage_SaturatesThePathingPenalty_AtExactlyHisOwnHp()
        {
            Assert.AreEqual(CajeroAssetBuilder.BaseHP,
                CajeroAssetBuilder.SpikeDamage + CajeroAssetBuilder.SpikeAIVirtualDamage,
                "El daño real más el virtual dejó de dar la vida entera del jefe, así que el penalty " +
                "del planner ya no satura en 10 y el pincho armado vuelve a ser sólo caro. Si se " +
                "movió BaseHP, este número se mueve con él.");
            Assert.Greater(CajeroAssetBuilder.SpikeDamage, 0,
                "Sin daño real el pincho es un cartel: el planner lo rodea y al jugador no le cuesta nada.");
        }

        // ---- Las Comisiones -----------------------------------------------

        [Test]
        public void CritterGate_SpawnsTwoOfThemAtFiftyPercentHp()
        {
            var gate = FindGateAtPercent<AINode_SpawnReinforcements>(CajeroAssetBuilder.CritterHpThreshold);

            Assert.IsNotNull(gate, "No hay gate de HP al 50% para las Comisiones.");
            Assert.IsInstanceOf<AINode_Wait>(gate.Else,
                "El gate necesita Else: un If sin rama devuelve Failed y aborta el turno.");

            var spawn = FindNode<AINode_SpawnReinforcements>();
            Assert.AreEqual(2, spawn.Count, "Dos bichos, los que pidió el diseño.");
            Assert.AreEqual(CajeroAssetBuilder.CritterCount, spawn.Count,
                "El nodo tiene que salir cableado desde la constante de la ficha, no de su default.");
        }

        /// <summary>Sin <see cref="AINode_Once"/> el nodo se auto-gatea y repone la oleada cada vez
        /// que la matan, justo cuando el jefe ya se está curando con las monedas que se vencen: la
        /// pelea no termina.</summary>
        [Test]
        public void CritterGate_IsLatchedOnce_SoTheWaveNeverRespawns()
        {
            var gate = FindGateAtPercent<AINode_SpawnReinforcements>(CajeroAssetBuilder.CritterHpThreshold);
            var once = gate.Then as AINode_Once;

            Assert.IsNotNull(once, "Sin Once las Comisiones se repondrían para siempre.");
            Assert.IsInstanceOf<AINode_SpawnReinforcements>(once.Child,
                "El Once envuelve el spawn y nada más.");
        }

        /// <summary>
        /// Es el único umbral y el único evento de la pelea, así que también es el único
        /// <see cref="AINode_Once"/>: cualquier otro latch es una mecánica que se colgó de un umbral
        /// sin que la ficha lo diga.
        /// </summary>
        [Test]
        public void Once_WrapsOnlyThePhaseGate_SoTheCoinsKeepRunning()
        {
            var latches = Descendants(_root).OfType<AINode_Once>().ToList();

            Assert.AreEqual(1, latches.Count, "El one-shot del jefe es uno y sólo uno: las Comisiones.");

            foreach (var latch in latches)
            {
                Assert.IsEmpty(Descendants(latch).OfType<AINode_CajeroCoinRain>().ToList(),
                    "La lluvia de monedas es el reloj de la pelea: un Once la apagaría tras la primera tanda.");
                Assert.IsEmpty(Descendants(latch).OfType<AINode_CajeroCoinVault>().ToList(),
                    "Un Once sobre la caja deja de vencer monedas después de la primera y el techo de " +
                    "curación nunca se alcanza.");
            }
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

        /// <summary>
        /// El refuerzo es <b>su</b> Comisión, no el ranged compartido del juego. Ese asset trae 50 de
        /// vida y 10 de daño, y a la altura del 50% dos de ésos son un segundo jefe; además es el
        /// asset de todos los encuentros normales, así que autorarle 18/6 se lo cambia a media run.
        /// </summary>
        [Test]
        public void Reinforcements_AreHisOwnComision_NotTheSharedRangedEnemy()
        {
            Assert.AreEqual(CajeroAssetBuilder.CritterAssetPath,
                CajeroAssetBuilder.ReinforcementAssetPath,
                "El refuerzo dejó de ser la Comisión. Si vuelve a apuntar al ranged común, los dos " +
                "bichos del 50% pegan 20 por turno y aguantan 50 cada uno.");
            Assert.AreNotEqual("Assets/Rollgeon/Enemies/ED_RangedEnemy.asset",
                CajeroAssetBuilder.ReinforcementAssetPath,
                "ED_RangedEnemy es el asset compartido de todos los encuentros del juego: el kit de " +
                "la Comisión no se puede autorar ahí sin tocarle el enemigo a todo el resto.");
        }

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
                Assert.AreEqual(CajeroAssetBuilder.CritterHp, critter.BaseHP);
                Assert.AreEqual(18, critter.BaseHP,
                    "Muere de un golpe de la mediana del piso 2 (24): sacárselos de encima cuesta " +
                    "un golpe cada uno, y ese es todo el precio.");
                Assert.Less(critter.BaseHP, CajeroAssetBuilder.BaseHP / 4,
                    "Es un bicho, no un segundo jefe.");
                Assert.AreEqual(CajeroAssetBuilder.CritterDamage, critter.BaseAttack,
                    "El stat se escribe con el mismo número que el nodo: es el que leen el tooltip y " +
                    "los TargetSelector_ByAttribute, y en 0 la marcarían como support.");
                Assert.Less(2 * critter.BaseAttack, CajeroAssetBuilder.HeavyDamage,
                    "Los dos juntos tienen que pegar ESTRICTAMENTE menos que el mandoble del jefe: " +
                    "son el precio de huir, no una segunda amenaza principal. Empatarlo las pone a " +
                    "competir con el golpe que define la pelea.");
                Assert.AreEqual(CajeroAssetBuilder.CritterRange, critter.BaseAttackRange,
                    "Tiran de lejos: es lo que hace que huir a juntar monedas tenga precio.");
            }
            finally
            {
                Object.DestroyImmediate(critter);
            }
        }

        /// <summary>Las monedas del piso son un reloj, no un botín: un refuerzo que paga al morir le
        /// daría al jugador una fuente de oro que la sala no controla.</summary>
        [Test]
        public void CritterData_DropsNoGold()
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

        [Test]
        public void CritterAI_ShootsFirstAndFliesAfter_SoArrivingDoesNotEatItsShot()
        {
            var root = CajeroAssetBuilder.BuildCritterAIRoot();

            Assert.IsNotNull(root, "La Comisión necesita árbol propio: sin él cae al BasicEnemyAI.");
            Assert.AreEqual(2, root.Children.Count, "Dispara y vuela, nada más.");

            int shotIdx = root.Children.FindIndex(c =>
                Descendants(c).Any(n => n is AINode_CashierRangedShot));
            int moveIdx = root.Children.FindIndex(c =>
                Descendants(c).Any(n => n is AINode_Move));

            Assert.Greater(shotIdx, -1, "Falta el disparo.");
            Assert.Greater(moveIdx, shotIdx,
                "AINode_Move devuelve Running cuando se mueve, y un Running corta el Sequence: con " +
                "el orden invertido, el turno en que entra en rango se le comería el disparo.");
        }

        /// <summary>
        /// Dispara desde <b>su</b> alcance y camina hasta ahí, no hasta el contacto: pegada al
        /// jugador muere de un golpe cualquiera para pegar exactamente lo mismo que pega de lejos.
        /// </summary>
        [Test]
        public void CritterAI_ShootsFromItsOwnRange_NotFromContact()
        {
            var root = CajeroAssetBuilder.BuildCritterAIRoot();
            var shot = Descendants(root).OfType<AINode_CashierRangedShot>().First();
            var move = Descendants(root).OfType<AINode_Move>().First();

            Assert.AreEqual(CajeroAssetBuilder.CritterRange, shot.Range,
                "El alcance sale de la ficha (es el mismo del ranged común del juego), no del " +
                "default del nodo.");
            Assert.AreEqual(CajeroAssetBuilder.CritterDamage, shot.Damage,
                "Cableado desde la constante de la ficha, no del default de 12 del nodo.");
            Assert.AreEqual(CajeroAssetBuilder.CritterRange, ReadInt(move.DesiredRange),
                "Camina hasta el contacto en vez de hasta su alcance: se pone al lado del jugador, " +
                "donde muere de un golpe, para pegar lo mismo que pegaba de lejos.");
            Assert.AreEqual(CajeroAssetBuilder.CritterMoveSteps, ReadInt(move.MaxSteps));
        }

        [Test]
        public void CritterAI_EveryStepIsIsolated_SoABenignFailedDoesNotEatItsTurn()
        {
            var root = CajeroAssetBuilder.BuildCritterAIRoot();

            // El disparo falla con el jugador lejos y el vuelo cuando ya está a tiro: los dos Failed
            // son normales.
            foreach (var child in root.Children)
            {
                var selector = child as AINode_Selector;
                Assert.IsNotNull(selector, "Cada paso de la Comisión va en Selector[acción, Wait].");
                Assert.IsTrue(selector.Children.Any(c => c is AINode_Wait),
                    "El Selector sin Wait de fallback devuelve Failed igual.");
            }
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
                Assert.AreEqual(350, data.BaseHP,
                    "Piso 2, y la pelea es larga a propósito: el jugador tiene que poder elegir " +
                    "varias veces entre pegarle y juntar monedas, y con 170 la elección no llegaba " +
                    "a aparecer. Lo que se cura con monedas vencidas es presupuesto aparte " +
                    "(MaxHealPerFight) y no figura acá.");
                Assert.AreEqual(14, data.BaseAttack,
                    "Baja de 30: el techo de daño por turno ahora lo pone el tumbo contra los " +
                    "pinchos, no el golpe directo.");
                Assert.AreEqual(CajeroAssetBuilder.MeleeRange, data.BaseAttackRange,
                    "Melee puro: no tiene nada a distancia.");
                Assert.AreEqual(30, data.MinGoldDrop, "Drop de piso 2: 30-60.");
                Assert.AreEqual(60, data.MaxGoldDrop);
                Assert.AreEqual(ComboId.FullHouse, data.WeaknessComboId,
                    "\"La mano que paga fijo, la de la casa\": combo.full_house.");
                Assert.AreEqual(1.5f, data.WeaknessMultiplierOverride, PercentTolerance);
                Assert.IsInstanceOf<AINode_Sequence>(data.AIRoot);
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        /// <summary>
        /// El arte tiene alas y <c>IsFlying</c> se escribe explícito por eso: los pinchos son
        /// <c>GroundOnly</c>, así que un tick en el Inspector le sacaría el único costo que la sala
        /// le cobra a él — y "los esquiva caminando pero los come empujado" es la única herramienta
        /// defensiva real que el jugador tiene acá.
        /// </summary>
        [Test]
        public void PopulateEnemyData_KeepsHimGrounded_SoTheSpikesStillBillHim()
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                data.IsFlying = true;

                CajeroAssetBuilder.PopulateEnemyData(data);

                Assert.IsFalse(data.IsFlying,
                    "PopulateEnemyData dejó de escribir IsFlying: un asset con el tick puesto se " +
                    "queda volando y los pinchos GroundOnly no le cobran nada.");
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
            // El builder se re-corre para refrescar números: nulear el visual dejaría al jefe sin
            // cuerpo en cada rebuild.
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
                Assert.AreEqual(RootSteps, second.Children.Count, "Re-ejecutar el builder no acumula hijos.");
                Assert.AreNotSame(first, second,
                    "Cada build es un árbol nuevo: nodos compartidos arrastrarían estado runtime — el " +
                    "índice del Alternate y el techo de curación de la caja viven en la instancia.");
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        // ---- Helpers ------------------------------------------------------

        /// <summary>El plano de la sala del Cajero, tal como lo va a construir el menú de salas.</summary>
        private static BossRoomPlan CajeroRoomPlan()
        {
            var plan = BossRoomBuilder.Plans.FirstOrDefault(p => p.BossName == "Cajero");
            Assert.IsNotNull(plan,
                "No hay plano de sala para el Cajero en BossRoomBuilder.Plans. Sin plano, la pelea " +
                "pasa en la sala base del piso: sin pinchos, sin cajas fuertes y con el jefe donde " +
                "haya quedado el spawn.");
            return plan;
        }

        /// <summary>Las celdas de pinchos que el plano coloca de verdad.</summary>
        private static Vector2Int[] PlannedSpikeCells()
        {
            var plan = CajeroRoomPlan();
            Assert.IsNotNull(plan.SpecialTiles, "El plano no autora ninguna casilla especial.");

            var cells = plan.SpecialTiles
                .Where(g => g?.PlanCells != null)
                .SelectMany(g => g.PlanCells)
                .ToArray();

            Assert.IsNotEmpty(cells, "El plano quedó sin celdas de pinchos: la sala sale pelada.");
            return cells;
        }

        private static SpecialTileDefinitionSO LoadSharedSpikeTile()
        {
            var definition = AssetDatabase.LoadAssetAtPath<SpecialTileDefinitionSO>(SharedSpikeTilePath);
            Assert.IsNotNull(definition,
                $"Fixture roto: no existe '{SharedSpikeTilePath}'. Se usa una definición cualquiera " +
                "que ya esté en disco porque la del Cajero la crea su propio menú.");
            return definition;
        }

        /// <summary>
        /// Corre el paso que convierte celdas de plano en placements y devuelve sus findings.
        /// </summary>
        /// <remarks>
        /// Por reflexión porque el paso es privado, y en un solo lugar por lo mismo: si se renombra o
        /// cambia de firma, rompe acá con un mensaje que lo dice, en vez de en cada test. Vale la
        /// incomodidad: es el único paso que escribe las casillas especiales de la sala, y cuando no
        /// se llama la sala se construye pelada <b>sin que falle nada</b> — que es exactamente el
        /// estado que se shippeó.
        /// </remarks>
        private static List<string> ApplySpecialTiles(RoomLayout layout, params Vector2Int[] planCells)
        {
            var apply = typeof(BossRoomBuilder).GetMethod(
                "ApplySpecialTiles", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(apply,
                "BossRoomBuilder.ApplySpecialTiles se movió o cambió de firma. Es el único paso que " +
                "convierte las celdas del plano en placements: si nadie lo llama, la sala se " +
                "construye sin casillas especiales y no falla nada.");

            var plan = new BossRoomPlan
            {
                BossName = "TestBoss",
                SpecialTiles = new[]
                {
                    new BossRoomSpecialTilePlan
                    {
                        DefinitionAssetPath = SharedSpikeTilePath,
                        PlanCells = planCells,
                    },
                },
            };

            var failures = new List<string>();
            apply.Invoke(null, new object[] { plan, layout, failures });
            return failures;
        }

        /// <summary>Sprite in-memory: no reimporta la textura compartida del pack de símbolos.</summary>
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

        private AINode_Alternate Alternate()
        {
            var cycle = Descendants(_root).OfType<AINode_Alternate>().SingleOrDefault();
            Assert.IsNotNull(cycle, "No hay ciclo de ataque en el árbol (o hay más de uno).");
            return cycle;
        }

        private static int ReadInt(AIIntReader reader)
        {
            var constant = reader as AIConstantInt;
            Assert.IsNotNull(constant,
                "Se esperaba un AIConstantInt. AIReadSelfStat devuelve 0 sin AttributesManager " +
                "(EditMode) y el ?? del nodo no cubre un reader no-null, así que el jefe se " +
                "quedaría clavado en los tests y nadie sabría por qué.");
            return constant.Value;
        }

        /// <summary>Índice del hijo del Sequence raíz que contiene un <typeparamref name="T"/>.</summary>
        private int IndexOfStepWith<T>() where T : class =>
            _root.Children.FindIndex(c => Descendants(c).OfType<T>().Any());

        /// <summary>Desenvuelve el <typeparamref name="T"/> de un hijo, venga suelto o dentro del
        /// <see cref="AINode_Selector"/> de aislamiento de fallos.</summary>
        private static T Unwrap<T>(AIDecisionNode child) where T : class
        {
            if (child is T direct) return direct;
            if (child is AINode_Selector sel && sel.Children != null)
                return sel.Children.OfType<T>().FirstOrDefault();
            return null;
        }

        /// <summary>
        /// El gate de HP del hijo raíz que además contiene un <typeparamref name="T"/>. El tipo
        /// desambigua por si algún día dos mecánicas vuelven a compartir umbral.
        /// </summary>
        private AINode_If FindGateAtPercent<T>(float percent) where T : class
        {
            return _root.Children.Select(Unwrap<AINode_If>).FirstOrDefault(g =>
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
            return _root.Children.FindIndex(c => ReferenceEquals(Unwrap<AINode_If>(c), gate));
        }

        /// <summary>Tree-walker por reflexión, sin descender en <see cref="Object"/>. Copiado de
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
