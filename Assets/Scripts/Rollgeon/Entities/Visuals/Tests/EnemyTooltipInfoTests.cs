using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
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

        private EnemyTooltipInfo MakeInfo(EnemyDataSO data, Guid guid = default)
        {
            var go = new GameObject("Pawn") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            var info = go.AddComponent<EnemyTooltipInfo>();
            if (data != null) info.Bind(data, guid);
            return info;
        }

        [Test]
        public void BuildContent_SinBrief_ElParrafoCaeALaDescripcionDelSO()
        {
            // Arrange — el id no tiene entry .brief ni .desc en la tabla.
            var info = MakeInfo(MakeData("El Croupier", "Siembra bombas y dispara de lejos."));

            // Act
            var content = info.BuildContent();

            // Assert — decisión del 03/09 (caso "Artillery"): lo que se autora en la tool
            // tiene que verse en el panel; sin frase .brief, el párrafo es la descripción
            // de la ficha. La .brief autorada sigue ganando cuando existe.
            Assert.AreEqual("Siembra bombas y dispara de lejos.", content.Text,
                "Sin .brief en la tabla, el párrafo del panel es la Description del SO.");
            // La frase viaja como párrafo (el bloque de header del mockup), no como pie: el pie
            // quedaba abajo de las tarjetas y el header la quiere pegada al nombre.
            Assert.IsEmpty(content.Flavor ?? string.Empty,
                "Algo llegó al pie del panel: el enemigo ya no manda nada ahí.");
            StringAssert.Contains("Siembra bombas", info.BuildTooltip());
        }

        [Test]
        public void BuildContent_SinBriefNiDescripcion_ElParrafoQuedaVacio()
        {
            // Arrange — ficha sin descripción autorada.
            var info = MakeInfo(MakeData("El Croupier", description: null));

            // Act
            var content = info.BuildContent();

            // Assert — sin nada autorado no se inventa párrafo.
            Assert.IsEmpty(content.Text ?? string.Empty,
                "Sin .brief ni Description, el panel no debe mostrar párrafo.");
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
        public void BuildContent_ElTipoAutoradoEnElSO_PisaAlArchetype()
        {
            // Arrange — el id no existe en la tabla Content, así que gana el campo del SO.
            var data = MakeData("Guardián", "Protege a los suyos.");
            data.Archetype = Rollgeon.Entities.Traits.EnemyArchetype.Ranged;
            data.TooltipType = "Jefe · Centinela";

            // Act
            string type = MakeInfo(data).BuildContent().Type;

            // Assert — el texto de la tool va tal cual: quien lo autora decide todo el
            // renglón, prefijo de jefe incluido.
            Assert.AreEqual("Jefe · Centinela", type);
        }

        [Test]
        public void BuildContent_TipoAutoradoEnBlanco_CaeAlArchetype()
        {
            // Arrange — whitespace no cuenta como autorado.
            var data = MakeData("Guardián", "Protege a los suyos.");
            data.Archetype = Rollgeon.Entities.Traits.EnemyArchetype.Ranged;
            data.TooltipType = "   ";

            // Act
            string type = MakeInfo(data).BuildContent().Type;

            // Assert — deriva del Archetype como siempre.
            Assert.AreEqual(
                Rollgeon.UI.HUD.Status.EnemyArchetypeText.Describe(
                    Rollgeon.Entities.Traits.EnemyArchetype.Ranged, isBoss: false),
                type);
        }

        [Test]
        public void BuildContent_TraeLaVidaDelRegistroYLosAtributos()
        {
            var guid = Guid.NewGuid();

            var attrs = new AttributesManager();
            var ma = new ModifiableAttributes();
            ma.EnsureInitialized();
            ma.SetAttribute<Health>(new Health(180));
            attrs.Register(guid, ma);

            // El max de referencia sale del registry de AI (todos los spawns lo dejan ahí): el
            // atributo no lo guarda, porque el daño escribe sobre Health.Value.
            var registry = new EnemyAIRegistry();
            registry.Register(guid, new AINode_Wait(), 250);

            ServiceLocator.AddService<AttributesManager>(attrs);
            ServiceLocator.AddService<IEnemyAIRegistry>(registry);
            try
            {
                var content = MakeInfo(MakeData("El Croupier", "Siembra bombas."), guid)
                    .BuildContent();

                Assert.IsTrue(content.HasVitals,
                    "La banda tiene que traer la vida: la pila del panel es la misma que flota " +
                    "sobre la cabeza y sin vitales no se dibuja.");
                Assert.AreEqual(180, content.Health);
                Assert.AreEqual(250, content.MaxHealth);
            }
            finally
            {
                ServiceLocator.RemoveService<AttributesManager>();
                ServiceLocator.RemoveService<IEnemyAIRegistry>();
                attrs.Dispose();
            }
        }

        [Test]
        public void BuildContent_SinServiciosDeCombate_NoTraeVitalesYNoRompe()
        {
            // El preview de editor arma este panel sin combate: sin registry ni atributos la
            // banda sale sin vida, no con una excepción ni con un 0/0.
            var content = MakeInfo(MakeData("El Croupier", "Siembra bombas."), Guid.NewGuid())
                .BuildContent();

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
            unbound.Bind(null, Guid.Empty);

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
