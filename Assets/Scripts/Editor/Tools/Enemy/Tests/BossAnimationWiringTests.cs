using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Rooms;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Entities;
using Rollgeon.Entities.Visuals;
using Rollgeon.Feedback;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>
    /// Cierra el circuito animación ↔ ataque de los seis jefes. Los dos bugs que atrapa son mudos:
    /// un <c>SetTrigger</c> de un parámetro inexistente sólo loguea un warning que se pierde, y un
    /// <see cref="AINode_ExecuteTelegraph"/> sin <c>WindupFeedbackId</c> cobra igual con el jefe en
    /// idle. Los jefes visten rigs prestados, así que sus triggers cambian sin avisar.
    /// </summary>
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

        /// <summary>Los jefes que hoy tienen rig. El resto conserva sus entries en el FeedbackDB
        /// pero no un prefab contra el cual verificarlas.</summary>
        private static readonly HashSet<string> SlugsInUse =
            new HashSet<string> { "croupier", "cajero", "generala" };

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

                if (!SlugsInUse.Contains(slug)) continue;

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
        /// Cada jefe que cobra un área marcada, y su árbol. Dos no entran, por razones distintas:
        /// el Croupier detona con su propio nodo, y <b>el Cajero no telegrafía nada</b> — es melee
        /// puro de alcance 1, así que no tiene área que avisar. Sus gestos los cubre
        /// <see cref="TheCajeroHitsWithAGesture"/>. Los árboles se arman <b>dentro</b> del test —
        /// una excepción al recolectar el source hace desaparecer el fixture entero en vez de
        /// reportar rojo.
        /// </summary>
        private static IEnumerable<TestCaseData> TelegraphCases()
        {
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

            // Act
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

        /// <summary>
        /// El Cajero no marca áreas, pega. Lo que en los demás cubre el telegraph, acá lo tienen
        /// que cubrir sus dos golpes: si uno pierde el gesto, el daño sale y el jefe no se mueve,
        /// y eso no falla en runtime — simplemente no se ve.
        /// </summary>
        [Test]
        public void TheCajeroHitsWithAGesture()
        {
            // Arrange
            var db = AssetDatabase.LoadAssetAtPath<FeedbackDBSO>(DbPath);
            Assert.IsNotNull(db, $"No se encontró el FeedbackDB en '{DbPath}'.");

            // Act — sus dos golpes son nodos de daño directo, no de marca. El empujón hereda de
            // AINode_RangedShot, así que un solo filtro agarra el mandoble y el empujón.
            var hits = Descendants(CajeroAssetBuilder.BuildAIRoot(null))
                .OfType<AINode_RangedShot>()
                .ToList();

            // Assert
            CollectionAssert.IsNotEmpty(hits,
                "El Cajero se quedó sin golpes: su pelea entera son dos ataques melee que se " +
                "intercalan.");
            foreach (var hit in hits)
            {
                Assert.IsNotEmpty(hit.AnimFeedbackId ?? string.Empty,
                    "Un golpe del Cajero cobra sin animación: el daño sale y él no se mueve.");
                Assert.IsTrue(db.HasFeedback(hit.AnimFeedbackId),
                    $"El Cajero pide el feedback '{hit.AnimFeedbackId}', que no está en el " +
                    "FeedbackDB. Correr 'Tools → Rollgeon → Bosses → Build Boss Feedback'.");
            }
        }

        [Test]
        public void TheGeneralaRefillsHerTable_WithAGesture()
        {
            // Arrange — es el único uso que tiene esa animación del rig (ver BossFeedbackInstaller).
            var db = AssetDatabase.LoadAssetAtPath<FeedbackDBSO>(DbPath);
            Assert.IsNotNull(db, $"No se encontró el FeedbackDB en '{DbPath}'.");

            // Act
            var spawn = Descendants(GeneralaAssetBuilder.BuildAIRoot(null))
                .OfType<AINode_SpawnRoomObjects>()
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
        // Del nodo del árbol a la entry (la mitad que faltaba)
        // ==================================================================

        /// <summary>
        /// Los tres jefes en uso más la Comisión, con el <c>EntityId</c> cuyo rig tiene que poder
        /// tocar sus gestos.
        /// </summary>
        /// <remarks>
        /// La Comisión entra con <b>su propio</b> EntityId y no con el del Cajero: no viste el rig
        /// del jefe.
        /// </remarks>
        private static IEnumerable<TestCaseData> GestureChainCases()
        {
            yield return ChainCase("El Croupier", CroupierAssetBuilder.EntityId,
                () => CroupierAssetBuilder.BuildAIRoot(null));
            yield return ChainCase("El Cajero", CajeroAssetBuilder.EntityId,
                () => CajeroAssetBuilder.BuildAIRoot(null, null));
            yield return ChainCase("La Comisión", CajeroAssetBuilder.CritterEntityId,
                () => CajeroAssetBuilder.BuildCritterAIRoot());
            yield return ChainCase("La Generala", GeneralaAssetBuilder.BossEntityId,
                () => GeneralaAssetBuilder.BuildAIRoot(null));
        }

        private static TestCaseData ChainCase(string bossName, string entityId,
                                              System.Func<object> buildRoot) =>
            new TestCaseData(buildRoot, bossName, entityId).SetName(bossName);

        /// <summary>
        /// Cierra el circuito en la dirección que faltaba:
        /// <b>nodo del árbol → id → entry del DB → trigger → parámetro del rig</b>.
        /// </summary>
        /// <remarks>
        /// <see cref="EveryBossAnimEntry_NamesATriggerItsOwnRigDeclares"/> recorre el circuito desde
        /// la entry, así que una entry perfecta que <b>nadie llama</b> le pasa en verde: un
        /// <c>AnimFeedbackId</c> vacío no es un error en runtime, el nodo degrada a silencio y el
        /// jefe cobra el daño sin mover un dedo.
        /// <para>
        /// Se lee la propiedad <b>resuelta</b> y no el campo autorado: los nodos migrados
        /// (<c>AINode_CashierRangedShot</c>, el mazazo de la Generala) tapan el campo vacío con un
        /// default de subclase, y afirmar sobre el campo daría rojo por un cableado que sí funciona.
        /// </para>
        /// </remarks>
        [TestCaseSource(nameof(GestureChainCases))]
        public void EveryAttackNode_ResolvesAGestureItsRigCanPlay(
            System.Func<object> buildRoot, string bossName, string entityId)
        {
            // Arrange
            var db = AssetDatabase.LoadAssetAtPath<FeedbackDBSO>(DbPath);
            Assert.IsNotNull(db, $"No se encontró el FeedbackDB en '{DbPath}'.");

            var controller = ControllerOf(entityId);
            Assert.IsNotNull(controller,
                $"'{entityId}' no tiene AnimatorController — ningún gesto de {bossName} puede sonar.");

            // Act — cada nodo del árbol que declara un slot de gesto, con el valor que de verdad
            // llega al bus de feedback.
            var slots = new List<KeyValuePair<string, string>>();
            foreach (var node in Descendants(buildRoot()))
            {
                if (!TryReadGestureSlot(node, out var member, out var id)) continue;
                slots.Add(new KeyValuePair<string, string>($"{node.GetType().Name}.{member}", id));
            }

            // Assert
            CollectionAssert.IsNotEmpty(slots,
                $"{bossName} no tiene ni un nodo con slot de gesto. O se le vació el árbol, o " +
                "cambiaron los nombres de los campos de presentación y este test dejó de mirar nada.");

            var problems = new List<string>();
            foreach (var slot in slots)
            {
                if (string.IsNullOrEmpty(slot.Value))
                {
                    problems.Add($"{slot.Key} está vacío: la acción se cobra y el jefe no se mueve.");
                    continue;
                }

                if (!db.TryGetFeedback(slot.Value, out var entry))
                {
                    problems.Add($"{slot.Key} pide '{slot.Value}', que no está en el FeedbackDB.");
                    continue;
                }

                if (string.IsNullOrEmpty(entry.AnimTrigger))
                {
                    problems.Add($"{slot.Key} → '{slot.Value}' es una entry sin AnimTrigger.");
                    continue;
                }

                if (!DeclaresTrigger(controller, entry.AnimTrigger))
                {
                    problems.Add($"{slot.Key} → '{slot.Value}' pide el trigger " +
                                 $"'{entry.AnimTrigger}', pero '{controller.name}' declara " +
                                 $"[{string.Join(", ", TriggerNames(controller))}].");
                }
            }

            Assert.IsEmpty(problems,
                $"{bossName} tiene acciones que se cobran sin gesto. Nada de esto falla en " +
                "runtime — el nodo degrada a silencio y el jefe se ve congelado:\n  - " +
                string.Join("\n  - ", problems) +
                "\nSetear el id en su builder y correr 'Tools → Rollgeon → Bosses → Build Boss " +
                "Feedback' si la entry es nueva.");
        }

        /// <summary>Nombres de slot de gesto, en orden de prioridad.</summary>
        /// <remarks>
        /// <c>ResolvedAnimFeedbackId</c> va primero porque es el valor efectivo cuando el nodo tiene
        /// fallback de subclase; el campo crudo queda como respaldo para los que no lo tienen. Por
        /// nombre y no por tipo a propósito: un nodo de ataque nuevo con un campo llamado igual entra
        /// solo, que es lo que evita que el próximo jefe se cablee mudo sin que nadie se entere.
        /// </remarks>
        private static readonly string[] GestureSlotNames =
        {
            "ResolvedAnimFeedbackId",
            "AnimFeedbackId",
            "WindupFeedbackId",
            "SpawnFeedbackId",
        };

        private static bool TryReadGestureSlot(object node, out string member, out string id)
        {
            member = null;
            id = null;
            if (node == null) return false;

            foreach (var name in GestureSlotNames)
            {
                if (!TryReadStringMember(node, name, out id)) continue;
                member = name;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Sube por la jerarquía a mano: los slots resueltos son <c>protected</c> y los de la
        /// Generala <c>private</c>, y <c>FlattenHierarchy</c> no ve miembros no públicos del base.
        /// </summary>
        private static bool TryReadStringMember(object instance, string name, out string value)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public
                                       | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            for (var type = instance.GetType(); type != null; type = type.BaseType)
            {
                var property = type.GetProperty(name, Flags);
                if (property != null && property.PropertyType == typeof(string)
                    && property.GetGetMethod(true) != null)
                {
                    value = (string)property.GetValue(instance);
                    return true;
                }

                var field = type.GetField(name, Flags);
                if (field != null && field.FieldType == typeof(string))
                {
                    value = (string)field.GetValue(instance);
                    return true;
                }
            }

            value = null;
            return false;
        }

        // ==================================================================
        // El desplazamiento también es una animación
        // ==================================================================

        /// <summary>Bool del Animator que gatea Idle ⇄ caminata. Lo prende <c>EntityPawn</c>.</summary>
        private const string MovementParam = "Movement";

        /// <summary>
        /// Un jefe que se desliza por el piso es el defecto que más se nota, y no lo cubre ningún id
        /// de feedback: el desplazamiento no pasa por el <c>FeedbackDB</c> sino por el bool
        /// <c>Movement</c> que <c>EntityPawn.SetMovementAnim</c> prende al animar el path. Si el
        /// controller no lo declara, el <c>SetBool</c> es un no-op silencioso y el cuerpo se traslada
        /// en T-pose animada de Idle.
        /// </summary>
        /// <remarks>
        /// Los que van en <see cref="EntityPawn.LocomotionStyle.Blink"/> quedan afuera a propósito:
        /// su gesto de desplazamiento <b>es</b> el clip de teletransporte, y quién blinkea lo fija
        /// <c>EnemyLocomotionWiringTests</c>. Acá sólo se mira a los que caminan.
        /// </remarks>
        [Test]
        public void EveryWalkingRigOfAnInUseFight_DeclaresTheMovementBool()
        {
            // Arrange
            var entityIds = new[]
            {
                CroupierAssetBuilder.EntityId,
                CajeroAssetBuilder.EntityId,
                CajeroAssetBuilder.CritterEntityId,
                GeneralaAssetBuilder.BossEntityId,
            };

            // Act
            var sliding = new List<string>();
            foreach (var entityId in entityIds)
            {
                var prefab = VisualPrefabOf(entityId);
                Assert.IsNotNull(prefab, $"'{entityId}' no tiene VisualPrefab.");

                var controller = ControllerOf(entityId);
                Assert.IsNotNull(controller,
                    $"'{entityId}' no tiene AnimatorController — se mueve en T-pose.");

                if (StyleOf(prefab) == EntityPawn.LocomotionStyle.Blink) continue;
                if (DeclaresBool(controller, MovementParam)) continue;

                sliding.Add(entityId);
            }

            // Assert
            CollectionAssert.IsEmpty(
                sliding,
                "Alguien quedó deslizándose sin ciclo de caminata. Un rig que no declara el bool " +
                $"'{MovementParam}' hace que EntityPawn traslade el cuerpo con el Animator en Idle: " +
                "no tira ningún error, sólo se ve mal.");
        }

        private static GameObject VisualPrefabOf(string entityId)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDataSO"))
            {
                var data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (data != null && data.EntityId == entityId) return data.VisualPrefab;
            }
            return null;
        }

        /// <summary>Por <c>SerializedObject</c> porque el campo es privado serializado.</summary>
        private static EntityPawn.LocomotionStyle StyleOf(GameObject prefab)
        {
            var pawn = prefab.GetComponentInChildren<EntityPawn>(true);
            Assert.IsNotNull(pawn, $"'{prefab.name}' no tiene EntityPawn.");

            var prop = new SerializedObject(pawn).FindProperty("_locomotion");
            Assert.IsNotNull(prop, "EntityPawn no expone '_locomotion'.");
            return (EntityPawn.LocomotionStyle)prop.enumValueIndex;
        }

        private static bool DeclaresBool(AnimatorController controller, string param)
        {
            foreach (var p in controller.parameters)
                if (p.type == AnimatorControllerParameterType.Bool && p.name == param) return true;
            return false;
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

        /// <summary>Por el <c>VisualPrefab</c> y no por el path del <c>.controller</c>: el YAML del
        /// prefab miente, el arte va anidado y sus componentes aparecen <c>stripped</c>.</summary>
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

        /// <summary>Tree-walker por reflexión, sin descender en assets referenciados.</summary>
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
