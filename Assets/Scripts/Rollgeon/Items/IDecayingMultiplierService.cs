namespace Rollgeon.Items
{
    /// <summary>
    /// Estado de run de los items con <see cref="ItemSO.DecayingMultiplier"/> (Eco Menguante):
    /// cuántos combos descontó cada uno y qué multiplicador les queda. Solo lectura desde
    /// afuera — el servicio se alimenta de <c>ComboPlayedPayload</c>.
    /// </summary>
    public interface IDecayingMultiplierService
    {
        /// <summary>Combos de combate descontados al item desde que se adquirió (0 si no está).</summary>
        int GetCombosPlayed(string itemId);

        /// <summary>Multiplicador que aplicaría el PRÓXIMO ataque con ese item (Start al adquirirlo).</summary>
        float GetCurrentMultiplier(ItemSO item);
    }
}
