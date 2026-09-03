using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    // LifetimeRounds está duplicado de CajeroAssetBuilder, que vive en un assembly de Editor y no
    // se puede referenciar desde acá.
    [TestFixture]
    public class AINode_CajeroCoinVaultTests
    {
        private const int LifetimeRounds = 3;
        private const int BossMaxHp = 350;

        /// <summary>Lastimado a propósito: con el jefe lleno, "no se curó" pasaría igual por el clamp
        /// al máximo y el test no probaría nada.</summary>
        private const int BossStartHp = 100;

        private AttributesManager _attributes;
        private FakeHazardService _hazards;
        private HazardDefinitionSO _coin;
        private HazardDefinitionSO _otherHazard;
        private Guid _boss;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _attributes = new AttributesManager();
            _boss = Guid.NewGuid();
            GiveBossHealth(BossStartHp);

            _hazards = new FakeHazardService();
            ServiceLocator.AddService<IHazardService>(_hazards, ServiceScope.Global);

            _coin = NewDefinition("Coin");
        }

        [TearDown]
        public void TearDown()
        {
            _attributes.Dispose();
            if (_coin != null) UnityEngine.Object.DestroyImmediate(_coin);
            _coin = null;
            if (_otherHazard != null) UnityEngine.Object.DestroyImmediate(_otherHazard);
            _otherHazard = null;

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void ACoinNobodyPicksUp_ExpiresAndIsLostWithoutHealingHim()
        {
            var node = NewNode();
            var coin = DropCoin();

            node.Tick(NewContext(round: 0));          // La ve por primera vez: vence en la 3.
            var result = node.Tick(NewContext(round: LifetimeRounds));

            Assert.AreEqual(AIResult.Succeeded, result, "El vencimiento no se cobró.");
            Assert.AreEqual(BossStartHp, BossHp(),
                "La moneda vencida lo curó. La plata que el jugador deja vencer se pierde y nada más: " +
                "lo que dejó de ganar es todo el precio, y el jefe no tiene forma de recuperar vida.");
            CollectionAssert.Contains(_hazards.Deactivated, coin,
                "La moneda vencida sigue en el piso: el jugador la levantaría pasado el plazo, y el " +
                "plazo dejaría de significar algo.");
        }

        [Test]
        public void ACoinThatIsNotDueYet_StaysOnTheFloor()
        {
            var node = NewNode();
            DropCoin();

            node.Tick(NewContext(round: 0));
            for (int round = 1; round < LifetimeRounds; round++) node.Tick(NewContext(round));

            Assert.IsEmpty(_hazards.Deactivated,
                $"Se cobró antes de las {LifetimeRounds} rondas: al jugador le queda menos tiempo " +
                "para llegar a la moneda que el que la ficha promete.");
        }

        // El reloj vive en el nodo porque el servicio de hazards expira igual la levantada y la
        // vencida: desde afuera no se pueden distinguir.
        [Test]
        public void ACoinThePlayerPickedUp_IsForgotten()
        {
            var node = NewNode();
            var coin = DropCoin();

            node.Tick(NewContext(round: 0));
            _hazards.Pickup(coin);
            var result = node.Tick(NewContext(round: LifetimeRounds));

            Assert.AreEqual(AIResult.Failed, result,
                "Venció una moneda que el jugador había levantado: levantarla es la jugada que la " +
                "mecánica premia, y el nodo la sigue contando como si estuviera en el piso.");
            CollectionAssert.DoesNotContain(_hazards.Deactivated, coin,
                "Apagó una instancia que ya no existía.");
        }

        [Test]
        public void AnotherHazardOnTheFloor_IsNeverTakenForACoin()
        {
            _otherHazard = NewDefinition("Spikes");
            var node = NewNode();
            var other = _hazards.Activate(_otherHazard, new[] { new GridCoord(1, 1) });

            node.Tick(NewContext(round: 0));
            node.Tick(NewContext(round: LifetimeRounds));

            CollectionAssert.DoesNotContain(_hazards.Deactivated, other,
                "Apagó un hazard ajeno: la caja sólo administra las monedas del Cajero.");
        }

        [Test]
        public void ARainOfCoins_ExpiresOnePerTurn_NotTheWholeBatchInOneBeat()
        {
            // CajeroAssetBuilder.CoinsPerRain: la tanda que suelta la sala.
            const int coinsPerRain = 4;

            var node = NewNode();
            for (int i = 0; i < coinsPerRain; i++) DropCoin();

            node.Tick(NewContext(round: 0)); // Las cuatro quedan con el MISMO vencimiento.

            for (int taken = 1; taken <= coinsPerRain; taken++)
            {
                node.Tick(NewContext(LifetimeRounds + taken - 1));

                Assert.AreEqual(taken, _hazards.Deactivated.Count,
                    $"Se llevó {_hazards.Deactivated.Count} monedas en {taken} turno(s). Las cuatro " +
                    "vencen juntas y de a una es el diseño: la tanda entera en un tick le devuelve " +
                    "casi todo el techo de la pelea de un saque.");
                Assert.AreEqual(coinsPerRain - taken, _hazards.Count,
                    "Las monedas que todavía no le tocaron turno tienen que seguir en el piso y " +
                    "levantables: si desaparecen con la primera, el jugador nunca llega a ninguna.");
            }
        }

        /// <summary>Reemplaza al viejo test del techo de curación: sin techo, lo que hay que sostener
        /// con volumen es que ninguna cantidad de monedas vencidas le devuelve vida.</summary>
        [Test]
        public void ManyExpiredCoins_NeverGiveHimBackAnyHealth()
        {
            const int coins = 6;

            var node = NewNode();
            var dropped = new List<Guid>();
            for (int i = 0; i < coins; i++) dropped.Add(DropCoin());

            node.Tick(NewContext(round: 0));
            Drain(node, fromRound: LifetimeRounds, coins);

            Assert.AreEqual(BossStartHp, BossHp(),
                $"Se curó {BossHp() - BossStartHp} con {coins} monedas vencidas. Sin curación, los " +
                "450 de vida de la ficha son exactamente lo que aguanta, y la pelea dura lo que dice " +
                "que dura.");
            CollectionAssert.AreEquivalent(dropped, _hazards.Deactivated,
                "Alguna moneda quedó en el piso: vencerlas no depende de nada que se agote.");
        }

        private static AINode_CajeroCoinVault NewNodeWith(HazardDefinitionSO coin) =>
            new AINode_CajeroCoinVault
            {
                Coin = coin,
                LifetimeRounds = LifetimeRounds,
            };

        private AINode_CajeroCoinVault NewNode() => NewNodeWith(_coin);

        // La caja vence UNA moneda por tick, así que un tick sobre una tanda vencida cobra una sola.
        // Devuelve la última ronda usada: lo que se dropee después no puede volver atrás en el tiempo.
        private int Drain(AINode_CajeroCoinVault node, int fromRound, int coins)
        {
            int round = fromRound;
            for (int i = 0; i < coins; i++, round++) node.Tick(NewContext(round));
            return round - 1;
        }

        private AIContext NewContext(int round) => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = Guid.NewGuid(),
            Attributes = _attributes,
            SelfMaxHp = BossMaxHp,
            RoundIndex = round,
        };

        /// <summary>Una moneda más en el piso, cada una en su propia casilla.</summary>
        private Guid DropCoin() =>
            _hazards.Activate(_coin, new[] { new GridCoord(_hazards.Count, 5) });

        private void GiveBossHealth(int current)
        {
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(current));
            _attributes.Register(_boss, attrs);
        }

        private int BossHp() => _attributes.GetAttributeValue<Health, int>(_boss);

        private static HazardDefinitionSO NewDefinition(string name)
        {
            var definition = ScriptableObject.CreateInstance<HazardDefinitionSO>();
            definition.name = name;
            definition.hideFlags = HideFlags.HideAndDontSave;
            definition.Trigger = HazardTriggerMode.OnEnter;
            definition.ConsumeOnTrigger = true;
            definition.DurationRounds = 0; // No vence sola: el reloj es del nodo.
            definition.SourceId = Guid.NewGuid().ToString();
            return definition;
        }

        // No simula duración: la moneda nace permanente a propósito, así que el único reloj en juego
        // es el del nodo, que es lo que se está probando.
        private sealed class FakeHazardService : IHazardService
        {
            public readonly List<Guid> Deactivated = new List<Guid>();

            private readonly Dictionary<Guid, HazardInstanceInfo> _instances =
                new Dictionary<Guid, HazardInstanceInfo>();

            public int Count => _instances.Count;

            /// <summary>El jugador la levantó: desaparece sin pasar por <see cref="Deactivate"/>.</summary>
            public void Pickup(Guid instanceId) => _instances.Remove(instanceId);

            public void Activate(HazardDefinitionSO definition) { }

            public Guid Activate(HazardDefinitionSO definition, IEnumerable<GridCoord> tiles)
            {
                if (definition == null || tiles == null) return Guid.Empty;

                var coords = new List<GridCoord>(tiles);
                if (coords.Count == 0) return Guid.Empty;

                var id = Guid.NewGuid();
                _instances[id] = new HazardInstanceInfo(id, definition, coords, 0);
                return id;
            }

            public bool IsActive(HazardDefinitionSO definition) =>
                definition != null && _instances.Values.Any(i => i.Definition == definition);

            public bool IsActive(Guid sourceId) => _instances.ContainsKey(sourceId);

            public bool TryGetHazardAt(GridCoord coord, out HazardInstanceInfo info)
            {
                foreach (var instance in _instances.Values)
                {
                    if (!instance.Tiles.Contains(coord)) continue;
                    info = instance;
                    return true;
                }

                info = default;
                return false;
            }

            public IEnumerable<HazardInstanceInfo> ActiveInstances() =>
                new List<HazardInstanceInfo>(_instances.Values);

            public void Deactivate(Guid instanceId)
            {
                Deactivated.Add(instanceId);
                _instances.Remove(instanceId);
            }

            public void SkipNextTick(Guid instanceId) { }
        }
    }
}
