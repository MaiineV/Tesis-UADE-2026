using System;

namespace Rollgeon.Meta.Conditions
{
    /// <summary>
    /// Placeholder para contenido gateado pero todavía no implementado (ej. clases
    /// Mage/Rogue sin ClassHeroSO): nunca se cumple. Reemplazar por la condición
    /// real desde la Unlock Condition Tool cuando el contenido exista.
    /// </summary>
    [Serializable]
    public sealed class ComingSoonCondition : IUnlockCondition
    {
        public bool Evaluate(UnlockEvaluationContext ctx) => false;
    }
}
