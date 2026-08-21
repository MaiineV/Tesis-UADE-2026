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
    /// <summary>
    /// La caja del Cajero: le pone reloj a cada moneda del piso, y la que se vence sin que nadie la
    /// levante se la lleva él y lo cura — hasta un techo por pelea.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El techo es la mecánica, no un tope de seguridad: es lo que hace que juntar monedas sea la
    /// jugada ganadora en vez de una carrera imposible. Sin él, cada tanda que se le escapa le
    /// devuelve vida cada pocas rondas y la pelea no cierra.
    /// </para>
    /// <para>
    /// Los tres números están duplicados de <c>CajeroAssetBuilder</c>, que vive en un assembly de
    /// Editor y no se puede referenciar desde acá. Que el builder los cablee de verdad en el nodo lo
    /// cubre <c>CajeroPhaseWiringTests.CoinVault_CarriesTheClockAndTheHealCeilingFromTheSheet</c>.
    /// </para>
    /// <para>
    /// <b>Por qué varios tests drenan con un <c>for</c> de ticks.</b> La caja vence <b>una</b> moneda
    /// por tick, nunca la tanda entera (lo pide la ficha con esas palabras: "se vencen de a una, no
    /// todas juntas"). El nodo tickea una vez por turno del jefe, así que "una por tick" ya es "una
    /// por ronda" y una tanda de cuatro se paga a lo largo de cuatro turnos. Un solo tick sobre seis
    /// monedas vencidas cobra 12, no 72: para llegar al techo hay que darle los turnos, y las rondas
    /// van creciendo porque el vencimiento se compara contra <c>RoundIndex</c>.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class AINode_CajeroCoinVaultTests
    {
        private const int HealPerCoin = 12;
        private const int MaxHealPerFight = 60;
        private const int LifetimeRounds = 3;
        private const int BossMaxHp = 350;

        /// <summary>Suficientemente lastimado para que el techo entero entre sin tocar el máximo.</summary>
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

        // =====================================================================
        // El reloj
        // =====================================================================

        [Test]
        public void ACoinNobodyPicksUp_ExpiresAndHealsHim()
        {
            var node = NewNode();
            var coin = DropCoin();

            node.Tick(NewContext(round: 0));          // La ve por primera vez: vence en la 3.
            var result = node.Tick(NewContext(round: LifetimeRounds));

            Assert.AreEqual(AIResult.Succeeded, result, "El vencimiento no se cobró.");
            Assert.AreEqual(BossStartHp + HealPerCoin, BossHp(),
                "La moneda venció y el jefe no se curó: es todo el castigo por dejarla en el piso.");
            CollectionAssert.Contains(_hazards.Deactivated, coin,
                "La moneda vencida sigue en el piso: el jugador podría levantarla después de que ya " +
                "le pagó al jefe, o sea cobrar dos veces por la misma moneda.");
        }

        [Test]
        public void ACoinThatIsNotDueYet_StaysOnTheFloorAndHealsNothing()
        {
            var node = NewNode();
            DropCoin();

            node.Tick(NewContext(round: 0));
            for (int round = 1; round < LifetimeRounds; round++) node.Tick(NewContext(round));

            Assert.AreEqual(BossStartHp, BossHp(),
                $"Se cobró antes de las {LifetimeRounds} rondas: al jugador le queda menos tiempo " +
                "para llegar a la moneda que el que la ficha promete.");
            Assert.IsEmpty(_hazards.Deactivated, "La moneda desapareció antes de vencer.");
        }

        /// <summary>
        /// Una moneda que <b>desaparece antes</b> de su vencimiento la levantó el jugador, y ésa no
        /// cura a nadie. Es la razón entera de que el reloj viva en este nodo: el servicio de hazards
        /// expira igual la levantada y la vencida, así que desde afuera no se pueden distinguir.
        /// </summary>
        [Test]
        public void ACoinThePlayerPickedUp_IsForgottenWithoutHealing()
        {
            var node = NewNode();
            var coin = DropCoin();

            node.Tick(NewContext(round: 0));
            _hazards.Pickup(coin);
            node.Tick(NewContext(round: LifetimeRounds));

            Assert.AreEqual(BossStartHp, BossHp(),
                "Cobró por una moneda que el jugador había levantado: levantarla es exactamente la " +
                "jugada que la mecánica premia, y así pagaría lo mismo que ignorarla.");
        }

        /// <summary>Sólo vigila su propia definición: el resto de los hazards de la sala no son
        /// monedas y hacerlos vencer acá los apagaría antes de tiempo.</summary>
        [Test]
        public void AnotherHazardOnTheFloor_IsNeverTakenForACoin()
        {
            _otherHazard = NewDefinition("Spikes");
            var node = NewNode();
            var other = _hazards.Activate(_otherHazard, new[] { new GridCoord(1, 1) });

            node.Tick(NewContext(round: 0));
            node.Tick(NewContext(round: LifetimeRounds));

            Assert.AreEqual(BossStartHp, BossHp(), "Se curó con algo que no era una moneda.");
            CollectionAssert.DoesNotContain(_hazards.Deactivated, other,
                "Apagó un hazard ajeno: la caja sólo administra las monedas del Cajero.");
        }

        /// <summary>
        /// Una tanda entera vencida se cobra <b>de a una por turno</b>, no de un saque.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Es la ficha con esas palabras: "se vencen de a una, no todas juntas: la presión es
        /// constante, no un golpe". Y la cuenta la fuerza: la sala suelta las cuatro monedas de la
        /// tanda en el mismo barrido y las cuatro nacen con el mismo reloj, así que cobrarlas juntas
        /// sería 4 × 12 = 48 del techo de 60 <b>de toda la pelea</b> en un solo turno — un salto que
        /// el jugador ya no puede contestar. De a una, la misma tanda se paga a lo largo de cuatro
        /// turnos y la barra sube despacio.
        /// </para>
        /// <para>
        /// El intervalo no es un número aparte: el nodo tickea una vez por turno del jefe, así que
        /// "una por tick" ya es "una por ronda". Y la que ya venció pero no le tocó turno <b>sigue en
        /// el piso</b> y sigue siendo levantable: la carrera por juntarlas no se cierra de golpe.
        /// </para>
        /// </remarks>
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
                Assert.AreEqual(taken * HealPerCoin, BossHp() - BossStartHp,
                    "La curación no acompañó el ritmo: una moneda por turno es una curación por turno.");
                Assert.AreEqual(coinsPerRain - taken, _hazards.Count,
                    "Las monedas que todavía no le tocaron turno tienen que seguir en el piso y " +
                    "levantables: si desaparecen con la primera, el jugador nunca llega a ninguna.");
            }
        }

        // =====================================================================
        // El techo de curación
        // =====================================================================

        /// <summary>
        /// El caso central: se le vencen más monedas de las que el techo puede pagar, y lo curado en
        /// toda la pelea tiene que ser <b>exactamente</b> el techo.
        /// </summary>
        [Test]
        public void MoreCoinsThanTheCeilingCanPay_HealsExactlyTheCeiling()
        {
            var node = NewNode();
            int coins = MaxHealPerFight / HealPerCoin + 1; // Una más de las que el techo alcanza a pagar.
            var dropped = new List<Guid>();
            for (int i = 0; i < coins; i++) dropped.Add(DropCoin());

            node.Tick(NewContext(round: 0));
            Drain(node, fromRound: LifetimeRounds, coins);

            Assert.AreEqual(MaxHealPerFight, BossHp() - BossStartHp,
                $"Se curó {BossHp() - BossStartHp} con {coins} monedas vencidas. El techo de la " +
                $"pelea es {MaxHealPerFight}: por encima, dejarle monedas deja de tener un precio " +
                "acotado y la pelea puede no terminar.");
            CollectionAssert.AreEquivalent(dropped, _hazards.Deactivated,
                "Alguna moneda quedó en el piso con el techo alcanzado. Lo que se agota es la " +
                "curación, no el vencimiento: dejarla ahí la convierte en plata gratis para el jugador.");
        }

        [Test]
        public void CoinsThatRotAfterTheCeiling_StillVanishButHealNothing()
        {
            var node = NewNode();
            int coinsToTheCeiling = MaxHealPerFight / HealPerCoin;
            for (int i = 0; i < coinsToTheCeiling; i++) DropCoin();

            node.Tick(NewContext(round: 0));
            int lastDrainRound = Drain(node, fromRound: LifetimeRounds, coinsToTheCeiling);
            Assert.AreEqual(MaxHealPerFight, BossHp() - BossStartHp, "Fixture: el techo no se alcanzó.");

            // Una moneda más, ya con el presupuesto agotado. Las rondas van DESPUÉS del drenaje: el
            // reloj de la moneda se cuenta desde el tick que la descubre, y volver atrás la dejaría
            // con un vencimiento que ya pasó antes de existir.
            var late = DropCoin();
            node.Tick(NewContext(lastDrainRound + 1));
            node.Tick(NewContext(lastDrainRound + 1 + LifetimeRounds));

            Assert.AreEqual(MaxHealPerFight, BossHp() - BossStartHp,
                "El techo se recargó: es por PELEA, no por tanda de monedas.");
            CollectionAssert.Contains(_hazards.Deactivated, late,
                "Con el techo alcanzado la moneda dejó de vencerse y se quedó en el piso: pasada esa " +
                "línea, ignorarlas sería gratis y juntarlas dejaría de ser la jugada.");
        }

        /// <summary>
        /// El techo cuenta lo que <b>entró</b>, no lo que se ofreció: una moneda que se vence con el
        /// jefe lleno no puede gastarle presupuesto que todavía no usó.
        /// </summary>
        [Test]
        public void ACoinThatRotsWhileHeIsAtFullHp_DoesNotSpendTheCeiling()
        {
            GiveBossHealth(BossMaxHp);
            var node = NewNode();
            DropCoin();

            node.Tick(NewContext(round: 0));
            node.Tick(NewContext(round: LifetimeRounds));
            Assert.AreEqual(BossMaxHp, BossHp(), "Fixture: no tenía vida que recuperar.");

            // Ahora sí lastimado, y con el techo entero todavía disponible.
            GiveBossHealth(BossStartHp);
            int coinsToTheCeiling = MaxHealPerFight / HealPerCoin;
            for (int i = 0; i < coinsToTheCeiling; i++) DropCoin();

            node.Tick(NewContext(round: LifetimeRounds));
            Drain(node, fromRound: LifetimeRounds * 2, coinsToTheCeiling);

            Assert.AreEqual(MaxHealPerFight, BossHp() - BossStartHp,
                "La moneda que venció con el jefe lleno le comió presupuesto de curación. Así, " +
                "dejarle monedas mientras está intacto es la forma de anularle la mecánica sin " +
                "haberle costado nada — y al revés, el jefe pierde vida que la ficha le prometió.");
        }

        [Test]
        public void ItNeverHealsPastHisMaxHp()
        {
            GiveBossHealth(BossMaxHp - 1);
            var node = NewNode();
            DropCoin();

            node.Tick(NewContext(round: 0));
            node.Tick(NewContext(round: LifetimeRounds));

            Assert.AreEqual(BossMaxHp, BossHp(),
                "Se pasó del máximo: la barra de jefe queda mostrando más vida de la que la ficha " +
                "dice que tiene.");
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private static AINode_CajeroCoinVault NewNodeWith(HazardDefinitionSO coin) =>
            new AINode_CajeroCoinVault
            {
                Coin = coin,
                LifetimeRounds = LifetimeRounds,
                HealPerCoin = HealPerCoin,
                MaxHealPerFight = MaxHealPerFight,
            };

        private AINode_CajeroCoinVault NewNode() => NewNodeWith(_coin);

        /// <summary>
        /// Le da al nodo un turno por cada moneda pendiente, con la ronda subiendo de una en una, y
        /// devuelve la última ronda usada.
        /// </summary>
        /// <remarks>
        /// La caja vence UNA moneda por tick (ver
        /// <see cref="ARainOfCoins_ExpiresOnePerTurn_NotTheWholeBatchInOneBeat"/>), así que un solo
        /// tick sobre una tanda vencida cobra una sola moneda. Devolver la ronda importa: el reloj de
        /// una moneda se cuenta desde el tick que la descubre, y lo que se dropee después del drenaje
        /// tiene que seguir avanzando en el tiempo, no volver atrás.
        /// </remarks>
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

        /// <summary>
        /// Lleva las instancias vivas y nada más. No simula duración —la moneda nace permanente a
        /// propósito, ver <c>CajeroAssetBuilder.EnsureChipHazard</c>— así que el único reloj en juego
        /// es el del nodo, que es lo que se está probando.
        /// </summary>
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
