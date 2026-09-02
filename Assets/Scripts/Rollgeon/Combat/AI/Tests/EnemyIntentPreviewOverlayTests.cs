using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Dice;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Qué queda dibujado en la grilla mientras el mouse está sobre un enemigo — y qué no.
    /// </summary>
    [TestFixture]
    public sealed class EnemyIntentPreviewOverlayTests
    {
        private EnemyIntentPreviewOverlay _preview;
        private FakeIntentService _intents;
        private SpyThreatOverlay _overlay;
        private StubPlayerService _players;

        private Guid _boss;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();

            _boss = Guid.NewGuid();
            _player = Guid.NewGuid();

            _players = new StubPlayerService { PlayerGuid = _player };
            ServiceLocator.AddService<IPlayerService>(_players);

            _intents = new FakeIntentService();
            ServiceLocator.AddService<IEnemyIntentService>(_intents);

            _overlay = new SpyThreatOverlay();
            ServiceLocator.AddService<IThreatOverlayService>(_overlay, ServiceScope.Global);

            _preview = new EnemyIntentPreviewOverlay();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void UnaCeldaEnLasDosListas_SePintaUnaSolaVez()
        {
            _intents.Standing.Add(Intent(Cells((2, 2), (3, 2))));
            _intents.Next.Add(Intent(Cells((3, 2), (4, 2))));

            _preview.Show(_boss);

            var todas = _overlay.Painted.SelectMany(p => p.Cells).ToList();
            Assert.AreEqual(todas.Distinct().Count(), todas.Count,
                "Una celda se llevó dos quads apilados. Se dibujan uno encima del otro y la casilla " +
                "se lee más brillante que sus vecinas, o sea como si fuera otra amenaza.");
        }

        [Test]
        public void LaCeldaCompartida_SeQuedaConLoQueYaEstaPuesto()
        {
            var compartida = new GridCoord(3, 2);
            _intents.Standing.Add(Intent(Cells((2, 2), (3, 2))));
            _intents.Next.Add(Intent(Cells((3, 2), (4, 2))));

            _preview.Show(_boss);

            var conLaCompartida = _overlay.Painted.Single(p => p.Cells.Contains(compartida));
            Assert.AreEqual(ThreatOverlayState.Marked, conLaCompartida.State,
                "La casilla que va a arder Y entra en el próximo ataque se quedó con el aviso de lo " +
                "que todavía puede caer en otro lado. Un quad es un solo aviso: en el empate tiene " +
                "que ganar lo que el jefe ya congeló, que es lo único seguro.");
        }

        [Test]
        public void LoPuestoYLoProximo_SeDibujanEnCanalesDistintos()
        {
            _intents.Standing.Add(Intent(Cells((1, 1))));
            _intents.Next.Add(Intent(Cells((5, 5))));

            _preview.Show(_boss);

            Assert.AreEqual(ThreatOverlayState.Marked,
                _overlay.Painted.Single(p => p.Source == EnemyIntentPreviewOverlay.StandingSource(_boss)).State);
            Assert.AreEqual(ThreatOverlayState.Incoming,
                _overlay.Painted.Single(p => p.Source == EnemyIntentPreviewOverlay.NextSource(_boss)).State,
                "Lo que viene se dibuja con el mismo aviso que lo que ya está puesto: el jugador no " +
                "puede distinguir dónde no pararse ahora de dónde no pararse después.");
        }

        [Test]
        public void ShowForSubject_PintaSoloLaCruzDeEsaBomba()
        {
            var bombaA = Guid.NewGuid();
            var bombaB = Guid.NewGuid();
            _intents.Standing.Add(Intent(Cells((1, 1)), subject: bombaA));
            _intents.Standing.Add(Intent(Cells((7, 7)), subject: bombaB));

            _preview.ShowForSubject(_boss, bombaA);

            var pintadas = _overlay.Painted.SelectMany(p => p.Cells).ToList();
            CollectionAssert.AreEquivalent(new[] { new GridCoord(1, 1) }, pintadas,
                "El hover de una bomba mostró las cruces de sus hermanas. Con las cuatro encendidas " +
                "a la vez no se lee ninguna.");
        }

        [Test]
        public void ElHoverDelJefe_NoPintaLasCrucesDeSusBombas()
        {
            var bomba = Guid.NewGuid();
            _intents.Standing.Add(Intent(Cells((1, 1))));
            _intents.Standing.Add(Intent(Cells((2, 2)), subject: _boss));
            _intents.Standing.Add(Intent(Cells((7, 7)), subject: bomba));

            _preview.Show(_boss);

            var pintadas = _overlay.Painted.SelectMany(p => p.Cells).ToList();
            CollectionAssert.AreEquivalent(Cells((1, 1), (2, 2)), pintadas,
                "El hover del jefe encendió el daño de sus bombas. Cada bomba ya cuenta su propia " +
                "cruz en su propio hover: acá se lee solo lo que el jefe hace con su cuerpo.");
        }

        [Test]
        public void FueraDelTurnoDelJugador_NoPintaNada()
        {
            _intents.CanRead = false;
            _intents.Standing.Add(Intent(Cells((1, 1))));

            _preview.Show(_boss);

            Assert.AreEqual(0, _overlay.Painted.Count,
                "Pintó una predicción que el servicio se negó a dar. Durante el turno del jefe su " +
                "ciclo ya avanzó: lo que se dibuje ahí es una promesa vencida.");
        }

        [Test]
        public void CuandoArrancaElTurnoDelJefe_SeApagaYVuelveEnElDelJugador()
        {
            _intents.Standing.Add(Intent(Cells((1, 1))));
            _preview.Show(_boss);
            _overlay.Painted.Clear();

            EventManager.Trigger(EventName.OnTurnStarted, _boss);
            Assert.AreEqual(0, _overlay.Painted.Count, "Repintó en el turno del jefe.");
            CollectionAssert.Contains(_overlay.Cleared, EnemyIntentPreviewOverlay.StandingSource(_boss),
                "Se quedó en pantalla mientras el jefe juega: con el mouse quieto encima, el dibujo " +
                "sigue mostrando la predicción del turno anterior mientras el jefe la ejecuta.");

            EventManager.Trigger(EventName.OnTurnStarted, _player);
            Assert.AreEqual(1, _overlay.Painted.Count,
                "No volvió al recuperar el turno el jugador. El mouse nunca se movió: para él, " +
                "el dibujo se apagó solo y no vuelve.");
        }

        [Test]
        public void MoverElMouseDeUnEnemigoAOtro_ApagaElAnterior()
        {
            var otro = Guid.NewGuid();
            _intents.Standing.Add(Intent(Cells((1, 1))));
            _preview.Show(_boss);

            _preview.Show(otro);

            CollectionAssert.Contains(_overlay.Cleared, EnemyIntentPreviewOverlay.StandingSource(_boss),
                "El paño quedó con las amenazas de los dos. El mouse puede saltar de un enemigo al " +
                "de al lado sin pasar por el vacío, así que nadie más apaga el anterior.");
        }

        [Test]
        public void ElAlcanceDelAtaque_SePintaEnSuPropioCanalYEnRojo()
        {
            _intents.Reach.UnionWith(Cells((2, 3), (4, 3), (3, 2), (3, 4)));

            _preview.Show(_boss);

            var alcance = _overlay.Painted.Single(
                p => p.Source == EnemyIntentPreviewOverlay.RangeSource(_boss));
            CollectionAssert.AreEquivalent(Cells((2, 3), (4, 3), (3, 2), (3, 4)), alcance.Cells,
                "El alcance del arma no llegó entero a la grilla: el jugador no puede leer " +
                "hasta dónde le pegan antes de decidir cuánto acercarse.");
            Assert.AreEqual(ThreatOverlayState.Incoming, alcance.State,
                "El alcance se dibujó con la alarma de lo ya comprometido. Es el aviso menos " +
                "urgente de los tres: pulso lento de fondo, no la marca que ya está congelada.");
            Assert.AreEqual(ThreatTelegraphOverlay.ReachTint, alcance.Tint,
                "El alcance salió sin su rojo propio. Con el tint default del estado se lee " +
                "idéntico al próximo golpe (magenta) o a lo comprometido (rojo de DefaultTint).");
        }

        [Test]
        public void ElAlcance_NoTapaLoQueYaEstaPuestoNiLoProximo()
        {
            _intents.Standing.Add(Intent(Cells((2, 2))));
            _intents.Next.Add(Intent(Cells((3, 3))));
            _intents.Reach.UnionWith(Cells((2, 2), (3, 3), (4, 4)));

            _preview.Show(_boss);

            var alcance = _overlay.Painted.Single(
                p => p.Source == EnemyIntentPreviewOverlay.RangeSource(_boss));
            CollectionAssert.AreEquivalent(Cells((4, 4)), alcance.Cells,
                "Una celda comprometida o del próximo golpe se llevó además el quad del " +
                "alcance: dos avisos apilados se leen como una amenaza distinta. En el empate " +
                "el alcance pierde — es el menos urgente de los tres.");
        }

        [Test]
        public void Clear_ApagaTambienElCanalDeAlcance()
        {
            _intents.Reach.UnionWith(Cells((1, 2)));
            _preview.Show(_boss);

            _preview.Clear();

            CollectionAssert.Contains(_overlay.Cleared, EnemyIntentPreviewOverlay.RangeSource(_boss),
                "El rojo del alcance quedó encendido después de sacar el mouse. Clear sólo " +
                "conocía los canales viejos y el nuevo quedó huérfano en el paño.");
        }

        [Test]
        public void ElHoverDeUnaBomba_NoPintaElAlcanceDelDueno()
        {
            var bomba = Guid.NewGuid();
            _intents.Standing.Add(Intent(Cells((1, 1)), subject: bomba));
            _intents.Reach.UnionWith(Cells((2, 3), (4, 3)));

            _preview.ShowForSubject(_boss, bomba);

            Assert.IsFalse(_overlay.Painted.Any(
                    p => p.Source == EnemyIntentPreviewOverlay.RangeSource(_boss)),
                "El hover de una bomba encendió el alcance del arma de su dueño. La bomba " +
                "cuenta su cruz y nada más; el arma del jefe se lee sobre su cuerpo.");
        }

        [Test]
        public void SiElServicioNoSabeDeAlcances_ElRestoSePintaIgual()
        {
            ServiceLocator.Clear();
            ServiceLocator.AddService<IPlayerService>(_players);
            var minimo = new MinimalIntentService();
            minimo.Standing.Add(Intent(Cells((1, 1))));
            ServiceLocator.AddService<IEnemyIntentService>(minimo);
            ServiceLocator.AddService<IThreatOverlayService>(_overlay, ServiceScope.Global);
            var preview = new EnemyIntentPreviewOverlay();

            preview.Show(_boss);

            Assert.IsTrue(_overlay.Painted.Any(
                    p => p.Source == EnemyIntentPreviewOverlay.StandingSource(_boss)),
                "Un servicio que sólo sabe de intents dejó de pintar lo que sí sabía: el " +
                "default de TryReadReach existe para que un lector parcial siga funcionando.");
            Assert.IsFalse(_overlay.Painted.Any(
                    p => p.Source == EnemyIntentPreviewOverlay.RangeSource(_boss)),
                "Sin implementación de alcance apareció igual un canal de alcance: alguien " +
                "está inventando celdas que el servicio nunca afirmó.");
        }

        private static AIIntent Intent(IReadOnlyCollection<GridCoord> tiles, Guid subject = default)
            => new AIIntent("test.intent", "prueba", 10, AttackKind.Environmental, tiles,
                            subjectGuid: subject);

        private static List<GridCoord> Cells(params (int x, int y)[] coords)
            => coords.Select(c => new GridCoord(c.x, c.y)).ToList();

        private sealed class FakeIntentService : IEnemyIntentService
        {
            public bool CanRead = true;
            public readonly List<AIIntent> Standing = new();
            public readonly List<AIIntent> Next = new();
            public readonly HashSet<GridCoord> Reach = new();

            public bool TryRead(Guid enemyId, List<AIIntent> standing, List<AIIntent> next,
                                List<AIIntent> options = null)
            {
                standing?.Clear();
                next?.Clear();
                options?.Clear();
                if (!CanRead) return false;

                standing?.AddRange(Standing);
                next?.AddRange(Next);
                return true;
            }

            public bool TryReadReach(Guid enemyId, HashSet<GridCoord> into)
            {
                into?.Clear();
                if (!CanRead || into == null) return false;

                foreach (var cell in Reach) into.Add(cell);
                return true;
            }
        }

        private sealed class SpyThreatOverlay : IThreatOverlayService
        {
            public readonly List<(Guid Source, List<GridCoord> Cells, ThreatOverlayState State, Color? Tint)> Painted = new();
            public readonly List<Guid> Cleared = new();

            public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles)
                => Record(sourceGuid, tiles, ThreatOverlayState.Marked, null);

            public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles, Color tint)
                => Record(sourceGuid, tiles, ThreatOverlayState.Marked, tint);

            public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles, ThreatOverlayState state,
                             Color? tint = null)
                => Record(sourceGuid, tiles, state, tint);

            public void Clear(Guid sourceGuid) => Cleared.Add(sourceGuid);
            public void ClearAll() => Painted.Clear();

            private void Record(Guid source, IEnumerable<GridCoord> tiles, ThreatOverlayState state,
                                Color? tint)
                => Painted.Add((source, new List<GridCoord>(tiles), state, tint));
        }

        private sealed class MinimalIntentService : IEnemyIntentService
        {
            public readonly List<AIIntent> Standing = new();

            public bool TryRead(Guid enemyId, List<AIIntent> standing, List<AIIntent> next,
                                List<AIIntent> options = null)
            {
                standing?.Clear();
                next?.Clear();
                options?.Clear();
                standing?.AddRange(Standing);
                return true;
            }
        }

        private sealed class StubPlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; set; } = Guid.NewGuid();
            public Guid RunId { get; set; } = Guid.NewGuid();
            public ClassHeroSO CurrentHero { get; set; }
            public DiceBagSO DiceBag { get; set; }
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) { DiceBag = bag; }
            public void ClearPlayer() { }
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
        }
    }
}
