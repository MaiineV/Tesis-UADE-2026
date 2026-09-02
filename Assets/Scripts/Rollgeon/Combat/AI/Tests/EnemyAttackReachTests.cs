using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Grid;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Qué celdas afirma <see cref="EnemyAttackReach"/> como alcance del arma de un enemigo —
    /// y cuáles no.
    /// </summary>
    [TestFixture]
    public sealed class EnemyAttackReachTests
    {
        private AttributesManager _attrs;
        private GridManager _grid;
        private HashSet<GridCoord> _reach;

        private Guid _enemy;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            _attrs = new AttributesManager();
            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            _reach = new HashSet<GridCoord>();

            _enemy = Register(new GridCoord(4, 4), hp: 100);
            _player = Register(new GridCoord(7, 4), hp: 100);
        }

        [TearDown]
        public void TearDown() => _attrs?.Dispose();

        [Test]
        public void GateMeleeManhattan1_PintaSoloLasCuatroOrtogonales()
        {
            var root = Gate(1, MeleeAttack());

            EnemyAttackReach.Collect(root, Context(), _reach);

            CollectionAssert.AreEquivalent(Cells((3, 4), (5, 4), (4, 3), (4, 5)), _reach,
                "El golpe melee de rango 1 Manhattan pega a las cuatro ortogonales y a nada más. " +
                "Con una celda de menos el jugador se para 'seguro' donde le pegan; con una de " +
                "más, regala una casilla que era segura.");
        }

        [Test]
        public void GateEntityInRange_ElDelMeleeAutorado_PintaSoloLasCuatroOrtogonales()
        {
            // ED_MeleeCardEnemy no gatea con PcTargetInRange sino con PCEntityInRange
            // (MaxRange 1, Manhattan, ancla-a-ancla): sin reconocerlo, el hover del melee
            // salia sin alcance pintado.
            var root = new AINode_If { Then = MeleeAttack() };
            root.Conditions.Add(new PCEntityInRange { MaxRange = 1, Metric = DistanceMetric.Manhattan });

            EnemyAttackReach.Collect(root, Context(), _reach);

            CollectionAssert.AreEquivalent(Cells((3, 4), (5, 4), (4, 3), (4, 5)), _reach,
                "El gate PCEntityInRange del melee autorado tiene que pintar igual que un " +
                "PcTargetInRange de rango 1: las cuatro ortogonales y nada mas.");
        }

        [Test]
        public void GateChebyshev1_PintaLasOchoDeAlrededor()
        {
            var root = Gate(1, MeleeAttack(), metric: DistanceMetric.Chebyshev);

            EnemyAttackReach.Collect(root, Context(), _reach);

            CollectionAssert.AreEquivalent(
                Cells((3, 3), (4, 3), (5, 3), (3, 4), (5, 4), (3, 5), (4, 5), (5, 5)), _reach,
                "El sweeper gatea con Chebyshev: sus diagonales también pegan. Dibujarle el " +
                "rombo Manhattan le promete al jugador que la diagonal es segura, y no lo es.");
        }

        [Test]
        public void RangedShot_PintaElRomboManhattanDesdeSuCelda()
        {
            var root = new AINode_RangedShot { Damage = 10, Range = 4 };

            EnemyAttackReach.Collect(root, Context(), _reach);

            Assert.AreEqual(40, _reach.Count,
                "El rombo Manhattan de radio 4 desde (4,4) en una sala 9×9 son exactamente 40 " +
                "celdas (2·r·(r+1)), sin la del propio enemigo.");
            Assert.IsTrue(_reach.Contains(new GridCoord(0, 4)) && _reach.Contains(new GridCoord(4, 8)),
                "Las puntas del rombo (distancia exacta = Range) están dentro del alcance: el " +
                "disparo pega a ≤ Range, no a < Range.");
            Assert.IsTrue(_reach.All(c => c.Manhattan(new GridCoord(4, 4)) <= 4),
                "Apareció una celda a más de Range del enemigo: eso ya no es su alcance, es " +
                "otra cosa.");
        }

        [Test]
        public void GateConRangoDeFicha_LeeElAtributoYNoElNumeroDelNodo()
        {
            _attrs.GetAttributes(_enemy).SetAttribute<AttackRange>(new AttackRange(3));
            var root = Gate(1, MeleeAttack(), useOwnerRange: true);

            EnemyAttackReach.Collect(root, Context(), _reach);

            Assert.IsTrue(_reach.Contains(new GridCoord(1, 4)),
                "Con UseOwnerAttackRange y AttackRange 3 en la ficha, la celda a distancia 3 " +
                "está en alcance. Si se lee el Range=1 del nodo en vez del atributo, el dibujo " +
                "miente exactamente igual que antes del fix de PcTargetInRange.");
            Assert.IsFalse(_reach.Contains(new GridCoord(0, 4)),
                "La celda a distancia 4 quedó pintada con rango efectivo 3: se está sumando el " +
                "rango del nodo al de la ficha en vez de reemplazarlo.");
        }

        [Test]
        public void GateDiagonalOnly_DejaSoloLasDiagonalesExactas()
        {
            var root = Gate(4, MeleeAttack(), metric: DistanceMetric.Chebyshev,
                            alignment: TargetAlignment.DiagonalOnly);

            EnemyAttackReach.Collect(root, Context(), _reach);

            var esperadas = new List<GridCoord>();
            for (int k = 1; k <= 4; k++)
            {
                esperadas.Add(new GridCoord(4 + k, 4 + k));
                esperadas.Add(new GridCoord(4 - k, 4 - k));
                esperadas.Add(new GridCoord(4 + k, 4 - k));
                esperadas.Add(new GridCoord(4 - k, 4 + k));
            }
            CollectionAssert.AreEquivalent(esperadas, _reach,
                "El skirmisher sólo pega en diagonal exacta. Una celda fuera de la X le dice al " +
                "jugador que ahí le pegan, y ahí es justamente donde tiene que pararse.");
        }

        [Test]
        public void GateFilaOColumna_DejaSoloLaCruzOrtogonal()
        {
            var root = Gate(8, MeleeAttack(), alignment: TargetAlignment.SameRowOrColumn);

            EnemyAttackReach.Collect(root, Context(), _reach);

            var esperadas = new List<GridCoord>();
            for (int i = 0; i < 9; i++)
            {
                if (i != 4) esperadas.Add(new GridCoord(i, 4));
                if (i != 4) esperadas.Add(new GridCoord(4, i));
            }
            CollectionAssert.AreEquivalent(esperadas, _reach,
                "El sniper sólo pega en su fila o columna. Pintarle el rombo entero convierte " +
                "una amenaza esquivable con un paso lateral en 'toda la sala es roja'.");
        }

        [Test]
        public void ConLineaDeVisionExigida_PintaIgualConUnBloqueoEnElMedio()
        {
            Register(new GridCoord(5, 4), hp: 10);
            var root = Gate(8, MeleeAttack(), alignment: TargetAlignment.SameRowOrColumn,
                            lineOfSight: true);

            EnemyAttackReach.Collect(root, Context(), _reach);

            Assert.IsTrue(_reach.Contains(new GridCoord(7, 4)),
                "La celda detrás del blocker se dejó de pintar. La línea de visión se ignora a " +
                "propósito: el blocker puede morir o correrse dentro del turno del jugador, y un " +
                "alcance que pinta de menos es una promesa de seguridad falsa.");
        }

        [Test]
        public void ElGateDelHeal_NoAportaAlcance()
        {
            var root = Gate(2, HealBehavior());

            EnemyAttackReach.Collect(root, Context(), _reach);

            Assert.IsEmpty(_reach,
                "El radio de curación del healer se pintó como si fuera un arma. Detrás de un " +
                "PcTargetInRange también viven acciones que no pegan: sólo cuenta el gate cuyo " +
                "Then hace daño.");
        }

        [Test]
        public void UnGateSinAtaqueEnElThen_NoAportaAlcance()
        {
            var root = Gate(3, new AINode_Sequence());

            EnemyAttackReach.Collect(root, Context(), _reach);

            Assert.IsEmpty(_reach,
                "Un gate de rango cuyo Then no ataca (la fuga del Croupier: 'si está cerca, " +
                "teleport') se pintó como alcance de arma. El jugador leería 'acá me pega' " +
                "donde en realidad el jefe huye.");
        }

        [Test]
        public void GateSobreUnTelegraph_AportaElAlcanceDelAviso()
        {
            var root = Gate(6, new AINode_TelegraphMark { Damage = 25 });

            EnemyAttackReach.Collect(root, Context(), _reach);

            Assert.IsTrue(_reach.Contains(new GridCoord(0, 4)) && _reach.Contains(new GridCoord(4, 8)),
                "Sniper, artillery y mago atacan con un telegraph dentro del gate: el aviso con " +
                "daño ES su golpe aunque se cobre al turno siguiente. Sin esto, los tres " +
                "arquetipos ranged-telegraph quedan mudos en el piso.");
            Assert.IsFalse(_reach.Contains(new GridCoord(0, 0)),
                "La esquina a distancia 8 entró en un gate de rango 6.");
        }

        [Test]
        public void UnAtaqueAnidadoEnUnSequenceDelThen_Cuenta()
        {
            var root = Gate(5, new AINode_Sequence
            {
                Children = new List<AIDecisionNode> { MeleeAttack() },
            });

            EnemyAttackReach.Collect(root, Context(), _reach);

            Assert.IsTrue(_reach.Contains(new GridCoord(0, 4)),
                "El kiter envuelve su disparo en un Sequence (atacar y después alejarse): el " +
                "ataque hay que buscarlo en TODO el subtree del Then, no en el hijo directo.");
        }

        [Test]
        public void DosAtaques_PintanLaUnionDeAlcances()
        {
            var root = new AINode_Sequence
            {
                Children = new List<AIDecisionNode>
                {
                    Gate(2, MeleeAttack(), metric: DistanceMetric.Chebyshev,
                         alignment: TargetAlignment.DiagonalOnly),
                    new AINode_RangedShot { Damage = 10, Range = 1 },
                },
            };

            EnemyAttackReach.Collect(root, Context(), _reach);

            CollectionAssert.AreEquivalent(
                Cells((3, 4), (5, 4), (4, 3), (4, 5),
                      (3, 3), (5, 5), (3, 5), (5, 3),
                      (2, 2), (6, 6), (2, 6), (6, 2)), _reach,
                "Con dos ataques de formas distintas se pinta la unión: las cuatro ortogonales " +
                "del disparo más las diagonales del tajo. Si gana uno solo, la mitad del peligro " +
                "queda invisible.");
        }

        [Test]
        public void AlcanceQueCubreTodaLaSala_SeDegradaANada()
        {
            var root = new AINode_RangedShot { Damage = 10, Range = 32 };

            EnemyAttackReach.Collect(root, Context(), _reach);

            Assert.IsEmpty(_reach,
                "Un alcance que es la sala entera se pintó igual. Todo rojo informa lo mismo " +
                "que nada rojo, pero cuesta un quad por casilla y entierra los avisos que sí " +
                "importan.");
        }

        [Test]
        public void LasCeldasDelPropioEnemigo_QuedanAfuera()
        {
            var root = new AINode_RangedShot { Damage = 10, Range = 2 };

            EnemyAttackReach.Collect(root, Context(), _reach);

            Assert.IsFalse(_reach.Contains(new GridCoord(4, 4)),
                "La celda donde está parado el enemigo se pintó como alcanzable. Nadie puede " +
                "pararse ahí: es ruido que además tapa al propio enemigo.");
        }

        [Test]
        public void ConGrafoVacio_NoPintaNada()
        {
            var bare = new GridManager();
            bare.Register(_enemy, new GridCoord(4, 4));
            var context = Context();
            context.Grid = bare;
            var root = new AINode_RangedShot { Damage = 10, Range = 4 };

            EnemyAttackReach.Collect(root, context, _reach);

            Assert.IsEmpty(_reach,
                "Con el grafo vacío (el stub 'infinito' donde InBounds contesta true a todo) se " +
                "pintaron celdas: no hay sala declarada, así que no hay dónde pintar.");
        }

        [Test]
        public void ArbolSinGatesNiDisparos_QuedaVacioSinError()
        {
            var root = new AINode_Sequence();

            EnemyAttackReach.Collect(root, Context(), _reach);

            Assert.IsEmpty(_reach,
                "Un árbol sin gates de rango ni disparos (jefes por telegraph puro, o el " +
                "healer cuyo tiro no tiene gate) no afirma alcance: vacío significa 'no se " +
                "sabe', nunca una estimación.");
        }

        private AIContext Context() => new AIContext
        {
            SelfGuid = _enemy,
            PlayerGuid = _player,
            Attributes = _attrs,
            Grid = _grid,
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

        private static AINode_If Gate(int range, AIDecisionNode then,
                                      DistanceMetric metric = DistanceMetric.Manhattan,
                                      TargetAlignment alignment = TargetAlignment.Any,
                                      bool useOwnerRange = false, bool lineOfSight = false)
            => new AINode_If
            {
                Conditions = new List<BasePreCondition>
                {
                    new PcTargetInRange
                    {
                        Range = range,
                        Metric = metric,
                        Alignment = alignment,
                        UseOwnerAttackRange = useOwnerRange,
                        RequireLineOfSight = lineOfSight,
                    },
                },
                Then = then,
            };

        private static AINode_Behavior MeleeAttack()
            => new AINode_Behavior
            {
                Behavior = new EnemyActionBehavior
                {
                    ActionName = "Golpe",
                    Effects = new List<EffectData>
                    {
                        new EffectData { Effects = new List<IEffect> { new EffDealDamage() } },
                    },
                },
            };

        private static AINode_Behavior HealBehavior()
            => new AINode_Behavior
            {
                Behavior = new EnemyActionBehavior
                {
                    ActionName = "Cura",
                    Effects = new List<EffectData> { new EffectData() },
                },
            };

        private static List<GridCoord> Cells(params (int x, int y)[] coords)
            => coords.Select(c => new GridCoord(c.x, c.y)).ToList();
    }
}
