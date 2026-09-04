using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Entities;
using Rollgeon.Feedback;
using UnityEditor;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>
    /// Los dos tiempos del ciclo melee salen del mismo nodo de disparo, así que sin etiqueta propia
    /// el panel anunciaba "Disparo" para los dos. Se lee el árbol del asset, no el del builder.
    /// </summary>
    [TestFixture]
    public class CajeroMeleeCardTests
    {
        private AINode_RangedShot _heavyBlow;
        private AINode_CajeroShove _shove;

        [SetUp]
        public void SetUp()
        {
            var data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(CajeroAssetBuilder.EnemyAssetPath);
            Assert.IsNotNull(data, $"No se pudo cargar {CajeroAssetBuilder.EnemyAssetPath}.");

            var root = data.CreateRuntimeAIRoot();
            Assert.IsNotNull(root, "El asset del Cajero no tiene AIRoot.");

            // El empujón hereda del disparo: un solo barrido devuelve los dos tiempos.
            var shots = new List<AINode_RangedShot>();
            AIIntentWalker.CollectNodes(root, shots);

            foreach (var shot in shots)
            {
                if (shot is AINode_CajeroShove cajeroShove) _shove = cajeroShove;
                else _heavyBlow = shot;
            }

            Assert.IsNotNull(_heavyBlow, "El mandoble no está en el árbol del asset.");
            Assert.IsNotNull(_shove, "El empujón no está en el árbol del asset.");
        }

        [Test]
        public void TheHeavyBlow_IsAnnouncedAsAStrike_NotAsAShot()
        {
            Assert.IsTrue(Option(_heavyBlow, out var card));

            Assert.AreEqual(AIIntentTextKeys.Attack, card.LabelKey,
                "Pega a una casilla: anunciarlo como tiro promete algo que nunca sale.");
        }

        [Test]
        public void TheShove_SaysItPushesAndCharges()
        {
            Assert.IsTrue(Option(_shove, out var card));

            Assert.AreEqual(AIIntentTextKeys.CashierShove, card.LabelKey,
                "Con la key heredada del disparo los dos tiempos se leían idénticos.");
            Assert.AreEqual(_shove.PushTiles, card.Amount, "El {1} de la frase son las casillas del tumbo.");
        }

        /// <summary>Alternan, y con el mismo trigger el turno que empuja se veía igual que el que sólo pega.</summary>
        [Test]
        public void TheTwoMeleeBeats_DoNotShareAGesture()
        {
            Assert.AreEqual(BossFeedbackIds.CajeroShoveAnim, _shove.AnimFeedbackId);
            Assert.AreNotEqual(_heavyBlow.AnimFeedbackId, _shove.AnimFeedbackId);
        }

        /// <summary>Por el repertorio y no por la intención viva: no depende de que haya grilla.</summary>
        private static bool Option(AINode_RangedShot node, out AIIntent card) =>
            ((IAIIntentNode)node).TryDescribeOption(new AIContext(), out card);
    }
}
