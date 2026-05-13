using System;
using Rollgeon.Combat.AI.Targeting;
using Rollgeon.Effects;
using Rollgeon.Entities.Behaviors;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy
{
    /// <summary>
    /// Pre-armed behavior templates so authors don't start from null inspectors.
    /// Each template returns a fully-instantiated <see cref="BaseBehavior"/> with sane
    /// defaults. New templates are added here, not as ScriptableObjects, so they can
    /// freely cross-reference subtypes.
    /// </summary>
    public static class EnemyBehaviorPalette
    {
        public static void Show(Action<BaseBehavior> onPick)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Action — Empty (EnemyActionBehavior)"), false, () => onPick(BuildEmptyAction()));
            menu.AddItem(new GUIContent("Action — Always-target Player (EnemyActionBehavior)"), false, () => onPick(BuildPlayerAction()));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Support — Heal Lowest-HP Ally"), false, () => onPick(new SupportHealBehavior()));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Boss — Combo Block"), false, () => onPick(new BossComboBlockBehavior()));
            menu.AddItem(new GUIContent("Boss — Combo Immunity"), false, () => onPick(new BossComboImmunityBehavior()));
            menu.AddItem(new GUIContent("Boss — Energy Buildup"), false, () => onPick(new BossEnergyBuildupBehavior()));
            menu.AddItem(new GUIContent("Boss — Attack"), false, () => onPick(new BossAttackBehavior()));
            menu.ShowAsContext();
        }

        static BaseBehavior BuildEmptyAction()
        {
            return new EnemyActionBehavior
            {
                ActionName = "New Action",
                Trigger = BehaviorTrigger.OnTurnStart,
                AllowedPhases = GamePhaseMask.All,
                Effects = new System.Collections.Generic.List<EffectData>(),
            };
        }

        static BaseBehavior BuildPlayerAction()
        {
            var behavior = (EnemyActionBehavior)BuildEmptyAction();
            behavior.ActionName = "Attack Player";
            behavior.TargetSelector = new TargetSelector_AlwaysPlayer();
            return behavior;
        }
    }
}
