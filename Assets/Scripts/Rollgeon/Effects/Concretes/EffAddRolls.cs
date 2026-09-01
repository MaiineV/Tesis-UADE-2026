using System;
using Patterns;
using Rollgeon.Combat.Rolls;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Suma rolls al pool del jugador AHORA (clampeado al máximo,
    /// <see cref="IRollPoolService.AddRolls"/>). Para items/pasivas tipo "al hacer X
    /// ganás una tirada". Solo tiene sentido en combate — fuera, el pool no existe.
    /// </summary>
    /// <remarks>
    /// Para el bonus PERMANENTE de pool (Llamado de Emergencia) usar
    /// <c>ItemSO.RollPoolBonus</c>, no este effect: un effect colgado de
    /// <c>OnItemObtained</c> se re-dispararía en cada restore de save y duplicaría
    /// el bonus. Este effect es para ganancias puntuales dentro del combate.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffAddRolls : BaseEffect, IUsesValue, ICanBeGenericValue
    {
        [OdinSerialize, SerializeReference]
        [Tooltip("Cuántos rolls sumar (reader: constante o dinámico).")]
        private EffectIntReader _amount = new ReadConstantInt { Value = 1 };

        public EffectIntReader Amount
        {
            get => _amount;
            set => _amount = value;
        }

        protected override bool ShowSelection => false;

        public override string GetEffectName() => "Add Rolls";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null || context.SourceGuid == Guid.Empty) return false;
            if (!ServiceLocator.TryGetService<IRollPoolService>(out var rolls) || rolls == null)
            {
                Debug.LogWarning("[EffAddRolls] IRollPoolService no registrado.");
                return false;
            }
            if (!rolls.IsCombatActive) return true; // fuera de combate el pool no existe: no-op

            int amount = _amount?.Read(context) ?? 0;
            if (amount > 0) rolls.AddRolls(context.SourceGuid, amount);
            return true;
        }
    }
}
