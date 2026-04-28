using System;
using Patterns;
using Rollgeon.Combat.ComboBlock;
using Rollgeon.Combos;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Entities.Behaviors
{
    /// <summary>
    /// Pasiva de boss que hace que un combo específico (típicamente "Par") no pegue contra
    /// la entidad. Cada turno del boss re-bloquea el combo via <see cref="IComboBlockService"/>
    /// con duración alta; como <c>Block</c> toma el max de las duraciones, el bloqueo se
    /// renueva y nunca expira en la práctica durante el combate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué `OnTurnStart`</b>: no existe trigger <c>OnCombatStart</c> en el sistema de
    /// behaviors actual. Setear el bloqueo cada turno del boss es idempotente y mantiene la
    /// pasiva activa durante todo el combate. Edge case mínimo: si el jugador tira el combo
    /// antes del primer turno del boss, ese hit pega — para FP es aceptable.
    /// </para>
    /// <para>
    /// <b>Cleanup</b>: <see cref="ComboBlockService"/> ya escucha <c>OnCombatEnd</c> y limpia
    /// los bloqueos activos, así que la inmunidad no leakea a combates posteriores.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public class BossComboImmunityBehavior : BaseBehavior
    {
        public override string BehaviorName => "Boss Combo Immunity";

        [Title("Immunity")]
        [Required("Arrastrar el combo al que el boss es inmune (ej. Combo_Par).")]
        [Tooltip("Combo cuyo daño no afecta al boss. Default esperado: Par.")]
        public BaseComboSO ImmuneCombo;

        [MinValue(1)]
        [Tooltip("Duración (en turnos del jugador) de cada renovación del bloqueo. " +
                 "El bloqueo se re-aplica cada turno del boss, así que con 99 alcanza para " +
                 "que nunca expire dentro del combate.")]
        public int RefreshDurationTurns = 99;

        public override void Execute(BehaviorContext ctx)
        {
            if (ImmuneCombo == null)
            {
                Debug.LogWarning("[BossComboImmunityBehavior] ImmuneCombo no asignado — la pasiva no aplica.");
                return;
            }

            var comboId = ImmuneCombo.ComboId;
            if (string.IsNullOrEmpty(comboId))
            {
                Debug.LogWarning(
                    $"[BossComboImmunityBehavior] El combo '{ImmuneCombo.name}' tiene ComboId vacío — la pasiva no aplica.");
                return;
            }

            if (!ServiceLocator.TryGetService<IComboBlockService>(out var blockService) || blockService == null)
            {
                Debug.LogError(
                    "[BossComboImmunityBehavior] IComboBlockService no registrado. " +
                    "Agregá ComboBlockServiceBootstrap a ServiceBootstrapSO.ExtraServices.");
                return;
            }

            blockService.Block(comboId, RefreshDurationTurns);
        }
    }
}
