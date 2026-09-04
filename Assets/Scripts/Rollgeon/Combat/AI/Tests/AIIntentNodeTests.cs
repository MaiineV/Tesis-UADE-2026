using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Entities.Behaviors;
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
        public void ExecuteTelegraph_ConMarcaPendiente_DescribeLaMarcaSinConsumirla()
        {
            // Arrange — la marca congelada del turno anterior, con sus casillas y su número.
            var tiles = new[] { new GridCoord(2, 0), new GridCoord(3, 0) };
            _threat.Mark(_boss, tiles, damage: 25, AttackKind.ScriptedAbility);
            var execute = new AINode_ExecuteTelegraph();

            // Act
            bool described = execute.TryDescribeIntent(Context(), out var intent);

            // Assert
            Assert.IsTrue(described, "El nodo que cobra la marca es el único que puede describirla.");
            CollectionAssert.AreEquivalent(tiles, intent.Tiles,
                "Las casillas no son las de la marca: el dibujo dejaría de coincidir con lo que va a cobrar.");
            Assert.AreEqual(25, intent.Damage, "El número sale de la marca congelada.");
            Assert.AreEqual(AttackKind.ScriptedAbility, intent.Kind);
            Assert.IsTrue(_threat.HasPending(_boss),
                "Leer el aviso se comió la marca: el jefe pasaría el turno sin su ataque, y lo " +
                "habría causado el jugador por pasar el mouse.");
        }

        /// <summary>Con cuatro jefes cobrando marcas, la key genérica los deja a todos diciendo
        /// "Golpe marcado" con la descripción vacía.</summary>
        [Test]
        public void ExecuteTelegraph_ConKeyAutorada_UsaLaSuyaYNoLaGenerica()
        {
            _threat.Mark(_boss, new[] { new GridCoord(2, 0) }, damage: 28, AttackKind.BasicAttack);
            var execute = new AINode_ExecuteTelegraph
            {
                IntentLabelKey = "intent.test_slam_due",
                IntentLabelFallback = "Cañonazo",
            };

            Assert.IsTrue(execute.TryDescribeIntent(Context(), out var intent));
            Assert.AreEqual("intent.test_slam_due", intent.LabelKey);
            Assert.AreEqual("Cañonazo", intent.LabelFallback);
        }

        [Test]
        public void ExecuteTelegraph_SinKeyAutorada_SigueUsandoLaGenerica()
        {
            _threat.Mark(_boss, new[] { new GridCoord(2, 0) }, damage: 25, AttackKind.BasicAttack);

            Assert.IsTrue(new AINode_ExecuteTelegraph().TryDescribeIntent(Context(), out var intent));
            Assert.AreEqual(AIIntentTextKeys.Telegraph, intent.LabelKey,
                "Los jefes que no autoran nada tienen que anunciarse igual que siempre.");
        }

        [Test]
        public void ExecuteTelegraph_SinMarca_NoPrometeNada()
        {
            // Arrange
            var execute = new AINode_ExecuteTelegraph();

            // Act + Assert — sin marca no hay forma, daño ni tipo que afirmar.
            Assert.IsFalse(execute.TryDescribeIntent(Context(), out _),
                "Prometió cobrar una marca que no existe.");
        }

        [Test]
        public void ExecuteTelegraph_ComoRepertorio_NoAfirmaNada()
        {
            // Arrange — incluso con marca puesta: el repertorio describe el kit, y el kit de este
            // nodo es "cobrar lo que otro marcó", que no es un ataque propio que listar.
            _threat.Mark(_boss, new[] { new GridCoord(2, 0) }, damage: 25, AttackKind.ScriptedAbility);
            var execute = new AINode_ExecuteTelegraph();

            // Act + Assert
            Assert.IsFalse(((IAIIntentNode)execute).TryDescribeOption(Context(), out _),
                "Listó 'cobrar la marca' como un ataque del repertorio.");
        }

        [Test]
        public void Behavior_ConDanoConstante_DescribeElGolpe()
        {
            // Arrange — el ataque del bestiario común: un behavior componible con EffDealDamage.
            var node = new AINode_Behavior { Behavior = AttackBehavior(new Rollgeon.Effects.Concretes.EffDealDamage()) };

            // Act
            bool described = node.TryDescribeIntent(Context(), out var intent);

            // Assert — EffDealDamage nace Constant con _baseAmount 10.
            Assert.IsTrue(described, "El golpe del behavior no se describió: el bestiario común " +
                "quedaría sin bloque de próximo turno.");
            Assert.AreEqual(AIIntentTextKeys.Attack, intent.LabelKey);
            Assert.AreEqual(10, intent.Damage);
            Assert.AreEqual(1, intent.Tiles.Count,
                "El golpe afirmado no pinta dónde cae: el panel dice Golpe y el piso calla.");
            Assert.AreEqual(new GridCoord(2, 0), intent.Tiles.First(),
                "La casilla prometida no es la del blanco que la ejecución va a resolver.");
        }

        [Test]
        public void Behavior_ConReaderDeStat_AfirmaElStatVivoDelDueno()
        {
            // Arrange — el camino real de ED_MeleeCardEnemy: FromReader leyendo el Attack del
            // dueño. El número afirmado tiene que ser el stat vivo, no una copia serializada.
            var attack = new Rollgeon.Attributes.Stats.Attack(13);
            _attrs.GetAttributes(_boss).SetAttribute<Rollgeon.Attributes.Stats.Attack>(attack);

            var damage = new Rollgeon.Effects.Concretes.EffDealDamage();
            SetPrivateField(damage, "_damageSource", Rollgeon.Effects.Concretes.DamageSource.FromReader);
            SetPrivateField(damage, "_reader", new Rollgeon.Effects.Readers.ReadEntityStat());
            var node = new AINode_Behavior { Behavior = AttackBehavior(damage) };

            // Act
            bool described = node.TryDescribeIntent(Context(), out var intent);

            // Assert
            Assert.IsTrue(described);
            Assert.AreEqual(13, intent.Damage, "El daño afirmado no es el stat vivo del dueño.");
        }

        [Test]
        public void Behavior_LibroDeEnergia_NoDescribeNada()
        {
            // Arrange — Reset/Charge Energy: administración, no un ataque que anunciar.
            var bookkeeping = new EnemyActionBehavior
            {
                ActionName = "Charge Energy",
                Effects = new List<Rollgeon.Effects.EffectData>
                {
                    new Rollgeon.Effects.EffectData
                    {
                        Effects = new List<Rollgeon.Effects.IEffect>
                            { new Rollgeon.Effects.Concretes.EffModifyIntAttribute() },
                    },
                },
            };
            var node = new AINode_Behavior { Behavior = bookkeeping };

            // Act + Assert
            Assert.IsFalse(node.TryDescribeIntent(Context(), out _),
                "Anunció 'Golpe' por un behavior que solo mueve energía.");
        }

        /// <summary>
        /// El guard de honestidad: la celda sale del selector que la ejecución va a usar, no de
        /// "siempre el jugador". Un behavior que se pega a sí mismo pinta SU casilla.
        /// </summary>
        [Test]
        public void Behavior_ConSelectorPropio_PintaLaCasillaDeEseBlanco()
        {
            var behavior = AttackBehavior(new Rollgeon.Effects.Concretes.EffDealDamage());
            behavior.Effects[0].TargetSelector = new Rollgeon.Combat.AI.Targeting.TargetSelector_Self();
            var node = new AINode_Behavior { Behavior = behavior };

            Assert.IsTrue(node.TryDescribeIntent(Context(), out var intent));
            Assert.AreEqual(new GridCoord(0, 0), intent.Tiles.First(),
                "Pintó la casilla del jugador para un golpe cuyo selector apunta a otro blanco.");
        }

        /// <summary>Sin grilla no hay celda que afirmar — pero el golpe se anuncia igual: la
        /// tarjeta del panel no depende de poder pintar el piso.</summary>
        [Test]
        public void Behavior_SinGrilla_DescribeSinCasillas()
        {
            var node = new AINode_Behavior { Behavior = AttackBehavior(new Rollgeon.Effects.Concretes.EffDealDamage()) };
            var context = Context();
            context.Grid = null;

            Assert.IsTrue(node.TryDescribeIntent(context, out var intent),
                "Sin grilla el golpe dejó de anunciarse: el panel perdería la tarjeta entera.");
            Assert.AreEqual(0, intent.Tiles.Count);
        }

        private static EnemyActionBehavior AttackBehavior(Rollgeon.Effects.Concretes.EffDealDamage damage)
            => new EnemyActionBehavior
            {
                ActionName = "Attack",
                Effects = new List<Rollgeon.Effects.EffectData>
                {
                    new Rollgeon.Effects.EffectData
                    {
                        Effects = new List<Rollgeon.Effects.IEffect> { damage },
                    },
                },
            };

        // Los campos del effect son privados a propósito (autorado Odin): el test arma el mismo
        // estado que un asset deserializado, no un camino público nuevo.
        private static void SetPrivateField(object target, string field, object value)
            => target.GetType()
                .GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(target, value);

        /// <summary>El nodo que ANUNCIA sigue callado por default: ancla su forma al tickear y detrás
        /// de la fuga, un paso posterior del árbol puede descartarla en el mismo turno, y una forma
        /// dispersa consumiría el azar del turno real sólo para dibujar un preview. Quien la
        /// describe es el nodo que la consume, leyéndola ya congelada.</summary>
        [Test]
        public void TelegraphMark_SinKeyAutorada_NoDescribeIntencion()
        {
            Assert.IsFalse(((IAIIntentNode)ConeMark()).TryDescribeIntent(Context(), out _),
                "El aviso volvió a hablar sin que nadie lo autore: los cuatro jefes que lo usan " +
                "ganan una tarjeta que promete una forma que todavía no existe.");
        }

        /// <summary>La excepción: el jefe en cuyo turno marcar ES la acción, que si no deja el panel
        /// vacío. Aun autorado no promete casillas.</summary>
        [Test]
        public void TelegraphMark_ConKeyAutorada_DescribeElGolpeSinCasillas()
        {
            var mark = ConeMark();
            mark.Damage = 28;
            mark.IntentLabelKey = "intent.test_slam";
            mark.IntentLabelFallback = "Cañonazo";

            Assert.IsTrue(((IAIIntentNode)mark).TryDescribeIntent(Context(), out var intent));
            Assert.AreEqual("intent.test_slam", intent.LabelKey);
            Assert.AreEqual(28, intent.Damage);
            CollectionAssert.IsEmpty(intent.Tiles,
                "Todavía te queda un turno para moverte: prometer casillas ahora es una estimación.");
        }

        [Test]
        public void Mark_MarcaSiempre_YNuncaPinta()
        {
            // Act
            ConeMark().Tick(Context());

            // Assert — regla Mewgenics del spec de tooltips: los tiles de ataque solo se
            // dibujan con el mouse encima. Marcar y dibujar quedaron separados para siempre.
            Assert.IsTrue(_threat.HasPending(_boss),
                "Dejó de marcar: sin marca no hay nada que mostrar en el hover ni nada que " +
                "prender después.");
            Assert.AreEqual(0, _overlay.Shown.Count,
                "Pintó al marcar: el paño tiene que quedar limpio hasta que el jugador consulte.");
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
