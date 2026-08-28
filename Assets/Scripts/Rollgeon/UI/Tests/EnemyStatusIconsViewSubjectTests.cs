using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.UI.HUD.Status;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// De quién es cada intención que el jefe tickea, y cuál de ellas entra en su columna.
    /// </summary>
    /// <remarks>
    /// El nodo que detona las bombas publica una cruz POR BOMBA, y lo tickea el jefe: leídas
    /// todas, su columna era una tarjeta por bomba y su próximo ataque quedaba abajo de todo.
    /// Cada bomba las trae marcadas con su propio guid, y ese es el filtro.
    /// </remarks>
    [TestFixture]
    public sealed class EnemyStatusIconsViewSubjectTests
    {
        private FakeIntentService _intents;
        private EnemyStatusIconsView _view;
        private GameObject _go;
        private Guid _boss;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();

            _boss = Guid.NewGuid();
            _intents = new FakeIntentService();
            ServiceLocator.AddService<IEnemyIntentService>(_intents);

            _go = new GameObject("StatusRow", typeof(RectTransform));
            _view = _go.AddComponent<EnemyStatusIconsView>();
            _view.Initialize(_boss, null, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void LasCrucesDeLasBombas_NoSonTarjetasDelJefe()
        {
            // Arrange
            _intents.Next.Add(Own(AIIntentTextKeys.RangedShot, "Te dispara"));
            for (int i = 0; i < 3; i++)
                _intents.Standing.Add(OfBomb(Guid.NewGuid()));

            // Act
            var applied = _view.CollectApplied();

            // Assert
            Assert.AreEqual(1, applied.Count,
                "La columna del jefe se llenó con una tarjeta por bomba en el paño. La cruz es de " +
                "la bomba y se lee pasándole el mouse a la bomba; acá tapa lo primero que el " +
                "jugador busca, que es el próximo ataque.");
            Assert.AreEqual(AIIntentTextKeys.RangedShot, applied[0].Id);
        }

        [Test]
        public void ElProximoTiempoLlevaFecha_YLoQueTickeaSiempreNo()
        {
            // Arrange — el ciclo del Croupier: el disparo es el próximo tiempo, y el fuego del
            // Pleno cuelga FUERA del Alternate y se tickea todos los turnos.
            _intents.Next.Add(Own(AIIntentTextKeys.RangedShot, "Te dispara"));
            _intents.Standing.Add(Own(AIIntentTextKeys.Ignite, "Prende el suelo"));

            // Act
            var applied = _view.CollectApplied();

            // Assert — comparten columna, así que lo que los separa es la etiqueta de fecha: sin
            // ella el panel mostraba dos tarjetas de ataque y había que adivinar cuál iba a pasar.
            Assert.AreEqual(2, applied.Count);
            Assert.AreEqual(AIIntentTextKeys.RangedShot, applied[0].Id,
                "El próximo ataque va arriba de la columna: es lo más urgente de lo que va a pasar.");
            Assert.IsNotEmpty(applied[0].Eyebrow ?? string.Empty,
                "El próximo tiempo del ciclo lleva la fecha —'Próximo turno'— en chico.");
            Assert.IsEmpty(applied[1].Eyebrow ?? string.Empty,
                "Lo que el jefe mantiene en el paño no lleva fecha: no va a pasar, está pasando.");
        }

        [Test]
        public void LaColumnaPrincipal_EsLaDebilidad_NoElAtaque()
        {
            // Arrange — la debilidad sale del registry vivo, como en el spawn real.
            var registry = new Rollgeon.Combat.Weakness.WeaknessRegistry();
            registry.SetWeakness(_boss, "combo.poker", 1.3f);
            ServiceLocator.AddService<Rollgeon.Combat.Weakness.IWeaknessRegistry>(
                registry, ServiceScope.Global);

            var data = ScriptableObject.CreateInstance<Rollgeon.Entities.EnemyDataSO>();
            try
            {
                _view.Initialize(_boss, null, null, data);
                _intents.Next.Add(Own(AIIntentTextKeys.RangedShot, "Te dispara"));

                // Act
                var attack = _view.CollectAttack();

                // Assert
                Assert.AreEqual(1, attack.Count);
                Assert.AreEqual("enemy.weakness", attack[0].Id,
                    "El panel principal lleva la debilidad —lo único que cambia qué TIRÁS— y el " +
                    "ataque bajó al costado con su fecha.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void LoQueElJefeTickeaParaSiMismo_SiEsTarjetaSuya()
        {
            // Arrange
            _intents.Standing.Add(Own(AIIntentTextKeys.Ignite, "Prende el suelo"));
            _intents.Standing.Add(new AIIntent(
                AIIntentTextKeys.BombField, "Siembra bombas", 0, AttackKind.Environmental,
                amount: 3, subjectGuid: _boss));

            // Act
            var applied = _view.CollectApplied();

            // Assert
            Assert.AreEqual(2, applied.Count,
                "Se filtró de más: una intención sin dueño, o marcada con el guid del propio jefe, " +
                "es suya y tiene que salir en su panel.");
        }

        private AIIntent Own(string key, string fallback)
            => new AIIntent(key, fallback, 24, AttackKind.BasicAttack);

        private static AIIntent OfBomb(Guid bomb)
            => new AIIntent(AIIntentTextKeys.BombBlast, "Detonar la bomba", 0,
                            AttackKind.Environmental, subjectGuid: bomb);

        private sealed class FakeIntentService : IEnemyIntentService
        {
            public readonly List<AIIntent> Standing = new List<AIIntent>();
            public readonly List<AIIntent> Next = new List<AIIntent>();

            public bool TryRead(Guid enemyId, List<AIIntent> standing, List<AIIntent> next)
            {
                standing?.Clear();
                next?.Clear();
                standing?.AddRange(Standing);
                next?.AddRange(Next);
                return true;
            }
        }
    }
}
