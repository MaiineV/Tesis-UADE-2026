namespace Rollgeon.Items.Active
{
    /// <summary>
    /// Estructura de resolucion del dado propio de un item activo. Feature#0084 —
    /// "Ítems Activos Rediseñados". Define cuantos grupos de efectos tiene el item y
    /// como se calcula la banda / magnitud a partir de la cara.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><see cref="Bands"/>: el modelo original (Feature#0064). Tres grupos de
    ///         efectos (<c>OnNegativeBand</c>/<c>OnMixedBand</c>/<c>OnPositiveBand</c>).
    ///         Los cortes son tercios proporcionales por default, o los cortes custom
    ///         de <c>ItemSO.NegativeMaxFace</c>/<c>MixedMaxFace</c> si estan seteados.</item>
    ///   <item><see cref="Binary"/>: dos grupos, por paridad de la cara (Coin Shield).</item>
    ///   <item><see cref="Gradient"/>: un solo grupo (<c>OnPositiveBand</c>). La cara ES la
    ///         magnitud del efecto (Grapple Claw, Blood D6).</item>
    ///   <item><see cref="Hierarchy"/>: un solo grupo. La cara define cuantos niveles de
    ///         un efecto acumulativo corren (Bottle'o Thunder: cara = cantidad de
    ///         objetivos aturdidos).</item>
    /// </list>
    /// </remarks>
    public enum ActiveItemResolution
    {
        Bands = 0,
        Binary = 1,
        Gradient = 2,
        Hierarchy = 3,
    }
}
