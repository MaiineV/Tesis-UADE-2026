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
    /// fuente se apague. El aislamiento lo da el cache de materiales por par (estado, matiz): dos
    /// amenazas que se ven distinto tienen material propio, y dos que se ven igual comparten uno
    /// solo — que es lo que las deja entrar en el mismo batch.
    /// </summary>
    [TestFixture]
    public sealed class ThreatOverlayStateTests
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int PulseAlphaId = Shader.PropertyToID("_PulseAlpha");

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

        /// <remarks>Se lee del material compartido del par (estado, matiz) y ya no de un property
        /// block por renderer. El alpha efectivo vive en _PulseAlpha; en la ruta degradada (sin el
        /// shader del proyecto) viaja en el alpha de _Color, así que se leen las dos: lo que se está
        /// verificando es la semántica, no cuál de las dos rutas está activa.</remarks>
        private static Color PaintedColor(ThreatOverlayQuad quad)
        {
            var material = quad.Renderer.sharedMaterial;
            var color = material.GetColor(ColorId);
            if (material.HasProperty(PulseAlphaId)) color.a = material.GetFloat(PulseAlphaId);
            return color;
        }

        private static Texture PaintedPattern(ThreatOverlayQuad quad) =>
            quad.Renderer.sharedMaterial.GetTexture(MainTexId);

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
        public void Show_TwoSourcesWithDifferentTints_GetTheirOwnMaterial()
        {
            // Arrange / Act
            _overlay.Show(_boss, Tiles((1, 1)), ThreatOverlayState.Marked, Color.red);
            _overlay.Show(_hazard, Tiles((3, 3)), ThreatOverlayState.Marked, Color.blue);

            // Assert — el matiz vive en el _Color del material compartido, así que acá compartirlo
            // sería el bug.
            var bossQuad = OnlyQuadOf(_boss);
            var hazardQuad = OnlyQuadOf(_hazard);
            Assert.AreNotSame(bossQuad.Renderer.sharedMaterial, hazardQuad.Renderer.sharedMaterial,
                "Dos tints sobre un mismo material significa que uno de los dos no está en pantalla.");
            AssertRgb(Color.red, PaintedColor(bossQuad), "El quad del boss quedó pintado de rojo.");
            AssertRgb(Color.blue, PaintedColor(hazardQuad), "El quad del hazard quedó pintado de azul.");
        }

        [Test]
        public void Show_SamePairOfStateAndTint_SharesOneMaterialAcrossSources()
        {
            // Arrange / Act — dos fuentes distintas que se ven exactamente igual.
            _overlay.Show(_boss, Tiles((1, 1)), ThreatOverlayState.Marked, Color.red);
            _overlay.Show(_hazard, Tiles((3, 3)), ThreatOverlayState.Marked, Color.red);

            // Assert
            Assert.AreSame(OnlyQuadOf(_boss).Renderer.sharedMaterial,
                OnlyQuadOf(_hazard).Renderer.sharedMaterial,
                "Un material por fuente en vez de por par (estado, matiz) devuelve los SetPass calls " +
                "que se vinieron a sacar: dos amenazas idénticas no pueden costar dos binds.");
        }

        [Test]
        public void Show_SameTintButDifferentState_GetsItsOwnMaterial()
        {
            // Arrange / Act — mismo matiz, distinta urgencia: el alpha del latido vive en el
            // material, así que compartirlo mezclaría las dos bandas.
            _overlay.Show(_boss, Tiles((1, 1)), ThreatOverlayState.Detonating, Color.red);
            _overlay.Show(_hazard, Tiles((3, 3)), ThreatOverlayState.Incoming, Color.red);

            // Assert
            Assert.AreNotSame(OnlyQuadOf(_boss).Renderer.sharedMaterial,
                OnlyQuadOf(_hazard).Renderer.sharedMaterial,
                "Compartir material entre estados haría que lo que detona late con la banda de lo " +
                "que recién viene.");
        }

        [Test]
        public void Show_TintsThatDifferOnlyInAlpha_ShareOneMaterial()
        {
            // Arrange — el alpha del tint no llega a pantalla (lo pisa el latido), así que dos tints
            // que solo difieren ahí se ven idénticos.
            var opaque = new Color(1f, 0.45f, 0.1f, 1f);
            var faint = new Color(1f, 0.45f, 0.1f, 0.2f);

            // Act
            _overlay.Show(_boss, Tiles((1, 1)), ThreatOverlayState.Marked, opaque);
            _overlay.Show(_hazard, Tiles((3, 3)), ThreatOverlayState.Marked, faint);

            // Assert
            Assert.AreSame(OnlyQuadOf(_boss).Renderer.sharedMaterial,
                OnlyQuadOf(_hazard).Renderer.sharedMaterial,
                "Dos matices que se dibujan idénticos no pueden abrir dos materiales: el alpha del " +
                "tint no se dibuja.");

            var marked = _overlay.StyleOf(ThreatOverlayState.Marked);
            Assert.That(PaintedColor(OnlyQuadOf(_hazard)).a,
                Is.InRange(marked.MinAlpha, marked.MaxAlpha),
                "El alpha en pantalla lo tiene que seguir poniendo el latido del estado, no el tint.");
        }

        [Test]
        public void Show_UrgencyBands_NeverOverlapBetweenStatesThatShareAHue()
        {
            // Arrange — el latido es la mitad del aviso que se lee sin mirar el patrón, así que
            // entre dos avisos del mismo color las bandas no pueden solaparse: en ningún momento
            // del pulso un Marked puede verse tan opaco como un Detonating.
            var marked = _overlay.StyleOf(ThreatOverlayState.Marked);
            var detonating = _overlay.StyleOf(ThreatOverlayState.Detonating);

            // Assert
            Assert.AreEqual(marked.Tint, detonating.Tint,
                "Dejaron de compartir matiz. Si se separan por color, la escalera de alpha de abajo " +
                "ya no es lo que los distingue y este test está midiendo la cosa equivocada.");
            Assert.Less(marked.MaxAlpha, detonating.MinAlpha);
        }

        [Test]
        public void Incoming_SeparaPorColorYNoPorTransparencia()
        {
            // Arrange
            var incoming = _overlay.StyleOf(ThreatOverlayState.Incoming);
            var marked = _overlay.StyleOf(ThreatOverlayState.Marked);

            // Assert
            Assert.AreNotEqual(marked.Tint, incoming.Tint,
                "Volvió al naranja de Marked. Compartiendo matiz, la única separación es el alpha, " +
                "y como Incoming tiene que quedar por debajo del piso de Marked termina casi " +
                "invisible — justo el aviso que el jugador pidió al pasar el mouse.");
            Assert.GreaterOrEqual(incoming.MinAlpha, marked.MinAlpha,
                "Lo que viene se dibuja más transparente que lo que ya está puesto. Los dos salen " +
                "del mismo hover y son información nueva: el que se lee peor es el que se ignora.");
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
            // Arrange — Marked con patrón autorado, Detonating sin él (el caso que el cache por par
            // (estado, matiz) tiene que cubrir: los quads se reciclan entre fuentes, así que un quad
            // pooled tiene que quedar apuntando al material de su estado nuevo).
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
