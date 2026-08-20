namespace Rollgeon.Tiles
{
    /// <summary>Estado que aplica una casilla de categoría ApplyStatus.</summary>
    public enum TileStatusKind
    {
        None = 0,
        /// <summary>Envenenado: StatusTickDamage × StatusTurns, refresh-no-stack (Veneno).</summary>
        Poison = 1,
        /// <summary>Aturdimiento: StatusTurns perdidos, max() sin stacking (Charco Eléctrico).</summary>
        Stun = 2,
    }
}
