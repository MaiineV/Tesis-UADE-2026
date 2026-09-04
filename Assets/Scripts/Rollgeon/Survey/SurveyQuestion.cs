using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rollgeon.Survey
{
    /// <summary>
    /// Una pregunta del cuestionario de evento (Feature#0074). Los textos van inline
    /// ES/EN porque cambian por evento y los edita alguien no-programador en el
    /// inspector; no vale la pena meterlos en la tabla de localización.
    /// </summary>
    [Serializable]
    public sealed class SurveyQuestion
    {
        [Tooltip("Id estable de la pregunta: columna q_<id> en la planilla. Solo letras, números y _. Ej: fun")]
        public string Id;

        [Tooltip("Tipo de control que se muestra.")]
        public SurveyQuestionType Type = SurveyQuestionType.Rating1to5;

        [Tooltip("Enunciado en español (obligatorio).")]
        [TextArea(2, 4)]
        public string TextEs;

        [Tooltip("Enunciado en inglés. Vacío = se muestra el español.")]
        [TextArea(2, 4)]
        public string TextEn;

        [Tooltip("Opciones en español (solo SingleChoice, mínimo 2).")]
        public List<string> OptionsEs = new List<string>();

        [Tooltip("Opciones en inglés, en el MISMO orden que las de español. Vacío = se muestran las de español.")]
        public List<string> OptionsEn = new List<string>();

        [Tooltip("Si está tildado, Enviar exige respuesta.")]
        public bool Required = true;

        [Tooltip("Largo máximo del texto libre (solo FreeText).")]
        public int MaxLength = 240;

        /// <summary>Enunciado en el idioma pedido, con fallback al español.</summary>
        public string GetText(string localeCode)
        {
            return IsEnglish(localeCode) && !string.IsNullOrWhiteSpace(TextEn) ? TextEn : TextEs;
        }

        /// <summary>
        /// Opciones en el idioma pedido. Las inglesas solo se usan si están completas
        /// (mismo largo que las españolas): un desfasaje cambiaría el índice guardado.
        /// </summary>
        public IReadOnlyList<string> GetOptions(string localeCode)
        {
            var es = OptionsEs ?? new List<string>();
            if (IsEnglish(localeCode) && OptionsEn != null && OptionsEn.Count == es.Count && es.Count > 0)
            {
                return OptionsEn;
            }
            return es;
        }

        internal static bool IsEnglish(string localeCode)
            => !string.IsNullOrEmpty(localeCode) && localeCode.StartsWith("en", StringComparison.OrdinalIgnoreCase);
    }
}
