using System;
using Rollgeon.Attributes;
using Rollgeon.Combat.AI.Targeting;
using Rollgeon.Effects;
using Rollgeon.Effects.Selection;
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
            menu.AddItem(new GUIContent("Acción — Vacía"), false, () => onPick(BuildEmptyAction()));
            menu.AddItem(new GUIContent("Acción — Siempre al jugador"), false, () => onPick(BuildPlayerAction()));
            menu.AddItem(new GUIContent("Acción — Al aliado con menos vida"), false, () => onPick(BuildLowestHpAllyAction()));
            menu.AddItem(new GUIContent("Acción — Al rival con más ataque"), false, () => onPick(BuildHighestAttackEnemyAction()));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Apoyo — Curar al aliado con menos vida"), false, () => onPick(new SupportHealBehavior()));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Jefe — Bloqueo de combo"), false, () => onPick(new BossComboBlockBehavior()));
            menu.AddItem(new GUIContent("Jefe — Inmunidad a combo"), false, () => onPick(new BossComboImmunityBehavior()));
            menu.AddItem(new GUIContent("Jefe — Acumulación de energía"), false, () => onPick(new BossEnergyBuildupBehavior()));
            menu.AddItem(new GUIContent("Jefe — Ataque"), false, () => onPick(new BossAttackBehavior()));
            menu.ShowAsContext();
        }

        static BaseBehavior BuildEmptyAction()
        {
            return new EnemyActionBehavior
            {
                ActionName = "Nueva acción",
                Trigger = BehaviorTrigger.OnTurnStart,
                AllowedPhases = GamePhaseMask.All,
                Effects = new System.Collections.Generic.List<EffectData>(),
            };
        }

        static BaseBehavior BuildPlayerAction()
        {
            var behavior = (EnemyActionBehavior)BuildEmptyAction();
            behavior.ActionName = "Atacar al jugador";
            behavior.TargetSelector = new TargetSelector_AlwaysPlayer();
            return behavior;
        }

        static BaseBehavior BuildLowestHpAllyAction()
        {
            var behavior = (EnemyActionBehavior)BuildEmptyAction();
            behavior.ActionName = "Apoyar al aliado con menos vida";
            behavior.TargetSelector = new TargetSelector_ByAttribute
            {
                Relation = EntityFilterMask.Allies,
                Stat = StatType.Health,
                Mode = ExtremumMode.Lowest,
            };
            return behavior;
        }

        static BaseBehavior BuildHighestAttackEnemyAction()
        {
            var behavior = (EnemyActionBehavior)BuildEmptyAction();
            behavior.ActionName = "Enfocar al rival con más ataque";
            behavior.TargetSelector = new TargetSelector_ByAttribute
            {
                Relation = EntityFilterMask.Enemies,
                Stat = StatType.Attack,
                Mode = ExtremumMode.Highest,
            };
            return behavior;
        }
    }
}
