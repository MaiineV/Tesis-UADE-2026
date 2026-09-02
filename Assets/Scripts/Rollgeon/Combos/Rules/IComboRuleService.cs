namespace Rollgeon.Combos.Rules
{
    /// <summary>
    /// Reglas de validación de combos que los items pueden ALTERAR mientras estén en el
    /// inventario (GDD "Ítems que modifican la forma de calcular o resolver combos").
    /// Los <c>BaseComboSO</c> consultan acá antes de matchear; sin servicio rigen las
    /// reglas estándar.
    /// </summary>
    /// <remarks>
    /// Cada regla se habilita por fuente (ItemId) y queda activa mientras al menos una
    /// fuente la sostenga — registrar dos veces la misma fuente no duplica nada y quitar
    /// una fuente que no estaba no rompe nada.
    /// </remarks>
    public interface IComboRuleService
    {
        /// <summary>
        /// Escalera admite progresiones con un valor intermedio omitido (paso 2, cualquier
        /// paridad: 3-5-7-9-11, 2-4-6-8-10). Compás Salteado. Sigue siendo
        /// <c>combo.ladder</c>: las pasivas de Escalera aplican igual.
        /// </summary>
        bool LadderAllowsSkippedStep { get; }

        void AddLadderSkippedStep(string sourceId);

        void RemoveLadderSkippedStep(string sourceId);
    }
}
