namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Variante visual del tablero de dados (el rectángulo de <c>DiceZoneView</c> en
    /// <c>Canvas_ActionRoll</c>). Lo declara el behavior/effect de cada acción para
    /// decir qué skin usa su tirada; lo resuelve <see cref="DiceBoardSkinCatalogSO"/>
    /// a un sprite concreto y lo aplica <see cref="DiceBoardSkinView"/> sobre el
    /// Image existente.
    /// </summary>
    /// <remarks>
    /// Los valores nuevos se agregan al final para no correr los serializados.
    /// </remarks>
    public enum DiceBoardType
    {
        /// <summary>Tablero por defecto — cualquier tirada sin tipo propio (Heal, Forzar Puerta).</summary>
        Default = 0,

        /// <summary>Tirada de ataque.</summary>
        Attack = 1,

        /// <summary>Tirada de defensa (shield).</summary>
        Defense = 2,
    }
}
