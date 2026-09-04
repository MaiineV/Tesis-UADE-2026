using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Selection;
using Rollgeon.Items;
using Rollgeon.PreConditions.Concretes;

namespace Rollgeon.Editor.Tools.Item.ActiveItemBuilders
{
    /// <summary>
    /// Llena el único grupo (Gradient) de Blood D6 (D6, Feature#0085 §6): la cara resuelta
    /// arma la carga del próximo combo de Ataque, gateada por <see cref="PcBloodD6Ready"/>
    /// ("ningún Blood D6 pendiente").
    /// </summary>
    public static class BloodD6Builder
    {
        public static void Build(ItemSO item)
        {
            var charge = new EffBloodD6Charge();
            charge.Selection.SlotState = SlotState.Self;

            item.OnPositiveBand = new EffectData
            {
                Label = "Al resolver — Carga el próximo combo de Ataque",
                PreConditions = { new PcBloodD6Ready() },
                Effects = { charge },
            };
        }
    }
}
