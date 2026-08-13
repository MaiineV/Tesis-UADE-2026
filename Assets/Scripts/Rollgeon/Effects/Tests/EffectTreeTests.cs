using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rollgeon.Effects.Concretes;
using Rollgeon.Feedback;
using Rollgeon.Heroes;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// Cobertura del recorrido del árbol de efectos. Existe por un bug recurrente: cada
    /// nivel de anidamiento nuevo que se agrega deja ciegas a las recursiones escritas a
    /// mano y la UI se queda en blanco <b>sin ningún error</b> que lo delate.
    /// </summary>
    [TestFixture]
    public class EffectTreeTests
    {
        private static EffPlaySequence MakeSequenceWrapping(params IEffect[] inner)
        {
            var step = new FeedbackSequenceStep
            {
                Source = StepSource.InlineEffect,
                StartMode = StepStartMode.OnEvent,
                StartOnEventKey = "hit",
                InlineEffects = new EffectData(),
            };
            step.InlineEffects.Effects = new List<IEffect>(inner);

            var sequence = new EffPlaySequence();
            typeof(EffPlaySequence)
                .GetField("_steps", System.Reflection.BindingFlags.NonPublic
                                    | System.Reflection.BindingFlags.Instance)
                .SetValue(sequence, new List<FeedbackSequenceStep> { step });
            return sequence;
        }

        private static EffChain MakeChainWrapping(params IEffect[] phaseZeroEffects)
        {
            var phase = new ChainPhase { Effects = new EffectData() };
            phase.Effects.Effects = new List<IEffect>(phaseZeroEffects);

            var chain = new EffChain();
            chain.Phases = new List<ChainPhase> { phase };
            return chain;
        }

        [Test]
        public void DirectChildren_Leaf_ReturnsEmptyNotNull()
        {
            var leaf = new EffDealDamage();

            var children = EffectTree.DirectChildren(leaf);

            Assert.IsNotNull(children, "Nunca null — los callers iteran sin chequear.");
            Assert.AreEqual(0, children.Count);
        }

        [Test]
        public void DirectChildren_Null_ReturnsEmptyNotNull()
        {
            var children = EffectTree.DirectChildren(null);

            Assert.IsNotNull(children);
            Assert.AreEqual(0, children.Count);
        }

        [Test]
        public void DirectChildren_Chain_YieldsPhaseEffects()
        {
            var damage = new EffDealDamage();
            var chain = MakeChainWrapping(damage);

            var children = EffectTree.DirectChildren(chain);

            Assert.AreEqual(1, children.Count);
            Assert.AreSame(damage, children[0]);
        }

        [Test]
        public void DirectChildren_Sequence_YieldsInlineStepEffects()
        {
            var damage = new EffDealDamage();
            var sequence = MakeSequenceWrapping(damage);

            var children = EffectTree.DirectChildren(sequence);

            Assert.AreEqual(1, children.Count);
            Assert.AreSame(damage, children[0],
                "Sin esto la secuencia es una hoja opaca y el daño diferido queda invisible.");
        }

        [Test]
        public void DirectChildren_SequenceWithNonInlineSteps_IgnoresThem()
        {
            var sequence = new EffPlaySequence();
            typeof(EffPlaySequence)
                .GetField("_steps", System.Reflection.BindingFlags.NonPublic
                                    | System.Reflection.BindingFlags.Instance)
                .SetValue(sequence, new List<FeedbackSequenceStep>
                {
                    new FeedbackSequenceStep { Source = StepSource.FeedbackRef, FeedbackRefId = "vfx.x" },
                    new FeedbackSequenceStep { Source = StepSource.InlineWait, WaitDuration = 0.5f },
                });

            var children = EffectTree.DirectChildren(sequence);

            Assert.AreEqual(0, children.Count);
        }

        [Test]
        public void SelfAndDescendants_ChainWrappingSequence_ReachesTheNestedEffect()
        {
            // La forma real del ataque del guerrero: chain → secuencia → daño.
            var damage = new EffDealDamage();
            var chain = MakeChainWrapping(MakeSequenceWrapping(damage));

            var walked = EffectTree.SelfAndDescendants(chain).ToList();

            Assert.AreSame(chain, walked[0], "Pre-orden: la raíz primero.");
            CollectionAssert.Contains(walked, damage);
        }

        [Test]
        public void FindFirstDealDamageEffect_DamageNestedInSequenceInsideChain_IsFound()
        {
            // Regresión: mover el EffDealDamage a un step InlineEffect dejó el formula
            // label y el texto de combo en blanco, porque la recursión solo bajaba por las
            // fases del chain y no por los steps.
            var damage = new EffDealDamage();
            var behavior = new HeroActionBehavior();
            var group = new EffectData();
            group.Effects = new List<IEffect> { MakeChainWrapping(MakeSequenceWrapping(damage)) };
            behavior.Effects = new List<EffectData> { group };

            var found = behavior.FindFirstDealDamageEffect();

            Assert.AreSame(damage, found);
        }

        [Test]
        public void FindFirstDealDamageEffect_NoDamageAnywhere_ReturnsNull()
        {
            var behavior = new HeroActionBehavior();
            var group = new EffectData();
            group.Effects = new List<IEffect> { MakeChainWrapping(MakeSequenceWrapping(new EffAddShield())) };
            behavior.Effects = new List<EffectData> { group };

            var found = behavior.FindFirstDealDamageEffect();

            Assert.IsNull(found, "Una acción sin daño (curación, escudo) no debe inventar uno.");
        }
    }
}
