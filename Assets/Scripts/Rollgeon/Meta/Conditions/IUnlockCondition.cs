namespace Rollgeon.Meta.Conditions
{
    /// <summary>
    /// Bloque condicional de la Unlock Condition Tool (#164). Las implementaciones
    /// son clases planas <c>[Serializable]</c> que Odin serializa polimórficamente
    /// dentro del <see cref="UnlockDefinitionSO"/>, componibles con
    /// <see cref="AndCondition"/> / <see cref="OrCondition"/>.
    /// </summary>
    public interface IUnlockCondition
    {
        /// <summary>
        /// <c>true</c> si la condición se cumple contra el snapshot actual.
        /// Las condiciones de consistencia total devuelven <c>false</c> mientras
        /// <see cref="UnlockEvaluationContext.RunEnded"/> sea <c>false</c>.
        /// </summary>
        bool Evaluate(UnlockEvaluationContext ctx);

        /// <summary>
        /// <c>true</c> si la condición ya es imposible de cumplir en esta run
        /// (el jugador rompió la consistencia — ej. usó una poción, huyó de un
        /// combate). Default: nunca se invalida.
        /// </summary>
        bool IsInvalidated(UnlockEvaluationContext ctx) => false;
    }
}
