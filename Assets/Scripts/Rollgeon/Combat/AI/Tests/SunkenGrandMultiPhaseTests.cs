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
    /// Integration test que carga el asset REAL del boss de piso-1
    /// (<c>ED_Boss_Sunken_Grand</c>) y valida que el AI tree multi-fase quedó bien
    /// wireado. EditMode (no PlayMode) a propósito: los nodos son C# plano y el asset
    /// deserializa vía Odin al hacer <see cref="AssetDatabase.LoadAssetAtPath"/>.
    /// </summary>
    /// <remarks>
    /// Valida el patrón del boss de PR #46: pool de ataque <see cref="AINode_Alternate"/> A/B
    /// (área + slash) con telegraph→golpe (<see cref="AINode_TelegraphMark"/> /
    /// <see cref="AINode_ExecuteTelegraph"/>) y chase (<see cref="AINode_Move"/>), más las fases
    /// por HP: lluvia 70%, refuerzos 50%, speed-buff 10%.
    /// </remarks>
    [TestFixture]
    public class SunkenGrandMultiPhaseTests
    {
        private const string BossAssetPath = "Assets/Rollgeon/Enemies/ED_Boss_Sunken_Grand.asset";

        private const float PercentTolerance = 0.0001f;

        private EnemyDataSO _boss;
        private AIDecisionNode _root;

        [SetUp]
        public void SetUp()
        {
            _boss = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(BossAssetPath);
            Assert.IsNotNull(_boss,
                $"No se pudo cargar el asset del boss en '{BossAssetPath}'. " +
                "¿Se movió o renombró? El test necesita el asset real.");

            _root = _boss.AIRoot;
            Assert.IsNotNull(_root,
                "El boss no tiene AIRoot. ¿Odin no deserializó el árbol o el asset quedó vacío?");
        }

        // -----------------------------------------------------------------
        // Estructura raíz
        // -----------------------------------------------------------------

        [Test]
        public void Boss_AIRoot_IsSequence()
        {
            // Arrange — asset cargado en SetUp.
            // Act
            var seq = _root as AINode_Sequence;

            // Assert
            Assert.IsNotNull(seq, $"AIRoot debería ser un AINode_Sequence, pero es {_root.GetType().Name}.");
            Assert.IsNotNull(seq.Children, "El Sequence raíz no tiene lista de Children.");
            Assert.Greater(seq.Children.Count, 0, "El Sequence raíz está vacío.");
        }

        // -----------------------------------------------------------------
        // Gate de lluvia @ 85%
        // -----------------------------------------------------------------

        [Test]
        public void Boss_HasRainGate_At85Percent()
        {
            // Arrange
            var allNodes = CollectAllNodes(_root);

            // Act — la acción existe, y está gateada por un If con PcOwnerHpBelow ~= 0.85.
            var rain = allNodes.OfType<AINode_ActivateRainHazard>().FirstOrDefault();
            var gate = FindGatingIf<AINode_ActivateRainHazard>(_root);

            // Assert
            Assert.IsNotNull(rain, "No se encontró ningún AINode_ActivateRainHazard en el árbol del boss.");
            Assert.IsNotNull(gate, "El AINode_ActivateRainHazard no está bajo el Then de ningún AINode_If.");
            AssertGatedAtPercent(gate, 0.85f, "rain hazard");
        }

        // -----------------------------------------------------------------
        // Gate de refuerzos @ 65% (respawn loop: sin AINode_Once)
        // -----------------------------------------------------------------

        [Test]
        public void Boss_HasReinforcementsGate_At65Percent_WithEnemyAndCount()
        {
            // Arrange
            var allNodes = CollectAllNodes(_root);

            // Act
            var spawn = allNodes.OfType<AINode_SpawnReinforcements>().FirstOrDefault();
            var gate = FindGatingIf<AINode_SpawnReinforcements>(_root);

            // Assert
            Assert.IsNotNull(spawn, "No se encontró ningún AINode_SpawnReinforcements en el árbol del boss.");
            Assert.IsNotNull(spawn.EnemyToSpawn, "SpawnReinforcements.EnemyToSpawn está en null (debería ser ED_RangedEnemy).");
            Assert.AreEqual(2, spawn.Count, "SpawnReinforcements.Count debería ser 2.");

            Assert.IsNotNull(gate, "El AINode_SpawnReinforcements no está bajo el Then de ningún AINode_If.");
            AssertGatedAtPercent(gate, 0.65f, "reinforcements");

            // El gate NO debe estar envuelto en AINode_Once (si no, no habría respawn loop).
            Assert.IsFalse(gate.Then is AINode_Once,
                "El gate de refuerzos quedó con AINode_Once — rompe el respawn loop; el nodo debe tickear cada turno.");
        }

        // -----------------------------------------------------------------
        // Pool de ataque: Alternate A/B con telegraph y chase (el patrón de #46)
        // -----------------------------------------------------------------

        [Test]
        public void Boss_HasAlternatingAttackPool_WithTelegraphAndChase()
        {
            // Arrange
            var allNodes = CollectAllNodes(_root);

            // Act
            bool hasAlternate = allNodes.OfType<AINode_Alternate>().Any();
            int telegraphMarks = allNodes.OfType<AINode_TelegraphMark>().Count();
            int executes = allNodes.OfType<AINode_ExecuteTelegraph>().Count();
            int chases = allNodes.OfType<AINode_Move>().Count();

            // Assert — rotación A/B de ataques (área + slash).
            Assert.IsTrue(hasAlternate,
                "Falta el AINode_Alternate: el boss no tiene el pool de ataque rotativo A/B.");

            // Assert — cada pata: telegraph (aviso→golpe) + chase.
            Assert.AreEqual(2, telegraphMarks,
                "Deberían existir 2 AINode_TelegraphMark (un ataque de área y uno de slash).");
            Assert.AreEqual(2, executes,
                "Deberían existir 2 AINode_ExecuteTelegraph (resuelven el golpe telegrafiado).");
            Assert.AreEqual(2, chases,
                "Deberían existir 2 AINode_Move (el chase de cada pata).");
        }

        // -----------------------------------------------------------------
        // Fase de buff de velocidad @ 30%
        // -----------------------------------------------------------------

        [Test]
        public void Boss_HasSpeedPhase_At30Percent()
        {
            // Arrange
            var speedPhase = CollectAllNodes(_root).OfType<AINode_ApplyStatModifier>().FirstOrDefault();
            var speedGate = FindGatingIf<AINode_ApplyStatModifier>(_root);

            // Assert
            Assert.IsNotNull(speedPhase,
                "Falta el AINode_ApplyStatModifier (fase de speed-buff a vida baja).");
            Assert.IsNotNull(speedGate,
                "El AINode_ApplyStatModifier no está gateado por ningún AINode_If.");
            AssertGatedAtPercent(speedGate, 0.30f, "speed phase");
        }

        // -----------------------------------------------------------------
        // Candado de dado: cada turno del boss, acotado a su pelea (no global, no gateado por HP)
        // -----------------------------------------------------------------

        [Test]
        public void Boss_LocksADie_EveryTurn_NotGatedByHp()
        {
            // Arrange
            var seq = _root as AINode_Sequence;
            Assert.IsNotNull(seq, "AIRoot debería ser un AINode_Sequence.");

            // Act — el candado vive como HIJO DIRECTO del Sequence raíz (corre cada turno del
            // boss). Va primero porque el Sequence corta en el primer hijo Running/Failed (un
            // ataque telegrafiado), así que en última posición se saltearía en turnos de ataque.
            var directRotate = seq.Children.OfType<AINode_RotateBlock>().FirstOrDefault();

            // Assert
            Assert.IsNotNull(directRotate,
                "El boss no tiene un AINode_RotateBlock como hijo directo del Sequence raíz. " +
                "El candado debe correr cada turno del boss, no venir del pasivo global.");
            Assert.AreEqual(AINode_RotateBlock.BlockTarget.Dice, directRotate.Target,
                "El candado del boss de piso 1 bloquea DADOS (Target=Dice), no combos.");
            Assert.IsNull(FindGatingIf<AINode_RotateBlock>(_root),
                "El RotateBlock quedó bajo el Then de un AINode_If — eso lo gatea por HP y " +
                "rompe el 'cada turno'. Debe ser hijo directo del Sequence raíz.");
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        /// <summary>
        /// Devuelve el primer <see cref="AINode_If"/> cuyo subárbol <c>Then</c> contiene un
        /// nodo del tipo <typeparamref name="T"/>. Modela el patrón
        /// <c>If(PcOwnerHpBelow) -> Once(...) -> T</c> usado en todas las fases del boss.
        /// </summary>
        private static AINode_If FindGatingIf<T>(AIDecisionNode root)
        {
            foreach (var ifNode in CollectAllNodes(root).OfType<AINode_If>())
            {
                if (ifNode.Then == null) continue;
                if (CollectAllNodes(ifNode.Then).OfType<T>().Any())
                    return ifNode;
            }
            return null;
        }

        /// <summary>
        /// Afirma que <paramref name="gate"/> tiene entre sus <c>Conditions</c> una
        /// <see cref="PcOwnerHpBelow"/> con <c>Percent</c> ~= <paramref name="expectedPercent"/>.
        /// </summary>
        private static void AssertGatedAtPercent(AINode_If gate, float expectedPercent, string label)
        {
            Assert.IsNotNull(gate.Conditions, $"El If que gatea '{label}' no tiene Conditions.");
            var hp = gate.Conditions.OfType<PcOwnerHpBelow>().FirstOrDefault();
            Assert.IsNotNull(hp,
                $"El If que gatea '{label}' no tiene una PcOwnerHpBelow entre sus Conditions.");
            Assert.AreEqual(expectedPercent, hp.Percent, PercentTolerance,
                $"El gate de '{label}' debería dispararse a HP < {expectedPercent:P0}, " +
                $"pero está en {hp.Percent:P0}.");
        }

        /// <summary>
        /// Tree-walker por reflexión: recolecta todos los objetos alcanzables desde
        /// <paramref name="root"/>. Recorre fields public + non-public de instancia, recursa en
        /// <see cref="IEnumerable"/> y en objetos/structs del namespace <c>Rollgeon</c>, guarda
        /// un set de visitados (por referencia) para cortar ciclos y NO desciende en
        /// <see cref="UnityEngine.Object"/> (para no arrastrar assets referenciados).
        /// </summary>
        private static List<object> CollectAllNodes(object root)
        {
            var all = new List<object>();
            var visited = new HashSet<object>(RefComparer.Instance);

            void Walk(object o)
            {
                if (o == null) return;
                if (o is string) return;
                if (o is UnityEngine.Object) return; // no descender en assets referenciados.

                var type = o.GetType();
                if (type.IsPrimitive || type.IsEnum) return;

                // Guard de ciclos solo para reference types (los structs no cyclan por sí mismos).
                if (!type.IsValueType)
                {
                    if (!visited.Add(o)) return;
                }

                all.Add(o);

                if (o is IEnumerable enumerable)
                {
                    foreach (var item in enumerable) Walk(item);
                    return;
                }

                // Solo descendemos en tipos del dominio; evita vagar por tipos de System/Unity.
                var ns = type.Namespace ?? string.Empty;
                if (!ns.StartsWith("Rollgeon")) return;

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
