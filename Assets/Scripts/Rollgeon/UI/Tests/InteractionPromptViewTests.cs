using System.Reflection;
using NUnit.Framework;
using Rollgeon.UI.HUD;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Verifica <see cref="InteractionPromptView"/>: el owner-guard de
    /// <see cref="InteractionPromptView.Hide"/> (mismo contrato que
    /// <c>TooltipController</c>), que un segundo <c>Show</c> reemplaza el contenido
    /// ("último gana") y el formatter puro <see cref="InteractionPromptView.BuildFooter"/>.
    /// </summary>
    /// <remarks>
    /// <c>InteractionPromptView</c> es <c>static</c> con un runtime interno privado
    /// (<c>Runtime</c>) — sin <c>InternalsVisibleTo</c> hacia este assembly, así que
    /// (siguiendo el patrón de <c>FloatingDamageSpawnerTests</c>/<c>TurnQueueViewTests</c>)
    /// se inspecciona el estado vía reflection en vez de exponer accessors públicos
    /// que sólo servirían a los tests.
    /// </remarks>
    [TestFixture]
    public class InteractionPromptViewTests
    {
        [SetUp]
        public void Setup()
        {
            // Los statics de InteractionPromptView sobreviven entre tests dentro de la
            // misma sesión del Editor — arrancar cada test con el overlay destruido
            // evita que un test contamine al siguiente con owner/contenido viejo.
            InteractionPromptView.ResetForTests();
        }

        [TearDown]
        public void Teardown()
        {
            // HideForce por prolijidad (no hay corrutina corriendo en EditMode, pero si
            // algún test dejara el panel "visible" el próximo SetUp igual lo resetea);
            // ResetForTests ya se encarga del DestroyImmediate del overlay.
            InteractionPromptView.HideForce();
            InteractionPromptView.ResetForTests();
        }

        [Test]
        public void Show_ActivatesPanelAndSetsTitle()
        {
            var content = new InteractionPromptContent("F", "Comprar", "Poción", "Cura 10 HP", 8, true);

            InteractionPromptView.Show(1, in content);

            var runtime = GetRuntimeInstance();
            var panelGO = GetPrivate<GameObject>(runtime, "_panelGO");
            var titleText = GetPrivate<TextMeshProUGUI>(runtime, "_titleText");

            Assert.IsTrue(panelGO.activeSelf, "Show debe activar el panel.");
            Assert.AreEqual("Poción", titleText.text);
        }

        [Test]
        public void Show_WithDescription_ActivatesDescriptionAndSetsText()
        {
            var content = new InteractionPromptContent("F", "Comprar", "Poción", "Cura 10 HP", 8, true);

            InteractionPromptView.Show(1, in content);

            var runtime = GetRuntimeInstance();
            var descGO = GetPrivate<GameObject>(runtime, "_descGO");
            var descText = GetPrivate<TextMeshProUGUI>(runtime, "_descText");

            Assert.IsTrue(descGO.activeSelf, "Con Description no vacía, el GO debe estar activo.");
            Assert.AreEqual("Cura 10 HP", descText.text);
        }

        [Test]
        public void Show_WithoutDescription_DeactivatesDescriptionGO()
        {
            var content = new InteractionPromptContent("F", "Tomar", "Espada", string.Empty);

            InteractionPromptView.Show(1, in content);

            var runtime = GetRuntimeInstance();
            var descGO = GetPrivate<GameObject>(runtime, "_descGO");

            Assert.IsFalse(descGO.activeSelf, "Sin Description, el GO debe quedar inactivo.");
        }

        [Test]
        public void Hide_WithWrongOwner_IsIgnored()
        {
            var content = new InteractionPromptContent("F", "Comprar", "Poción", string.Empty, 8, true);
            InteractionPromptView.Show(1, in content);

            InteractionPromptView.Hide(999);

            var panelGO = GetPrivate<GameObject>(GetRuntimeInstance(), "_panelGO");
            Assert.IsTrue(panelGO.activeSelf, "Hide con un owner distinto no debe cerrar el prompt de otro.");
        }

        [Test]
        public void Hide_WithCorrectOwner_HidesPanel()
        {
            var content = new InteractionPromptContent("F", "Comprar", "Poción", string.Empty, 8, true);
            InteractionPromptView.Show(1, in content);

            InteractionPromptView.Hide(1);

            var panelGO = GetPrivate<GameObject>(GetRuntimeInstance(), "_panelGO");
            Assert.IsFalse(panelGO.activeSelf, "Hide con el owner correcto debe cerrar el prompt.");
        }

        [Test]
        public void Show_SecondOwner_ReplacesContent_UltimoGana()
        {
            var first = new InteractionPromptContent("F", "Comprar", "Poción", string.Empty, 8, true);
            InteractionPromptView.Show(1, in first);

            var second = new InteractionPromptContent("F", "Tomar", "Espada", string.Empty);
            InteractionPromptView.Show(2, in second);

            var runtime = GetRuntimeInstance();
            var titleText = GetPrivate<TextMeshProUGUI>(runtime, "_titleText");
            Assert.AreEqual("Espada", titleText.text, "El segundo Show debe reemplazar el contenido del primero.");

            // El owner viejo (1) ya no es dueño del prompt — su Hide no debe afectar
            // el contenido que acaba de tomar el owner 2.
            InteractionPromptView.Hide(1);
            var panelGO = GetPrivate<GameObject>(runtime, "_panelGO");
            Assert.IsTrue(panelGO.activeSelf, "El owner reemplazado no debe poder cerrar el prompt del nuevo owner.");
        }

        [Test]
        public void BuildFooter_WithPriceAndCanAfford_UsesGoldColor()
        {
            string footer = InteractionPromptView.BuildFooter("F", "Comprar", 8, canAfford: true);

            Assert.AreEqual("<color=#FFD75A>[F]</color> Comprar   <color=#FFC533>8 G</color>", footer);
        }

        [Test]
        public void BuildFooter_WithPriceAndCannotAfford_UsesWarningColor()
        {
            string footer = InteractionPromptView.BuildFooter("F", "Comprar", 8, canAfford: false);

            Assert.AreEqual("<color=#FFD75A>[F]</color> Comprar   <color=#FF6B6B>8 G</color>", footer);
        }

        [Test]
        public void BuildFooter_NegativePrice_OmitsPriceSegment()
        {
            string footer = InteractionPromptView.BuildFooter("F", "Tomar", -1, canAfford: true);

            Assert.AreEqual("<color=#FFD75A>[F]</color> Tomar", footer);
        }

        [Test]
        public void BuildConfirmLabel_FormatsKeyAndVerb()
        {
            string label = InteractionPromptView.BuildConfirmLabel("F", "Comprar");

            Assert.AreEqual("<color=#FFD75A>[F]</color> Comprar", label);
        }

        [Test]
        public void BuildPriceLabel_NegativePrice_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, InteractionPromptView.BuildPriceLabel(-1, canAfford: true));
        }

        // --------------------------------------------------------------
        // Confirm button (Show con callback)
        // --------------------------------------------------------------

        [Test]
        public void Show_WithConfirmCallback_ActivatesButtonAndBlocksRaycasts()
        {
            var content = new InteractionPromptContent("F", "Comprar", "Poción", string.Empty, 8, true);

            InteractionPromptView.Show(1, in content, () => { });

            var runtime = GetRuntimeInstance();
            var confirmGO = GetPrivate<GameObject>(runtime, "_confirmGO");
            var canvasGroup = GetPrivate<CanvasGroup>(runtime, "_canvasGroup");

            Assert.IsTrue(confirmGO.activeSelf, "Con callback, el botón de confirmación debe estar activo.");
            Assert.IsTrue(canvasGroup.blocksRaycasts, "Con callback, el CanvasGroup debe bloquear raycasts para que el botón sea clickeable.");
        }

        [Test]
        public void Show_WithoutConfirmCallback_KeepsButtonHiddenAndRaycastsOff()
        {
            var content = new InteractionPromptContent("F", "Tomar", "Espada", string.Empty);

            InteractionPromptView.Show(1, in content);

            var runtime = GetRuntimeInstance();
            var confirmGO = GetPrivate<GameObject>(runtime, "_confirmGO");
            var canvasGroup = GetPrivate<CanvasGroup>(runtime, "_canvasGroup");

            Assert.IsFalse(confirmGO.activeSelf, "Sin callback, el botón no debe aparecer.");
            Assert.IsFalse(canvasGroup.blocksRaycasts, "Sin callback, el prompt no debe interceptar clicks (contrato original).");
        }

        [Test]
        public void Show_WithConfirmAndPrice_FooterShowsOnlyPriceAndButtonShowsKeyVerb()
        {
            var content = new InteractionPromptContent("F", "Comprar", "Poción", string.Empty, 8, true);

            InteractionPromptView.Show(1, in content, () => { });

            var runtime = GetRuntimeInstance();
            var footerText = GetPrivate<TextMeshProUGUI>(runtime, "_footerText");
            var confirmText = GetPrivate<TextMeshProUGUI>(runtime, "_confirmText");

            Assert.AreEqual("<color=#FFC533>8 G</color>", footerText.text);
            Assert.AreEqual("<color=#FFD75A>[F]</color> Comprar", confirmText.text);
        }

        [Test]
        public void Show_WithConfirmAndCannotAfford_ButtonNotInteractable()
        {
            var content = new InteractionPromptContent("F", "Comprar", "Poción", string.Empty, 999, canAfford: false);

            InteractionPromptView.Show(1, in content, () => { });

            var confirmButton = GetPrivate<UnityEngine.UI.Button>(GetRuntimeInstance(), "_confirmButton");
            Assert.IsFalse(confirmButton.interactable, "Sin oro suficiente el botón debe verse deshabilitado.");
        }

        [Test]
        public void ConfirmButtonClick_InvokesCallback()
        {
            int calls = 0;
            var content = new InteractionPromptContent("F", "Comprar", "Poción", string.Empty, 8, true);
            InteractionPromptView.Show(1, in content, () => calls++);

            var confirmButton = GetPrivate<UnityEngine.UI.Button>(GetRuntimeInstance(), "_confirmButton");
            confirmButton.onClick.Invoke();

            Assert.AreEqual(1, calls, "El click del botón debe invocar el callback de confirmación.");
        }

        [Test]
        public void ConfirmButtonClick_AfterHide_DoesNotInvokeCallback()
        {
            int calls = 0;
            var content = new InteractionPromptContent("F", "Comprar", "Poción", string.Empty, 8, true);
            InteractionPromptView.Show(1, in content, () => calls++);
            InteractionPromptView.Hide(1);

            var confirmButton = GetPrivate<UnityEngine.UI.Button>(GetRuntimeInstance(), "_confirmButton");
            confirmButton.onClick.Invoke();

            Assert.AreEqual(0, calls, "Tras Hide, el callback quedó limpio — un click fantasma no debe comprar.");
        }

        // --------------------------------------------------------------
        // Reflection helpers — InteractionPromptView no expone accessors
        // públicos sólo-para-tests (ver remarks de la clase).
        // --------------------------------------------------------------

        private static object GetRuntimeInstance()
        {
            var field = typeof(InteractionPromptView).GetField("_instance",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "'_instance' no encontrado en InteractionPromptView.");
            var value = field.GetValue(null);
            Assert.IsNotNull(value, "InteractionPromptView.Show debió crear el runtime antes de inspeccionarlo.");
            return value;
        }

        private static T GetPrivate<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' no encontrado en {target.GetType().Name}.");
            return (T)field.GetValue(target);
        }
    }
}
