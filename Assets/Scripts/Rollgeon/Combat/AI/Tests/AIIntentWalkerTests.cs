using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Leer el árbol de un enemigo sin tickearlo: qué tiene en curso y cuál es su próximo ataque.
    /// </summary>
    [TestFixture]
    public class AIIntentWalkerTests
    {
        private AttributesManager _attrs;
        private GridManager _grid;
        private DamagePipeline _pipeline;
        private ThreatenedAreaService _threat;
        private Guid _boss;
        private Guid _player;

        private readonly List<AIIntent> _standing = new();
        private readonly List<AIIntent> _next = new();

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            TypedEvent<DamageResolvedPayload>.Clear();

            _attrs = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_attrs);

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(8, 8));

            _pipeline = new DamagePipeline(_attrs);

            _threat = new ThreatenedAreaService();
            ServiceLocator.AddService<IThreatenedAreaService>(_threat);

            _boss = Register(new GridCoord(0, 0), hp: 100);
            _player = Register(new GridCoord(2, 0), hp: 100);
        }

        [TearDown]
        public void TearDown()
        {
            _attrs?.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void NextChild_EsElHijoQueVaACorrerEnElProximoTick()
        {
            var alternate = new AINode_Alternate
            {
                Children = new List<AIDecisionNode> { Shot(1), Shot(2), Shot(3) },
            };
            var context = Context();

            alternate.Tick(context);
            alternate.Tick(context);

            Assert.AreSame(alternate.Children[2], alternate.NextChild,
                "NextChild no coincide con el hijo que el próximo Tick va a correr. Si esto se " +
                "desfasa, el tooltip anuncia un ataque y el jefe ejecuta otro, que es peor que " +
                "no anunciar nada.");
        }

        [Test]
        public void ElCicloAportaSoloSuProximoTiempo_NoLosTres()
        {
            var root = new AINode_Alternate
            {
                Children = new List<AIDecisionNode> { Shot(11), Shot(22), Shot(33) },
            };

            AIIntentWalker.Collect(root, Context(), _standing, _next);

            Assert.AreEqual(1, _next.Count,
                "El ciclo aportó más de una intención: sus hijos son turnos distintos, y mostrarlos " +
                "juntos diría que el jefe hace las tres cosas a la vez.");
            Assert.AreEqual(11, _next[0].Damage, "No es el tiempo que le toca al próximo turno.");
        }

        [Test]
        public void UnIf_SeBajaPorLaRamaQueElMismoIfElegiria()
        {
            var root = new AINode_If
            {
                Conditions = new List<BasePreCondition> { new PcOwnerHpBelow { Percent = 0.5f } },
                Then = Shot(7),
                Else = Shot(9),
            };

            AIIntentWalker.Collect(root, Context(), _standing, _next);
            Assert.AreEqual(9, _standing[0].Damage,
                "Con el jefe a full vida se bajó por el Then: el aviso promete el ataque de una " +
                "fase que el jefe todavía no se ganó.");

            _attrs.GetAttributes(_boss).SetAttribute<Health>(new Health(10));
            AIIntentWalker.Collect(root, Context(), _standing, _next);
            Assert.AreEqual(7, _standing[0].Damage,
                "Cruzado el umbral se siguió bajando por el Else: el aviso quedó una fase atrás.");
        }

        [Test]
        public void UnOnceYaLatcheado_NoSeAbre()
        {
            var once = new AINode_Once { Child = Shot(5) };
            var context = Context();
            once.Tick(context);

            AIIntentWalker.Collect(once, context, _standing, _next);

            Assert.AreEqual(0, _standing.Count,
                "Se anunció el hijo de un Once que ya corrió: ese nodo es transparente y no vuelve " +
                "a ejecutarse nunca.");
        }

        [Test]
        public void UnNodoQueNoSabeDescribirse_NoSeAdivina()
        {
            var root = new AINode_Random
            {
                Options = new List<AINode_Random.Option>
                {
                    new AINode_Random.Option { Weight = 1f, Node = Shot(50) },
                },
            };

            AIIntentWalker.Collect(root, Context(), _standing, _next);

            Assert.AreEqual(0, _standing.Count,
                "Se abrió un nodo que el walker no entiende. Un Random elige al tickear, así que " +
                "cualquier cosa que se anuncie de adentro es una adivinanza — y una promesa " +
                "equivocada es peor que el silencio.");
        }

        private AINode_RangedShot Shot(int damage)
            => new AINode_RangedShot { Damage = damage, Range = 10, Metric = DistanceMetric.Manhattan };

        private AIContext Context() => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = _player,
            SelfMaxHp = 100,
            Attributes = _attrs,
            Grid = _grid,
            DamagePipeline = _pipeline,
        };

        private Guid Register(GridCoord coord, int hp)
        {
            var guid = Guid.NewGuid();
            var ma = new ModifiableAttributes();
            ma.EnsureInitialized();
            ma.SetAttribute<Health>(new Health(hp));
            _attrs.Register(guid, ma);
            _grid.Register(guid, coord);
            return guid;
        }
    }
}
