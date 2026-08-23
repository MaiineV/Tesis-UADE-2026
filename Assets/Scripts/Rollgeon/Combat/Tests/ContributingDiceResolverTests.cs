using NUnit.Framework;
using Rollgeon.Combat.Damage;
using Rollgeon.Dice;

namespace Rollgeon.Combat.Tests
{
    /// <summary>Tests de <see cref="ContributingDiceResolver.ResolveDetailed"/>.</summary>
    [TestFixture]
    public class ContributingDiceResolverTests
    {
        [Test]
        public void ResolveDetailed_MapsSubsetIndices_ToSlotFaceAndType()
        {
            // Subset holdeado = bag slots [1, 3]; contribuyen ambos (índices locales 0 y 1).
            var contributing = new[] { 0, 1 };
            var keptOriginal = new[] { 1, 3 };
            var keptFaces = new[] { 4, 6 };
            var bag = new[] { DiceType.D6, DiceType.D8, DiceType.D6, DiceType.D20 };

            var result = ContributingDiceResolver.ResolveDetailed(contributing, keptOriginal, keptFaces, bag);

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(1, result[0].BagSlot);
            Assert.AreEqual(4, result[0].Face);
            Assert.AreEqual(DiceType.D8, result[0].Type);
            Assert.AreEqual(3, result[1].BagSlot);
            Assert.AreEqual(6, result[1].Face);
            Assert.AreEqual(DiceType.D20, result[1].Type);
        }

        [Test]
        public void ResolveDetailed_NullKeptIndices_TreatsContributingAsBagSlots()
        {
            var contributing = new[] { 0, 2 };
            var faces = new[] { 3, 1, 5 };
            var bag = new[] { DiceType.D6, DiceType.D6, DiceType.D10 };

            var result = ContributingDiceResolver.ResolveDetailed(contributing, null, faces, bag);

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(0, result[0].BagSlot);
            Assert.AreEqual(3, result[0].Face);
            Assert.AreEqual(2, result[1].BagSlot);
            Assert.AreEqual(5, result[1].Face);
            Assert.AreEqual(DiceType.D10, result[1].Type);
        }

        [Test]
        public void ResolveDetailed_BagSlotOutOfRange_SkipsThatDie()
        {
            var contributing = new[] { 0, 1 };
            var keptOriginal = new[] { 0, 9 }; // slot 9 no existe en el bag
            var keptFaces = new[] { 2, 6 };
            var bag = new[] { DiceType.D6, DiceType.D6 };

            var result = ContributingDiceResolver.ResolveDetailed(contributing, keptOriginal, keptFaces, bag);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(0, result[0].BagSlot);
            Assert.AreEqual(2, result[0].Face);
        }

        [Test]
        public void ResolveDetailed_FaceIndexOutOfRange_SkipsThatDie()
        {
            var contributing = new[] { 0, 1 };
            var keptFaces = new[] { 5 }; // solo hay cara para el índice local 0
            var bag = new[] { DiceType.D6, DiceType.D6 };

            var result = ContributingDiceResolver.ResolveDetailed(contributing, null, keptFaces, bag);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(5, result[0].Face);
        }

        [Test]
        public void ResolveDetailed_NullBag_ReturnsNull()
        {
            Assert.IsNull(ContributingDiceResolver.ResolveDetailed(new[] { 0 }, null, new[] { 4 }, null));
        }

        [Test]
        public void ResolveDetailed_NullOrEmptyContributing_ReturnsNull()
        {
            var bag = new[] { DiceType.D6 };

            Assert.IsNull(ContributingDiceResolver.ResolveDetailed(null, null, new[] { 4 }, bag));
            Assert.IsNull(ContributingDiceResolver.ResolveDetailed(new int[0], null, new[] { 4 }, bag));
        }

        [Test]
        public void ResolveDetailed_NullFaces_ReturnsNull()
        {
            // Sin caras no hay término de N que reportar: contrato igual a "sin bag".
            Assert.IsNull(ContributingDiceResolver.ResolveDetailed(
                new[] { 0 }, null, null, new[] { DiceType.D6 }));
        }

        // ================================================================
        // ResolveBagSlot — factoreado de CarrierParticipates/PcCarrierFace.
        // ================================================================

        [Test]
        public void ResolveBagSlot_WithMap_UsesMappedSlot()
        {
            var map = new[] { 1, 3 };

            Assert.AreEqual(1, ContributingDiceResolver.ResolveBagSlot(0, map));
            Assert.AreEqual(3, ContributingDiceResolver.ResolveBagSlot(1, map));
        }

        [Test]
        public void ResolveBagSlot_NullMap_TreatsIndexAsBagSlotDirectly()
        {
            Assert.AreEqual(2, ContributingDiceResolver.ResolveBagSlot(2, null));
        }

        [Test]
        public void ResolveBagSlot_LocalIndexOutOfMapRange_FallsBackToIdentity()
        {
            var map = new[] { 1, 3 };

            Assert.AreEqual(5, ContributingDiceResolver.ResolveBagSlot(5, map));
        }
    }
}
