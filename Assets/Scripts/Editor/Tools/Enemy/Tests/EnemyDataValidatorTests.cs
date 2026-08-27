using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combos;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Entities;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Entities.Bosses;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    [TestFixture]
    public class EnemyDataValidatorTests
    {
        readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        T New<T>() where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            _spawned.Add(so);
            return so;
        }

        /// <summary>Ficha "sana": todo lo obligatorio cargado, árbol mínimo sin avisos.</summary>
        EnemyDataSO Healthy(string id = "enemy.test")
        {
            var so = New<EnemyDataSO>();
            so.EntityId = id;
            so.DisplayName = "Test";
            so.VisualPrefab = new GameObject("pf");
            _spawned.Add(so.VisualPrefab);
            so.Portrait = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.zero);
            _spawned.Add(so.Portrait);
            so.AIRoot = new AINode_Move();
            return so;
        }

        static List<EnemyIssue> Validate(EnemyDataSO so, params EnemyDataSO[] others)
        {
            var all = new List<EnemyDataSO> { so };
            all.AddRange(others);
            return EnemyDataValidator.Validate(so, all, null);
        }

        static bool Has(List<EnemyIssue> issues, EnemyIssueSeverity sev, string fragment)
            => issues.Exists(i => i.Severity == sev && i.Message.Contains(fragment));

        static AINode_Behavior BehaviorWith(IEffect effect)
        {
            var group = new EffectData();
            group.Effects.Add(effect);
            var behavior = new EnemyActionBehavior();
            behavior.Effects.Add(group);
            return new AINode_Behavior { Behavior = behavior };
        }

        [Test]
        public void Validate_HealthySheet_NoIssues()
        {
            var issues = Validate(Healthy());
            Assert.IsEmpty(issues, string.Join(" | ", issues.ConvertAll(i => i.ToString())));
        }

        [Test]
        public void Validate_EmptyEntityId_ReportsError()
        {
            var so = Healthy(id: "");
            Assert.IsTrue(Has(Validate(so), EnemyIssueSeverity.Error, "EntityId vacío"));
        }

        [Test]
        public void Validate_DuplicateEntityId_ReportsErrorNamingOtherAsset()
        {
            var a = Healthy("dup");
            var b = Healthy("dup");
            b.name = "ED_Otro";
            var issues = Validate(a, b);
            Assert.IsTrue(Has(issues, EnemyIssueSeverity.Error, "ED_Otro"));
        }

        [Test]
        public void Validate_NullVisualPrefab_ReportsError()
        {
            var so = Healthy();
            so.VisualPrefab = null;
            Assert.IsTrue(Has(Validate(so), EnemyIssueSeverity.Error, "Visual Prefab"));
        }

        [Test]
        public void Validate_NullPortrait_ReportsWarning()
        {
            var so = Healthy();
            so.Portrait = null;
            Assert.IsTrue(Has(Validate(so), EnemyIssueSeverity.Warning, "retrato"));
        }

        [Test]
        public void Validate_MinGoldAboveMax_ReportsWarning()
        {
            var so = Healthy();
            so.MinGoldDrop = 9; so.MaxGoldDrop = 3;
            Assert.IsTrue(Has(Validate(so), EnemyIssueSeverity.Warning, "Oro mínimo"));
        }

        [Test]
        public void Validate_UnknownWeaknessCombo_ReportsError_WithEmptyCatalog()
        {
            var so = Healthy();
            so.WeaknessComboId = "combo.inexistente";
            var catalog = New<ComboCatalogSO>();
            var issues = EnemyDataValidator.Validate(so, new List<EnemyDataSO> { so }, catalog);
            Assert.IsTrue(Has(issues, EnemyIssueSeverity.Error, "combo.inexistente"));
        }

        [Test]
        public void Validate_WeaknessWithoutCatalog_ReportsWarning()
        {
            var so = Healthy();
            so.WeaknessComboId = "combo.par";
            Assert.IsTrue(Has(Validate(so), EnemyIssueSeverity.Warning, "ComboCatalogSO"));
        }

        [Test]
        public void Validate_NonMonotonicTiers_NamesTheTier()
        {
            var so = Healthy();
            so.ExtraTiers.Add(new EnemyTier { Label = "Fuerte", MinFloor = 3 });
            so.ExtraTiers.Add(new EnemyTier { Label = "Élite", MinFloor = 2 });
            var issues = Validate(so);
            Assert.IsTrue(Has(issues, EnemyIssueSeverity.Warning, "Tier 3 — Élite"));
        }

        [Test]
        public void Validate_NullAIRoot_ReportsFallbackWarning()
        {
            var so = Healthy();
            so.AIRoot = null;
            Assert.IsTrue(Has(Validate(so), EnemyIssueSeverity.Warning, "BasicEnemyAI"));
        }

        [Test]
        public void Validate_BossFloorManagerWithoutIsBoss_ReportsWarning()
        {
            var so = New<BossFloorManagerSO>();
            so.EntityId = "boss.x";
            so.IsBoss = false;
            Assert.IsTrue(Has(Validate(so), EnemyIssueSeverity.Warning, "BossFloorManagerSO"));
        }

        [Test]
        public void Validate_IfWithoutThen_RollsUpTreeWarning_WithNodeName()
        {
            var so = Healthy();
            so.AIRoot = new AINode_If();
            var issues = Validate(so);
            Assert.IsTrue(Has(issues, EnemyIssueSeverity.Warning, "Entonces"));
            // El mensaje va prefijado con el NodeName del nodo (lo que muestra el canvas), no con el tipo.
            Assert.IsTrue(issues.Exists(i => i.Section == EnemyDataValidator.SecAI && i.Message.Contains(": ") && i.Message.Contains("Entonces")));
        }

        [Test]
        public void Validate_TelegraphTimingWithoutTelegraphNodes_ReportsWarning()
        {
            var so = Healthy();
            so.Design.Timing = AttackTiming.Telegraph;
            Assert.IsTrue(Has(Validate(so), EnemyIssueSeverity.Warning, "Timing = Telegraph"));
        }

        [Test]
        public void Validate_InstantTimingWithTelegraphNodes_ReportsWarning()
        {
            var so = Healthy();
            so.Design.Timing = AttackTiming.Instant;
            var seq = new AINode_Sequence();
            seq.Children.Add(new AINode_TelegraphMark());
            seq.Children.Add(new AINode_ExecuteTelegraph());
            so.AIRoot = seq;
            Assert.IsTrue(Has(Validate(so), EnemyIssueSeverity.Warning, "Instantáneo"));
        }

        [Test]
        public void Validate_SupportWithoutHeal_ReportsWarning_AndWithHealDoesNot()
        {
            var so = Healthy();
            so.Design.Archetype = EnemyArchetype.Support;
            Assert.IsTrue(Has(Validate(so), EnemyIssueSeverity.Warning, "Apoyo"));

            so.AIRoot = BehaviorWith(new EffHeal());
            Assert.IsFalse(Has(Validate(so), EnemyIssueSeverity.Warning, "Apoyo"));
        }

        [Test]
        public void Validate_RangedWithoutRangedTools_ReportsWarning_AndKeepDistanceClearsIt()
        {
            var so = Healthy();
            so.Design.Archetype = EnemyArchetype.Ranged;
            Assert.IsTrue(Has(Validate(so), EnemyIssueSeverity.Warning, "A distancia"));

            so.AIRoot = new AINode_KeepDistance();
            Assert.IsFalse(Has(Validate(so), EnemyIssueSeverity.Warning, "A distancia"));
        }

        [Test]
        public void Validate_MeleeWithoutMovement_ReportsInfoOnly()
        {
            var so = Healthy();
            so.Design.Archetype = EnemyArchetype.Melee;
            so.AIRoot = new AINode_Wait();
            var issues = Validate(so);
            Assert.IsTrue(Has(issues, EnemyIssueSeverity.Info, "Cuerpo a cuerpo"));
            Assert.AreEqual(0, EnemyDataValidator.Count(issues, EnemyIssueSeverity.Warning));
        }

        [Test]
        public void Validate_SortsErrorsFirst()
        {
            var so = Healthy(id: "");
            so.Portrait = null;
            var issues = Validate(so);
            Assert.AreEqual(EnemyIssueSeverity.Error, issues[0].Severity);
        }
    }
}
