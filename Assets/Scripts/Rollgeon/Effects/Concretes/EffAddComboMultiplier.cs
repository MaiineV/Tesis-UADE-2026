using System;
using Rollgeon.Effects.Readers;
using Rollgeon.Upgrades;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Suma al bono ADITIVO del multiplicador del combo en curso
    /// (<c>EnchantmentScratch.ComboMultiplierBonus</c>): en la fórmula N×M,
    /// <c>M = (1 + Σ bonos) × Π factores × ability</c>. Es el "+X al multiplicador" del GDD
    /// (Piedra Angular +2, Ayuno +3, Segunda Oportunidad +1.5 por roll, Vértigo +0.05 por
    /// combo…). Hermano aditivo de <see cref="EffMultiplyComboDamage"/>: +2 y +3 de dos
    /// fuentes dan +5, nunca ×.
    /// </summary>
    /// <remarks>
    /// Los readers son enteros (<c>ReadFloat</c> cae en <c>Read</c>); la fracción por unidad
    /// va en <see cref="ReaderScale"/> (Vértigo: reader de combos × 0.05) para no crear un
    /// reader "Scaled" por item. Solo funciona dentro de un dispatch de trigger de combo
    /// (<see cref="ScratchTriggerContext"/>), igual que el resto de la familia scratch.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public class EffAddComboMultiplier : BaseEffect,
        IUsesValue, ICanBeConstantValue,
        IComboScratchWriter, IRequiresTriggerContext<ScratchTriggerContext>
    {
        [Title("Combo Multiplier Bonus")]
        [Tooltip("Cuánto sumar al bono de M. +2 = M pasa de 1 a 3 sin otros factores. Suma entre " +
                 "fuentes, no multiplica. Se ignora si hay reader.")]
        [SerializeField]
        private float _amount;

        [Tooltip("Opcional: reader que resuelve la cantidad en cada dispatch (vía ReadFloat) y PISA " +
                 "la constante. Segunda Oportunidad: ReadCurrentRolls; Fuente Mágica: " +
                 "ReadHighestContributingDie; Vértigo: ReadCombosSinceLastCombo. Null = constante.")]
        [OdinSerialize, SerializeReference]
        private EffectIntReader _amountReader;

        [Tooltip("Multiplica lo que devuelve el reader (los readers son enteros). Segunda " +
                 "Oportunidad: 1.5 por roll (era 3; playtest 2026-09-04); Dados en Reserva: 2 por " +
                 "dado; Vértigo: 0.05 por combo.")]
        [ShowIf("@_amountReader != null")]
        [SerializeField]
        private float _readerScale = 1f;

        protected override bool ShowSelection => false;

        public float Amount
        {
            get => _amount;
            set => _amount = value;
        }

        public EffectIntReader AmountReader
        {
            get => _amountReader;
            set => _amountReader = value;
        }

        public float ReaderScale
        {
            get => _readerScale;
            set => _readerScale = value;
        }

        public override string GetEffectName() => "Add Combo Multiplier";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null
                || !context.TryGetTriggerContext<ScratchTriggerContext>(out var trig)
                || trig.Scratch == null)
            {
                Debug.LogWarning("[EffAddComboMultiplier] sin ScratchTriggerContext — este efecto " +
                                 "solo funciona dentro de un dispatch de trigger de combo.");
                return false;
            }

            float bonus = _amountReader != null
                ? _amountReader.ReadFloat(context) * _readerScale
                : _amount;
            trig.Scratch.ComboMultiplierBonus += bonus;
            return true;
        }
    }
}
