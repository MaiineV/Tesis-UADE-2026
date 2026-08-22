using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Editor.Tools.Enemy;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Entities;
using Rollgeon.Entities.Visuals;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>
    /// Fija que cada prefab se desplace como dice su rig: los que tienen clip de teletransporte van
    /// en <see cref="EntityPawn.LocomotionStyle.Blink"/> y el resto en <c>Walk</c>.
    /// </summary>
    /// <remarks>
    /// El bug que esto atrapa es silencioso: con el lerp de siempre, un rig cuyo clip lo hace
    /// desvanecerse se desliza suave por el piso mientras la animación dice que desapareció. No
    /// tira ningún error — sólo se ve mal, que es la clase de cosa que sobrevive meses.
    /// <para>
    /// Lo escribe <c>Rollgeon → Enemies → Apply Teleport Locomotion</c>; este test es lo que
    /// avisa cuando alguien reconstruye un prefab y se lleva puesto el flag.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class EnemyLocomotionWiringTests
    {
        /// <summary>Prefab visual de cada ficha, deduplicado (varias fichas comparten rig).</summary>
        private static IEnumerable<GameObject> VisualPrefabs()
        {
            var seen = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDataSO"))
            {
                var data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (data?.VisualPrefab == null) continue;

                var path = AssetDatabase.GetAssetPath(data.VisualPrefab);
                if (string.IsNullOrEmpty(path) || !seen.Add(path)) continue;

                yield return data.VisualPrefab;
            }
        }

        private static EntityPawn.LocomotionStyle StyleOf(GameObject prefab)
        {
            var pawn = prefab.GetComponentInChildren<EntityPawn>(true);
            Assert.IsNotNull(pawn, $"'{prefab.name}' no tiene EntityPawn.");

            var so = new SerializedObject(pawn);
            var prop = so.FindProperty("_locomotion");
            Assert.IsNotNull(prop, "EntityPawn no expone '_locomotion'.");
            return (EntityPawn.LocomotionStyle)prop.enumValueIndex;
        }

        private static bool Teleports(GameObject prefab)
        {
            var animator = prefab.GetComponentInChildren<Animator>(true);
            return animator != null
                   && EnemyLocomotionInstaller.HasTeleportClip(animator.runtimeAnimatorController);
        }

        [Test]
        public void EveryRigWithATeleportClip_Blinks()
        {
            // Arrange / Act / Assert — la regla base: si el clip de movimiento es un TP, el cuerpo
            // tiene que saltar. Los parches de ForcedBlinkEntityIds pueden agregar más Blinks, así
            // que acá se afirma una sola dirección; la otra la cubre la lista de abajo.
            foreach (var prefab in VisualPrefabs())
            {
                if (!Teleports(prefab)) continue;

                Assert.AreEqual(EntityPawn.LocomotionStyle.Blink, StyleOf(prefab),
                    $"'{prefab.name}': su clip de movimiento es un teletransporte pero se desliza. " +
                    "Correr 'Rollgeon → Enemies → Apply Teleport Locomotion'.");
            }
        }

        [Test]
        public void TheTeleportingRigs_AreTheOnesWeExpect()
        {
            // Arrange — el test de arriba es una tautología si nadie fija QUIÉNES se teletransportan:
            // pasaría igual con el bestiario entero en Walk. Esta es la lista real.
            var blinking = new List<string>();
            foreach (var prefab in VisualPrefabs())
                if (StyleOf(prefab) == EntityPawn.LocomotionStyle.Blink) blinking.Add(prefab.name);

            // Assert — el Croupier entra porque viste el rig del Healer. El Cajero no:
            // MechaBoss_Animated trae ciclo de caminata (AnimCon_Mecha → Movement). La Comisión sí
            // entra, pero por el parche de ForcedBlinkEntityIds: GeneralDirector_Animated no tiene
            // ciclo de caminata propio.
            CollectionAssert.AreEquivalent(
                new[] { "PF_Boss_Croupier", "SunkedGrand", "Healer", "PF_Min_Comision" },
                blinking,
                "Cambió quién se teletransporta. Si es a propósito, actualizá esta lista; si no, " +
                "alguien reconstruyó un prefab y se llevó puesto el flag.");
        }

        [Test]
        public void HasTeleportClip_IsNullSafe()
        {
            // Arrange / Act / Assert — los prefabs sin Animator (props, jefes viejos sin rig) pasan
            // por acá en cada corrida del instalador.
            Assert.IsFalse(EnemyLocomotionInstaller.HasTeleportClip(null));
        }

        [Test]
        public void ForcedBlinkRoster_MatchesTheComisionEntityId()
        {
            // Arrange / Act / Assert — el literal es a propósito (el instalador es genérico y no
            // conoce los builders de cada jefe), y este test es lo único que lo mantiene sincronizado
            // con la ficha real: el mismo idiom que BossPortraitLibraryTests usa para
            // CajeroAssetBuilder.PortraitTexturePath.
            // Assert.Contains pide un ICollection no genérico y HashSet<string> no lo implementa;
            // CollectionAssert.Contains sí acepta el IEnumerable que HashSet<string> sí es.
            CollectionAssert.Contains(EnemyLocomotionInstaller.ForcedBlinkEntityIds,
                CajeroAssetBuilder.CritterEntityId,
                $"El literal de ForcedBlinkEntityIds dejó de coincidir con " +
                $"'{CajeroAssetBuilder.CritterEntityId}': la Comisión volvería a caminar en pose de " +
                "Idle sobre GeneralDirector_Animated.");
        }
    }
}
