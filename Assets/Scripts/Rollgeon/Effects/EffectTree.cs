using System.Collections.Generic;
using Rollgeon.Effects.Concretes;
using Rollgeon.Feedback;

namespace Rollgeon.Effects
{
    /// <summary>
    /// Único lugar que sabe <b>qué efectos anidan otros efectos</b>. Los consumidores que
    /// recorren el árbol (tooltips, fórmula de daño, scans de selección) preguntan acá en
    /// vez de conocer cada tipo compuesto por su cuenta.
    /// </summary>
    /// <remarks>
    /// Existe por un bug recurrente: cada vez que aparece un nivel de anidamiento nuevo,
    /// las recursiones escritas a mano lo ignoran y la UI se queda en blanco sin ningún
    /// error. Pasó con <see cref="EffChain"/> (el formula label quedaba vacío para todo
    /// ataque con chain) y volvió a pasar cuando el daño se movió adentro de un step
    /// <see cref="StepSource.InlineEffect"/> de <see cref="EffPlaySequence"/>.
    /// <para>
    /// <b>Al agregar un tipo compuesto nuevo, sumarlo acá</b> — no en cada consumidor.
    /// </para>
    /// </remarks>
    public static class EffectTree
    {
        private static readonly IReadOnlyList<IEffect> Empty = new List<IEffect>();

        /// <summary>
        /// Hijos directos de un efecto compuesto. Lista vacía si es una hoja — nunca null.
        /// No es recursivo: el caller decide cómo bajar (los tooltips concatenan por nivel,
        /// las búsquedas cortan en el primer hit).
        /// </summary>
        public static IReadOnlyList<IEffect> DirectChildren(IEffect eff)
        {
            if (eff is EffChain chain && chain.Phases != null)
            {
                var result = new List<IEffect>();
                foreach (var phase in chain.Phases)
                {
                    var inner = phase?.Effects?.Effects;
                    if (inner == null) continue;
                    result.AddRange(inner);
                }
                return result;
            }

            if (eff is EffPlaySequence sequence && sequence.Steps != null)
            {
                var result = new List<IEffect>();
                foreach (var step in sequence.Steps)
                {
                    if (step == null || step.Source != StepSource.InlineEffect) continue;
                    var inner = step.InlineEffects?.Effects;
                    if (inner == null) continue;
                    result.AddRange(inner);
                }
                return result;
            }

            return Empty;
        }

        /// <summary>
        /// El efecto y todos sus descendientes, en pre-orden. Para consumidores que solo
        /// quieren "¿hay algún X acá abajo?" sin importarles la estructura.
        /// </summary>
        public static IEnumerable<IEffect> SelfAndDescendants(IEffect root)
        {
            if (root == null) yield break;
            yield return root;

            foreach (var child in DirectChildren(root))
                foreach (var nested in SelfAndDescendants(child))
                    yield return nested;
        }
    }
}
