using Rollgeon.Effects.Concretes;
using Rollgeon.Items;

namespace Rollgeon.Editor.Tools.Item.ActiveItemBuilders
{
    /// <summary>
    /// Efectos de Justa de Justicia (Feature#0084 §4, Bandas D12, dirección). Mismo
    /// <see cref="EffJoustCharge"/> en las 3 bandas — solo cambia <see cref="JoustPushMode"/>,
    /// que decide cómo empuja tras el impacto.
    /// </summary>
    public static class JustaDeJusticiaBuilder
    {
        public static void Build(ItemSO item)
        {
            if (item == null) return;

            item.OnNegativeBand.Effects.Add(new EffJoustCharge { PushMode = JoustPushMode.RandomAdjacent });
            item.OnMixedBand.Effects.Add(new EffJoustCharge { PushMode = JoustPushMode.OneForward });
            item.OnPositiveBand.Effects.Add(new EffJoustCharge { PushMode = JoustPushMode.TwoForwardWithCollision });
        }
    }
}
