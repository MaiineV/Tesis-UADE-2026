using System.Collections.Generic;
using Rollgeon.Attributes;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Combat.AI.Targeting;
using Rollgeon.Combat.Threat;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Feedback;
using Rollgeon.PreConditions.Concretes;
using Rollgeon.Tiles;
using UnityEditor;

namespace Rollgeon.Editor.Tools.Enemy.Templates
{
    /// <summary>
    /// Piezas del esqueleto canónico de los enemigos autorados a mano (MeleeCard, Ranged,
    /// Healer…), para que una plantilla se arme combinando lo mismo que el designer ve en el
    /// canvas: Behaviors con Eff, If con PC, Move/KeepDistance/Telegraph. Todo público y sin
    /// estado para que otras herramientas lo reusen.
    /// </summary>
    public static class EnemyTreeKit
    {
        public const string ResetEnergyName = "Recargar energía";
        public const string SpendEnergyName = "Gastar energía";

        // ---- efectos y behaviors ------------------------------------------

        public static EffectData Group(params IEffect[] effects)
        {
            var g = new EffectData();
            g.Effects.AddRange(effects);
            return g;
        }

        public static AINode_Behavior Behavior(string name, BaseEnemyTargetSelector selector, params EffectData[] groups)
        {
            var b = new EnemyActionBehavior { ActionName = name, TargetSelector = selector };
            b.Effects.AddRange(groups);
            return new AINode_Behavior { Behavior = b };
        }

        public static AINode_Behavior ResetEnergy(int max = 3)
            => Behavior(ResetEnergyName, new TargetSelector_Self(),
                Group(EffectAuthoring.ModifyStat(StatType.Energy, IntOperation.Set, max)));

        public static AINode_Behavior SpendEnergy()
            => Behavior(SpendEnergyName, new TargetSelector_Self(),
                Group(EffectAuthoring.ModifyStat(StatType.Energy, IntOperation.Subtract, 1)));

        /// <summary>
        /// <c>Sequence[Recargar, While(Energía > 0){ Sequence[Gastar, body] }]</c>: cada iteración
        /// paga una energía y corre <paramref name="body"/>; el turno dura tantas acciones como
        /// energía tenga el enemigo.
        /// </summary>
        public static AINode_Sequence EnergyLoop(AIDecisionNode body, int maxEnergy = 3)
        {
            var loop = new AINode_While
            {
                TargetSelector = new TargetSelector_Self(),
                Body = Sequence(SpendEnergy(), body),
                MaxIterations = 16,
            };
            loop.Conditions.Add(new PcOwnerStatCompare
            {
                Stat = StatType.Energy, Comparison = IntComparison.Greater, Value = 0,
            });
            return Sequence(ResetEnergy(maxEnergy), loop);
        }

        /// <summary>Golpe de contacto: animación → daño desde ATK → impacto (vfx/sfx/feel/impulso).</summary>
        public static AINode_Behavior AttackMelee(string name = "Ataque", float multiplier = 1f)
            => Behavior(name, new TargetSelector_AlwaysPlayer(), Group(
                EffectAuthoring.Sequence(EffectAuthoring.Step("anim.enemy.melee.attack", StepEndMode.OnEvent, "hit")),
                EffectAuthoring.DealDamageFromStat(StatType.Attack, multiplier),
                EffectAuthoring.Sequence(
                    EffectAuthoring.Step("vfx.enemy.melee.impact"),
                    EffectAuthoring.Step("sfx.enemy.melee.hit"),
                    EffectAuthoring.Step("feel.enemy.melee.impact"),
                    EffectAuthoring.Step("hit.impulse"))));

        /// <summary>Disparo directo: mismo pipeline que el melee con los feedbacks <c>enemy.ranged.*</c>.</summary>
        public static AINode_Behavior AttackRanged(string name = "Disparo", float multiplier = 1f)
            => Behavior(name, new TargetSelector_AlwaysPlayer(), Group(
                EffectAuthoring.Sequence(EffectAuthoring.Step("anim.enemy.ranged.attack", StepEndMode.OnDuration, "hit", 0.25f)),
                EffectAuthoring.DealDamageFromStat(StatType.Attack, multiplier),
                EffectAuthoring.Sequence(
                    EffectAuthoring.Step("vfx.enemy.ranged.impact"),
                    EffectAuthoring.Step("sfx.enemy.ranged.hit"),
                    EffectAuthoring.Step("feel.enemy.ranged.impact"))));

        /// <summary>Cura al aliado con menos vida (HealStrength), como ED_Healer.</summary>
        public static AINode_Behavior HealAlly(string name = "Curar")
            => Behavior(name, LowestHpAlly(), Group(
                EffectAuthoring.Sequence(EffectAuthoring.Step("anim.enemy.healer.cast", StepEndMode.OnEvent, "cast")),
                EffectAuthoring.HealFromStat(),
                EffectAuthoring.Sequence(
                    EffectAuthoring.Step("vfx.enemy.healer.heal"),
                    EffectAuthoring.Step("sfx.enemy.healer.cast"),
                    EffectAuthoring.Step("feel.enemy.healer.cast"))));

        // ---- selectores -----------------------------------------------------

        public static TargetSelector_ByAttribute LowestHpAlly() => new TargetSelector_ByAttribute
        {
            Relation = EntityFilterMask.Allies, Stat = StatType.Health, Mode = ExtremumMode.Lowest,
            UseModifiedValue = true, SkipDead = true,
        };

        public static TargetSelector_Nearest NearestAlly() => new TargetSelector_Nearest { Relation = EntityFilterMask.Allies };

        // ---- ramificación ---------------------------------------------------

        public static AINode_If IfTargetInRange(int range, AIDecisionNode then, AIDecisionNode @else = null,
                                                DistanceMetric metric = DistanceMetric.Manhattan,
                                                BaseEnemyTargetSelector selector = null)
        {
            var n = new AINode_If { TargetSelector = selector ?? new TargetSelector_AlwaysPlayer(), Then = then, Else = @else };
            n.Conditions.Add(new PcTargetInRange { Range = range, Metric = metric });
            return n;
        }

        public static AINode_If IfOwnerHpBelow(float percent, AIDecisionNode then, AIDecisionNode @else = null)
        {
            var n = new AINode_If { TargetSelector = new TargetSelector_Self(), Then = then, Else = @else };
            n.Conditions.Add(new PcOwnerHpBelow { Percent = percent });
            return n;
        }

        public static AINode_If IfAllyBelowMax(AIDecisionNode then, AIDecisionNode @else = null)
        {
            var n = new AINode_If { TargetSelector = new TargetSelector_Self(), Then = then, Else = @else };
            n.Conditions.Add(new PcAllyBelowMaxExists());
            return n;
        }

        public static AINode_Random Random(params (float weight, AIDecisionNode node)[] options)
        {
            var r = new AINode_Random();
            foreach (var (w, n) in options) r.Options.Add(new AINode_Random.Option { Weight = w, Node = n });
            return r;
        }

        public static AINode_Selector Selector(params AIDecisionNode[] children)
        {
            var s = new AINode_Selector();
            s.Children.AddRange(children);
            return s;
        }

        public static AINode_Sequence Sequence(params AIDecisionNode[] children)
        {
            var s = new AINode_Sequence();
            s.Children.AddRange(children);
            return s;
        }

        // ---- hojas ---------------------------------------------------------

        public static AINode_Move Chase(int steps = 3, int range = 1, BaseEnemyTargetSelector selector = null) => new AINode_Move
        {
            MaxSteps = Const(steps), DesiredRange = Const(range),
            TargetSelector = selector ?? new TargetSelector_AlwaysPlayer(),
        };

        public static AINode_Move MoveToAlly(int steps = 3, int range = 1) => Chase(steps, range, NearestAlly());

        public static AINode_KeepDistance Kite(int steps = 3, int ideal = 3)
            => new AINode_KeepDistance { MaxSteps = Const(steps), IdealDistance = Const(ideal) };

        /// <summary>Marca el área este turno; <see cref="ExecuteTelegraph"/> al inicio del siguiente la cobra.</summary>
        public static AINode_TelegraphMark Telegraph(ThreatShape shape, int size, int damage, int depth = 0)
        {
            var t = new AINode_TelegraphMark { Shape = shape, Size = size, Damage = damage };
            if (depth > 0) t.Depth = depth;
            return t;
        }

        public static AINode_ExecuteTelegraph ExecuteTelegraph() => new AINode_ExecuteTelegraph();

        /// <summary>Planta <paramref name="tile"/> sobre la marca pendiente (la del propio enemigo).</summary>
        public static AINode_IgniteArea Ignite(SpecialTileDefinitionSO tile, int durationRounds)
            => new AINode_IgniteArea { Definition = tile, DurationRounds = durationRounds };

        public static AINode_Wait Wait() => new AINode_Wait();

        public static AIConstantInt Const(int value) => new AIConstantInt { Value = value };

        /// <summary>Primer <see cref="SpecialTileDefinitionSO"/> cuyo nombre contiene el fragmento, o null.</summary>
        public static SpecialTileDefinitionSO FindTile(string nameFragment)
        {
            foreach (var guid in AssetDatabase.FindAssets($"{nameFragment} t:SpecialTileDefinitionSO"))
            {
                var tile = AssetDatabase.LoadAssetAtPath<SpecialTileDefinitionSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (tile != null && tile.name.Contains(nameFragment)) return tile;
            }
            return null;
        }
    }
}
