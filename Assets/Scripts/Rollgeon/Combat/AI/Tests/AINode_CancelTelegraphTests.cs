using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// El paso que saca un aviso de la cola sin ejecutarlo, contra el servicio de amenaza real.
    /// Lo que se verifica es que descarte <b>exactamente</b> el canal declarado —ni más ni menos— y
    /// que apague su dibujo junto con el estado lógico.
    /// </summary>
    [TestFixture]
    public class AINode_CancelTelegraphTests
    {
        private const string OtherChannel = "pleno";

        private GridManager _grid;
        private ThreatenedAreaService _threat;
        private SpyThreatOverlay _overlay;

        private Guid _boss;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _threat = new ThreatenedAreaService();
            _threat.Register();

            _overlay = new SpyThreatOverlay();
            ServiceLocator.AddService<IThreatOverlayService>(_overlay, ServiceScope.Global);

            _boss = Guid.NewGuid();
            _player = Guid.NewGuid();
            _grid.Register(_boss, new GridCoord(4, 4));
            _grid.Register(_player, new GridCoord(2, 2));
        }

        [TearDown]
        public void TearDown()
        {
            _threat?.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // =====================================================================
        // Qué descarta
        // =====================================================================

        [Test]
        public void ItDropsThePendingMarkOfTheMainChannel()
        {
            Mark(channel: null, new GridCoord(3, 3));

            var result = Cancel(channel: null);

            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.IsFalse(_threat.HasPending(Source(null)),
                "El aviso siguió pendiente: al turno siguiente detona igual, que es exactamente la " +
                "segunda detonación que este paso existe para evitar.");
        }

        [Test]
        public void ItAlsoTurnsOffTheOverlayOfWhatItDropped()
        {
            Mark(channel: null, new GridCoord(3, 3));

            Cancel(channel: null);

            CollectionAssert.Contains(_overlay.Cleared, Source(null),
                "Se descartó el área pero quedó el dibujo: un aviso pintado que nunca detona es " +
                "peor que dos avisos, porque no miente ni consistentemente.");
        }

        /// <remarks>
        /// El caso del Croupier: el Pleno vive en su propio canal y descarta la banda del ciclo, que
        /// va bajo el guid pelado. Si el nodo cancelara de más, el ataque que reemplaza a la banda se
        /// descartaría a sí mismo y el 50% no prendería nunca.
        /// </remarks>
        [Test]
        public void ItLeavesEveryOtherChannelAlone()
        {
            Mark(channel: null, new GridCoord(3, 3));
            Mark(OtherChannel, new GridCoord(6, 6));

            Cancel(channel: null);

            Assert.IsTrue(_threat.HasPending(Source(OtherChannel)),
                "Canceló un canal que no le pidieron: el aviso que reemplaza al descartado se cayó " +
                "con él y el jefe queda sin ataque.");
            CollectionAssert.DoesNotContain(_overlay.Cleared, Source(OtherChannel),
                "Apagó el dibujo de otro canal, que sigue con su área pendiente: detona a ciegas.");
        }

        [Test]
        public void WithAChannelDeclared_ItDropsThatOneAndNotTheMainMark()
        {
            Mark(channel: null, new GridCoord(3, 3));
            Mark(OtherChannel, new GridCoord(6, 6));

            Cancel(OtherChannel);

            Assert.IsFalse(_threat.HasPending(Source(OtherChannel)), "No descartó el canal pedido.");
            Assert.IsTrue(_threat.HasPending(Source(null)),
                "Se llevó puesta la marca principal. El canal es un guid derivado: cancelar por el " +
                "guid pelado del jefe cuando el aviso vive en un canal apunta al lugar equivocado.");
        }

        // =====================================================================
        // Cuándo no hay nada que descartar
        // =====================================================================

        [Test]
        public void WithNothingPending_ItSucceedsInsteadOfFailing()
        {
            var result = Cancel(channel: null);

            // Va desnudo dentro de la Sequence que arma el Pleno: un Failed acá corta esa Sequence
            // —el aviso nuevo no se levanta— y encima deja al AINode_Once sin latchear, así que el
            // cambio de fase se re-anuncia todos los turnos.
            Assert.AreEqual(AIResult.Succeeded, result,
                "Falló por no tener nada que tirar. El ciclo del jefe pudo caer en el beat que " +
                "consume el aviso en vez del que lo marca, y eso es normal, no un error.");
            Assert.IsEmpty(_overlay.Cleared,
                "Apagó overlays sin que hubiera un área pendiente: puede estar pisando el dibujo " +
                "de otra cosa.");
        }

        /// <remarks>
        /// El paso va desnudo dentro del Sequence que arma el Pleno, y ese Sequence corta en el
        /// primer <c>Failed</c>: fallar acá es lo que evita que el turno siga hasta el marcado
        /// creyendo que la cola quedó vacía cuando en realidad nunca se pudo mirar.
        /// </remarks>
        [Test]
        public void WithoutTheThreatService_ItFailsWithoutTouchingTheOverlay()
        {
            ServiceLocator.RemoveService<IThreatenedAreaService>();

            LogAssert.Expect(LogType.Error, new Regex("AINode_CancelTelegraph"));
            var result = Cancel(channel: null);

            Assert.AreEqual(AIResult.Failed, result,
                "Sin el servicio no hay cola que mirar, y un Succeeded acá deja seguir el turno " +
                "como si el aviso viejo ya no existiera.");
            Assert.IsEmpty(_overlay.Cleared,
                "Apagó dibujos sin poder saber qué había pendiente.");
        }

        [Test]
        public void WithoutAnOwner_ItFails()
        {
            var result = new AINode_CancelTelegraph().Tick(new AIContext
            {
                SelfGuid = Guid.Empty,
                PlayerGuid = _player,
                Grid = _grid,
            });

            Assert.AreEqual(AIResult.Failed, result,
                "Sin dueño no hay canal que resolver: Guid.Empty derivaría una fuente que nadie " +
                "marcó, y devolver Succeeded escondería el árbol mal armado.");
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private void Mark(string channel, params GridCoord[] coords) =>
            _threat.Mark(Source(channel), coords, 0, AttackKind.Environmental);

        private AIResult Cancel(string channel) =>
            new AINode_CancelTelegraph { ChannelId = channel }.Tick(new AIContext
            {
                SelfGuid = _boss,
                PlayerGuid = _player,
                Grid = _grid,
            });

        private Guid Source(string channel) => AINode_TelegraphMark.SourceKey(_boss, channel);

        /// <summary>
        /// Registra qué fuentes se apagaron. Los otros fixtures lo tienen anidado y privado, así que
        /// no se puede reusar; y acá además hace falta que <b>observe</b>, no sólo que exista.
        /// </summary>
        private sealed class SpyThreatOverlay : IThreatOverlayService
        {
            public readonly List<Guid> Cleared = new List<Guid>();

            public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles) { }
            public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles, Color tint) { }
            public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles, ThreatOverlayState state,
                Color? tint = null) { }
            public void Clear(Guid sourceGuid) => Cleared.Add(sourceGuid);
            public void ClearAll() => Cleared.Clear();
        }
    }
}
