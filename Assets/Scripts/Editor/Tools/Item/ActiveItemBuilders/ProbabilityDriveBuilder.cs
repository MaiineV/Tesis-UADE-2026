using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Selection;
using Rollgeon.Items;

namespace Rollgeon.Editor.Tools.Item.ActiveItemBuilders
{
    /// <summary>
    /// Efectos de Probability Drive (Feature#0085 §5, Bandas D4 con cortes 1/3). Una casilla
    /// central se elige ANTES de tirar (<c>SlotState.Empty</c>, rango 8) y cada banda resuelve un
    /// reposicionamiento distinto. La <see cref="SelectionSettings"/> se autoría IDÉNTICA en las
    /// 3 bandas: <c>ActiveItemActivationService.ResolveSelectionSettings</c> solo mira la primera
    /// con requerimiento, pero cualquiera de las 3 puede terminar siendo esa primera.
    /// </summary>
    public static class ProbabilityDriveBuilder
    {
        public static void Build(ItemSO item)
        {
            if (item == null) return;

            var distortion = new EffProbabilityDistortion { Selection = NewCenterSelection() };
            var jump = new EffProbabilityJump { Selection = NewCenterSelection() };
            var choice = new EffProbabilityChoice { Selection = NewCenterSelection() };

            item.OnNegativeBand.Effects.Add(distortion);
            item.OnMixedBand.Effects.Add(jump);
            item.OnPositiveBand.Effects.Add(choice);
        }

        // Cada efecto necesita su PROPIA instancia — compartir la referencia entre los 3
        // significaría que ajustar el rango en uno pisa a los otros dos sin que se note en
        // el inspector.
        private static SelectionSettings NewCenterSelection() => new SelectionSettings
        {
            SlotState = SlotState.Empty,
            Range = 8,
            RangeMode = RangeMode.Manhattan,
            TargetMode = TargetMode.Single,
        };
    }
}
