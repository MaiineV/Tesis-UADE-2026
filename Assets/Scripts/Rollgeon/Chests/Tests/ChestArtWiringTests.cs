using NUnit.Framework;
using Rollgeon.Entities.Visuals;
using Rollgeon.Feedback;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Chests.Tests
{
    /// <summary>
    /// Tests de integridad de assets: los prefabs de arte del cofre/mimic deben
    /// quedar cableados para gameplay (lo hace 'Rollgeon/Chests/4 - Setup Art
    /// Prefabs'). Si fallan, correr ese menú — atrapan la regresión de swapear
    /// ChestConfig a un prefab pelado del artista (sin pawn/binding/HP bar).
    /// </summary>
    [TestFixture]
    public class ChestArtWiringTests
    {
        private const string ConfigPath = "Assets/Rollgeon/Chests/ChestConfig.asset";
        private const string MimicAttackClipPath =
            "Assets/Art/3D/Animations/Enemies/ChestMimic/Anim_ChestMimic_Attack.anim";

        private static ChestConfigSO LoadConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<ChestConfigSO>(ConfigPath);
            Assert.IsNotNull(config, $"No se encontró {ConfigPath}.");
            return config;
        }

        private static void AssertPawnWiring(GameObject prefab, string label)
        {
            Assert.IsNotNull(prefab, $"{label} sin prefab asignado.");

            var pawn = prefab.GetComponent<EntityPawn>();
            Assert.IsNotNull(pawn, $"{label} sin EntityPawn en el root.");

            Assert.IsNotNull(prefab.GetComponent<PawnRegistryBinding>(),
                $"{label} sin PawnRegistryBinding — FeedbackManager no resuelve el pawn (anims/VFX mudos).");

            Assert.IsNotNull(prefab.GetComponentInChildren<WorldSpaceHealthBar>(true),
                $"{label} sin WorldSpaceHealthBar.");

            var so = new SerializedObject(pawn);
            Assert.IsNotNull(so.FindProperty("_healthBar").objectReferenceValue,
                $"{label}: EntityPawn._healthBar sin wirear.");
        }

        [Test]
        public void ChestPrefab_HasGameplayWiring()
        {
            // Arrange
            var config = LoadConfig();

            // Act + Assert
            AssertPawnWiring(config.ChestPrefab, "ChestConfig.ChestPrefab");
        }

        [Test]
        public void TierPrefabOverrides_WhenAssigned_HaveGameplayWiring()
        {
            // Arrange
            var config = LoadConfig();

            // Act + Assert — hoy no hay overrides configurados; el test es la red
            // para cuando se asigne un prefab por tier sin pasar por Setup Art Prefabs.
            foreach (var tier in config.Tiers)
            {
                if (tier?.ChestPrefabOverride == null) continue;
                AssertPawnWiring(tier.ChestPrefabOverride, $"Tiers[{tier.Tier}].ChestPrefabOverride");
            }
        }

        [Test]
        public void MimicVisualPrefab_HasGameplayWiring()
        {
            // Arrange
            var config = LoadConfig();
            Assert.IsNotNull(config.MimicEnemy, "ChestConfig.MimicEnemy sin asignar.");

            // Act + Assert
            AssertPawnWiring(config.MimicEnemy.VisualPrefab, "MimicEnemy.VisualPrefab");
        }

        [Test]
        public void MimicVisualPrefab_HasAnimationFeedbackEvent_OnAnimator()
        {
            // Arrange
            var config = LoadConfig();
            var prefab = config.MimicEnemy != null ? config.MimicEnemy.VisualPrefab : null;
            Assert.IsNotNull(prefab, "MimicEnemy.VisualPrefab sin asignar.");

            // Act
            var animator = prefab.GetComponentInChildren<Animator>(true);

            // Assert — el receptor de Animation Events vive junto al Animator.
            Assert.IsNotNull(animator, "MimicEnemy.VisualPrefab sin Animator.");
            Assert.IsNotNull(animator.GetComponent<AnimationFeedbackEvent>(),
                "Animator del mimic sin AnimationFeedbackEvent — el evento 'hit' del clip no llega al feedback.");
        }

        [Test]
        public void MimicAttackClip_HasHitAnimationEvent()
        {
            // Arrange
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(MimicAttackClipPath);
            Assert.IsNotNull(clip, $"No se encontró {MimicAttackClipPath}.");

            // Act
            bool hasHit = false;
            foreach (var evt in AnimationUtility.GetAnimationEvents(clip))
            {
                if (evt.functionName == "PushFeedbackEvent" && evt.stringParameter == "hit")
                    hasHit = true;
            }

            // Assert — sin 'hit' el step del ataque melee termina por timeout, no en el impacto.
            Assert.IsTrue(hasHit, "Anim_ChestMimic_Attack sin Animation Event PushFeedbackEvent('hit').");
        }
    }
}
