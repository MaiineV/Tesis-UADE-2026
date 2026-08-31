using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rollgeon.Editor.Tools.Menu.Tests
{
    /// <summary>
    /// Wiring de responsive del menú (correr <c>Rollgeon → Responsive Menu → Setup All</c>
    /// si fallan): con <c>ScreenMatchMode.Expand</c> el canvas contiene SIEMPRE el rect
    /// de referencia 1920×1080 completo, así que basta con que cada interactivo de
    /// tamaño fijo caiga dentro de ese rect central para garantizar que es visible y
    /// clickeable en cualquier resolución — sin simular ninguna.
    /// </summary>
    [TestFixture]
    public class MainMenuResponsiveTests
    {
        private const string ScenePath = "Assets/Scenes/01_MainMenu.unity";
        private const float HalfWidth = 960f;
        private const float HalfHeight = 540f;
        private const float Tolerance = 0.5f;

        private Scene _scene;
        private bool _openedByTest;

        [OneTimeSetUp]
        public void OpenScene()
        {
            _scene = SceneManager.GetSceneByPath(ScenePath);
            _openedByTest = !(_scene.IsValid() && _scene.isLoaded);
            if (_openedByTest)
            {
                _scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }
        }

        [OneTimeTearDown]
        public void CloseScene()
        {
            if (_openedByTest && _scene.IsValid())
            {
                EditorSceneManager.CloseScene(_scene, removeScene: true);
            }
        }

        private Canvas FindMenuCanvas()
        {
            foreach (var root in _scene.GetRootGameObjects())
            {
                var canvas = root.GetComponentInChildren<Canvas>(includeInactive: true);
                if (canvas != null) return canvas;
            }
            return null;
        }

        [Test]
        public void should_use_expand_match_mode_on_menu_canvas_scaler()
        {
            // Arrange
            var canvas = FindMenuCanvas();
            Assert.IsNotNull(canvas, "01_MainMenu debe tener un Canvas.");
            var scaler = canvas.GetComponent<CanvasScaler>();
            Assert.IsNotNull(scaler, "El Canvas del menú debe tener CanvasScaler.");

            // Assert — Expand garantiza que el rect de referencia entra entero en
            // cualquier aspect; MatchWidthOrHeight 0.5 clipeaba en 21:9 y 16:10.
            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
            Assert.AreEqual(CanvasScaler.ScreenMatchMode.Expand, scaler.screenMatchMode,
                "Correr Rollgeon → Responsive Menu → Setup All.");
        }

        [Test]
        public void should_keep_every_fixed_size_selectable_inside_reference_rect()
        {
            // Arrange
            var canvas = FindMenuCanvas();
            Assert.IsNotNull(canvas);
            var canvasRect = (RectTransform)canvas.transform;
            var selectables = canvas.GetComponentsInChildren<Selectable>(includeInactive: true);
            Assert.Greater(selectables.Length, 0, "El menú debe tener interactivos.");

            var corners = new Vector3[4];
            var offenders = new List<string>();

            // Act — cada interactivo de tamaño fijo (anclas puntuales; los stretch
            // full-bleed como catchers/scrims siguen al canvas y no pueden clipear)
            // se proyecta al espacio local del canvas.
            foreach (var selectable in selectables)
            {
                var rect = selectable.transform as RectTransform;
                if (rect == null || rect.anchorMin != rect.anchorMax) continue;

                rect.GetWorldCorners(corners);
                for (int i = 0; i < 4; i++)
                {
                    Vector3 local = canvasRect.InverseTransformPoint(corners[i]);
                    if (Mathf.Abs(local.x) > HalfWidth + Tolerance
                        || Mathf.Abs(local.y) > HalfHeight + Tolerance)
                    {
                        offenders.Add($"{Path(rect)} — esquina ({local.x:F0},{local.y:F0})");
                        break;
                    }
                }
            }

            // Assert
            Assert.IsEmpty(offenders,
                "Interactivos fuera del rect de referencia 1920×1080 (van a clipear " +
                "en algún aspect):\n" + string.Join("\n", offenders));
        }

        private static string Path(Transform t)
        {
            var path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
