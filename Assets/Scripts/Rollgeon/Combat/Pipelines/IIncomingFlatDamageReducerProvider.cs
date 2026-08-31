namespace Rollgeon.Combat.Pipelines
{
    /// <summary>
    /// Reducción PLANA de daño entrante por target — espejo entrante de
    /// <see cref="IOutgoingFlatDamageBonusProvider"/>. La consulta el
    /// <see cref="DamagePipeline"/> (y su Preview) entre el multiplicador entrante y el
    /// Shield, con piso de daño 1. Pull on-demand: el provider decide en el momento del
    /// golpe (ej. aura de Guardian: ¿hay un portador vivo aliado a ≤ radio?), sin
    /// bookkeeping ni invalidación.
    /// </summary>
    public interface IIncomingFlatDamageReducerProvider
    {
        /// <summary>Puntos de daño a descontar del golpe descrito por <paramref name="ctx"/>; 0 = nada.</summary>
        int GetFlatReduction(DamageContext ctx);
    }
}
