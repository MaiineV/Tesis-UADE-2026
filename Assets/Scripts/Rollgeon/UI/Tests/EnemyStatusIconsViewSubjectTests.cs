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
            var panel = _view.CollectPanelCards();
            var applied = _view.CollectApplied();

            // Assert
            Assert.AreEqual(1, panel.Count,
                "El bloque de próximo turno tiene que ser el disparo, y las cruces de las bombas " +
                "se leen pasándole el mouse a la bomba.");
            Assert.AreEqual(AIIntentTextKeys.RangedShot, panel[0].Id);
            Assert.AreEqual(0, applied.Count,
                "La columna del jefe se llenó con una tarjeta por bomba en el paño.");
        }

        [Test]
        public void ElProximoTurno_VaALaColumnaPrincipalConFecha_YElStandingAlCostadoSin()
        {
            // Arrange — el ciclo del Croupier: el disparo es el próximo tiempo, y el fuego del
            // Pleno cuelga FUERA del Alternate y se tickea todos los turnos.
            _intents.Next.Add(Own(AIIntentTextKeys.RangedShot, "Te dispara"));
            _intents.Standing.Add(Own(AIIntentTextKeys.Ignite, "Prende el suelo"));

            // Act
            var panel = _view.CollectPanelCards();
            var applied = _view.CollectApplied();

            // Assert — dos columnas con papeles distintos: el bloque de próximo turno en la
            // principal con su fecha, y lo que el jefe mantiene en el paño al costado, sin fecha.
            Assert.AreEqual(1, panel.Count);
            Assert.AreEqual(AIIntentTextKeys.RangedShot, panel[0].Id);
            Assert.IsNotEmpty(panel[0].Eyebrow ?? string.Empty,
                "El próximo turno lleva la fecha —'Próximo turno'— en chico.");
            Assert.AreEqual(1, applied.Count);
            Assert.AreEqual(AIIntentTextKeys.Ignite, applied[0].Id);
            Assert.IsEmpty(applied[0].Eyebrow ?? string.Empty,
                "Lo que el jefe mantiene en el paño no lleva fecha: no va a pasar, está pasando.");
        }

        [Test]
        public void SinCiclo_ElStandingPropioSePromueveAProximoTurno()
        {
            // Arrange — el bestiario común: su ataque no vive en un Alternate, se tickea todos
            // los turnos. ESO es su próximo turno, y sin la promoción el bloque salía vacío
            // para casi todos los enemigos del juego.
            _intents.Standing.Add(Own(AIIntentTextKeys.Attack, "Te ataca"));

            // Act
            var panel = _view.CollectPanelCards();
            var applied = _view.CollectApplied();

            // Assert
            Assert.AreEqual(1, panel.Count,
                "El golpe de todos los turnos no se promovió al bloque de próximo turno.");
            Assert.AreEqual(AIIntentTextKeys.Attack, panel[0].Id);
            Assert.IsNotEmpty(panel[0].Eyebrow ?? string.Empty);
            Assert.AreEqual(0, applied.Count,
                "La tarjeta promovida salió también al costado: el mismo ataque dos veces.");
        }

        [Test]
        public void ElTituloDelProximoTurno_LlevaElTipoDeAtaque()
        {
            // Arrange
            _intents.Next.Add(Own(AIIntentTextKeys.RangedShot, "Te dispara"));
            _intents.Standing.Add(Own(AIIntentTextKeys.Ignite, "Prende el suelo"));

            // Act
            var panel = _view.CollectPanelCards();
            var applied = _view.CollectApplied();

            // Assert — resuelto por el mismo camino que producción, para no romperse con el
            // editor en otro idioma. Sólo la tarjeta de próximo turno califica su ataque: las
            // del costado hablan de efectos y terreno.
            string kind = AttackKindText.Describe(AttackKind.BasicAttack);
            StringAssert.Contains(kind, panel[0].DisplayName,
                "El título del próximo turno no dice de qué tipo es el ataque.");
            foreach (var state in applied)
                StringAssert.DoesNotContain(kind, state.DisplayName,
                    "Una tarjeta del costado calificó su ataque: el tipo va sólo en el bloque " +
                    "de próximo turno.");
        }

        [Test]
        public void LaDebilidad_EsUnSlotDeLaFilaDeAbajo_NoUnBloqueDeTexto()
        {
            // Arrange — la debilidad registrada y viva, como en el spawn real. En el mockup es
            // la piedrita rota de la fila de estados: un slot de ícono, nunca un bloque de
            // texto ni un renglón.
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
                var panel = _view.CollectPanelCards();
                var applied = _view.CollectApplied();
                var bottom = _view.CollectBottomIcons();

                // Assert — en la columna principal nunca; en los estados sí, como rasgo; y a la
                // fila de abajo recién llega cuando el catálogo tenga el sprite de la piedrita
                // (acá no hay catálogo, así que el filtro la deja afuera sin placa vacía).
                foreach (var state in panel)
                    Assert.AreNotEqual(EnemyKitStatusProvider.WeaknessId, state.Id,
                        "La debilidad apareció en la columna principal: el spec no le da bloque.");

                bool inApplied = false;
                foreach (var state in applied)
                    if (state.Id == EnemyKitStatusProvider.WeaknessId) inApplied = true;
                Assert.IsTrue(inApplied,
                    "La debilidad registrada no llegó a los estados: la piedrita rota del " +
                    "mockup no tiene de dónde salir.");

                foreach (var state in bottom)
                    Assert.AreNotEqual(EnemyKitStatusProvider.WeaknessId, state.Id,
                        "Un slot sin sprite llegó a la fila: una placa vacía.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void ElRepertorio_YaNoSale()
        {
            // Arrange — el spec de tooltips dejó el panel en cuatro bloques: header, próximo
            // turno, maldición y estados. El repertorio entero era ruido: lo que el jefe SABE
            // hacer se aprende peleando, lo que VA a hacer es lo que el panel promete.
            _intents.Next.Add(Own(AIIntentTextKeys.RangedShot, "Te dispara"));
            _intents.Options.Add(Own(AIIntentTextKeys.BombField, "Siembra bombas"));
            _intents.Options.Add(Own(AIIntentTextKeys.Ignite, "Prende el cono"));

            // Act
            var panel = _view.CollectPanelCards();
            var applied = _view.CollectApplied();

            // Assert
            Assert.AreEqual(1, panel.Count, "Sólo el próximo turno: el repertorio no vuelve.");
            Assert.AreEqual(AIIntentTextKeys.RangedShot, panel[0].Id);
            Assert.AreEqual(0, applied.Count,
                "El repertorio volvió a salir al costado.");
        }

        [Test]
        public void LoQueElJefeTickeaParaSiMismo_SiEsTarjetaSuya()
        {
            // Arrange
            _intents.Standing.Add(Own(AIIntentTextKeys.Ignite, "Prende el suelo"));
            _intents.Standing.Add(new AIIntent(
                AIIntentTextKeys.BombField, "Siembra bombas", 0, AttackKind.Environmental,
                amount: 3, subjectGuid: _boss));

            // Act — sin ciclo, el primer standing propio se promueve al bloque principal y el
            // otro queda al costado: los dos son suyos y los dos se ven.
            var panel = _view.CollectPanelCards();
            var applied = _view.CollectApplied();

            // Assert
            Assert.AreEqual(1, panel.Count);
            Assert.AreEqual(AIIntentTextKeys.Ignite, panel[0].Id);
            Assert.AreEqual(1, applied.Count,
                "Se filtró de más: una intención marcada con el guid del propio jefe es suya y " +
                "tiene que salir en su panel.");
            Assert.AreEqual(AIIntentTextKeys.BombField, applied[0].Id);
        }

        [Test]
        public void ConCurseAutorado_LaMaldicionSaleDebajoDelProximoTurno()
        {
            // Arrange — la pasiva del jefe sobre el jugador, autorada en su data: el panel la
            // anuncia desde el turno 1, no recién cuando traba el primer dado.
            var curse = ScriptableObject.CreateInstance<Rollgeon.Entities.BossCurseSO>();
            var data = ScriptableObject.CreateInstance<Rollgeon.Entities.EnemyDataSO>();
            try
            {
                // Key sin entry a propósito (mismo truco que EnemyTooltipInfoTests): con una key
                // real el assert dependería del idioma del editor.
                curse.CurseId = "test.curse.unmapped";
                curse.DisplayName = "Candado";
                curse.Description = "Te traba un dado.";
                data.Curse = curse;
                _view.Initialize(_boss, null, null, data);
                _intents.Next.Add(Own(AIIntentTextKeys.RangedShot, "Te dispara"));

                // Act
                var panel = _view.CollectPanelCards();

                // Assert — el orden ES el spec: header (fuera de esta lista), próximo turno,
                // maldición. La etiqueta chica la separa del ataque.
                Assert.AreEqual(2, panel.Count);
                Assert.AreEqual("test.curse.unmapped", panel[1].Id);
                Assert.AreEqual(EnemyStatusIconsView.PlayerCurseEyebrow(), panel[1].Eyebrow,
                    "La maldición sin su etiqueta se lee como un segundo ataque.");
                Assert.IsTrue(string.IsNullOrEmpty(panel[1].DisplayName),
                    "La maldición llevó título: el mockup la deja en label + regla — " +
                    "'PLAYER CURSE / Te traba un dado.'");
                StringAssert.Contains("Te traba", panel[1].Description);
            }
            finally
            {
                Object.DestroyImmediate(curse);
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void SinCurse_ElBloqueNoExiste()
        {
            // Arrange — bestiario común: data sin Curse.
            var data = ScriptableObject.CreateInstance<Rollgeon.Entities.EnemyDataSO>();
            try
            {
                _view.Initialize(_boss, null, null, data);
                _intents.Next.Add(Own(AIIntentTextKeys.RangedShot, "Te dispara"));

                // Act
                var panel = _view.CollectPanelCards();

                // Assert — ni tarjeta ni hueco.
                Assert.AreEqual(1, panel.Count,
                    "Apareció un bloque de maldición para un enemigo que no maldice nada.");
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void LaFilaDeAbajo_SoloLlevaLoQueTieneArte()
        {
            // Arrange — sin catálogo ningún intent resuelve sprite: lo que mantiene en el paño
            // existe como estado pero no puede ser slot, porque el slot es sólo ícono y sin
            // arte quedaría una placa vacía.
            _intents.Next.Add(Own(AIIntentTextKeys.RangedShot, "Te dispara"));
            _intents.Standing.Add(Own(AIIntentTextKeys.Ignite, "Prende el suelo"));

            // Act
            var applied = _view.CollectApplied();
            var bottom = _view.CollectBottomIcons();

            // Assert
            Assert.AreEqual(1, applied.Count);
            Assert.AreEqual(0, bottom.Count,
                "Un estado sin sprite llegó a la fila de abajo: su slot sería una placa vacía.");
        }

        [Test]
        public void ElFiltroDeLaFilaDeAbajo_DejaPasarLoQueSiTieneSprite()
        {
            // Arrange — el filtro directo, sin catálogo de por medio: es el mismo que usa el
            // preview de editor.
            var tex = new Texture2D(4, 4);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            try
            {
                var applied = new List<StatusIconState>
                {
                    new StatusIconState("status.stun", "Aturdido", "No ataca.", sprite,
                                        active: true),
                    new StatusIconState("status.sin_arte", "Invisible", "Sin sprite.", null,
                                        active: true),
                };
                var bottom = new List<StatusIconState>();

                // Act
                EnemyStatusIconsView.AppendBottomIcons(applied, bottom);

                // Assert
                Assert.AreEqual(1, bottom.Count);
                Assert.AreEqual("status.stun", bottom[0].Id);
            }
            finally
            {
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(tex);
            }
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
            public readonly List<AIIntent> Options = new List<AIIntent>();

            public bool TryRead(Guid enemyId, List<AIIntent> standing, List<AIIntent> next,
                                List<AIIntent> options = null)
            {
                standing?.Clear();
                next?.Clear();
                options?.Clear();
                standing?.AddRange(Standing);
                next?.AddRange(Next);
                options?.AddRange(Options);
                return true;
            }
        }
    }
}
