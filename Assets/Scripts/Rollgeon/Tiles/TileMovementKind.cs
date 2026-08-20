namespace Rollgeon.Tiles
{
    /// <summary>
    /// Cómo entró la unidad a una celda. El motor lo mapea a qué triggers satisface la
    /// entrada — es la pieza que hace cumplir dos reglas duras del GDD: el teleport NO
    /// dispara OnEnter en el portal destino, y aparecer (spawn) NUNCA dispara nada.
    /// </summary>
    public enum TileMovementKind
    {
        /// <summary>Movimiento propio (Move del player o de la IA). Satisface OnEnter/OnPassThrough.</summary>
        Voluntary = 0,

        /// <summary>Empuje. Satisface OnEnter/OnPassThrough Y OnForcedMovementInto.</summary>
        Forced = 1,

        /// <summary>Deslizamiento de Hielo. Satisface OnEnter/OnPassThrough.</summary>
        Slide = 2,

        /// <summary>Remanente de empuje tras cruzar un Portal. Satisface OnEnter/OnPassThrough
        /// y OnForcedMovementInto (sigue siendo movimiento forzado).</summary>
        PortalRemainder = 3,

        /// <summary>Teleport de Portal. No satisface ningún trigger.</summary>
        Teleport = 4,

        /// <summary>Aparición en el tablero. No satisface ningún trigger, NUNCA (GDD, sección 10).</summary>
        Spawn = 5,
    }
}
