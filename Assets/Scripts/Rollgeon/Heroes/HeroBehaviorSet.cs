using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;

namespace Rollgeon.Heroes
{
    [Serializable, HideReferenceObjectPicker]
    public class HeroBehaviorSet
    {
        [FoldoutGroup("Movement")]
        [OdinSerialize]
        public HeroActionBehavior Movement = new HeroActionBehavior
        {
            ActionName = "Movement",
            EnergyCost = 1,
            BlockOnRepeat = false,
            FreeRollCount = 1,
            AllowsReroll = false,
        };

        [FoldoutGroup("Base Attack")]
        [OdinSerialize]
        public HeroActionBehavior BaseAttack = new HeroActionBehavior
        {
            ActionName = "Base Attack",
            EnergyCost = 1,
            BlockOnRepeat = true,
            FreeRollCount = 3,
            AllowsReroll = true,
        };

        [FoldoutGroup("Special Attack")]
        [OdinSerialize]
        public HeroActionBehavior SpecialAttack = new HeroActionBehavior
        {
            ActionName = "Special Attack",
            EnergyCost = 2,
            BlockOnRepeat = true,
            FreeRollCount = 1,
            AllowsReroll = false,
        };

        [FoldoutGroup("Healing")]
        [OdinSerialize]
        public HeroActionBehavior Healing = new HeroActionBehavior
        {
            ActionName = "Healing",
            EnergyCost = 1,
            BlockOnRepeat = true,
            FreeRollCount = 1,
            AllowsReroll = false,
        };

        public bool IsValid => Movement != null && BaseAttack != null
                               && SpecialAttack != null && Healing != null;

        public IEnumerable<HeroActionBehavior> All
        {
            get
            {
                yield return Movement;
                yield return BaseAttack;
                yield return SpecialAttack;
                yield return Healing;
            }
        }

        public HeroActionBehavior GetByIndex(int index)
        {
            switch (index)
            {
                case 0: return Movement;
                case 1: return BaseAttack;
                case 2: return SpecialAttack;
                case 3: return Healing;
                default: return null;
            }
        }

        public HeroBehaviorSet CreateRuntimeCopy()
        {
            return SerializationUtility.CreateCopy(this) as HeroBehaviorSet;
        }
    }
}
