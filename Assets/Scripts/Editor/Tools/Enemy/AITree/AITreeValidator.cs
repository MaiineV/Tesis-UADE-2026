using System;
using System.Collections.Generic;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Entities.Behaviors;
using Rollgeon.PreConditions;

namespace Rollgeon.Editor.Tools.Enemy.AITree
{
    public enum IssueSeverity { Error, Warning, Info }

    public sealed class ValidationIssue
    {
        public readonly AIDecisionNode Node;   // null = issue global del árbol
        public readonly string Message;
        public readonly IssueSeverity Severity;

        public ValidationIssue(AIDecisionNode node, string message, IssueSeverity severity)
        {
            Node = node; Message = message; Severity = severity;
        }
    }

    /// <summary>
    /// Diagnóstico del snapshot. Solo <see cref="IssueSeverity.Error"/> impide serializar (ciclo,
    /// multi-padre: no caben en un árbol). Todo lo demás es aviso: el árbol se guarda igual y el
    /// canvas lo marca. Los avisos de Eff/PC existen porque el bridge enemigo
    /// (<c>AIContextPcExtensions.BuildPcContext</c>, <c>EnemyActionBehavior.BuildEffectContext</c>)
    /// no tiene dados ni combo del jugador: una PC o un efecto que los lea no hace nada útil.
    /// </summary>
    public static class AITreeValidator
    {
        public static bool HasErrors(List<ValidationIssue> issues)
        {
            if (issues == null) return false;
            foreach (var i in issues) if (i.Severity == IssueSeverity.Error) return true;
            return false;
        }

        public static int Count(List<ValidationIssue> issues, IssueSeverity severity)
        {
            int c = 0;
            if (issues == null) return 0;
            foreach (var i in issues) if (i.Severity == severity) c++;
            return c;
        }

        public static List<ValidationIssue> Validate(GraphSnapshot snap)
        {
            var issues = new List<ValidationIssue>();
            if (snap == null)
            {
                issues.Add(new ValidationIssue(null, "Snapshot nulo.", IssueSeverity.Error));
                return issues;
            }

            // ---- estructura (errores) -------------------------------------
            var inbound = new Dictionary<AIDecisionNode, int>();
            foreach (var n in snap.Nodes) inbound[n] = 0;
            foreach (var e in snap.Edges)
                if (inbound.ContainsKey(e.Child)) inbound[e.Child]++;

            foreach (var n in snap.Nodes)
            {
                if (inbound.TryGetValue(n, out int c) && c > 1)
                    issues.Add(new ValidationIssue(n, "Tiene más de un padre: un árbol necesita un único padre por nodo.", IssueSeverity.Error));
            }

            // DFS desde todos los nodos: un ciclo puro (a → b → a) no tiene raíz suelta, así que
            // arrancar solo desde las raíces lo dejaría pasar.
            var done = new HashSet<AIDecisionNode>();
            foreach (var n in snap.Nodes)
            {
                if (n == null || done.Contains(n)) continue;
                if (HasCycle(n, snap, new HashSet<AIDecisionNode>(), done))
                {
                    issues.Add(new ValidationIssue(n, "Hay un ciclo: un nodo termina apuntando a uno de sus ancestros.", IssueSeverity.Error));
                    break;
                }
            }

            // ---- info global ----------------------------------------------
            if (snap.Root == null && snap.Nodes.Count > 0)
                issues.Add(new ValidationIssue(null, "Sin raíz: clic derecho en un nodo → Marcar como raíz.", IssueSeverity.Info));

            int detached = snap.DetachedRoots().Count;
            if (detached > 0)
                issues.Add(new ValidationIssue(null, detached == 1
                    ? "1 subárbol suelto (no se ejecuta hasta conectarlo)."
                    : $"{detached} subárboles sueltos (no se ejecutan hasta conectarlos).", IssueSeverity.Info));

            // ---- por nodo (avisos) ----------------------------------------
            foreach (var n in snap.Nodes)
            {
                if (n == null) continue;
                var slots = AITreeTopology.SlotsOf(n);
                int outgoing = 0;
                foreach (var e in snap.Edges) if (e.Parent == n) outgoing++;

                switch (n)
                {
                    case AINode_If i:
                        if (snap.ChildrenOf(n, 0).Count == 0)
                            issues.Add(new ValidationIssue(n, "If sin rama 'Entonces': nunca hace nada cuando la condición pasa.", IssueSeverity.Warning));
                        if (CountNonNull(i.Conditions) == 0)
                            issues.Add(new ValidationIssue(n, "If sin condiciones: siempre pasa.", IssueSeverity.Warning));
                        CheckConditions(n, i.Conditions, issues);
                        break;

                    case AINode_While w:
                        if (outgoing == 0)
                            issues.Add(new ValidationIssue(n, "While sin cuerpo.", IssueSeverity.Warning));
                        if (CountNonNull(w.Conditions) == 0)
                            issues.Add(new ValidationIssue(n, $"While sin condiciones: itera hasta MaxIterations ({w.MaxIterations}) y falla.", IssueSeverity.Warning));
                        CheckConditions(n, w.Conditions, issues);
                        break;

                    case AINode_Random r:
                        if (outgoing < 2)
                            issues.Add(new ValidationIssue(n, "Random con menos de dos opciones: no hay nada que sortear.", IssueSeverity.Warning));
                        else if (TotalWeight(r) <= 0f)
                            issues.Add(new ValidationIssue(n, "Random con peso total 0: ninguna opción puede salir.", IssueSeverity.Warning));
                        break;

                    case AINode_Behavior b:
                        if (b.Behavior == null)
                            issues.Add(new ValidationIssue(n, "Behavior vacío: elegí un tipo de behavior.", IssueSeverity.Warning));
                        else if (!HasAnyEffect(b.Behavior))
                            issues.Add(new ValidationIssue(n, "Behavior sin efectos: no hace nada al ejecutarse.", IssueSeverity.Warning));
                        else CheckBehavior(n, b.Behavior, issues);
                        break;

                    default:
                        if (slots.Count > 0 && outgoing == 0)
                            issues.Add(new ValidationIssue(n, "Compuesto sin hijos: no hace nada.", IssueSeverity.Warning));
                        break;
                }
            }

            return issues;
        }

        // ---- Eff / PC ------------------------------------------------------

        static void CheckBehavior(AIDecisionNode owner, EnemyActionBehavior behavior, List<ValidationIssue> issues)
        {
            if (behavior.Effects == null) return;
            foreach (var group in behavior.Effects)
            {
                if (group == null) continue;
                CheckConditions(owner, group.PreConditions, issues);
                if (group.Effects == null) continue;
                foreach (var eff in group.Effects)
                {
                    if (eff == null) continue;
                    foreach (var e in EffectTree.SelfAndDescendants(eff))
                    {
                        if (e == null) continue;
                        if (NeedsPlayerRollContext(e.GetType()))
                            issues.Add(new ValidationIssue(owner,
                                $"{e.GetType().Name} necesita contexto de dados/combo del jugador: dentro de un Behavior enemigo no hace nada.",
                                IssueSeverity.Warning));
                    }
                }
            }
        }

        /// <summary>
        /// Regla genérica: un efecto que exige <see cref="IRequiresTriggerContext{TCtx}"/> con un
        /// contexto que <see cref="EnemyAIBehaviorContext"/> no satisface. <see cref="EffClassSkillPush"/>
        /// lee el combo sin declararlo, así que va explícito.
        /// </summary>
        public static bool NeedsPlayerRollContext(Type effectType)
        {
            if (effectType == null) return false;
            if (effectType == typeof(EffClassSkillPush)) return true;
            foreach (var itf in effectType.GetInterfaces())
            {
                if (!itf.IsGenericType || itf.GetGenericTypeDefinition() != typeof(IRequiresTriggerContext<>)) continue;
                var required = itf.GetGenericArguments()[0];
                if (!required.IsAssignableFrom(typeof(EnemyAIBehaviorContext))) return true;
            }
            return false;
        }

        static void CheckConditions(AIDecisionNode owner, List<BasePreCondition> conditions, List<ValidationIssue> issues)
        {
            if (conditions == null) return;
            foreach (var pc in conditions)
            {
                if (pc == null) continue;
                if (pc is IReadsTriggerEffect)
                    issues.Add(new ValidationIssue(owner,
                        $"{pc.GetType().Name} lee el roll/combo del jugador: en un árbol enemigo nunca hay. No va a evaluar como esperás.",
                        IssueSeverity.Warning));
                if (pc is PCComposite composite) CheckConditions(owner, composite.Children, issues);
            }
        }

        // ---- helpers -------------------------------------------------------

        static bool HasAnyEffect(EnemyActionBehavior behavior)
        {
            if (behavior.Effects == null) return false;
            foreach (var g in behavior.Effects)
            {
                if (g?.Effects == null) continue;
                foreach (var e in g.Effects) if (e != null) return true;
            }
            return false;
        }

        static int CountNonNull(List<BasePreCondition> list)
        {
            if (list == null) return 0;
            int c = 0;
            foreach (var x in list) if (x != null) c++;
            return c;
        }

        static float TotalWeight(AINode_Random r)
        {
            if (r.Options == null) return 0f;
            float t = 0f;
            foreach (var o in r.Options) t += o.Weight;
            return t;
        }

        static bool HasCycle(AIDecisionNode node, GraphSnapshot snap, HashSet<AIDecisionNode> path, HashSet<AIDecisionNode> done)
        {
            if (path.Contains(node)) return true;
            if (done.Contains(node)) return false;
            path.Add(node);
            foreach (var e in snap.Edges)
            {
                if (e.Parent != node) continue;
                if (HasCycle(e.Child, snap, path, done)) return true;
            }
            path.Remove(node);
            done.Add(node);
            return false;
        }
    }
}
