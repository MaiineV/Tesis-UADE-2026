using System;
using System.Collections.Generic;
using Rollgeon.Localization;
using Rollgeon.Tutorial.UI;
using UnityEngine;

namespace Rollgeon.UI.Help
{
    /// <summary>
    /// Secuencia de coach-marks para explicar una pantalla: encadena N pasos sobre el
    /// overlay del tutorial, cada uno con spotlight sobre un <see cref="RectTransform"/>
    /// y avance por click. La usa el armado de bolsa, tanto en la primera visita como
    /// desde el botón de ayuda.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reusa <see cref="ITutorialOverlayService"/> a propósito: ese overlay ya es un
    /// display genérico (ancla a rects arbitrarios, recorta, dibuja popup y flecha, y
    /// resigue el anchor por frame). Lo acoplado al combate es el
    /// <c>TutorialFlowController</c>, no el overlay.
    /// </para>
    /// <para>
    /// Es plain C# y recibe el overlay por constructor para poder testear el orden y el
    /// salteo de pasos sin escena.
    /// </para>
    /// </remarks>
    public sealed class BuildHelpFlow
    {
        /// <summary>Un paso: qué ilumina y qué dice.</summary>
        public readonly struct Step
        {
            /// <summary>Rect a iluminar. <c>null</c> = el paso se saltea (los refs de la
            /// screen son opcionales y pueden no estar cableados).</summary>
            public readonly RectTransform Anchor;

            /// <summary>
            /// Rects extra que el recorte también debe abarcar. Solo tiene sentido para
            /// elementos ADYACENTES: el recorte es el círculo que cubre el bounding box
            /// del grupo, así que sumar algo lejano lo centra entre ambos, sobre nada.
            /// </summary>
            public readonly RectTransform[] Extras;

            public readonly string TextKey;
            public readonly string Fallback;

            public Step(RectTransform anchor, string textKey, string fallback, params RectTransform[] extras)
            {
                Anchor = anchor;
                TextKey = textKey;
                Fallback = fallback;
                Extras = null;

                if (extras == null) return;
                // Los refs de la screen son opcionales: filtramos los nulos para no
                // pasar un array de puros huecos.
                var kept = new List<RectTransform>(extras.Length);
                foreach (var extra in extras)
                {
                    if (extra != null) kept.Add(extra);
                }
                if (kept.Count > 0) Extras = kept.ToArray();
            }
        }

        private readonly ITutorialOverlayService _overlay;
        private readonly Func<string, string, string> _localize;
        private readonly List<Step> _steps = new();
        private int _index = -1;

        /// <param name="localize">
        /// Resolución key → texto. Default <c>LocalizedContent.Ui</c>; los tests inyectan
        /// uno trivial para no depender del locale activo del editor (Localization tira
        /// "SelectedLocale is null" al salir de play mode y el runner lo toma como fallo).
        /// </param>
        public BuildHelpFlow(ITutorialOverlayService overlay,
                             Func<string, string, string> localize = null)
        {
            _overlay = overlay;
            _localize = localize ?? LocalizedContent.Ui;
        }

        public bool IsRunning => _index >= 0;

        /// <summary>Cantidad de pasos que realmente se van a mostrar (los de anchor null no cuentan).</summary>
        public int StepCount => _steps.Count;

        /// <summary>
        /// Arranca la guía. No hace nada si ya está corriendo (re-entrada por doble push
        /// o por clickear el botón de ayuda a mitad) o si no hay overlay registrado —
        /// abriendo la escena del menú suelta en el editor el servicio no existe.
        /// </summary>
        /// <returns><c>true</c> si arrancó.</returns>
        public bool Start(IEnumerable<Step> steps)
        {
            if (IsRunning) return false;

            if (_overlay == null)
            {
                Debug.LogWarning("[BuildHelpFlow] Sin ITutorialOverlayService registrado — " +
                                 "la guía no se muestra. ¿Se abrió la escena sin pasar por 00_Bootstrap?");
                return false;
            }

            _steps.Clear();
            foreach (var step in steps)
            {
                // Los refs de la screen son opcionales: un paso sin anchor no se muestra
                // y tampoco cuenta para el "(i/n)".
                if (step.Anchor != null) _steps.Add(step);
            }

            if (_steps.Count == 0) return false;

            _index = 0;
            ShowCurrent();
            return true;
        }

        /// <summary>
        /// Corta la guía y esconde el overlay. Obligatorio al salir de la pantalla: el
        /// overlay solo se auto-oculta al cambiar de escena, y mientras está visible con
        /// política bloqueante mantiene suprimido el input map de gameplay — dejarlo
        /// colgado se arrastra hasta la run siguiente.
        /// </summary>
        public void Stop()
        {
            _index = -1;
            _steps.Clear();
            _overlay?.Hide();
        }

        private void ShowCurrent()
        {
            var step = _steps[_index];
            string body = _localize(step.TextKey, step.Fallback);

            // El "(i/n)" va en el cuerpo: el footer del overlay está fijo en
            // "click para continuar" y no acepta texto extra.
            string text = _steps.Count > 1
                ? $"({_index + 1}/{_steps.Count}) {body}"
                : body;

            _overlay.Show(new TutorialStepDisplayRequest
            {
                AnchorKind = TutorialAnchorKind.RectTransform,
                UiTarget = step.Anchor,
                UiTargetsExtra = step.Extras,
                Text = text,
                InputPolicy = TutorialInputPolicy.BlockUntilContinue,
            }, Advance);
        }

        private void Advance()
        {
            if (!IsRunning) return;

            _index++;
            if (_index >= _steps.Count)
            {
                Stop();
                return;
            }
            ShowCurrent();
        }
    }
}
