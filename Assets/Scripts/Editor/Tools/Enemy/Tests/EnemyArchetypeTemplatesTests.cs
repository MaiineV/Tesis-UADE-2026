using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Editor.Tools.Enemy.AITree;
using Rollgeon.Editor.Tools.Enemy.Templates;
using Rollgeon.Entities;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    [TestFixture]
    public class EnemyArchetypeTemplatesTests
    {
        readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        EnemyDataSO Apply(EnemyTemplate t)
        {
            var so = ScriptableObject.CreateInstance<EnemyDataSO>();
            _spawned.Add(so);
            EnemyArchetypeTemplates.ApplyTo(t, so);
            return so;
        }

        static IEnumerable<string> Ids()
        {
            foreach (var t in EnemyArchetypeTemplates.All) yield return t.Id;
        }

        [Test]
        public void All_HasTheTenGddSheets()
        {
            Assert.AreEqual(10, EnemyArchetypeTemplates.All.Count);
            var ids = new HashSet<string>();
            foreach (var t in EnemyArchetypeTemplates.All)
            {
                Assert.IsTrue(ids.Add(t.Id), "id repetido: " + t.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(t.Description), t.Id + " sin descripción");
                Assert.AreNotEqual(EnemyArchetype.Unspecified, t.Archetype, t.Id);
            }
        }

        [Test, TestCaseSource(nameof(Ids))]
        public void Apply_TreeHasNoErrorsNorWarnings(string id)
        {
            var t = EnemyArchetypeTemplates.Find(id);
            var so = Apply(t);

            Assert.IsNotNull(so.AIRoot, "sin árbol");
            var issues = AITreeValidator.Validate(AITreeSerializer.Load(so.AIRoot, so.AIDetachedNodes));
            var bad = issues.FindAll(i => i.Severity != IssueSeverity.Info);
            Assert.IsEmpty(bad, string.Join("\n", bad.ConvertAll(i => i.Message)));
        }

        [Test, TestCaseSource(nameof(Ids))]
        public void Apply_SheetOnlyLacksPrefabAndPortrait(string id)
        {
            var t = EnemyArchetypeTemplates.Find(id);
            var so = Apply(t);

            Assert.AreEqual(t.Archetype, so.Design.Archetype);
            Assert.AreEqual("enemy." + id, so.EntityId);
            StringAssert.Contains(t.Name, so.Design.Notes);

            var issues = EnemyDataValidator.Validate(so, new List<EnemyDataSO> { so }, null, null, null);
            var errors = issues.FindAll(i => i.Severity == EnemyIssueSeverity.Error);
            Assert.AreEqual(1, errors.Count, string.Join("\n", errors.ConvertAll(i => i.ToString())));
            StringAssert.Contains("Visual Prefab", errors[0].Message);
        }

        [Test]
        public void Summary_ReflectsEachArchetype()
        {
            Assert.IsTrue(EnemyTreeSummary.Build(Apply(EnemyArchetypeTemplates.Find("healer"))).HasHeal);
            Assert.IsTrue(EnemyTreeSummary.Build(Apply(EnemyArchetypeTemplates.Find("kiter"))).KeepsDistance);
            Assert.IsTrue(EnemyTreeSummary.Build(Apply(EnemyArchetypeTemplates.Find("sniper"))).HasTelegraph);
            Assert.IsFalse(EnemyTreeSummary.Build(Apply(EnemyArchetypeTemplates.Find("pursuer"))).HasTelegraph);
        }

        [Test]
        public void Sweeper_IsInstant_WithoutTelegraphNodes()
        {
            var so = Apply(EnemyArchetypeTemplates.Find("sweeper"));
            Assert.AreEqual(AttackTiming.Instant, so.Design.Timing);
            Assert.IsFalse(EnemyTreeSummary.Build(so).HasTelegraph, "el barrido es instantáneo");
        }

        [Test]
        public void Guardian_DeclaresAura_AndPassesSupportRule()
        {
            var so = Apply(EnemyArchetypeTemplates.Find("guardian"));
            Assert.IsTrue(so.HasAura);
            Assert.AreEqual(2, so.AuraRadius);

            var issues = EnemyDataValidator.Validate(so, new List<EnemyDataSO> { so }, null, null, null);
            Assert.IsFalse(issues.Exists(i => i.Message.Contains("Apoyo")),
                "el aura declarada cuenta como capacidad de soporte");
        }

        [Test]
        public void Templates_MainAttackGate_ReadsTheSheetRange()
        {
            // El gate principal (donde el rango del If coincidía con la ficha) pasa a leer el
            // atributo AttackRange — la ficha es LA fuente, sin número duplicado. El Charger
            // queda afuera a propósito: su If(1) es el contacto de la embestida, no su rango 2.
            var withOwnerRange = new[]
            {
                "pursuer", "sweeper", "skirmisher", "kiter", "sniper", "artillery", "mago",
                "healer", "guardian",
            };

            foreach (var id in withOwnerRange)
            {
                var so = Apply(EnemyArchetypeTemplates.Find(id));
                Assert.IsTrue(AnyGateUsesOwnerRange(so), id + ": ningún PcTargetInRange lee la ficha");
            }

            Assert.IsFalse(AnyGateUsesOwnerRange(Apply(EnemyArchetypeTemplates.Find("charger"))),
                "el If(1) del Charger es contacto de embestida, queda literal");
        }

        static bool AnyGateUsesOwnerRange(EnemyDataSO so)
        {
            foreach (var n in AITreeSerializer.Load(so.AIRoot).Nodes)
            {
                if (!(n is Rollgeon.Combat.AI.Decisions.AINode_If i) || i.Conditions == null) continue;
                foreach (var pc in i.Conditions)
                    if (pc is Rollgeon.PreConditions.Concretes.PcTargetInRange r && r.UseOwnerAttackRange)
                        return true;
            }
            return false;
        }

        [Test]
        public void Charger_TelegraphUsesTheTemplateAttack()
        {
            var so = Apply(EnemyArchetypeTemplates.Find("charger"));
            var summary = EnemyTreeSummary.Build(so);
            Assert.IsTrue(summary.HasTelegraph);
            var mark = FindMark(so.AIRoot);
            Assert.IsNotNull(mark);
            Assert.AreEqual(so.BaseAttack, mark.Damage, "el daño del telegraph se captura del ATK de la plantilla, no del default");
        }

        static Rollgeon.Combat.AI.Decisions.AINode_TelegraphMark FindMark(Rollgeon.Combat.AI.Decisions.AIDecisionNode root)
        {
            foreach (var n in AITreeSerializer.Load(root).Nodes)
                if (n is Rollgeon.Combat.AI.Decisions.AINode_TelegraphMark m) return m;
            return null;
        }
    }
}
