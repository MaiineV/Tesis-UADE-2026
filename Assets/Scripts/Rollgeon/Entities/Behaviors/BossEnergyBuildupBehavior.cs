using System;
using Rollgeon.Entities.Bosses;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Entities.Behaviors
{
    /// <summary>
    /// Behavior del Boss Floor Manager — rastrea la energia interna del boss (independiente del
    /// stat <c>Energy</c> global del jugador). Se dispara en <c>OnTurnStart</c> y suma
    /// <see cref="BossFloorManagerSO.BossEnergyGainPerTurn"/> capeado a
    /// <see cref="BossFloorManagerSO.BossEnergyMax"/>. Cuando la energia esta llena, el valor
    /// queda accesible via <see cref="CurrentEnergy"/> para que <see cref="BossAttackBehavior"/>
    /// ruede el doble dano. Plan §4.4 / §10.3.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Self-contained.</b> No tocamos el pipeline de <c>Modifier&lt;float&gt;</c> (ver plan
    /// §10.3 — "Decision — self-contained"). El estado vive en el campo
    /// <see cref="CurrentEnergy"/> de la instancia clonada por entity spawn.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public class BossEnergyBuildupBehavior : BaseBehavior
    {
        public override string BehaviorName => "Boss Energy Buildup";

        /// <summary>Override del SO con tuning. Ver <see cref="BossComboBlockBehavior.BossDataOverride"/>.</summary>
        [Tooltip("Override opcional del BossFloorManagerSO con los tuning values.")]
        public BossFloorManagerSO BossDataOverride;

        /// <summary>
        /// Energia actual del boss. Expuesto para lectura desde <see cref="BossAttackBehavior"/>
        /// y tests. NonSerialized — persiste durante la vida de la instancia spawneada.
        /// </summary>
        [NonSerialized]
        public int CurrentEnergy;

        /// <summary><c>true</c> si <see cref="CurrentEnergy"/> alcanzo el max del SO.</summary>
        public bool IsEnergyFull
        {
            get
            {
                var so = BossDataOverride;
                if (so == null) return false;
                return CurrentEnergy >= so.BossEnergyMax;
            }
        }

        /// <inheritdoc />
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
