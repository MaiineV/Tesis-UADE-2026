using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Entities;
using Rollgeon.PreConditions.Concretes;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Carga el asset REAL del boss de piso 1 y valida sus fases por HP: lluvia (vía la abstracción
    /// de hazards) y refuerzos.
    /// </summary>
    [TestFixture]
    public class SunkenGrandPhaseWiringTests
    {
        private const string BossAssetPath = "Assets/Rollgeon/Enemies/ED_Boss_Sunken_Grand.asset";
        private const float PercentTolerance = 0.0001f;

        private AINode_Sequence _root;

        [SetUp]
        public void SetUp()
        {
            var boss = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(BossAssetPath);
            Assert.IsNotNull(boss, $"No se pudo cargar el asset del boss en '{BossAssetPath}'.");

            _root = boss.AIRoot as AINode_Sequence;
            Assert.IsNotNull(_root, "El AIRoot del boss debería ser un AINode_Sequence.");
        }

        [Test]
        public void Boss_ActivatesRainHazard_At85Percent_ThroughTheGenericNode()
        {
            // Act
            var gate = FindGateAtPercent(0.85f);

            // Assert
            Assert.IsNotNull(gate, "No hay gate de HP a 85% en el árbol del boss.");
            var activate = Descendants(gate.Then).OfType<AINode_ActivateHazard>().FirstOrDefault();
            Assert.IsNotNull(activate,
                "El gate de 85% no activa un hazard vía AINode_ActivateHazard.");
            Assert.IsNotNull(activate.Hazard,
                "AINode_ActivateHazard no tiene HazardDefinitionSO asignada — la lluvia no caería.");
        }

        [Test]
        public void Boss_SpawnsReinforcements_At65Percent_WithoutOnce_SoTheWaveRespawns()
        {
            // Act
            var gate = FindGateAtPercent(0.65f);

            // Assert
            Assert.IsNotNull(gate, "No hay gate de HP a 65% en el árbol del boss.");
            var spawn = Descendants(gate.Then).OfType<AINode_SpawnReinforcements>().FirstOrDefault();
            Assert.IsNotNull(spawn, "El gate de 65% no tiene un AINode_SpawnReinforcements.");
            Assert.IsNotNull(spawn.EnemyToSpawn,
                "SpawnReinforcements.EnemyToSpawn está en null — no invocaría nada.");
            Assert.AreEqual(2, spawn.Count, "El boss debería invocar 2 refuerzos por oleada.");

            Assert.AreEqual(2, spawn.RespawnDelayTurns,
                "Tras aniquilar la oleada el boss debe esperar 2 turnos antes de la siguiente.");

            // El nodo se auto-gatea y necesita tickear cada turno para respawnear la oleada
            // siguiente; envuelto en Once quedaría latcheado tras la primera y no habría loop.
            Assert.IsFalse(gate.Then is AINode_Once,
                "El gate de refuerzos quedó envuelto en AINode_Once — rompe el respawn loop.");
        }

        /// <summary>
        /// En Play mode el árbol corre por <c>TickCoroutine</c> y <see cref="AINode_Sequence"/>
        /// aborta el turno con <see cref="AIResult.Failed"/>.
        /// <see cref="AINode_SpawnReinforcements"/> falla cuando no hay tiles de borde libres y
        /// <see cref="AINode_ActivateHazard"/> si su servicio no está registrado — sin aislar, ese
        /// fallo le cancela al boss el ataque y el candado, que van después en la secuencia.
        /// </summary>
        [Test]
        public void Boss_PhaseGateFailure_CannotAbortTheTurn()
        {
            // Arrange — los nodos de fase cuyo fallo hay que absorber.
            var riskyGates = _root.Children
                .Select(Unwrap)
                .Where(g => g != null && Descendants(g.Then).Any(n =>
                    n is AINode_ActivateHazard || n is AINode_SpawnReinforcements))
                .ToList();

            Assert.IsNotEmpty(riskyGates, "No se encontraron los gates de lluvia/refuerzos.");

            // Act + Assert — cada uno va dentro de un Selector con fallback que siempre sucede,
            // así el Selector devuelve Succeeded y la secuencia del turno continúa.
            foreach (var gate in riskyGates)
            {
                var wrapper = _root.Children.OfType<AINode_Selector>()
                    .FirstOrDefault(s => s.Children != null && s.Children.Contains(gate));

                Assert.IsNotNull(wrapper,
                    "Un gate de lluvia/refuerzos está suelto en la secuencia raíz: si su acción " +
                    "devuelve Failed, el boss pierde el ataque y el candado de ese turno. " +
                    "Envolverlo en Selector[gate, Wait] (el idiom que ya usa el resto del árbol).");
                Assert.IsTrue(wrapper.Children.Any(c => c is AINode_Wait),
                    "El Selector que envuelve al gate no tiene un AINode_Wait de fallback — " +
                    "sin él el Selector devuelve Failed y aborta el turno igual.");
            }
        }

        /// <summary>
        /// El boss bloquea siempre 1 dado, sin escalada por HP.
        /// </summary>
        [Test]
        public void Boss_LocksExactlyOneDie_WithNoHpEscalation()
        {
            // Arrange + Act
            var locks = Descendants(_root).OfType<AINode_RotateBlock>().ToList();

            // Assert
            Assert.AreEqual(1, locks.Count,
                "Debe haber un único AINode_RotateBlock — dos implican la escalada por HP que " +
                "bloqueaba un segundo dado a vida baja.");
            Assert.AreEqual(AINode_RotateBlock.BlockTarget.Dice, locks[0].Target);
            Assert.AreEqual(1, locks[0].Count, "El candado del boss bloquea 1 solo dado.");
        }

        [Test]
        public void Boss_TicksHazardPhases_BeforeTheAttack()
        {
            // Arrange — en el path NO-coroutine (AINode_Sequence.Tick, el que corren los tests y
            // cualquier simulación fuera de Play mode) un Running sí aborta la secuencia, y el
            // ataque telegrafiado devuelve Running. Ubicar las fases antes del ataque las hace
            // tickear en ambos paths.
            int attackIdx = _root.Children.FindIndex(c =>
                Descendants(c).Any(n => n is AINode_Behavior || n is AINode_TelegraphMark));
            int rainIdx = IndexOfGateAtPercent(0.85f);
            int reinfIdx = IndexOfGateAtPercent(0.65f);

            // Assert
            Assert.Greater(attackIdx, -1, "No se encontró el nodo de ataque del boss.");
            Assert.Greater(attackIdx, rainIdx, "El gate de lluvia quedó después del ataque.");
            Assert.Greater(attackIdx, reinfIdx, "El gate de refuerzos quedó después del ataque.");
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        /// <summary>Devuelve el <see cref="AINode_If"/> de un hijo del Sequence raíz, ya venga
        /// suelto o envuelto en el <see cref="AINode_Selector"/> de aislamiento de fallos.</summary>
        private static AINode_If Unwrap(AIDecisionNode child)
        {
            if (child is AINode_If direct) return direct;
            if (child is AINode_Selector sel && sel.Children != null)
                return sel.Children.OfType<AINode_If>().FirstOrDefault();
            return null;
        }

        /// <summary>Gate de fase por su umbral de HP, sin depender del orden ni del envoltorio.</summary>
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

        /// <summary>Tree-walker por reflexión: todo lo alcanzable desde <paramref name="root"/>,
        /// sin descender en <see cref="UnityEngine.Object"/> (no arrastra assets referenciados).</summary>
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
