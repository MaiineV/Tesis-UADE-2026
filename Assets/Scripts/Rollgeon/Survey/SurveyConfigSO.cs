using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Survey
{
    /// <summary>
    /// Configuración autoral del cuestionario de evento (Feature#0074). Va en
    /// <c>ServiceBootstrapSO.SettingsAssets</c>: el bootstrap lo registra por tipo y
    /// <see cref="SurveyServiceBootstrap"/> lo resuelve con
    /// <c>ServiceLocator.TryGetService&lt;SurveyConfigSO&gt;</c>, igual que <c>SteamConfigSO</c>.
    /// Se crea/cablea con el menú <b>Rollgeon → Survey → Setup Survey</b>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Survey Config", fileName = "SurveyConfig")]
    public sealed class SurveyConfigSO : ScriptableObject
    {
        /// <summary>Largo máximo del nombre de una pestaña de Google Sheets.</summary>
        public const int MaxEventIdLength = 100;

        private const string InvalidEventIdChars = "[]:*?/\\";

        [Title("Activación")]
        [Tooltip("Muestra la encuesta en editor y en builds normales. La build 'Evento' (define ROLLGEON_EVENT_BUILD) la muestra SIEMPRE, con o sin este tick.")]
        public bool Enabled;

        [Tooltip("Nombre de la pestaña de la planilla donde caen las respuestas. Una por evento. Sin [ ] : * ? / \\")]
        public string EventId = "evento";

        [Tooltip("Piso (0-based) cuya recompensa de boss dispara la encuesta. 0 = al terminar el primer piso.")]
        [Min(0)]
        public int TriggerFloorIndex;

        [Title("Envío")]
        [Tooltip("URL /exec del Apps Script desplegado como Web App. Vacío = solo se guarda en disco.")]
        public string EndpointUrl;

        [Tooltip("Secreto compartido que el Apps Script valida. Opcional. Viaja en el POST, nunca se escribe en disco.")]
        public string SharedSecret;

        [Tooltip("Timeout del POST en segundos.")]
        [Min(1)]
        public int TimeoutSeconds = 10;

        [Title("Formulario")]
        [Tooltip("Muestra el bloque 'Quiero participar del sorteo' + email.")]
        public bool AskEmailForRaffle = true;

        [Tooltip("Segundos que queda visible el estado ('Enviado' / 'Guardado') antes de cerrarse solo.")]
        [Min(0f)]
        public float AutoCloseSeconds = 1.5f;

        [Tooltip("Preguntas, en el orden en que se muestran.")]
        [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true)]
        public List<SurveyQuestion> Questions = new List<SurveyQuestion>();

        /// <summary><c>true</c> si hay al menos una pregunta para mostrar.</summary>
        public bool HasQuestions => Questions != null && Questions.Count > 0;

        /// <summary><c>true</c> si hay a dónde mandar las respuestas.</summary>
        public bool HasEndpoint => !string.IsNullOrWhiteSpace(EndpointUrl);

        /// <summary>
        /// Valida la config sin tocar Unity (testeable). Los errores impiden que la
        /// encuesta sirva; los warnings son cosas con las que igual se puede salir
        /// al stand (ej. sin endpoint: se guarda offline).
        /// </summary>
        public bool Validate(List<string> errors, List<string> warnings = null)
        {
            errors ??= new List<string>();
            int before = errors.Count;

            ValidateEventId(errors);

            if (TriggerFloorIndex < 0)
            {
                errors.Add("TriggerFloorIndex debe ser >= 0.");
            }

            if (HasEndpoint)
            {
                if (!EndpointUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("EndpointUrl tiene que empezar con https://.");
                }
            }
            else
            {
                warnings?.Add("EndpointUrl vacío: las respuestas solo se guardan en disco (survey/pending).");
            }

            if (TimeoutSeconds < 1)
            {
                errors.Add("TimeoutSeconds debe ser >= 1.");
            }

            ValidateQuestions(errors);

            return errors.Count == before;
        }

        private void ValidateEventId(List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(EventId))
            {
                errors.Add("EventId vacío.");
                return;
            }

            if (EventId.Length > MaxEventIdLength)
            {
                errors.Add($"EventId supera los {MaxEventIdLength} caracteres (límite de nombre de pestaña de Sheets).");
            }

            if (EventId.IndexOfAny(InvalidEventIdChars.ToCharArray()) >= 0)
            {
                errors.Add($"EventId no puede contener {InvalidEventIdChars}.");
            }
        }

        private void ValidateQuestions(List<string> errors)
        {
            if (!HasQuestions)
            {
                errors.Add("No hay preguntas.");
                return;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Questions.Count; i++)
            {
                var q = Questions[i];
                string label = $"Pregunta #{i}";
                if (q == null)
                {
                    errors.Add($"{label}: entry null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(q.Id))
                {
                    errors.Add($"{label}: Id vacío.");
                }
                else
                {
                    if (!IsValidId(q.Id))
                    {
                        errors.Add($"{label} ('{q.Id}'): Id solo admite letras, números y _.");
                    }

                    if (!seen.Add(q.Id))
                    {
                        errors.Add($"{label} ('{q.Id}'): Id duplicado.");
                    }
                }

                if (string.IsNullOrWhiteSpace(q.TextEs))
                {
                    errors.Add($"{label} ('{q.Id}'): TextEs vacío.");
                }

                switch (q.Type)
                {
                    case SurveyQuestionType.SingleChoice:
                        int es = q.OptionsEs?.Count ?? 0;
                        int en = q.OptionsEn?.Count ?? 0;
                        if (es < 2)
                        {
                            errors.Add($"{label} ('{q.Id}'): SingleChoice necesita al menos 2 opciones en OptionsEs.");
                        }

                        if (en != 0 && en != es)
                        {
                            errors.Add($"{label} ('{q.Id}'): OptionsEn tiene {en} entradas y OptionsEs {es}; deben coincidir o dejar OptionsEn vacío.");
                        }
                        break;

                    case SurveyQuestionType.FreeText:
                        if (q.MaxLength < 1)
                        {
                            errors.Add($"{label} ('{q.Id}'): MaxLength debe ser >= 1.");
                        }
                        break;
                }
            }
        }

        private static bool IsValidId(string id)
        {
            foreach (char c in id)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok) return false;
            }
            return true;
        }
    }
}
