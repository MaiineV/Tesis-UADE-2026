using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Selection;
using Rollgeon.Items;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.Editor.Tools.Item.ActiveItemBuilders
{
    /// <summary>
    /// Llena los 3 grupos de banda de Blood Transfusion (D10, Feature#0084 §1). Los cortes
    /// custom (<c>NegativeMaxFace = 3</c>, <c>MixedMaxFace = 7</c> — tercios darían 4-6) los
    /// fija la spec de seed sobre el <see cref="ItemSO"/>, no este builder: acá solo se
    /// autoran los efectos/precondiciones de cada grupo.
    /// </summary>
    public static class BloodTransfusionBuilder
    {
        public static void Build(ItemSO item)
        {
            var redistribute = new EffBloodRedistribute();
            redistribute.Selection.SlotState = SlotState.Self;

            item.OnNegativeBand = new EffectData
            {
                Label = "1-3 — Redistribución sanguínea",
                PreConditions = { new PcEligibleEnemyExists { ExcludeBloodless = true } },
                Effects = { redistribute },
            };

            var mixedDrain = new EffBloodDrain();
            mixedDrain.Selection.SlotState = SlotState.Self;
            mixedDrain.EditorSetHealPct(0.5f);

            item.OnMixedBand = new EffectData
            {
                Label = "4-7 — Transfusión parcial",
                PreConditions = { new PcEligibleEnemyExists { ExcludeBloodless = true } },
                Effects = { mixedDrain },
            };

            var fullDrain = new EffBloodDrain();
            fullDrain.Selection.SlotState = SlotState.Self;
            fullDrain.EditorSetHealPct(1.0f);

            item.OnPositiveBand = new EffectData
            {
                Label = "8-10 — Transfusión completa",
                PreConditions = { new PcEligibleEnemyExists { ExcludeBloodless = true } },
                Effects = { fullDrain },
            };
        }
    }
}
