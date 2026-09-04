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
// Explícito porque el archivo importa UnityEditor y UnityEngine juntos: sin él `Object` es ambiguo.
using Object = UnityEngine.Object;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>Wiring del árbol de El Cajero en memoria: contra el builder y no contra el
    /// <c>.asset</c>, que falla por reimports en vez de por diseño roto.</summary>
    [TestFixture]
    public class CajeroPhaseWiringTests
    {
        private const float PercentTolerance = 0.0001f;

        private const int RootSteps = 4;

        /// <summary>Una definición que <b>ya existe en disco</b>. No es la del Cajero a propósito:
        /// <c>Tile_Spikes_Cajero</c> la crea su propio menú, y usarla ataría el test a que se haya
        /// corrido.</summary>
        private const string SharedSpikeTilePath = "Assets/Rollgeon/Tiles/Tile_Spikes.asset";

        private AINode_Sequence _root;

        [SetUp]
        public void SetUp()
        {
            _root = CajeroAssetBuilder.BuildAIRoot();
            Assert.IsNotNull(_root, "El builder tiene que devolver un Sequence raíz.");
        }

        [Test]
        public void Root_HasTheStepsOfTheSheet()
        {
            Assert.AreEqual(RootSteps, _root.Children.Count,
                "Comisiones → persigue → ataca (mandoble o empujón, y las monedas del golpe) → caja.");

            Assert.IsNotNull(FindNode<AINode_SpawnReinforcements>(), "Faltan las Comisiones del 50%.");
            Assert.IsNotNull(FindNode<AINode_Alternate>(), "Falta el ciclo de los dos golpes.");
            Assert.IsNotNull(FindNode<AINode_CajeroShove>(), "Falta el empujón.");
            Assert.IsNotNull(FindNode<AINode_CajeroCoinRain>(), "Faltan las monedas del golpe.");
            Assert.IsNotNull(FindNode<AINode_CajeroCoinVault>(), "Falta la caja: nada vence las monedas.");
            Assert.IsNotNull(FindNode<AINode_Move>(), "Falta la persecución.");
        }

        [Test]
        public void Boss_HasNoneOfTheRetiredDesign()
        {
            Assert.IsEmpty(Descendants(_root).OfType<AINode_CashierRangedShot>().ToList(),
                "Volvió el disparo instantáneo del diseño viejo. Su daño a distancia es el " +
                "cañonazo, que avisa un turno antes y se esquiva; éste no.");
            Assert.IsEmpty(Descendants(_root).OfType<AINode_CashierCounterToll>().ToList(),
                "Volvió el peaje del mostrador, y la sala nueva no tiene mostrador que cruzar.");
            Assert.IsEmpty(Descendants(_root).OfType<AINode_TelegraphMarkGoldScaled>().ToList(),
                "Volvió la columna escalada por oro: el oro dejó de ser la unidad de daño del jefe.");
            Assert.IsEmpty(Descendants(_root).OfType<AINode_CashierAudit>().ToList(),
                "Volvió el arqueo, que cobra oro Y lo cura: el cobro es del empujón, y no se cura con nada.");
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

        /// <summary>La caja descubre las monedas barriendo las instancias vivas, así que corre después de
        /// todo lo que suelta monedas: adelante, cada moneda de este turno viviría una ronda de más.</summary>
        [Test]
        public void CoinVault_RunsAfterEverythingThatDropsCoins()
        {
            int vaultIdx = IndexOfStepWith<AINode_CajeroCoinVault>();
            int dropIdx = IndexOfStepWith<AINode_CajeroCoinRain>();
            int shoveIdx = IndexOfStepWith<AINode_CajeroShove>();

            Assert.Greater(vaultIdx, -1, "No hay caja en el árbol.");
            Assert.Greater(vaultIdx, dropIdx, "La caja quedó antes de las monedas del mandoble.");
            Assert.Greater(vaultIdx, shoveIdx, "La caja quedó antes del empujón, que también tira monedas.");
        }

        /// <summary>Las monedas cuelgan del golpe y de nada más: sueltas en la raíz volvían a caer por
        /// reloj, que es justo lo que se sacó.</summary>
        [Test]
        public void Coins_FallFromTheBlow_NotFromAClock()
        {
            int dropIdx = IndexOfStepWith<AINode_CajeroCoinRain>();
            int attackIdx = IndexOfStepWith<AINode_Alternate>();

            Assert.AreEqual(attackIdx, dropIdx,
                "Las monedas volvieron a ser un paso propio de la raíz: caen tickee lo que tickee el " +
                "jefe, incluidos los turnos en que no te alcanzó.");

            var drop = FindNode<AINode_CajeroCoinRain>();
            Assert.IsEmpty(
                drop.GetType().GetFields().Where(f => f.Name.Contains("EveryN")).ToList(),
                "Volvió el reloj al nodo: con período propio las monedas dejan de depender del golpe.");
        }

        /// <summary>La persecución corre antes del golpe: detrás, el jefe tumba al jugador tres casillas y
        /// en el mismo turno camina cuatro para volver a pegarse, así que el tumbo no cambia nada.</summary>
        [Test]
        public void Chase_RunsBeforeTheBlow_SoAShoveIsNotWalkedBack()
        {
            int chaseIdx = IndexOfStepWith<AINode_Move>();
            int blowIdx = IndexOfStepWith<AINode_Alternate>();

            Assert.Greater(chaseIdx, -1, "No hay persecución en el árbol.");
            Assert.Greater(blowIdx, -1, "No hay ciclo de golpes en el árbol.");
            Assert.Less(chaseIdx, blowIdx,
                "La persecución quedó detrás del golpe: el jefe vuelve a pegarse el mismo turno en " +
                "que empuja y el tumbo deja de tener efecto.");
        }

        /// <summary>Cerrar y pegar entran en el mismo turno porque la persecución apunta al mismo rango que
        /// exige el gate: con un <c>DesiredRange</c> mayor frena a un paso y no golpea al caminar.</summary>
        [Test]
        public void Chase_StopsExactlyWithinReachOfTheBlow()
        {
            var chase = FindNode<AINode_Move>();

            Assert.IsNotNull(chase, "No hay persecución en el árbol.");
            Assert.AreEqual(CajeroAssetBuilder.MeleeRange, ReadInt(chase.DesiredRange),
                "La persecución dejó de frenar en el rango del golpe.");
            Assert.AreEqual(CajeroAssetBuilder.ChaseSteps, ReadInt(chase.MaxSteps));
            Assert.IsFalse(chase.Retreat,
                "Con Retreat el jefe kitearía, y estando pegado se alejaría en vez de golpear.");
        }

        [Test]
        public void EveryChild_IsIsolatedInSelectorWithWaitFallback()
        {
            // Los cuatro tienen un Failed benigno (no cruzó el umbral, el jugador está lejos, ninguna
            // moneda venció, ya está pegado): suelto en el Sequence, cualquiera aborta el turno.
            for (int i = 0; i < _root.Children.Count; i++)
            {
                var selector = _root.Children[i] as AINode_Selector;
                Assert.IsNotNull(selector,
                    $"El hijo {i} del Sequence raíz no está envuelto en Selector: su Failed abortaría el turno.");
                Assert.IsTrue(selector.Children.Any(c => c is AINode_Wait),
                    $"El Selector del hijo {i} no tiene Wait de fallback — devolvería Failed igual.");
            }
        }

        /// <summary>El índice del <see cref="AINode_Alternate"/> arranca en 0, así que el primer hijo es
        /// con el que abre la pelea.</summary>
        [Test]
        public void AttackCycle_AlternatesTheHeavyBlowAndTheShove_InThatOrder()
        {
            var cycle = Alternate();

            Assert.AreEqual(2, cycle.Children.Count, "Son dos golpes y sólo dos.");

            // El mandoble va dentro de un Sequence con su drop de monedas; el empujón las tira solo.
            var first = Descendants(cycle.Children[0]).OfType<AINode_RangedShot>().FirstOrDefault();
            var second = Unwrap<AINode_RangedShot>(cycle.Children[1]);

            Assert.IsNotNull(first, "El primer tiempo del ciclo no es un golpe.");
            Assert.IsNotNull(second, "El segundo tiempo del ciclo no es un golpe.");
            Assert.IsNotInstanceOf<AINode_CajeroShove>(first,
                "La pelea abre con el empujón. El mandoble va primero porque es el golpe que no se " +
                "puede prevenir: el empujón es el que el jugador puede preparar eligiendo desde qué " +
                "casilla atacarlo, y para eso tiene que haber visto uno antes.");
            Assert.IsInstanceOf<AINode_CajeroShove>(second, "El segundo tiempo tiene que ser el empujón.");
        }

        /// <summary><see cref="AINode_Alternate"/> avanza el índice ANTES de tickear al hijo y no lo
        /// devuelve si el hijo falla: con el gate afuera, sólo se mueve en los turnos en que pega.</summary>
        [Test]
        public void AttackCycle_IsGatedByRangeFromOutsideTheAlternate()
        {
            var gate = RangeGate();

            Assert.IsInstanceOf<AINode_TelegraphMark>(gate.Else,
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

        /// <summary>Cobrar la marca ES el ataque del turno. Con el cobro como paso suelto,
        /// acercársele con una marca puesta costaba el cañonazo Y el mandoble en el mismo turno.</summary>
        [Test]
        public void TheSlamAndTheMeleeCycle_NeverLandInTheSameTurn()
        {
            var pending = PendingGate();

            Assert.IsInstanceOf<AINode_ExecuteTelegraph>(pending.Then,
                "El gate de marca pendiente tiene que cobrarla en su Then.");
            Assert.IsEmpty(Descendants(pending.Then).OfType<AINode_Alternate>().ToList(),
                "El ciclo melee quedó del lado del cobro: el jefe pega dos veces en el turno que dispara.");
            Assert.IsNotNull(Descendants(pending.Else).OfType<AINode_Alternate>().FirstOrDefault(),
                "El ciclo melee tiene que colgar de la rama sin marca pendiente.");
        }

        /// <summary>El paso que reemplaza al <c>Wait</c>: caminar y no llegar deja de ser un turno
        /// perdido.</summary>
        [Test]
        public void OutOfReach_HeMarksTheSlamInsteadOfWastingTheTurn()
        {
            var mark = RangeGate().Else as AINode_TelegraphMark;

            Assert.IsNotNull(mark,
                "Fuera de alcance el jefe volvió a esperar: camina cuatro casillas y no pasa nada.");
            Assert.AreEqual(ThreatShape.SquareAroundPlayer, mark.Shape,
                "El área se centra en el jugador y no en el jefe: es donde estás parado al marcar.");
            Assert.AreEqual(CajeroAssetBuilder.SlamRadius, mark.Size);
            Assert.AreEqual(3, 2 * mark.Size + 1, "3×3 contando la casilla del centro.");
            Assert.AreEqual(CajeroAssetBuilder.SlamDamage, mark.Damage,
                "Cableado desde la constante de la ficha, no del default del nodo.");
            Assert.Greater(mark.Damage, CajeroAssetBuilder.HeavyDamage,
                "Se esquiva con un turno entero de aviso: tiene que doler más que el mandoble, que " +
                "no se puede prevenir.");
            Assert.IsTrue(mark.IgnoreLineOfSight,
                "Con el filtro de visión el área promediaba 5.46 de 9 casillas y en el 13.7% de las " +
                "posiciones quedaba vacía: el jefe perdía el turno de marca sin que nada lo explicara.");
            Assert.IsTrue(mark.KeepSquareWhole,
                "Contra una pared el cuadrado sale mordido — en una esquina llegaba a 3 casillas.");
        }

        /// <summary>Un <c>SetTrigger</c> inexistente no falla —el jefe cobra y no se mueve— y dos
        /// tiempos con el mismo clip se leen como el mismo ataque.</summary>
        [Test]
        public void TheSlam_AimsAndFiresWithDifferentGestures()
        {
            var mark = Descendants(_root).OfType<AINode_TelegraphMark>().Single();
            var execute = Descendants(_root).OfType<AINode_ExecuteTelegraph>().Single();

            Assert.AreEqual(BossFeedbackIds.CajeroAimAnim, mark.WindupFeedbackId,
                "El turno de aviso sin gesto es un turno en que el jefe camina y nada más.");
            Assert.AreEqual(BossFeedbackIds.CajeroShotAnim, execute.WindupFeedbackId,
                "El disparo usa el gesto ranged del rig, el único de sus tres que ningún otro " +
                "tiempo ocupa.");
            Assert.AreNotEqual(mark.WindupFeedbackId, execute.WindupFeedbackId,
                "Apuntar y disparar con el mismo id son dos turnos que se ven iguales.");
        }

        /// <summary>El área quedó anclada donde estabas al marcar: si además caminara, el jugador vería
        /// al jefe encima suyo y el golpe cayendo en otro lado.</summary>
        [Test]
        public void TheTurnHeFires_HeStandsStill()
        {
            var gate = ChaseGate();

            Assert.IsInstanceOf<AINode_Wait>(gate.Then,
                "Con marca pendiente el jefe volvió a caminar: dispara a una casilla y está en otra.");
            Assert.IsNotNull(Descendants(gate.Else).OfType<AINode_Move>().FirstOrDefault(),
                "La persecución tiene que seguir corriendo los turnos sin marca — incluido el que " +
                "marca. Sin eso, kiteando no te alcanza nunca y sus dos golpes pegados no existen.");
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

            Assert.AreEqual(CajeroAssetBuilder.CoinsPerHit, shove.CoinCount,
                "Las monedas del tumbo salen de la ficha, no del default del nodo.");

            Assert.AreEqual(BossFeedbackIds.CajeroShoveAnim, shove.AnimFeedbackId,
                "Gesto propio: con el del mandoble los dos tiempos del ciclo se veían iguales.");
            Assert.AreEqual(BossFeedbackIds.CajeroImpactVfx, shove.ImpactVfxFeedbackId);
            Assert.AreEqual(BossFeedbackIds.CajeroImpactFeel, shove.ImpactFeelFeedbackId);
        }

        [Test]
        public void Shove_ChargesTheSheetsCutOfYourGold_AndGivesMostOfItBack()
        {
            var shove = FindNode<AINode_CajeroShove>();

            Assert.AreEqual(CajeroAssetBuilder.ShoveTaxPercent, shove.TaxPercent, 0.0001f,
                "El cobro sale de la ficha, no del default del nodo.");
            Assert.AreEqual(CajeroAssetBuilder.ShoveTaxMinimum, shove.TaxMinimum);
            Assert.AreEqual(CajeroAssetBuilder.ShoveRefundPercent, shove.RefundPercent, 0.0001f);

            Assert.Greater(shove.TaxMinimum, 0,
                "Sin piso, el jugador sin oro es inmune a media pelea: no le sacaría nada, no " +
                "caerían monedas, y el reloj de la sala desaparecería justo para el que peor viene.");
            Assert.Less(shove.RefundPercent, 1f,
                "Si volviera todo, el empujón dejaría de costar oro y sería sólo un traslado de " +
                "plata del bolsillo al piso.");
            Assert.Greater(shove.RefundPercent, 0f,
                "Si no volviera nada, no habría razón para caminar la sala y el jefe sería melee puro.");

            Assert.AreEqual(CajeroAssetBuilder.CoinMinSeparation, shove.CoinMinSeparation,
                "Los dos golpes reparten por la sala con el mismo criterio.");
            Assert.Greater(shove.CoinMinSeparation, 1,
                "Sin separación las dos monedas pueden caer pegadas, y entonces recuperarlas es un " +
                "solo viaje: el empujón te cobra dos veces y te devuelve en un desvío.");
        }

        [Test]
        public void CoinDrop_UsesTheSheetNumbers()
        {
            var drop = FindNode<AINode_CajeroCoinRain>();

            Assert.AreEqual(CajeroAssetBuilder.CoinsPerHit, drop.Count);
            Assert.AreEqual(CajeroAssetBuilder.ChipValue, drop.MinValue);
            Assert.AreEqual(CajeroAssetBuilder.ChipValue, drop.MaxValue);
            Assert.AreEqual(drop.MinValue, drop.MaxValue,
                "La moneda del mandoble vale fijo: un rango hace que el montón valga un número que " +
                "el jugador no puede leer del piso, y no cambia ninguna decisión suya.");
            Assert.AreEqual(CajeroAssetBuilder.CoinMinSeparation, drop.MinSeparation);
            Assert.Greater(drop.MinSeparation, 1,
                "\"Repartidas por la sala\" es media mecánica: cada moneda tiene que ser un punto al " +
                "que ir, y tres pegadas son un solo viaje.");
        }

        /// <summary>El <c>Sequence</c> corta en el primer Failed, así que el drop tiene que ir DETRÁS del
        /// golpe: adelante, paga aunque el mandoble no conecte.</summary>
        [Test]
        public void TheBlowsCoins_OnlyFallIfTheBlowLands()
        {
            var cycle = Alternate();
            var melee = Descendants(cycle.Children[0]).OfType<AINode_Sequence>().FirstOrDefault();

            Assert.IsNotNull(melee, "El mandoble dejó de arrastrar su drop de monedas.");

            int blowIdx = melee.Children.FindIndex(c => Descendants(c).OfType<AINode_RangedShot>().Any());
            int dropIdx = melee.Children.FindIndex(c => Descendants(c).OfType<AINode_CajeroCoinRain>().Any());

            Assert.Greater(blowIdx, -1, "No hay mandoble en la rama.");
            Assert.Greater(dropIdx, blowIdx, "Las monedas caen antes del golpe: pagan aunque no conecte.");

            Assert.IsInstanceOf<AINode_Selector>(melee.Children[dropIdx],
                "El drop suelto en el Sequence hace fallar al mandoble cuando la sala no tiene " +
                "casilla libre, y el golpe ya cobró.");
        }

        [Test]
        public void CoinVault_CarriesTheClockFromTheSheet()
        {
            var vault = FindNode<AINode_CajeroCoinVault>();

            Assert.AreEqual(CajeroAssetBuilder.ChipDurationRounds, vault.LifetimeRounds,
                "El reloj de la moneda vive en este nodo, no en el DurationRounds del hazard: el " +
                "servicio expira igual una moneda levantada y una vencida, y desde afuera no se " +
                "pueden distinguir.");
        }

        /// <summary>La caja reconoce el piso comparando <c>info.Definition == Coin</c>: con definiciones
        /// distintas no falla nada, simplemente ninguna moneda vence y el jefe no se cura jamás.</summary>
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
                    "El mandoble suelta monedas de otra definición.");
                Assert.AreSame(definition, Descendants(root).OfType<AINode_CajeroCoinVault>().Single().Coin,
                    "La caja vigila otra definición: nada vence y el jefe no se cura nunca.");
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        /// <summary>Lo que se construye es el plano, no el array de la ficha: todo lo de acá cruza los dos
        /// archivos, porque cruzar el plano contra sí mismo pasa en verde con la sala pelada.</summary>
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

        /// <summary>Descentrarlo hace que las cuatro esquinas por las que puede entrar el jugador dejen de
        /// ser equivalentes.</summary>
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

        /// <summary>Dos pegados forman una pared que el tumbo no cruza sin cobrar doble. Se verifica sobre
        /// las celdas que el plano coloca, no sobre el array: la regla vale sobre lo construido.</summary>
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

        /// <summary>Fuera del plano, <c>PlanToRoom</c> manda la celda a una casilla que la sala no tiene:
        /// el placement queda escrito y el layout se ve completo.</summary>
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

            // Más ajustado que los bordes del plano a propósito: el jugador entra por una esquina al
            // azar, y contra la pared un blocker no encarece ningún camino, sólo se come piso.
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

        /// <summary>La lista importa: <c>SpecialTilePlacements</c> es la permanente y <c>SpecialTileSlots</c>
        /// rolea el tipo, y acá la posición y el tipo son los dos autoría. Y agrega en vez de
        /// reemplazar: lo que ya está en la lista es autoría de la sala base compartida del piso.</summary>
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

        /// <summary>Dos placements en la misma coord <b>cobran los dos</b>: los triggers disparan una vez
        /// por instancia y <c>Place</c> no valida el solape.</summary>
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

        /// <summary><c>AIPathPlanner.ComputeHazardPenalty</c> es <c>ceil(daño / HP × 10 × Caution)</c> y la
        /// casilla cuesta <c>1 + penalty</c>: la suma apunta a que <c>daño / HP</c> dé 1 y el penalty
        /// sature en 10. No es daño — el filtro de supervivencia sólo mira los 20 reales.</summary>
        [Test]
        public void SpikeVirtualDamage_SaturatesThePathingPenalty_AtExactlyHisOwnHp()
        {
            Assert.AreEqual(CajeroAssetBuilder.BaseHP,
                CajeroAssetBuilder.SpikeDamage + CajeroAssetBuilder.SpikeAIVirtualDamage,
                "El daño real más el virtual dejó de dar la vida entera del jefe, así que el penalty " +
                "del planner ya no satura en 10 y el pincho armado vuelve a ser sólo caro.");
            Assert.Greater(CajeroAssetBuilder.SpikeDamage, 0,
                "Sin daño real el pincho es un cartel: el planner lo rodea y al jugador no le cuesta nada.");
        }

        [Test]
        public void SpikeTile_NeverWearsOut_SoTheFieldStaysAWallForHim()
        {
            var spikes = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            try
            {
                CajeroAssetBuilder.PopulateSpikeTile(spikes);

                Assert.IsFalse(spikes.DisarmOnTrigger,
                    "Volvió el desarme: un pincho disparado quedaría bajado, el pathing lo lee como " +
                    "suelo limpio, y cada pincho que el jugador gasta le abre al jefe un pasillo " +
                    "justo después de haberlo pagado. Las diez casillas cobran todas las veces.");
                Assert.IsFalse(spikes.RearmOnRoundWrap,
                    "Rearmar sin desarme no hace nada, y deja leyendo que hay una ventana en la que " +
                    "el campo se abre.");

                Assert.AreEqual(0, spikes.DefaultDurationRounds,
                    "Terreno de la sala, no algo que el jefe pone: no vence.");
                Assert.AreEqual(CajeroAssetBuilder.SpikeDamage, spikes.EnterDamage);
                Assert.IsTrue(spikes.Triggers.HasFlag(TileTrigger.OnForcedMovementInto),
                    "Sin OnForcedMovementInto el tumbo cruza los pinchos gratis, y el empujón deja " +
                    "de meter al jugador adonde duele.");
            }
            finally
            {
                Object.DestroyImmediate(spikes);
            }
        }

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

        /// <summary>Es el único umbral de la pelea, así que también el único <see cref="AINode_Once"/>:
        /// otro latch es una mecánica colgada de un umbral que la ficha no dice.</summary>
        [Test]
        public void Once_WrapsOnlyThePhaseGate_SoTheCoinsKeepRunning()
        {
            var latches = Descendants(_root).OfType<AINode_Once>().ToList();

            Assert.AreEqual(1, latches.Count, "El one-shot del jefe es uno y sólo uno: las Comisiones.");

            foreach (var latch in latches)
            {
                Assert.IsEmpty(Descendants(latch).OfType<AINode_CajeroCoinRain>().ToList(),
                    "Un Once sobre el drop deja de pagar monedas después del primer mandoble.");
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
                "El que se agita al invocar es el propio Cajero (trigger 'Attack_Melee' de " +
                "AnimCon_Mecha), no la Comisión que recién va a aparecer. Sin gesto, los dos bichos " +
                "se materializan con el jefe quieto y no se leen como cosa suya.");
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

        /// <summary>El ranged compartido trae 50 de vida y 10 de daño —dos de ésos son un segundo jefe— y
        /// además es el asset de todos los encuentros, así que autorarle 18/6 cambia media run.</summary>
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
                Assert.AreEqual("Enemigo a Distancia", critter.DisplayName,
                    "El refuerzo se lee como un ranged común más — un nombre propio le promete " +
                    "al jugador una mecánica que no tiene.");
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

        /// <summary><c>IsFlying</c> gatea <c>SpecialTileService.ShouldAffect</c>, y esa misma guarda filtra
        /// <c>TryGetTileFor</c> —la vista del planner—: sin el flag cobra los pinchos Y los rodea.</summary>
        [Test]
        public void CritterData_KeepsHerFlying_SoTheSpikesDoNotBillHer()
        {
            var critter = ScriptableObject.CreateInstance<EnemyDataSO>();
            critter.hideFlags = HideFlags.HideAndDontSave;
            var spikes = AssetDatabase.LoadAssetAtPath<SpecialTileDefinitionSO>(
                CajeroAssetBuilder.SpikeTilePath);
            try
            {
                critter.IsFlying = false;

                CajeroAssetBuilder.PopulateCritterData(critter);

                Assert.IsTrue(critter.IsFlying,
                    "PopulateCritterData dejó de escribir IsFlying: un asset sin el tick queda a " +
                    "ras del piso y los pinchos GroundOnly la borran de un toque.");

                Assert.IsNotNull(spikes, $"No se encontró el pincho en '{CajeroAssetBuilder.SpikeTilePath}'.");
                Assert.AreEqual(TileAffinity.GroundOnly, spikes.Affinity,
                    "Volar no la salva de nada si el pincho deja de ser GroundOnly: las dos mitades " +
                    "de la inmunidad viven en archivos distintos y se rompen por separado.");
                Assert.Greater(spikes.EnterDamage, critter.BaseHP - CajeroAssetBuilder.CritterDamage,
                    "El pincho le saca más de lo que le queda después de un golpe cualquiera: por eso " +
                    "esto no es un detalle cosmético sino la diferencia entre existir y no.");
            }
            finally
            {
                Object.DestroyImmediate(critter);
            }
        }

        /// <summary>
        /// El aspecto se copia del ranged común en cada build en vez de estar duplicado en las
        /// constantes: dos fichas a mano ya habían derivado en dos bichos distintos (otro arte,
        /// otro retrato, y una descripción que hablaba de una comisión que el bicho no cobra).
        /// </summary>
        [Test]
        public void CritterData_TakesItsLookFromTheCommonRanged_WithoutTakingItsNumbers()
        {
            var critter = ScriptableObject.CreateInstance<EnemyDataSO>();
            var look = ScriptableObject.CreateInstance<EnemyDataSO>();
            critter.hideFlags = HideFlags.HideAndDontSave;
            look.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                look.Description = "Goblin con ataque a distancia, dispara con una ballesta.";
                look.VisualPrefab = new GameObject("look_visual") { hideFlags = HideFlags.HideAndDontSave };
                look.Portrait = Sprite.Create(new Texture2D(2, 2), new Rect(0, 0, 2, 2), Vector2.zero);
                look.BaseHP = 50;
                look.BaseAttack = 10;

                CajeroAssetBuilder.PopulateCritterData(critter, look);

                Assert.AreEqual(look.Description, critter.Description,
                    "La descripción sigue siendo propia: el párrafo del tooltip la delata como " +
                    "personaje aparte.");
                Assert.AreSame(look.VisualPrefab, critter.VisualPrefab,
                    "Arte propio = el jugador ve dos bichos distintos, que es justo lo que no queremos.");
                Assert.AreSame(look.Portrait, critter.Portrait,
                    "El retrato sale en la cola de turnos: con uno propio se lee como cosa del jefe.");

                Assert.AreEqual(CajeroAssetBuilder.CritterHp, critter.BaseHP,
                    "Los números son suyos y los pone el balance: copiar los del común la vuelve un " +
                    "segundo jefe de 50 de vida.");
                Assert.AreEqual(CajeroAssetBuilder.CritterDamage, critter.BaseAttack);
            }
            finally
            {
                if (look.VisualPrefab != null) Object.DestroyImmediate(look.VisualPrefab);
                if (look.Portrait != null) Object.DestroyImmediate(look.Portrait);
                Object.DestroyImmediate(look);
                Object.DestroyImmediate(critter);
            }
        }

        /// <summary>
        /// El asset del ranged común tiene que existir donde el builder lo busca: sin él la
        /// Comisión queda con el arte que ya tenía y nadie se enteraría hasta ver la pelea.
        /// </summary>
        [Test]
        public void SharedRangedAsset_IsWhereTheBuilderLooksForIt()
        {
            var look = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(
                CajeroAssetBuilder.SharedRangedAssetPath);

            Assert.IsNotNull(look,
                $"No hay EnemyDataSO en '{CajeroAssetBuilder.SharedRangedAssetPath}'.");
            Assert.AreNotEqual(CajeroAssetBuilder.ReinforcementAssetPath,
                CajeroAssetBuilder.SharedRangedAssetPath,
                "El aspecto se copia de ahí, pero el spawn NO puede apuntar ahí: serían dos bichos " +
                "de 50 de vida a mitad de pelea.");
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
        public void CritterAI_ShootsFirstAndMovesAfter_SoArrivingDoesNotEatItsShot()
        {
            var root = CajeroAssetBuilder.BuildCritterAIRoot();

            Assert.IsNotNull(root, "La Comisión necesita árbol propio: sin él cae al BasicEnemyAI.");
            Assert.AreEqual(3, root.Children.Count, "Dispara, se despega y vuela, nada más.");

            int shotIdx = root.Children.FindIndex(c =>
                Descendants(c).Any(n => n is AINode_RangedShot));
            int keepIdx = root.Children.FindIndex(c =>
                Descendants(c).Any(n => n is AINode_KeepDistance));
            int moveIdx = root.Children.FindIndex(c =>
                Descendants(c).Any(n => n is AINode_Move));

            Assert.Greater(shotIdx, -1, "Falta el disparo.");
            Assert.Greater(keepIdx, shotIdx,
                "Los nodos de movimiento devuelven Running cuando se mueven, y un Running corta el " +
                "Sequence: con el orden invertido, el turno en que se despega se le comería el disparo.");
            Assert.Greater(moveIdx, keepIdx,
                "El approach va último: el que corta el turno tiene que ser el que resolvió que " +
                "había que moverse, y despegarse manda sobre acercarse.");
        }

        /// <summary>
        /// La frase de su ficha —compartida con el ranged común— dice que se aleja cuando te le
        /// acercás. Sin este nodo era la única de las dos que mentía.
        /// </summary>
        [Test]
        public void CritterAI_BacksAwayLikeTheCommonRanged()
        {
            var keep = Descendants(CajeroAssetBuilder.BuildCritterAIRoot())
                .OfType<AINode_KeepDistance>()
                .FirstOrDefault();

            Assert.IsNotNull(keep, "La Comisión dejó de despegarse: pegada al jugador es un melee " +
                                   "de 18 de vida que pega 8.");
            Assert.AreEqual(CajeroAssetBuilder.CritterRange, ReadInt(keep.IdealDistance),
                "Se despega hasta su alcance, que es el mismo 5 del ranged común.");
            Assert.AreEqual(CajeroAssetBuilder.CritterMoveSteps, ReadInt(keep.MaxSteps),
                "Los mismos 3 pasos del ranged común.");
        }

        /// <summary>
        /// El jugador tiene que ver un ranged común más, y el disparo del jefe se veía distinto:
        /// otro gesto, otro vfx y —sobre todo— sin impacto, porque la Comisión no tenía ni vfx ni
        /// feel cableados.
        /// </summary>
        [Test]
        public void CritterShot_WearsTheCommonRangedPresentation_NotItsBoss()
        {
            var shot = Descendants(CajeroAssetBuilder.BuildCritterAIRoot())
                .OfType<AINode_RangedShot>()
                .First();

            Assert.AreEqual("anim.enemy.ranged.attack", shot.AnimFeedbackId,
                "Volvió el gesto del jefe (o el del bite propio): el minion se delata como cosa suya.");
            Assert.AreEqual("vfx.enemy.ranged.impact", shot.ImpactVfxFeedbackId,
                "Sin el vfx compartido su disparo no revienta en el jugador y el común sí.");
            Assert.AreEqual("feel.enemy.ranged.impact", shot.ImpactFeelFeedbackId,
                "Sin el feel compartido su disparo no se siente y el común sí.");
            Assert.IsFalse(shot is AINode_CashierRangedShot,
                "La subclase defaultea a los feedbacks del Cajero: con ella, vaciar un id vuelve a " +
                "poner al minion a disparar como el jefe.");
        }

        /// <summary>Camina hasta su alcance y no hasta el contacto: pegada al jugador muere de un golpe
        /// cualquiera para pegar exactamente lo mismo que pega de lejos.</summary>
        [Test]
        public void CritterAI_ShootsFromItsOwnRange_NotFromContact()
        {
            var root = CajeroAssetBuilder.BuildCritterAIRoot();
            var shot = Descendants(root).OfType<AINode_RangedShot>().First();
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
                // Contra las constantes y no contra literales: son la ficha, y un retune tiene que
                // poder moverlas sin pasar por acá.
                Assert.AreEqual(CajeroAssetBuilder.BaseHP, data.BaseHP,
                    "La ficha escribe una vida distinta de la que dice su constante.");
                Assert.AreEqual(CajeroAssetBuilder.BaseAttack, data.BaseAttack,
                    "La ficha escribe un mandoble distinto del que dice su constante.");
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

        /// <summary>El arte tiene alas, así que <c>IsFlying</c> se escribe explícito: un tick en el
        /// Inspector le saca el único costo que la sala le cobra a él (los pinchos son
        /// <c>GroundOnly</c>), y "los esquiva caminando pero los come empujado" es la pelea.</summary>
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

        /// <summary>Por reflexión porque el paso es privado, y en un solo lugar por lo mismo: si se
        /// renombra rompe acá con un mensaje que lo dice, en vez de en cada test.</summary>
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

        /// <summary>El <c>If</c> que decide si cobra la marca; el de rango cuelga de su <c>Else</c>.</summary>
        /// <remarks>Desambigua por el <c>Then</c>: el gate que frena la caminata lee la misma
        /// precondición y va antes en el árbol.</remarks>
        private AINode_If PendingGate()
        {
            var gate = PendingGates().FirstOrDefault(g => g.Then is AINode_ExecuteTelegraph);

            Assert.IsNotNull(gate, "No hay gate de marca pendiente: el cañonazo nunca se cobra.");
            return gate;
        }

        /// <summary>El <c>If</c> que le saca la caminata al turno del disparo.</summary>
        private AINode_If ChaseGate()
        {
            var gate = PendingGates().FirstOrDefault(g => Descendants(g).OfType<AINode_Move>().Any());

            Assert.IsNotNull(gate, "La persecución dejó de estar gateada por la marca pendiente.");
            return gate;
        }

        private IEnumerable<AINode_If> PendingGates() =>
            Descendants(_root).OfType<AINode_If>().Where(
                g => g.Conditions != null && g.Conditions.OfType<PcOwnerHasPendingTelegraph>().Any());

        private AINode_If RangeGate()
        {
            var gate = Descendants(_root).OfType<AINode_If>()
                .FirstOrDefault(g => g.Then is AINode_Alternate);

            Assert.IsNotNull(gate,
                "El Alternate dejó de colgar de un If: sin gate afuera, los turnos de caminata le " +
                "queman slots al ciclo y la alternancia deja de ser estricta.");
            return gate;
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

        /// <summary>El tipo desambigua por si dos mecánicas vuelven a compartir umbral.</summary>
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

        /// <summary>Tree-walker por reflexión, sin descender en <see cref="Object"/>.</summary>
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
