using NUnit.Framework;
using Rollgeon.Entities;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    [TestFixture]
    public class EnemyEditorVocabTests
    {
        [Test]
        public void LabelOf_ReadsInspectorName()
        {
            Assert.AreEqual("Cuerpo a cuerpo", EnemyEditorVocab.LabelOf(EnemyArchetype.Melee));
            Assert.AreEqual("Telegraph", EnemyEditorVocab.LabelOf(AttackTiming.Telegraph));
            Assert.AreEqual("Contacto 1×1 adyacente", EnemyEditorVocab.LabelOf(AttackPatternKind.ContactAdjacent));
        }

        [Test]
        public void LabelsOf_FollowsDeclarationOrder_AndCoversEveryValue()
        {
            var labels = EnemyEditorVocab.LabelsOf<EnemyArchetype>();
            CollectionAssert.AreEqual(new[] { "Sin definir", "Cuerpo a cuerpo", "A distancia", "Apoyo" }, labels);
            Assert.AreEqual(12, EnemyEditorVocab.LabelsOf<AttackPatternKind>().Length, "Unspecified + 11 patrones del GDD");
        }

        [Test]
        public void FootprintBadge_EmptyForUnit_TextOtherwise()
        {
            Assert.AreEqual(string.Empty, EnemyEditorVocab.FootprintBadge(new UnityEngine.Vector2Int(1, 1)));
            Assert.AreEqual(string.Empty, EnemyEditorVocab.FootprintBadge(new UnityEngine.Vector2Int(0, 0)));
            Assert.AreEqual("2×2", EnemyEditorVocab.FootprintBadge(new UnityEngine.Vector2Int(2, 2)));
            Assert.AreEqual("2×1", EnemyEditorVocab.FootprintBadge(new UnityEngine.Vector2Int(2, 1)));
        }

        [Test]
        public void Chip_IsShortAndEmptyForUnspecified()
        {
            Assert.AreEqual("M", EnemyEditorVocab.Chip(EnemyArchetype.Melee));
            Assert.AreEqual("R", EnemyEditorVocab.Chip(EnemyArchetype.Ranged));
            Assert.AreEqual("S", EnemyEditorVocab.Chip(EnemyArchetype.Support));
            Assert.AreEqual(string.Empty, EnemyEditorVocab.Chip(EnemyArchetype.Unspecified));
        }
    }
}
