using System;
using System.Collections.Generic;
using Rollgeon.Attributes;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Threat;
using Rollgeon.Editor.Tools.Enemy.AITree;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Entities;
using Rollgeon.Entities.Behaviors;
using Rollgeon.PreConditions;

namespace Rollgeon.Editor.Tools.Enemy
{
    /// <summary>
    /// Lo que el árbol de IA (y la lista de Behaviors) realmente hace, derivado del dato: qué
    /// movimiento usa, qué formas telegrafía, qué precondiciones y efectos aparecen. Es la
    /// contraparte "real" de la ficha declarativa (<see cref="EnemyDesignSheet"/>): el validador
    /// cruza las dos. Solo cuentan los nodos alcanzables desde la raíz; los sueltos no se ejecutan.
    /// </summary>
    public sealed class EnemyTreeSummary
    {
        public bool HasTree;
        public int NodeCount;
        public int DetachedCount;
        public readonly List<Type> MovementNodes = new List<Type>();
        public readonly HashSet<ThreatShape> TelegraphShapes = new HashSet<ThreatShape>();
        public bool HasTelegraph;
        public bool HasRangedShot;
        public readonly HashSet<Type> PreConditionTypes = new HashSet<Type>();
        public readonly HashSet<Type> EffectTypes = new HashSet<Type>();
        public bool HasHeal;
        public bool HasBuff;
        public bool SpawnsReinforcements;
        public bool UsesBehaviorsList;

        public bool HasMovement => MovementNodes.Count > 0;
        public bool KeepsDistance => MovementNodes.Contains(typeof(AINode_KeepDistance));

        public static EnemyTreeSummary Build(EnemyDataSO so)
        {
            var s = new EnemyTreeSummary();
            if (so == null) return s;

            if (so.AIRoot != null)
            {
                s.HasTree = true;
                var snap = AITreeSerializer.Load(so.AIRoot);
                s.NodeCount = snap.Nodes.Count;
                foreach (var node in snap.Nodes) s.Visit(node);
            }
            if (so.AIDetachedNodes != null)
            {
                foreach (var d in so.AIDetachedNodes) if (d != null) s.DetachedCount++;
            }

            if (so.Behaviors != null)
            {
                foreach (var b in so.Behaviors)
                {
                    if (b == null) continue;
                    s.UsesBehaviorsList = true;
                    if (b is SupportHealBehavior) s.HasHeal = true;
                    if (b is EnemyActionBehavior action) s.VisitBehavior(action);
                }
            }
            return s;
        }

        void Visit(AIDecisionNode node)
        {
            switch (node)
            {
                case AINode_Move _:
                case AINode_KeepDistance _:
                case AINode_MoveToAlign _:
                case AINode_TeleportNearTarget _:
                case AINode_TeleportAwayToEdge _:
                case AINode_TeleportToRoomCenter _:
                    if (!MovementNodes.Contains(node.GetType())) MovementNodes.Add(node.GetType());
                    break;
                case AINode_TelegraphMark t:
                    HasTelegraph = true;
                    TelegraphShapes.Add(t.Shape);
                    break;
                case AINode_AuxTelegraph a:
                    HasTelegraph = true;
                    TelegraphShapes.Add(a.Shape);
                    break;
                case AINode_ExecuteTelegraph _:
                case AINode_IgniteArea _:
                    HasTelegraph = true;
                    break;
                case AINode_SpawnReinforcements _:
                    SpawnsReinforcements = true;
                    break;
                case AINode_ApplyStatModifier _:
                    HasBuff = true;
                    break;
                case AINode_If i:
                    CollectConditions(i.Conditions);
                    break;
                case AINode_While w:
                    CollectConditions(w.Conditions);
                    break;
                case AINode_Behavior b:
                    if (b.Behavior != null) VisitBehavior(b.Behavior);
                    break;
            }

            // Los jefes tienen variantes propias de disparo y telegraph (CashierRangedShot,
            // TelegraphMarkGoldScaled): se detectan por herencia o por nombre.
            if (node is AINode_RangedShot) HasRangedShot = true;
            else if (node.GetType().Name.Contains("Telegraph")) HasTelegraph = true;
        }

        void VisitBehavior(EnemyActionBehavior behavior)
        {
            if (behavior.Effects == null) return;
            foreach (var group in behavior.Effects)
            {
                if (group == null) continue;
                CollectConditions(group.PreConditions);
                if (group.Effects == null) continue;
                foreach (var eff in group.Effects)
                {
                    if (eff == null) continue;
                    foreach (var e in EffectTree.SelfAndDescendants(eff))
                    {
                        if (e == null) continue;
                        EffectTypes.Add(e.GetType());
                        switch (e)
                        {
                            case EffHeal _:
                                HasHeal = true;
                                break;
                            case EffModifyIntAttribute m when m.TargetStat == StatType.Health && m.Operation == IntOperation.Add:
                                HasHeal = true;
                                break;
                            case EffAddShield _:
                            case EffLowHpAttackBuff _:
                                HasBuff = true;
                                break;
                        }
                    }
                }
            }
        }

        void CollectConditions(List<BasePreCondition> conditions)
        {
            if (conditions == null) return;
            foreach (var pc in conditions)
            {
                if (pc == null) continue;
                PreConditionTypes.Add(pc.GetType());
                if (pc is PCComposite composite) CollectConditions(composite.Children);
            }
        }

        // ---- presentación --------------------------------------------------

        public static string Names(IEnumerable<Type> types)
        {
            var list = new List<string>();
            foreach (var t in types) list.Add(ShortName(t));
            list.Sort(StringComparer.Ordinal);
            return list.Count == 0 ? "—" : string.Join(", ", list);
        }

        public static string ShortName(Type t)
        {
            string n = t.Name;
            if (n.StartsWith("AINode_", StringComparison.Ordinal)) return n.Substring("AINode_".Length);
            return n;
        }

        public string ShapesText()
        {
            if (TelegraphShapes.Count == 0) return HasTelegraph ? "sí (forma heredada del jefe)" : "no";
            var list = new List<string>();
            foreach (var sh in TelegraphShapes) list.Add(sh.ToString());
            list.Sort(StringComparer.Ordinal);
            return string.Join(", ", list);
        }
    }
}
