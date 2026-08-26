using System;
using Patterns;
using Rollgeon.Combat.ComboBlock;
using Rollgeon.Combos;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Entities.Behaviors
{
    /// <summary>
    /// Re-bloquea el combo cada turno del boss con duración alta y <c>Block</c> toma el max de las
    /// duraciones, así que el bloqueo se renueva y nunca expira durante el combate. Va en
    /// <c>OnTurnStart</c> porque no existe trigger <c>OnCombatStart</c>: si el jugador tira el combo
    /// antes del primer turno del boss, ese hit pega. No limpia nada al terminar —
    /// <see cref="ComboBlockService"/> ya escucha <c>OnCombatEnd</c>.
    /// </summary>
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
            Debug.Log($"[BossComboImmunityBehavior] Block('{comboId}', {RefreshDurationTurns}) — IsBlocked now={blockService.IsBlocked(comboId)}");
        }
    }
}
