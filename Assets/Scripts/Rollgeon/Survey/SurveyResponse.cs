using System;
using System.Collections.Generic;

namespace Rollgeon.Survey
{
    /// <summary>Una respuesta a una pregunta. Campos en snake_case: es el wire format.</summary>
    [Serializable]
    public sealed class SurveyAnswer
    {
        public string id;
        public string value;

        public SurveyAnswer() { }

        public SurveyAnswer(string id, string value)
        {
            this.id = id;
            this.value = value;
        }
    }

    /// <summary>
    /// Una respuesta completa al cuestionario (Feature#0074). Es lo que se guarda en
    /// disco y, más <c>secret</c>, lo que viaja al Apps Script. Clase plana con campos
    /// públicos snake_case para que <c>JsonUtility</c> produzca el JSON que la
    /// planilla espera sin mapeos.
    /// </summary>
    [Serializable]
    public class SurveyResponse
    {
        /// <summary>Guid sin guiones. El Apps Script deduplica por este campo.</summary>
        public string response_id;

        /// <summary>Pestaña destino en la planilla.</summary>
        public string event_id;

        /// <summary>ISO-8601 UTC (formato "o").</summary>
        public string created_at;

        public string app_version;
        public string run_id;
        public int floor_index;
        public string hero_id;
        public string locale;

        /// <summary>Identifica la PC del stand, no a la persona.</summary>
        public string device_id;

        public bool raffle_opt_in;

        /// <summary>Vacío cuando <see cref="raffle_opt_in"/> es false.</summary>
        public string email = string.Empty;

        public List<SurveyAnswer> answers = new List<SurveyAnswer>();
    }
}
