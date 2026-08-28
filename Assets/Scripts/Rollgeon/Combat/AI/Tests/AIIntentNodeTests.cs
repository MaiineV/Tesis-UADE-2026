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
using Rollgeon.PreConditions.Concretes;
using Rollgeon.Tiles;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Qué promete cada nodo cuando el jugador pasa el mouse — y, sobre todo, qué NO promete.
    /// </summary>
    [TestFixture]
    public class AIIntentNodeTests
    {
        private AttributesManager _attrs;
        private GridManager _grid;
        private DamagePipeline _pipeline;
        private ThreatenedAreaService _threat;
        private SpyThreatOverlay _overlay;
        private Guid _boss;
        private Guid _player;

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

            _overlay = new SpyThreatOverlay();
            ServiceLocator.AddService<IThreatOverlayService>(_overlay, ServiceScope.Global);

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
        public void IgniteArea_SinMarcaPendiente_NoPrometeNada()
        {
            var ignite = new AINode_IgniteArea { Definition = FireTile(6, 10) };

            Assert.IsFalse(ignite.TryDescribeIntent(Context(), out _),
                "Prometió quemar sin tener nada marcado. Ese es exactamente el turno en que al " +
                "jefe le cancelaron la banda al cruzar el 50%: si el aviso re-dedujera la forma, " +
                "anunciaría un incendio que no va a pasar.");
        }

        [Test]
        public void IgniteArea_DescribeLasCasillasCongeladasYElDanoDeLaMarca()
        {
            var tiles = new[] { new GridCoord(3, 3), new GridCoord(4, 3) };
            _threat.Mark(_boss, tiles, damage: 7, AttackKind.Environmental);

            var ignite = new AINode_IgniteArea { Definition = FireTile(6, 10), DurationRounds = 4 };
            Assert.IsTrue(ignite.TryDescribeIntent(Context(), out var intent));

            CollectionAssert.AreEquivalent(tiles, intent.Tiles,
                "Las casillas no son las que el jefe ya dejó marcadas. Recalcularlas puede mover " +
                "el área, y el dibujo dejaría de coincidir con lo que se va a prender.");
            Assert.AreEqual(7, intent.Damage, "El número sale de la marca, no del nodo que prende.");
            Assert.IsNull(intent.Leaves,
                "El aviso volvió a cargar lo que deja: la tarjeta arrastraría 'Deja fuego: …' — " +
                "los números que, apenas prende, ya muestra Fire Tiles.");
        }

        [Test]
        public void IgniteArea_LeerLaIntencion_NoSeConsumeLaMarca()
        {
            _threat.Mark(_boss, new[] { new GridCoord(3, 3) }, damage: 7, AttackKind.Environmental);
            var ignite = new AINode_IgniteArea { Definition = FireTile(6, 10) };

            ignite.TryDescribeIntent(Context(), out _);

            Assert.IsTrue(_threat.HasPending(_boss),
                "Leer el aviso se comió la marca: el jefe pasaría el turno sin su ataque, y lo " +
                "habría causado el jugador por pasar el mouse.");
        }

        [Test]
        public void RangedShot_DescribeLaCasillaDelJugador()
        {
            var shot = new AINode_RangedShot { Damage = 18, Range = 24, Metric = DistanceMetric.Manhattan };

            Assert.IsTrue(shot.TryDescribeIntent(Context(), out var intent));
            CollectionAssert.AreEquivalent(new[] { new GridCoord(2, 0) }, intent.Tiles,
                "El disparo no se pinta sobre el jugador, que es lo único que un ataque a " +
                "distancia puede prometer.");
            Assert.AreEqual(18, intent.Damage);
        }

        [Test]
        public void RangedShot_FueraDeAlcance_NoPrometeNada()
        {
            var shot = new AINode_RangedShot { Damage = 18, Range = 1, Metric = DistanceMetric.Manhattan };

            Assert.IsFalse(shot.TryDescribeIntent(Context(), out _),
                "Prometió un disparo que el propio CanFire va a rechazar: el aviso tiene que " +
                "pasar por el mismo gate que el tick.");
        }

        [Test]
        public void IgniteArea_ComoRepertorio_SeAfirmaSinMarcaPendiente()
        {
            // Arrange — sin marca: el estado exacto en que la intención viva contesta false.
            var ignite = new AINode_IgniteArea { Definition = FireTile(6, 10) };

            // Act
            bool described = ((IAIIntentNode)ignite).TryDescribeOption(Context(), out var intent);

            // Assert — la marca es el estado de ESTE ciclo; "sabe prender un cono" no depende
            // de ella. Y sin daño: el número vive en la marca, prometer otro sería inventarlo.
            Assert.IsTrue(described,
                "El repertorio calló la bola de fuego: el panel listaría dos ataques posibles " +
                "cuando el jefe tiene tres.");
            Assert.AreEqual(AIIntentTextKeys.Ignite, intent.LabelKey);
            Assert.AreEqual(0, intent.Damage);
        }

        [Test]
        public void RangedShot_ComoRepertorio_SeAfirmaFueraDeAlcance()
        {
            // Arrange — rango 1 con el jugador a 2: la intención viva contesta false.
            var shot = new AINode_RangedShot { Damage = 18, Range = 1, Metric = DistanceMetric.Manhattan };

            // Act
            bool described = ((IAIIntentNode)shot).TryDescribeOption(Context(), out var intent);

            // Assert — dónde está parado el jugador es el estado de este turno, no el kit.
            Assert.IsTrue(described,
                "El repertorio calló el disparo por estar fuera de rango: 'qué sabe hacer' no " +
                "puede depender de dónde esté parado el jugador en el hover.");
            Assert.AreEqual(18, intent.Damage,
                "El daño del disparo sí es del kit y tiene que viajar en la tarjeta.");
        }

        [Test]
        public void BombField_NoPrometeCasillas_PorqueLasRanurasSeSorteanAlSembrar()
        {
            var bombs = ScriptableObject.CreateInstance<Rollgeon.Combat.Rooms.RoomObjectDefinitionSO>();
            var field = new Rollgeon.Combat.Rooms.AINode_BombField { Definition = bombs, Count = 3 };

            Assert.IsTrue(field.TryDescribeIntent(Context(), out var intent));
            Assert.AreEqual(0, intent.Tiles.Count,
                "Prometió dónde van a caer las bombas. Las ranuras las sortea el spawner en el " +
                "momento de sembrar, así que cualquier casilla dibujada acá es una invención.");
            Assert.AreEqual(3, intent.Amount, "No dice cuántas bombas siembra.");

            ScriptableObject.DestroyImmediate(bombs);
        }

        [Test]
        public void TelegraphMark_NoDescribeIntencion()
        {
            Assert.IsFalse(typeof(IAIIntentNode).IsAssignableFrom(typeof(AINode_TelegraphMark)),
                "El nodo que ANUNCIA no puede describirse: ancla su forma al tickear y detrás de " +
                "la fuga, un paso posterior del árbol puede descartarla en el mismo turno, y una " +
                "forma dispersa consumiría el azar del turno real sólo para dibujar un preview. " +
                "Lo describe el nodo que la consume, leyéndola ya congelada.");
        }

        [Test]
        public void ConHoverOnlyPaint_MarcaIgual_PeroNoPinta()
        {
            var mark = ConeMark();
            mark.HoverOnlyPaint = true;

            mark.Tick(Context());

            Assert.IsTrue(_threat.HasPending(_boss),
                "Dejó de marcar: el flag decide quién DIBUJA, no si el jefe amenaza. Sin marca no " +
                "hay nada que mostrar en el hover ni nada que prender después.");
            Assert.AreEqual(0, _overlay.Shown.Count,
                "Pintó igual: el paño tiene que quedar limpio hasta que el jugador consulte.");
        }

        [Test]
        public void SinElFlag_SiguePintandoComoSiempre()
        {
            ConeMark().Tick(Context());

            Assert.AreEqual(1, _overlay.Shown.Count,
                "Dejó de pintar sin que nadie se lo pidiera. El default tiene que ser el " +
                "comportamiento de siempre: Odin no corre inicializadores de campo al " +
                "deserializar, así que los cinco jefes ya autorados reciben el flag en false y " +
                "esta es la prueba de que eso los deja intactos.");
        }

        private AINode_TelegraphMark ConeMark() => new AINode_TelegraphMark
        {
            Shape = ThreatShape.DirectionalCone,
            Size = 0,
            Depth = 3,
            Damage = 0,
            Kind = AttackKind.Environmental,
        };

        private static SpecialTileDefinitionSO FireTile(int enter, int turnStart)
        {
            var def = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            def.EnterDamage = enter;
            def.TurnStartDamage = turnStart;
            return def;
        }

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

        /// <summary>Observa qué se pintó. Los otros fixtures lo tienen anidado y privado.</summary>
        private sealed class SpyThreatOverlay : IThreatOverlayService
        {
            public readonly List<Guid> Shown = new List<Guid>();
            public readonly List<Guid> Cleared = new List<Guid>();

            public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles) => Shown.Add(sourceGuid);
            public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles, Color tint) => Shown.Add(sourceGuid);
            public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles, ThreatOverlayState state,
                Color? tint = null) => Shown.Add(sourceGuid);
            public void Clear(Guid sourceGuid) => Cleared.Add(sourceGuid);
            public void ClearAll() => Cleared.Clear();
        }
    }
}
