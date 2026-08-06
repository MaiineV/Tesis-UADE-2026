using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Meta.Conditions;

namespace Rollgeon.Meta.Tests
{
    /// <summary>
    /// La <see cref="ComingSoonCondition"/> es el placeholder de contenido no
    /// implementado: no debe cumplirse ni invalidarse bajo ninguna circunstancia.
    /// </summary>
    [TestFixture]
    public class ComingSoonConditionTests
    {
        [Test]
        public void Evaluate_EmptyContext_ReturnsFalse()
        {
            // Arrange
            var condition = new ComingSoonCondition();
            var ctx = new UnlockEvaluationContext();

            // Act
            bool result = condition.Evaluate(ctx);

            // Assert
            Assert.IsFalse(result, "El placeholder nunca se cumple.");
        }

        [Test]
        public void Evaluate_RunWonWithEverything_ReturnsFalse()
        {
            // Arrange: run ganada con todos los contadores en positivo — el caso
            // más permisivo posible sigue sin cumplir el placeholder.
            var condition = new ComingSoonCondition();
            var ctx = new UnlockEvaluationContext
            {
                RunEnded = true,
                RunWon = true,
                ClassId = "Warrior",
                FlawlessCombats = 99,
                BossesDefeated = 99,
                FloorsVisited = 99,
                ConsecutiveWins = 99,
                ClassesPlayed = new List<string> { "Warrior", "Berserker", "Gambler" },
            };

            // Act
            bool result = condition.Evaluate(ctx);

            // Assert
            Assert.IsFalse(result, "Ni la run perfecta cumple el placeholder.");
        }

        [Test]
        public void IsInvalidated_AnyContext_ReturnsFalse()
        {
            // Arrange
            var condition = new ComingSoonCondition();
            var ctx = new UnlockEvaluationContext { RunEnded = true };

            // Act
            bool invalidated = ((IUnlockCondition)condition).IsInvalidated(ctx);

            // Assert
            Assert.IsFalse(invalidated,
                "El placeholder usa el default de la interfaz: nunca se invalida.");
        }
    }
}
