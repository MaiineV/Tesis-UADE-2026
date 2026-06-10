namespace Rollgeon.Meta
{
    /// <summary>
    /// En qué desenlace de run aplica un <see cref="UnlockDefinitionSO"/> (#164).
    /// <para>
    /// <b>Regla de evaluación mid-run.</b> Solo los unlocks con <see cref="Any"/>
    /// pueden completarse durante la run (notificación inmediata): si la condición
    /// exige ganar o perder, recién se sabe en la pantalla de resultados.
    /// </para>
    /// </summary>
    public enum UnlockOutcomeFilter
    {
        /// <summary>La condición solo cuenta si la run terminó ganada.</summary>
        Won,

        /// <summary>La condición solo cuenta si la run terminó perdida.</summary>
        Lost,

        /// <summary>Aplica en cualquier desenlace — elegible para unlock mid-run.</summary>
        Any,
    }
}
