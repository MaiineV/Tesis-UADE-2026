using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Entities;
using Rollgeon.Feedback;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>
    /// Cierra el circuito animación ↔ ataque de los seis jefes: que cada entry <c>anim.boss.*</c>
    /// nombre un trigger que el rig de ESE jefe declara, y que ningún ataque se resuelva sin gesto.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Los dos bugs que atrapa son mudos.</b> Un <c>AnimTrigger</c> con un nombre que el
    /// <c>AnimatorController</c> no tiene no tira nada: <c>Animator.SetTrigger</c> de un parámetro
    /// inexistente sólo loguea un warning que se pierde entre los cien de una pelea. Y un
    /// <see cref="AINode_ExecuteTelegraph"/> sin <c>WindupFeedbackId</c> tampoco falla — el daño
    /// sale igual, el jefe se queda en idle y aparece un número. Los dos se ven sólo mirando al
    /// jefe pelear, que es exactamente lo que nadie hace en cada build.
    /// </para>
    /// <para>
    /// Los jefes visten rigs prestados (el Croupier el del Healer, el Cajero el del General
    /// Director), así que qué triggers tiene cada uno depende de arte y cambia sin avisar. Cuando
    /// arte le autore clips propios a alguno, este test es el que dice qué quedó apuntando a un
    /// trigger que ya no existe.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class BossAnimationWiringTests
    {
        private const string DbPath = "Assets/Rollgeon/Feedback/FeedbackDB.asset";

        /// <summary>Prefijo de los ids de animación de jefe: <c>anim.boss.&lt;slug&gt;.&lt;acción&gt;</c>.</summary>
        private const string AnimPrefix = "anim.boss.";

        /// <summary>El slug del id ↔ el <c>EntityId</c> de la ficha. No coinciden: el id de feedback
        /// usa el nombre de la mesa y el EntityId el del bicho.</summary>
        private static readonly Dictionary<string, string> SlugToEntityId =
            new Dictionary<string, string>
            {
                { "croupier", CroupierAssetBuilder.EntityId },
                { "bandida",  BandidaAssetBuilder.BossEntityId },
                { "cajero",   CajeroAssetBuilder.EntityId },
                { "anotador", AnotadorAssetBuilder.EntityId },
                { "generala", GeneralaAssetBuilder.BossEntityId },
                { "tahur",    TahurAssetBuilder.EntityId },
            };

        // ==================================================================
        // El trigger existe en el rig del jefe
        // ==================================================================

        [Test]
        public void EveryBossAnimEntry_NamesATriggerItsOwnRigDeclares()
        {
            // Arrange
            var db = AssetDatabase.LoadAssetAtPath<FeedbackDBSO>(DbPath);
            Assert.IsNotNull(db, $"No se encontró el FeedbackDB en '{DbPath}'.");

            var problems = new List<string>();
            int checkedEntries = 0;

            // Act
            foreach (var entry in db.Entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.FeedbackId)) continue;
                if (!entry.FeedbackId.StartsWith(AnimPrefix)) continue;

                string slug = SlugOf(entry.FeedbackId);
                if (!SlugToEntityId.TryGetValue(slug, out var entityId))
                {
                    problems.Add($"'{entry.FeedbackId}': '{slug}' no es ninguno de los seis jefes.");
                    continue;
                }

                if (string.IsNullOrEmpty(entry.AnimTrigger))
                {
                    problems.Add($"'{entry.FeedbackId}' es una entry de animación sin AnimTrigger.");
                    continue;
                }

                var controller = ControllerOf(entityId);
                if (controller == null)
                {
                    problems.Add($"'{entityId}' no tiene AnimatorController — " +
                                 $"'{entry.FeedbackId}' no puede sonar.");
                    continue;
                }

                checkedEntries++;
                if (!DeclaresTrigger(controller, entry.AnimTrigger))
                {
                    problems.Add($"'{entry.FeedbackId}' pide el trigger '{entry.AnimTrigger}' pero " +
                                 $"'{controller.name}' declara [{string.Join(", ", TriggerNames(controller))}].");
                }
            }

            // Assert
            Assert.Greater(checkedEntries, 0,
                "No se verificó ninguna entry de animación de jefe — ¿cambió el prefijo " +
                $"'{AnimPrefix}' o se vació el FeedbackDB?");
            Assert.IsEmpty(problems,
                "Hay animaciones de jefe apuntando a triggers que su rig no tiene. Un SetTrigger " +
                "inexistente no falla: el jefe simplemente no hace nada.\n  - " +
                string.Join("\n  - ", problems) +
                "\nCorrer 'Tools → Rollgeon → Bosses → Build Boss Feedback' después de ajustar " +
                "BossFeedbackInstaller.");
        }

        // ==================================================================
        // Ningún ataque se resuelve sin gesto
        // ==================================================================

        /// <summary>
        /// Cada jefe y su árbol. El Croupier no entra: no usa
        /// <see cref="AINode_ExecuteTelegraph"/> — detona con su propio nodo, y ahí la ausencia de
        /// animación es deliberada (explota el paño, no él).
        /// </summary>
        /// <remarks>
        /// Los árboles se arman <b>dentro</b> del test y no acá: NUnit evalúa el source al recolectar,
        /// y una excepción en esa fase no se reporta como un test rojo sino como el fixture entero
        /// desaparecido de la lista.
        /// </remarks>
        private static IEnumerable<TestCaseData> TelegraphCases()
        {
            yield return Case("El Cajero", () => CajeroAssetBuilder.BuildAIRoot(null));
            yield return Case("La Generala", () => GeneralaAssetBuilder.BuildAIRoot(null));
            yield return Case("El Anotador", () => AnotadorAssetBuilder.BuildAIRoot(null));
            yield return Case("La Bandida", () => BandidaAssetBuilder.BuildAIRoot(null, null));
            yield return Case("El Tahúr", () => TahurAssetBuilder.BuildAIRoot());
        }

        private static TestCaseData Case(string bossName, System.Func<object> buildRoot) =>
            new TestCaseData(buildRoot, bossName).SetName(bossName);

        [TestCaseSource(nameof(TelegraphCases))]
        public void EveryTelegraphedAttack_PlaysSomething(System.Func<object> buildRoot, string bossName)
        {
            // Arrange
            var db = AssetDatabase.LoadAssetAtPath<FeedbackDBSO>(DbPath);
            Assert.IsNotNull(db, $"No se encontró el FeedbackDB en '{DbPath}'.");

            // Act — la marca del turno pasado es el ataque que más veces ve el jugador; sin windup
            // se cobra sola, con el jefe parado en idle.
            var executes = Descendants(buildRoot()).OfType<AINode_ExecuteTelegraph>().ToList();

            // Assert
            CollectionAssert.IsNotEmpty(executes, $"{bossName} no cobra ninguna marca.");
            foreach (var node in executes)
            {
                Assert.IsNotEmpty(node.WindupFeedbackId ?? string.Empty,
                    $"{bossName} cobra su marca sin animación: el daño sale y él no se mueve. " +
                    "Setear WindupFeedbackId en su builder.");
                Assert.IsTrue(db.HasFeedback(node.WindupFeedbackId),
                    $"{bossName} pide el feedback '{node.WindupFeedbackId}', que no está en el " +
                    "FeedbackDB. Correr 'Tools → Rollgeon → Bosses → Build Boss Feedback'.");
            }
        }

        [Test]
        public void TheGeneralaRefillsHerTable_WithAGesture()
        {
            // Arrange — los cinco dados aparecían de la nada en el borde de la sala con ella
            // parada en idle: se leía como un evento de la sala y no como algo que hizo el jefe.
            var db = AssetDatabase.LoadAssetAtPath<FeedbackDBSO>(DbPath);
            Assert.IsNotNull(db, $"No se encontró el FeedbackDB en '{DbPath}'.");

            // Act
            var spawn = Descendants(GeneralaAssetBuilder.BuildAIRoot(null))
                .OfType<AINode_SpawnReinforcements>()
                .FirstOrDefault();

            // Assert
            Assert.IsNotNull(spawn, "La Generala no repone la mesa.");
            Assert.AreEqual(BossFeedbackIds.GeneralaSummonAnim, spawn.SpawnFeedbackId,
                "La reposición de la mesa perdió su gesto de invocar.");
            Assert.IsTrue(db.HasFeedback(BossFeedbackIds.GeneralaSummonAnim),
                $"'{BossFeedbackIds.GeneralaSummonAnim}' no está en el FeedbackDB. Correr " +
                "'Tools → Rollgeon → Bosses → Build Boss Feedback'.");
        }

        // ==================================================================
        // Helpers
        // ==================================================================

        /// <summary>"anim.boss.cajero.shot" → "cajero".</summary>
        private static string SlugOf(string feedbackId)
        {
            var rest = feedbackId.Substring(AnimPrefix.Length);
            int dot = rest.IndexOf('.');
            return dot < 0 ? rest : rest.Substring(0, dot);
        }

        /// <summary>
        /// El controller que corre ese jefe, vía su <c>VisualPrefab</c>. Se pasa por el prefab y no
        /// por el path del <c>.controller</c> porque el YAML del prefab miente: el arte va anidado y
        /// sus componentes aparecen <c>stripped</c>.
        /// </summary>
        private static AnimatorController ControllerOf(string entityId)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDataSO"))
            {
                var data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null || data.EntityId != entityId || data.VisualPrefab == null) continue;

                var animator = data.VisualPrefab.GetComponentInChildren<Animator>(true);
                return animator?.runtimeAnimatorController as AnimatorController;
            }
            return null;
        }

        private static bool DeclaresTrigger(AnimatorController controller, string trigger)
        {
            foreach (var p in controller.parameters)
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == trigger) return true;
            return false;
        }

        private static IEnumerable<string> TriggerNames(AnimatorController controller) =>
            controller.parameters
                .Where(p => p.type == AnimatorControllerParameterType.Trigger)
                .Select(p => p.name);

        /// <summary>Tree-walker por reflexión, copiado de <c>CajeroPhaseWiringTests</c> (vive en el
        /// mismo assembly pero como privado del fixture).</summary>
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
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices
                .RuntimeHelpers.GetHashCode(obj);
        }
    }
}
