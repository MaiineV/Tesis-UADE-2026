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
    /// Carga el asset REAL del boss de piso 1 y valida las fases por HP que se le re-wirearon
    /// después de sincronizar con develop: lluvia (vía la abstracción de hazards) y refuerzos.
    /// </summary>
    /// <remarks>
    /// Cubre solo ese wiring — el resto del árbol (ataque anim-synced, KeepDistance, candado) es
    /// diseño de develop y se deja libre de aserciones para no frenar su iteración. Un test que
    /// falla acá significa que las fases se perdieron en un merge, que es exactamente el accidente
    /// que ya ocurrió una vez.
    /// </remarks>
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
            // Act — la lluvia entra por AINode_ActivateHazard (la abstracción), no por el shim viejo.
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

            // El nodo se auto-gatea y necesita tickear cada turno para respawnear la oleada
            // siguiente; envuelto en Once quedaría latcheado tras la primera y no habría loop.
            Assert.IsFalse(gate.Then is AINode_Once,
                "El gate de refuerzos quedó envuelto en AINode_Once — rompe el respawn loop.");
        }

        [Test]
        public void Boss_TicksHazardPhases_BeforeTheAttack_SoTheyAreNotSkipped()
        {
            // Arrange — el Sequence corta en el primer hijo que devuelve Running/Failed, y el
            // ataque telegrafiado devuelve Running. Las fases deben ir ANTES para tickear siempre.
            int attackIdx = _root.Children.FindIndex(c =>
                c is AINode_Selector s && s.Children != null
                && Descendants(s).Any(n => n is AINode_Behavior || n is AINode_TelegraphMark));
            int rainIdx = _root.Children.IndexOf(FindGateAtPercent(0.85f));
            int reinfIdx = _root.Children.IndexOf(FindGateAtPercent(0.65f));

            // Assert
            Assert.Greater(attackIdx, -1, "No se encontró el selector de ataque del boss.");
            Assert.Greater(attackIdx, rainIdx,
                "El gate de lluvia quedó después del ataque — se saltearía en turnos de ataque.");
            Assert.Greater(attackIdx, reinfIdx,
                "El gate de refuerzos quedó después del ataque — se saltearía en turnos de ataque.");
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        /// <summary>Hijo directo del Sequence raíz gateado por <see cref="PcOwnerHpBelow"/> a
        /// <paramref name="percent"/>. Identifica una fase por su umbral, sin depender del orden.</summary>
        private AINode_If FindGateAtPercent(float percent)
        {
            return _root.Children.OfType<AINode_If>().FirstOrDefault(i =>
                i.Conditions != null && i.Conditions.OfType<PcOwnerHpBelow>()
                    .Any(p => Mathf.Abs(p.Percent - percent) < PercentTolerance));
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
