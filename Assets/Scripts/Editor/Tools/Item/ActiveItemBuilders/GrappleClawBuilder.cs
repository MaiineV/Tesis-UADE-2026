using Rollgeon.Effects.Concretes;
using Rollgeon.Items;

namespace Rollgeon.Editor.Tools.Item.ActiveItemBuilders
{
    /// <summary>
    /// Efectos de Grapple Claw (Feature#0085 §3, Gradiente D6). Un solo grupo
    /// (<c>OnPositiveBand</c>): el gancho resuelve todo (ancla, atracción/avance, Cadena
    /// Inestable) adentro del propio efecto — la cara solo decide la magnitud del
    /// desplazamiento, no hay bandas cualitativas.
    /// </summary>
    public static class GrappleClawBuilder
    {
        public static void Build(ItemSO item)
        {
            if (item == null) return;

            item.OnPositiveBand.Effects.Add(new EffGrappleClaw());
        }
    }
}
