using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Rollgeon.Entities.Visuals.Tests
{
    /// <summary>
    /// Lo que un enemigo dice de sí mismo: el párrafo y el contenido del panel. El enganche al pawn (quién lo cuelga y con qué
    /// trigger) lo cubre <c>EntityVisualServiceTests</c>.
    /// </summary>
    /// <remarks>
    /// En EditMode <c>LocalizationSettings</c> no está inicializado, así que
    /// <c>LocalizedContent.Name/Description</c> se van por su catch y devuelven el fallback autorado
    /// en el SO. Ningún assert de acá mira una traducción: miran el valor del asset. El
    /// <see cref="UnmappedEntityId"/> lo garantiza incluso si alguien inicializa Localization en el
    /// futuro — esa key no existe en la tabla Content.
    /// </remarks>
    [TestFixture]
    public class EnemyTooltipInfoTests
    {
        private const string UnmappedEntityId = "test.enemy.tooltip.unmapped";

        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            _created.Clear();
        }

        private EnemyDataSO MakeData(string displayName, string description)
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.hideFlags = HideFlags.HideAndDontSave;
            data.EntityId = UnmappedEntityId;
            data.DisplayName = displayName;
            data.Description = description;
            _created.Add(data);
            return data;
        }

        private EnemyTooltipInfo MakeInfo(EnemyDataSO data)
        {
            var go = new GameObject("Pawn") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            var info = go.AddComponent<EnemyTooltipInfo>();
            if (data != null) info.Bind(data);
            return info;
        }

        [Test]
        public void BuildContent_NoTraeLore_AunqueElSOTengaDescripcion()
        {
            var info = MakeInfo(MakeData("El Croupier", "Siembra bombas y dispara de lejos."));

            var content = info.BuildContent();

            // El panel no lleva el lore del .desc: su frase es OTRA key (.brief), y un enemigo
            // sin frase autorada no muestra ninguna. El lore sigue vivo en BuildTooltip.
            Assert.IsEmpty(content.Flavor ?? string.Empty,
                "El .desc se coló en el panel: la frase táctica sale de .brief, y este id no " +
                "tiene entry.");
            StringAssert.Contains("Siembra bombas", info.BuildTooltip());
        }

        [Test]
        public void BuildContent_TraeLaFamiliaConSuPrefijoDeJefe()
        {
            var data = MakeData("El Croupier", "Siembra bombas.");
            data.Archetype = Rollgeon.Entities.Traits.EnemyArchetype.Ranged;
            data.IsBoss = true;

            string type = MakeInfo(data).BuildContent().Type;

            // Sin mirar la traducción: lo que se fija es que la familia llegue y que el prefijo
            // de jefe la envuelva en vez de reemplazarla.
            Assert.IsNotEmpty(type);
            StringAssert.Contains(
                Rollgeon.UI.HUD.Status.EnemyArchetypeText.Describe(
                    Rollgeon.Entities.Traits.EnemyArchetype.Ranged, isBoss: false),
                type);
        }

        [Test]
        public void BuildContent_NoTraeVitales_QueYaEstanSobreLaCabeza()
        {
            var content = MakeInfo(MakeData("El Croupier", "Siembra bombas.")).BuildContent();

            // La barra de vida flota sobre el bicho y es la que el jugador mira mientras le pega.
            // Repetirla adentro del panel gasta una fila en un número que está a dos centímetros.
            Assert.IsFalse(content.HasVitals);
            Assert.IsNull(content.Shield);
        }

        [Test]
        public void BuildContent_SinFamiliaAutorada_UnComunNoTieneFila()
        {
            var info = MakeInfo(MakeData("CardEnemy", "Un goblin."));

            Assert.IsEmpty(info.BuildContent().Type ?? string.Empty);
        }

        [Test]
        public void BuildTooltip_IsEmpty_WhenNoDataWasBound()
        {
            // Arrange
            var neverBound = MakeInfo(null);
            var unbound = MakeInfo(MakeData("El Croupier", "Enciende sectores del paño."));
            unbound.Bind(null);

            // Act + Assert — vacío y no un panel en blanco: el trigger no abre nada con texto vacío.
            Assert.IsEmpty(neverBound.BuildTooltip(),
                "Un pawn sin ficha devolvió texto: abriría un pergamino vacío encima de la sala en " +
                "vez de no abrir nada.");
            Assert.IsEmpty(unbound.BuildTooltip(),
                "Bind(null) dejó vivo el texto anterior: el tooltip le sobreviviría a la ficha que " +
                "lo describía.");
        }

        [Test]
        public void BuildTooltip_PutsTheNameInBoldAndTheDescriptionOnItsOwnLine()
        {
            // Arrange
            string displayName = "El Croupier";
            string description = "Enciende sectores del paño y huye del cuerpo a cuerpo.";
            var info = MakeInfo(MakeData(displayName, description));

            // Act — se parten los dos saltos porque el componente usa AppendLine (Environment.NewLine):
            // afirmar "\n" ataría el suite al sistema operativo del que corre los tests.
            string[] lines = info.BuildTooltip().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            // Assert
            Assert.AreEqual(2, lines.Length,
                "El tooltip dejó de ser nombre + descripción en dos renglones: el panel muestra las " +
                "dos cosas pegadas en una línea y no se lee cuál es el nombre.");
            Assert.AreEqual($"<b>{displayName}</b>", lines[0],
                "El nombre perdió el <b></b>: sin negrita el título no se distingue del cuerpo.");
            Assert.AreEqual(description, lines[1],
                "La descripción no llegó tal cual está autorada — es la única explicación de la " +
                "pelea que el jugador puede leer sin morir primero.");
        }

        [Test]
        public void BuildTooltip_HasNoTrailingNewline_WhenThereIsNoDescription()
        {
            // Arrange
            var info = MakeInfo(MakeData("El Croupier", string.Empty));

            // Act + Assert
            Assert.AreEqual("<b>El Croupier</b>", info.BuildTooltip(),
                "Una ficha sin descripción tiene que terminar en el nombre: un salto de línea " +
                "colgado le agrega al panel un renglón vacío de alto.");
        }

        [Test]
        public void BuildTooltip_IsEmpty_WhenBothFieldsAreBlank()
        {
            // Arrange — blancos y no vacíos: es el estado real de un SO recién creado al que alguien
            // le apretó la barra espaciadora.
            var info = MakeInfo(MakeData("   ", "\t"));

            // Act + Assert
            Assert.IsEmpty(info.BuildTooltip(),
                "Nombre y descripción en blanco devolvieron texto: el trigger abriría un panel con " +
                "espacios adentro, que se lee como un bug de UI y no como la ausencia de ficha.");
        }
    }
}
