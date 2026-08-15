using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Contrato de <see cref="ThreatOverlayState"/>: dos amenazas simultáneas tienen que poder
    /// leerse distinto (color <i>y</i> patrón), y el estado tiene que sobrevivir a que la otra
    /// fuente se apague. Antes esto era imposible: el color vivía en un Material por tint que
    /// compartían todos los quads del juego y que el pulso reescribía cada frame.
    /// </summary>
    [TestFixture]
    public sealed class ThreatOverlayStateTests
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        private ThreatTelegraphOverlay _overlay;
        private GridManager _grid;
        private Texture2D _pattern;

        private Guid _boss;
        private Guid _hazard;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(5, 5), Vector3.zero, 1f);
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _overlay = new ThreatTelegraphOverlay();
            _boss = Guid.NewGuid();
            _hazard = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            _overlay?.Dispose();
            var leftover = GameObject.Find("ThreatTelegraphOverlay");
            if (leftover != null) Object.DestroyImmediate(leftover);

            if (_pattern != null) Object.DestroyImmediate(_pattern);
            _pattern = null;

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private static List<GridCoord> Tiles(params (int x, int y)[] coords)
        {
            var list = new List<GridCoord>();
            foreach (var (x, y) in coords) list.Add(new GridCoord(x, y));
            return list;
        }

        private ThreatOverlayQuad OnlyQuadOf(Guid source)
        {
            var quads = _overlay.ActiveQuadsOf(source);
            Assume.That(quads.Count, Is.EqualTo(1), "El caso está armado con una sola casilla por fuente.");
            return quads[0];
        }

        /// <remarks>Bloque nuevo por lectura: lo que se está verificando es justamente que cada
        /// renderer tenga el suyo, así que reusar uno entre asserts escondería el bug.</remarks>
        private static Color PaintedColor(ThreatOverlayQuad quad)
        {
            var block = new MaterialPropertyBlock();
            quad.Renderer.GetPropertyBlock(block);
            return block.GetColor(ColorId);
        }

        private static Texture PaintedPattern(ThreatOverlayQuad quad)
        {
            var block = new MaterialPropertyBlock();
            quad.Renderer.GetPropertyBlock(block);
            return block.GetTexture(MainTexId);
        }

        private static void AssertRgb(Color expected, Color actual, string message)
        {
            Assert.AreEqual(expected.r, actual.r, 1e-3f, message);
            Assert.AreEqual(expected.g, actual.g, 1e-3f, message);
            Assert.AreEqual(expected.b, actual.b, 1e-3f, message);
        }

        // =====================================================================
        // Dos fuentes, dos estados
        // =====================================================================

        [Test]
        public void Show_TwoSourcesWithDifferentStates_KeepTheirOwnColor()
        {
            // Arrange / Act
            _overlay.Show(_boss, Tiles((1, 1)), ThreatOverlayState.Detonating, Color.red);
            _overlay.Show(_hazard, Tiles((3, 3)), ThreatOverlayState.Incoming, Color.blue);

            // Assert
            AssertRgb(Color.red, PaintedColor(OnlyQuadOf(_boss)),
                "El overlay del boss no debe tomar el matiz del hazard.");
            AssertRgb(Color.blue, PaintedColor(OnlyQuadOf(_hazard)),
                "El overlay del hazard no debe tomar el matiz del boss.");
        }

        [Test]
        public void Show_TwoSources_ShareTheMaterialButNotTheColor()
        {
            // Arrange / Act
            _overlay.Show(_boss, Tiles((1, 1)), ThreatOverlayState.Marked, Color.red);
            _overlay.Show(_hazard, Tiles((3, 3)), ThreatOverlayState.Marked, Color.blue);

            // Assert — el material puede seguir siendo uno solo (batching); lo que dejó de ser
            // compartido es el color, que ahora vive en el property block de cada renderer.
            var bossQuad = OnlyQuadOf(_boss);
            var hazardQuad = OnlyQuadOf(_hazard);
            Assert.AreSame(bossQuad.Renderer.sharedMaterial, hazardQuad.Renderer.sharedMaterial);
            AssertRgb(Color.red, PaintedColor(bossQuad), "El quad del boss quedó pintado de rojo.");
            AssertRgb(Color.blue, PaintedColor(hazardQuad), "El quad del hazard quedó pintado de azul.");
        }

        [Test]
        public void Show_TwoSourcesWithDifferentStates_KeepTheirOwnUrgency()
        {
            // Arrange / Act — mismo matiz a propósito: acá lo único que separa a las dos amenazas
            // es el estado.
            _overlay.Show(_boss, Tiles((1, 1)), ThreatOverlayState.Detonating, Color.red);
            _overlay.Show(_hazard, Tiles((3, 3)), ThreatOverlayState.Incoming, Color.red);

            // Assert
            Assert.Greater(PaintedColor(OnlyQuadOf(_boss)).a, PaintedColor(OnlyQuadOf(_hazard)).a,
                "Lo que detona ahora tiene que leerse más sólido que lo que cae en dos turnos.");
        }

        [Test]
        public void Show_UrgencyBands_NeverOverlapBetweenStates()
        {
            // Arrange — el latido es la mitad del aviso que se lee sin mirar el patrón, así que las
            // bandas no pueden solaparse: en ningún momento del pulso un Incoming puede verse tan
            // opaco como un Marked, ni un Marked como un Detonating.
            var incoming = _overlay.StyleOf(ThreatOverlayState.Incoming);
            var marked = _overlay.StyleOf(ThreatOverlayState.Marked);
            var detonating = _overlay.StyleOf(ThreatOverlayState.Detonating);

            // Assert
            Assert.Less(incoming.MaxAlpha, marked.MinAlpha);
            Assert.Less(marked.MaxAlpha, detonating.MinAlpha);
        }

        // =====================================================================
        // Aislamiento entre fuentes
        // =====================================================================

        [Test]
        public void Clear_OneSource_LeavesTheOtherStateUntouched()
        {
            // Arrange
            _overlay.Show(_boss, Tiles((1, 1)), ThreatOverlayState.Detonating, Color.red);
            _overlay.Show(_hazard, Tiles((3, 3)), ThreatOverlayState.Safe, Color.cyan);
            var survivor = OnlyQuadOf(_hazard);

            // Act
            _overlay.Clear(_boss);

            // Assert
            Assert.AreEqual(1, _overlay.ActiveQuadCount);
            Assert.AreEqual(0, _overlay.ActiveQuadsOf(_boss).Count);
            Assert.AreEqual(ThreatOverlayState.Safe, survivor.State,
                "Apagar la amenaza no puede degradar la zona segura declarada por otra fuente.");
            AssertRgb(Color.cyan, PaintedColor(survivor), "La zona segura conserva su matiz.");
        }

        [Test]
        public void Show_ReusingAPooledQuad_DropsThePreviousStatePattern()
        {
            // Arrange — Marked con patrón autorado, Detonating sin él (el caso que el Clear del
            // property block tiene que cubrir: los quads se reciclan entre fuentes).
            _pattern = new Texture2D(2, 2);
            _overlay.ApplyStyle(new ThreatOverlayStateStyle
            {
                State = ThreatOverlayState.Marked,
                Tint = ThreatTelegraphOverlay.DefaultTint,
                Pattern = _pattern,
                MinAlpha = 0.35f,
                MaxAlpha = 0.65f,
                PulseSpeed = 2.5f,
            });

            _overlay.Show(_boss, Tiles((1, 1)), ThreatOverlayState.Marked);
            Assume.That(PaintedPattern(OnlyQuadOf(_boss)), Is.SameAs(_pattern));
            _overlay.Clear(_boss);

            // Act
            _overlay.Show(_hazard, Tiles((3, 3)), ThreatOverlayState.Detonating);

            // Assert
            Assert.IsNull(PaintedPattern(OnlyQuadOf(_hazard)),
                "El quad reciclado no puede arrastrar el rayado del estado anterior.");
        }

        [Test]
        public void ApplyStyle_AfterShow_RepaintsTheLiveThreats()
        {
            // Arrange — el bootstrap puede cargar las texturas de patrón después de que un overlay
            // ya esté en pantalla; los quads vivos toman la autoría nueva sin re-marcar.
            _pattern = new Texture2D(2, 2);
            _overlay.Show(_boss, Tiles((1, 1)), ThreatOverlayState.Marked);
            var quad = OnlyQuadOf(_boss);
            Assume.That(PaintedPattern(quad), Is.Null);

            // Act
            _overlay.ApplyStyle(new ThreatOverlayStateStyle
            {
                State = ThreatOverlayState.Marked,
                Tint = ThreatTelegraphOverlay.DefaultTint,
                Pattern = _pattern,
                MinAlpha = 0.35f,
                MaxAlpha = 0.65f,
                PulseSpeed = 2.5f,
            });

            // Assert
            Assert.AreSame(_pattern, PaintedPattern(quad),
                "La amenaza ya visible tiene que tomar el patrón recién autorado, sin re-marcar.");
        }

        // =====================================================================
        // Sobrecargas previas al estado
        // =====================================================================

        [Test]
        public void Show_WithoutState_StillMapsToMarkedWithTheHistoricalOrange()
        {
            // Act
            _overlay.Show(_boss, Tiles((1, 1)));

            // Assert
            var quad = OnlyQuadOf(_boss);
            Assert.AreEqual(ThreatOverlayState.Marked, quad.State);
            AssertRgb(ThreatTelegraphOverlay.DefaultTint, PaintedColor(quad),
                "El overload sin color tiene que seguir pintando el naranja de siempre.");
        }

        [Test]
        public void Show_WithTintOnly_StillMapsToMarkedAndKeepsTheTint()
        {
            // Arrange — la firma que usa HazardService para que fuego y hielo no se lean igual.
            var ice = new Color(0.35f, 0.75f, 1f, 0.55f);

            // Act
            _overlay.Show(_hazard, Tiles((2, 2)), ice);

            // Assert
            var quad = OnlyQuadOf(_hazard);
            Assert.AreEqual(ThreatOverlayState.Marked, quad.State);
            AssertRgb(ice, PaintedColor(quad), "El overload con color no puede perder el matiz.");
        }

        [Test]
        public void Show_WithStateAndNoTint_UsesTheStateDefaultColor()
        {
            // Act
            _overlay.Show(_boss, Tiles((1, 1)), ThreatOverlayState.Safe);

            // Assert
            AssertRgb(ThreatTelegraphOverlay.SafeTint, PaintedColor(OnlyQuadOf(_boss)),
                "Sin tint explícito manda el color por defecto del estado.");
        }
    }
}
