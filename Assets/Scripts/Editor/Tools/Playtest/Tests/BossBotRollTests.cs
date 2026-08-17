using NUnit.Framework;

namespace Rollgeon.EditorTools.Playtest.Tests
{
    /// <summary>
    /// Tests de <see cref="BossBotRoll"/>: las caras que el bot le encola a
    /// <c>RiggedRollState</c> antes de cada ataque.
    /// </summary>
    /// <remarks>
    /// Lo que protegen: que la misma seed dé exactamente las mismas tiradas (si no, dos
    /// corridas no son comparables y fijar la tirada no sirvió de nada), que todos los dados
    /// salgan iguales (una tirada sin combo hace 0 de daño y ese turno no valida nada), y que
    /// una seed negativa no produzca una cara fuera de rango.
    /// </remarks>
    [TestFixture]
    public class BossBotRollTests
    {
        [Test]
        public void AllDiceGetTheSameFace_SoAComboAlwaysForms()
        {
            var faces = BossBotRoll.FacesFor(seed: 1234, turn: 3, diceCount: 5);

            Assert.AreEqual(5, faces.Length);
            foreach (int face in faces)
                Assert.AreEqual(faces[0], face);
        }

        [Test]
        public void TheSameSeedAndTurn_GiveTheSameFaces()
        {
            CollectionAssert.AreEqual(
                BossBotRoll.FacesFor(77, 4, 5),
                BossBotRoll.FacesFor(77, 4, 5));
        }

        [Test]
        public void TheTurnAdvancesTheFace_SoTheFightIsNotOneNoteRepeated()
        {
            Assert.AreNotEqual(BossBotRoll.FaceFor(1234, 1), BossBotRoll.FaceFor(1234, 2));
        }

        [Test]
        public void ADifferentSeed_MovesTheFight()
        {
            Assert.AreNotEqual(BossBotRoll.FaceFor(1, 1), BossBotRoll.FaceFor(2, 1));
        }

        [TestCase(0)]
        [TestCase(1234)]
        [TestCase(-5)]
        [TestCase(int.MinValue)]
        public void AnyFace_StaysWithinOneToSix(int seed)
        {
            // El módulo positivo importa: con un % crudo una seed negativa daría cara 0 o
            // negativa, y RiggedRollState la trataría como "rolar normal ese dado" — la
            // corrida dejaría de ser determinista sin ningún error visible.
            for (int turn = 0; turn < 20; turn++)
            {
                int face = BossBotRoll.FaceFor(seed, turn);
                Assert.GreaterOrEqual(face, 1, $"seed={seed} turn={turn}");
                Assert.LessOrEqual(face, 6, $"seed={seed} turn={turn}");
            }
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void ANonPositiveDiceCount_GivesAnEmptyRoll(int diceCount)
        {
            Assert.IsEmpty(BossBotRoll.FacesFor(1234, 1, diceCount));
        }

        [Test]
        public void TheFaceCountMatchesTheBag()
        {
            Assert.AreEqual(3, BossBotRoll.FacesFor(1234, 1, 3).Length);
            Assert.AreEqual(7, BossBotRoll.FacesFor(1234, 1, 7).Length);
        }
    }
}
