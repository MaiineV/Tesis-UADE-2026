namespace Rollgeon.Survey
{
    /// <summary>
    /// Tipo de pregunta del cuestionario de evento (Feature#0074). Se serializa por
    /// valor en <c>SurveyConfig.asset</c>: agregar miembros SOLO al final.
    /// </summary>
    public enum SurveyQuestionType
    {
        /// <summary>Cinco botones 1..5. Valor guardado: "1".."5".</summary>
        Rating1to5 = 0,

        /// <summary>Una opción entre N. Valor guardado: índice de la opción (misma columna en ES y EN).</summary>
        SingleChoice = 1,

        /// <summary>Texto libre multilínea. Valor guardado: texto trimmed.</summary>
        FreeText = 2,
    }
}
