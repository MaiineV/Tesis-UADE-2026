using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Readers;
using Rollgeon.Effects.Selection;
using Rollgeon.Items;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.Editor.Tools.Item.ActiveItemBuilders
{
    /// <summary>
    /// Llena los 2 grupos de banda de Coin Shield (D4 Binary, Feature#0085 §2). La spec de
    /// seed fija <c>BinaryPositiveParity = Even</c> sobre el <see cref="ItemSO"/>: acá solo
    /// se autoran los efectos/precondiciones de <c>OnNegativeBand</c> (impar, global) y
    /// <c>OnPositiveBand</c> (par, self).
    /// </summary>
    public static class CoinShieldBuilder
    {
        public static void Build(ItemSO item)
        {
            var addShieldToAll = new EffAddShieldToAll
            {
                IncludeOwner = true,
                IncludeEnemies = true,
            };
            addShieldToAll.Selection.SlotState = SlotState.Self;
            addShieldToAll.EditorSetAmount(new ReadOwnerShieldFraction
            {
                Fraction = 0.5f,
                Ceil = true,
                Min = 1,
            });

            item.OnNegativeBand = new EffectData
            {
                Label = "Impar — Protección global caótica",
                PreConditions = { new PcOwnerShieldAtLeast { Min = 1 } },
                Effects = { addShieldToAll },
            };

            var persistShield = new EffPersistShield();
            persistShield.Selection.SlotState = SlotState.Self;

            item.OnPositiveBand = new EffectData
            {
                Label = "Par — Conserva el escudo propio",
                PreConditions = { new PcOwnerShieldAtLeast { Min = 1 } },
                Effects = { persistShield },
            };
        }
    }
}
