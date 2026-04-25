using System;
using Patterns;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Dungeon.State;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Intenta forzar una puerta bloqueada usando la tirada de dados del combate.
    /// Suma las caras de <see cref="EffectContext.DiceResult"/> y compara contra
    /// <see cref="RequiredValue"/>. Si la suma alcanza, setea
    /// <see cref="DoorState.Forced"/> = true y cruza la puerta.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffForceDoor : BaseEffect
    {
        [Title("Force Door")]
        [InfoBox("Usa la tirada de dados del combate actual. Si la suma de las caras " +
                 ">= RequiredValue, fuerza la puerta y cruza a la sala vecina.")]
        public DoorDirection Direction = DoorDirection.North;

        [Tooltip("Suma mínima de las caras de los dados para forzar la puerta.")]
        [Min(1)]
        public int RequiredValue = 10;

        public override string GetEffectName() => "Force Door";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context.DiceResult == null || context.DiceResult.Count == 0)
                return false;

            int sum = 0;
            for (int i = 0; i < context.DiceResult.Count; i++)
                sum += context.DiceResult[i];

            if (sum < RequiredValue)
                return false;

            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon))
                return false;

            var instance = dungeon.CurrentRoomInstance;
            if (instance == null) return false;

            var doorKey = Direction.DoorStateKey();
            if (instance.ObjectStates.TryGet<DoorState>(doorKey, out var doorState))
                doorState.Forced = true;

            return dungeon.EnterRoomByDoor(Direction);
        }
    }
}
