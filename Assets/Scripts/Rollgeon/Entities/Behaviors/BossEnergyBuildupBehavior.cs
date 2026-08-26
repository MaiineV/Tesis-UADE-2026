using System;
using Rollgeon.Entities.Bosses;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Entities.Behaviors
{
    /// <summary>
    /// Energia interna del boss, independiente del stat <c>Energy</c> global del jugador: el estado
    /// vive en el campo <see cref="CurrentEnergy"/> de la instancia clonada por entity spawn, no en
    /// el pipeline de <c>Modifier&lt;float&gt;</c>.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class BossEnergyBuildupBehavior : BaseBehavior
    {
        public override string BehaviorName => "Boss Energy Buildup";

        [Tooltip("Override opcional del BossFloorManagerSO con los tuning values.")]
        public BossFloorManagerSO BossDataOverride;

        /// <summary>Expuesto para lectura desde <see cref="BossAttackBehavior"/> y tests.</summary>
        [NonSerialized]
        public int CurrentEnergy;

        public bool IsEnergyFull
        {
            get
            {
                var so = BossDataOverride;
                if (so == null) return false;
                return CurrentEnergy >= so.BossEnergyMax;
            }
        }

        public override void Execute(BehaviorContext ctx)
        {
            if (ctx == null || ctx.SourceEntity == null) return;

            var so = BossDataOverride;
            if (so == null)
            {
                Debug.LogWarning(
                    "[BossEnergyBuildupBehavior] BossFloorManagerSO no asignado. " +
                    "Asigna BossDataOverride en el Inspector.");
                return;
            }

            int next = CurrentEnergy + so.BossEnergyGainPerTurn;
            if (next > so.BossEnergyMax) next = so.BossEnergyMax;
            if (next < 0) next = 0;
            CurrentEnergy = next;
        }
    }
}
