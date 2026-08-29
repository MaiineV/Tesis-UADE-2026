using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.UI.Tooltips;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// El fijado del panel de enemigo: qué lo sostiene, qué lo suelta, y que el panel es uno.
    /// </summary>
    /// <remarks>
    /// La regla que fija todo esto es la del spec de tooltips §6.2: fijar nunca puede consumir
    /// el click que selecciona objetivo, así que entrar en modo ataque suelta el fijado solo.
    /// Pin/Unpin se ejercitan directo — simular el mouse acá probaría al raycast, no al fijado.
    /// </remarks>
    [TestFixture]
    public sealed class WorldTooltipTriggerPinTests
    {
        private GameObject _goA;
        private GameObject _goB;
        private WorldTooltipTrigger _a;
        private WorldTooltipTrigger _b;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();

            _goA = new GameObject("EnemyA");
            _a = _goA.AddComponent<WorldTooltipTrigger>();
            _a.Mode = WorldTooltipMode.Hover;
            _a.PinOnClick = true;

            _goB = new GameObject("EnemyB");
            _b = _goB.AddComponent<WorldTooltipTrigger>();
            _b.Mode = WorldTooltipMode.Hover;
            _b.PinOnClick = true;
        }

        [TearDown]
        public void TearDown()
        {
            // Unpin antes de destruir: s_pinned es estático y un pin colgado contamina al
            // próximo test (OnDisable también lo suelta, pero el orden de destrucción no es
            // parte del contrato).
            if (_a != null) _a.Unpin();
            if (_b != null) _b.Unpin();
            if (_goA != null) Object.DestroyImmediate(_goA);
            if (_goB != null) Object.DestroyImmediate(_goB);
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void Pin_Fija_YUnpinSuelta()
        {
            // Act + Assert
            _a.Pin();
            Assert.IsTrue(_a.IsPinned);

            _a.Unpin();
            Assert.IsFalse(_a.IsPinned);
        }

        [Test]
        public void FijarOtroTrigger_SueltaAlAnterior()
        {
            // Arrange — el panel es uno solo: dos fijados serían dos dueños del mismo rect.
            _a.Pin();

            // Act
            _b.Pin();

            // Assert
            Assert.IsFalse(_a.IsPinned, "El fijado viejo quedó vivo: dos dueños del mismo panel.");
            Assert.IsTrue(_b.IsPinned);
        }

        [Test]
        public void EntrarEnModoAtaque_SueltaElFijado()
        {
            // Arrange
            _a.Pin();

            // Act — la ficha de atacar: el click tiene que volver a ser 100% de apuntar.
            EventManager.Trigger(EventName.OnActionSelectionStarted, Guid.NewGuid());

            // Assert
            Assert.IsFalse(_a.IsPinned,
                "El fijado sobrevivió al modo ataque: el próximo click pelearía entre apuntar " +
                "y des-fijar.");
        }

        [Test]
        public void ElTargeteoEncadenado_TambienSuelta()
        {
            // Arrange
            _a.Pin();

            // Act
            EventManager.Trigger(EventName.OnChainTargetSelectionStarted, Guid.NewGuid());

            // Assert
            Assert.IsFalse(_a.IsPinned);
        }

        [Test]
        public void ElFijadoMuereConSuDueno()
        {
            // Arrange — el enemigo fijado muere: su GO se apaga.
            _a.Pin();

            // Act
            _goA.SetActive(false);

            // Assert
            Assert.IsFalse(_a.IsPinned,
                "El fijado sobrevivió a la muerte del dueño: el panel quedaría mostrando un " +
                "enemigo que ya no existe.");
        }

        [Test]
        public void ConElPanelFijado_ElTurnoNuevoReMuestra()
        {
            // Arrange — los providers recolectan fresh en cada Show: re-mostrar ES el refresh
            // del bloque de próximo turno. PinRefreshed avisa al overlay de amenaza, que solo
            // escucha el flanco del hover.
            _a.Pin();
            bool refreshed = false;
            _a.PinRefreshed += () => refreshed = true;

            // Act
            EventManager.Trigger(EventName.OnTurnStarted, Guid.NewGuid());

            // Assert
            Assert.IsTrue(refreshed,
                "El cambio de turno no re-mostró el panel fijado: el NEXT TURN quedaría viejo " +
                "hasta re-hoverear.");
        }

        [Test]
        public void SinFijado_ElTurnoNuevoNoHaceNada()
        {
            // Arrange
            bool refreshed = false;
            _a.PinRefreshed += () => refreshed = true;

            // Act
            EventManager.Trigger(EventName.OnTurnStarted, Guid.NewGuid());

            // Assert — sin pin no hay suscripción: un trigger por enemigo escuchando turnos
            // sería trabajo por frame de combate que nadie pidió.
            Assert.IsFalse(refreshed);
        }
    }
}
