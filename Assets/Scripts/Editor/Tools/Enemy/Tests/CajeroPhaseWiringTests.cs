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
            Assert.AreEqual(6, _root.Children.Count,
                "Detona → arqueo → arma el peaje → ataca (marca o dispara) → suelta → se corre.");
            Assert.IsNotNull(FindNode<AINode_TelegraphMarkGoldScaled>(), "Falta la columna que engorda.");
            Assert.IsNotNull(FindNode<AINode_CashierRangedShot>(), "Falta el disparo de los turnos sin columna.");
            Assert.IsNotNull(FindNode<AINode_CashierCounterToll>(), "Falta el peaje del mostrador.");
            Assert.IsNotNull(FindNode<AINode_CashierDropChips>(), "Faltan las fichas.");
            Assert.IsNotNull(FindNode<AINode_KeepDistance>(), "Falta el repliegue al otro lado del mostrador.");
            Assert.IsNotNull(FindNode<AINode_CashierAudit>(), "Falta el arqueo de caja.");
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
            var gate = FindGateAtPercent(0.5f);

            Assert.IsNotNull(gate, "No hay gate de HP al 50% — el arqueo nunca dispararía.");
            Assert.IsNotNull(gate.Else, "El gate necesita Else (un If sin rama devuelve Failed y aborta el turno).");
            Assert.IsInstanceOf<AINode_Wait>(gate.Else);
        }

        [Test]
        public void AuditGate_RunsBeforeTheAttack()
        {
            int gateIdx = IndexOfGateAtPercent(0.5f);
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
            var gate = FindGateAtPercent(0.5f);
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
        public void Once_WrapsOnlyTheAudit_SoChipsAndColumnKeepRunning()
        {
            var latches = Descendants(_root).OfType<AINode_Once>().ToList();

            Assert.AreEqual(1, latches.Count, "El único one-shot del jefe es el arqueo.");
            Assert.IsNotEmpty(Descendants(latches[0]).OfType<AINode_CashierAudit>().ToList());
            Assert.IsEmpty(Descendants(latches[0]).OfType<AINode_CashierDropChips>().ToList(),
                "Las fichas se sueltan todos los turnos en que le peguen — un Once las latchearía.");
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

            Assert.AreEqual(ThreatShape.Column, column.Shape, "Es una columna, no una fila.");
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
        public void Toll_ChargesTheSheetTen()
        {
            var toll = FindNode<AINode_CashierCounterToll>();

            Assert.AreEqual(CajeroAssetBuilder.CounterTollDamage, toll.Damage,
                "El nodo tiene que salir cableado desde la constante de la ficha, no de su default.");
            Assert.AreEqual(10, toll.Damage,
                "Sin peaje, elegir abertura no cuesta nada y el mostrador es decorado.");
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

            Assert.AreEqual(1, chips.Count, "Una ficha por golpe.");
            Assert.AreEqual(6, chips.MinValue);
            Assert.AreEqual(9, chips.MaxValue);
            Assert.AreEqual(2, chips.MinDistanceFromPlayer, "A 2-3 casillas: agarrarla cuesta el movimiento.");
            Assert.AreEqual(3, chips.MaxDistanceFromPlayer);
            Assert.IsTrue(chips.RequireDamageTaken, "El jefe te paga por lastimarlo, no gratis.");
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
                Assert.AreEqual(6, second.Children.Count, "Re-ejecutar el builder no acumula hijos.");
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

        private AINode_If FindGateAtPercent(float percent)
        {
            return _root.Children.Select(Unwrap).FirstOrDefault(g =>
                g?.Conditions != null && g.Conditions.OfType<PcOwnerHpBelow>()
                    .Any(p => Mathf.Abs(p.Percent - percent) < PercentTolerance));
        }

        private int IndexOfGateAtPercent(float percent)
        {
            var gate = FindGateAtPercent(percent);
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
