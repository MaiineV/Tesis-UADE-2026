using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Rooms.Visuals;
using Rollgeon.Tiles;
using Rollgeon.UI.HUD.Status;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Combat.Rooms.Tests
{
    /// <summary>
    /// El panel de la bomba: el plazo arriba como próximo turno, y lo que hace al estallar
    /// abajo en un bloque propio. Sin vida en ninguna parte.
    /// </summary>
    /// <remarks>
    /// Los asserts miran ids y números, nunca el texto localizado: las keys están sembradas en
    /// las dos tablas, así que afirmar sobre la frase ataría el test al idioma del editor.
    /// </remarks>
    [TestFixture]
    public sealed class RoomObjectPanelContentTests
    {
        private const int BlastDamage = 30;
        private const int FireEnter = 15;
        private const int FireTurnStart = 15;

        private GameObject _go;
        private RoomObjectTooltipInfo _info;
        private RoomObjectDefinitionSO _bomb;
        private SpecialTileDefinitionSO _fire;
        private FakeIntentService _intents;
        private readonly Guid _owner = Guid.NewGuid();
        private readonly Guid _self = Guid.NewGuid();

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();

            _fire = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            _fire.hideFlags = HideFlags.HideAndDontSave;
            _fire.EnterDamage = FireEnter;
            _fire.TurnStartDamage = FireTurnStart;

            _bomb = ScriptableObject.CreateInstance<RoomObjectDefinitionSO>();
            _bomb.hideFlags = HideFlags.HideAndDontSave;

            _intents = new FakeIntentService();
            ServiceLocator.AddService<IEnemyIntentService>(_intents);

            _go = new GameObject("Bomb");
            _info = _go.AddComponent<RoomObjectTooltipInfo>();
            _info.Bind(_bomb, _owner, _self);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_bomb);
            Object.DestroyImmediate(_fire);
            ServiceLocator.Clear();
        }

        [Test]
        public void ElPanel_NoLlevaVitales()
        {
            // La barra sobre la cabeza ya dice la vida, y ninguna otra cosa del juego la pone en
            // su panel: un solo objeto mostrándola se lee como un dato que los demás esconden.
            Publish(turnsAway: 1);

            var content = _info.BuildContent();

            Assert.IsFalse(content.HasVitals, "El panel de la bomba volvió a mostrar vida.");
            Assert.IsNull(content.Flavor,
                "Quedó pie en el panel: la mecha vive en el bloque de próximo turno, y la vida " +
                "no se muestra en ninguna parte.");
        }

        [Test]
        public void ConMechaPorDelante_ElProximoTurnoEsQueSeAcorta()
        {
            // Arrange
            Publish(turnsAway: 2);

            // Act
            var cards = _info.CollectCards();

            // Assert
            Assert.AreEqual(RoomObjectTooltipInfo.FuseTickKey, cards[0].Id,
                "El primer bloque no es el de la mecha.");
            Assert.AreEqual(2, cards[0].RemainingTurns,
                "El badge no cuenta los turnos que le quedan a la mecha.");
            Assert.IsNull(cards[0].Damage,
                "La tarjeta de la mecha muestra un número de daño: lo que cobra es el estallido, " +
                "y ese vive en su propio bloque.");
        }

        [Test]
        public void ElTurnoAntesDelPlazo_ElProximoTurnoEsQueExplota()
        {
            // Arrange — TurnsAway 0 es "en su próximo turno": ahí ya no queda mecha que acortar.
            Publish(turnsAway: 0);

            // Act
            var cards = _info.CollectCards();

            // Assert
            Assert.AreEqual(RoomObjectTooltipInfo.FuseBlowsKey, cards[0].Id,
                "El turno anterior al estallido sigue diciendo que la mecha se acorta: el " +
                "jugador no se entera de que el que viene es el bueno.");
            Assert.IsNull(cards[0].RemainingTurns,
                "Quedó badge al lado de 'Explota': el 0 se dibuja igual que cualquier número y " +
                "se lee como que faltan cero turnos.");
        }

        [Test]
        public void ElBloqueDelEstallido_EsUnoSolo_ConElGolpeYElFuego()
        {
            // Arrange
            Publish(turnsAway: 2);

            // Act
            var cards = _info.CollectCards();

            // Assert — golpe primero, y el fuego que deja detrás, en el MISMO bloque.
            Assert.AreEqual(4, cards.Count,
                "El bloque del estallido no trae el golpe más los dos precios del fuego.");
            Assert.AreEqual(RoomObjectTooltipInfo.BlastHitKey, cards[1].Id);
            Assert.AreEqual(BlastDamage, cards[1].Damage,
                "El golpe del estallido no viaja como dato.");
            Assert.AreEqual(FireEnter, cards[2].Damage);
            Assert.AreEqual(FireTurnStart, cards[3].Damage);

            Assert.IsFalse(string.IsNullOrEmpty(cards[1].Eyebrow),
                "El golpe no abre el bloque: sin etiqueta se lee pegado al de la mecha.");
            Assert.IsNull(cards[2].Eyebrow,
                "El fuego abrió un bloque propio: el golpe y lo que queda ardiendo son la misma " +
                "consecuencia, y dos etiquetas la parten en dos.");
            Assert.IsNull(cards[3].Eyebrow);
        }

        [Test]
        public void SinDanoDeEstallido_ElBloqueLoAbreElFuego()
        {
            // Arrange — un objeto que sólo planta terreno: un "0" ahí sería un precio inventado.
            Publish(turnsAway: 1, blastDamage: 0);

            // Act
            var cards = _info.CollectCards();

            // Assert
            Assert.AreEqual(3, cards.Count, "Apareció una tarjeta de golpe sin golpe que mostrar.");
            Assert.IsFalse(string.IsNullOrEmpty(cards[1].Eyebrow),
                "Sin golpe, el bloque tiene que abrirlo el fuego: si no, queda sin etiqueta.");
        }

        private void Publish(int turnsAway, int? blastDamage = null)
        {
            _intents.Standing.Clear();
            _intents.Standing.Add(new AIIntent(
                AIIntentTextKeys.BombBlast, "Detonar la bomba",
                blastDamage ?? BlastDamage, AttackKind.Environmental,
                leaves: _fire, leavesRounds: 4, turnsAway: turnsAway, subjectGuid: _self));
        }

        private sealed class FakeIntentService : IEnemyIntentService
        {
            public readonly List<AIIntent> Standing = new();

            public bool TryRead(Guid enemyId, List<AIIntent> standing, List<AIIntent> next,
                                List<AIIntent> options = null)
            {
                standing.Clear();
                next.Clear();
                options?.Clear();
                standing.AddRange(Standing);
                return true;
            }
        }
    }
}
