using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Upgrades.Character
{
    /// <summary>
    /// Reward de stats del personaje — Canal Personaje del Sistema de Mejoras
    /// In-Run. Otorgado al clearear un boss (o sala desafío, futuro): el player
    /// elige uno de 3 spawneados en pedestales en la sala.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Application.</b> Al claim, la <c>CharacterRewardService</c> crea un
    /// <c>Modifier&lt;int&gt;</c> con <c>ModifierDirection.Intrinsic</c> +
    /// <c>ModifierOperation.Add</c> + <c>ModifierLifetime.Run</c> y lo aplica al
    /// stat target del player. Permanente para la run.
    /// </para>
    /// <para>
    /// <b>Amount via reader.</b> Default es un <c>ReadConstantInt(5)</c> pero el
    /// patrón "readers de todos los sistemas" del repo permite scaling — ej.
    /// <c>ReadComboCounter('combo.par')</c> para un reward que crece con el uso
    /// de un combo.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(
        menuName = "Rollgeon/Upgrades/Character/Character Reward",
        fileName = "CharacterReward")]
    public class CharacterRewardSO : UpgradeSO
    {
        [Title("Target Stat")]
        [Tooltip("Stat base del personaje que este reward modifica. " +
                 "Phase 9 soporta Health/Energy/Speed/Attack — extender el switch " +
                 "del CharacterRewardService para más.")]
        [OdinSerialize]
        protected CharacterRewardTargetStat _targetStat = CharacterRewardTargetStat.Health;

        [Title("Amount")]
        [InfoBox("Magnitud del bonus aplicado al stat. Operation = Add. " +
                 "Usá ReadConstantInt(N) para fijo. Designers avanzados pueden " +
                 "componer readers para scaling.")]
        [Required]
        [OdinSerialize, SerializeReference]
        protected EffectIntReader _amount;

        [Title("Visual")]
        [InfoBox("Prefab 3D que se instancia ENCIMA del pedestal de reward al spawnear. " +
                 "Mismo rol que ItemSO.WorldPrefab. Null = pedestal solo (sin visual extra).")]
        [OdinSerialize]
        protected GameObject _worldPrefab;

        /// <inheritdoc />
        public override UpgradeChannel Channel => UpgradeChannel.Character;

        /// <summary>Stat target.</summary>
        public CharacterRewardTargetStat TargetStat => _targetStat;

        /// <summary>Reader del amount. Null = no aplica (warning en service).</summary>
        public EffectIntReader Amount => _amount;

        /// <summary>Prefab 3D del visual sobre el pedestal. Null = sin visual.</summary>
        public GameObject WorldPrefab => _worldPrefab;
    }
}
