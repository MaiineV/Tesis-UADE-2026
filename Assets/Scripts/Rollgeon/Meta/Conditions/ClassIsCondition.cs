using System;
using UnityEngine;

namespace Rollgeon.Meta.Conditions
{
    /// <summary>
    /// Por clase (#164): la run se jugó con la clase <see cref="ClassId"/>.
    /// La clase es fija desde el run start; el filtro de outcome del
    /// <see cref="UnlockDefinitionSO"/> decide si además exige ganar.
    /// </summary>
    [Serializable]
    public sealed class ClassIsCondition : IUnlockCondition
    {
        [Tooltip("EntityId de la clase requerida (ej. 'Warrior').")]
        public string ClassId;

        public bool Evaluate(UnlockEvaluationContext ctx)
        {
            if (ctx == null || string.IsNullOrEmpty(ClassId)) return false;
            return string.Equals(ctx.ClassId, ClassId, StringComparison.Ordinal);
        }
    }
}
