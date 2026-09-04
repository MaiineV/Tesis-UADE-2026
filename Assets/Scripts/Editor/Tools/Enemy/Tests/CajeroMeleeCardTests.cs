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
    /// Cada cosa que hace el Cajero tiene que anunciarse distinta: sus tres ataques salen de nodos
    /// compartidos, y sin etiqueta propia el panel decía "Disparo" para los dos melee y
    /// "Golpe marcado" sin texto para el cañonazo. Se lee el árbol del asset, no el del builder.
    /// </summary>
    [TestFixture]
    public class CajeroMeleeCardTests
    {
        private AINode_RangedShot _heavyBlow;
        private AINode_CajeroShove _shove;
        private AINode_TelegraphMark _slamMark;
        private AINode_ExecuteTelegraph _slamExecute;

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

            var marks = new List<AINode_TelegraphMark>();
            AIIntentWalker.CollectNodes(root, marks);
            _slamMark = marks.Count == 1 ? marks[0] : null;

            var executes = new List<AINode_ExecuteTelegraph>();
            AIIntentWalker.CollectNodes(root, executes);
            _slamExecute = executes.Count == 1 ? executes[0] : null;

            Assert.IsNotNull(_slamMark, "El aviso del cañonazo no está en el árbol del asset (o hay más de uno).");
            Assert.IsNotNull(_slamExecute, "El cobro del cañonazo no está en el árbol del asset (o hay más de uno).");
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

        /// <summary>El turno que avisa: hasta ahora era el <c>Wait</c> del gate y el panel no decía nada.</summary>
        [Test]
        public void TheSlamWarning_SaysWhereItLandsAndWhen()
        {
            Assert.IsTrue(((IAIIntentNode)_slamMark).TryDescribeOption(new AIContext(), out var card),
                "El aviso no publica tarjeta: estando lejos el panel del jefe queda vacío.");

            Assert.AreEqual(AIIntentTextKeys.CashierSlam, card.LabelKey);
            Assert.AreEqual(_slamMark.Damage, card.Damage,
                "La tarjeta tiene que traer el número, que es lo que decide si te movés.");
            CollectionAssert.IsEmpty(card.Tiles,
                "Las casillas se anclan recién al tickear y todavía te queda un turno para moverte: " +
                "prometerlas sería una estimación.");
        }

        /// <summary>La tarjeta del cobro. El árbol se lee del asset porque es lo que carga el juego:
        /// un rebuild que se coma la key deja la tarjeta genérica y sin texto.</summary>
        [Test]
        public void TheSlamHit_HasItsOwnCard_NotTheGenericMarkedStrike()
        {
            Assert.AreEqual(AIIntentTextKeys.CashierSlamDue, _slamExecute.IntentLabelKey,
                "Sin key propia el panel dice \"Golpe marcado\" con la descripción vacía.");
            Assert.AreNotEqual(AIIntentTextKeys.CashierSlam, _slamExecute.IntentLabelKey,
                "Avisar y cobrar dicen cosas distintas: con la misma key el aviso se repite.");
            Assert.IsNotEmpty(_slamExecute.IntentLabelFallback ?? string.Empty,
                "Sin fallback la tarjeta queda con la key cruda si la tabla no cargó.");
        }

        /// <summary>Los dos recortes del 3×3 se arreglaron en el nodo, así que lo que hay que fijar es
        /// que el asset —lo que carga el juego— los traiga: un rebuild que se los coma devuelve el bug
        /// sin tocar una línea de código.</summary>
        [Test]
        public void TheSlamArea_ArrivesWholeInTheShippedAsset()
        {
            Assert.IsTrue(_slamMark.IgnoreLineOfSight,
                "El asset volvió a filtrar por visión: el cuadrado sale mordido y a veces vacío.");
            Assert.IsTrue(_slamMark.KeepSquareWhole,
                "El asset volvió a dejar que la pared muerda el cuadrado.");
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
