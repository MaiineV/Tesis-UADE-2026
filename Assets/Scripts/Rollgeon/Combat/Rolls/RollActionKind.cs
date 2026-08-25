namespace Rollgeon.Combat.Rolls
{
    /// <summary>
    /// Discriminante de "qué acción generó esta tirada" — viaja junto a
    /// <see cref="Patterns.EventName.OnRollResolved"/> y
    /// <see cref="Patterns.ComboPlayedPayload"/> (BUG-060). Antes de esto, cualquier
    /// consumer que quisiera reaccionar SOLO a tiradas de combate (ej. encantamientos
    /// de oro) no tenía forma de distinguir un ataque de un Movement o de un Pasar
    /// Turno — todos emitían por el mismo canal sin discriminante.
    /// </summary>
    public enum RollActionKind
    {
        /// <summary>Sin discriminante resuelto. Tratado como NO pagable por los
        /// consumers que gatean por acción de combate — fail-safe: mejor de menos
        /// que pagar de más por un emisor sin clasificar.</summary>
        Unknown = 0,

        /// <summary>Ataque (BaseAttack / SpecialAttack).</summary>
        Attack,

        /// <summary>Defensa / escudo (fase de un chain o behavior dedicado).</summary>
        Defense,

        /// <summary>Curación — EN combate (fuera de combate ver <see cref="Exploration"/>).</summary>
        Heal,

        /// <summary>Movimiento. Puede reusar la tirada compartida (y su ComboResult) sin
        /// que eso constituya una acción de combate pagable.</summary>
        Movement,

        /// <summary>Cerrar turno sin ejecutar una acción (Pass). No hay tirada "real" que pagar.</summary>
        EndTurn,

        /// <summary>Forzar Puerta — tiene tirada propia en combate pero está explícitamente
        /// excluida de los encantamientos de oro (decisión de diseño BUG-060).</summary>
        ForceDoor,

        /// <summary>Action roll fuera de combate (Curarse/Forzar Puerta en exploración).</summary>
        Exploration,
    }

    /// <summary>Helpers de clasificación sobre <see cref="RollActionKind"/>.</summary>
    public static class RollActionKindExtensions
    {
        /// <summary>
        /// True para los únicos kinds que los encantamientos de oro (y los hooks de
        /// ítem que dependen de daño de combo) deben considerar pagables: Ataque,
        /// Defensa y Curación — y solo cuando ocurren en combate (BUG-060 — decisión
        /// de diseño del usuario). Movement/EndTurn/ForceDoor/Exploration/Unknown quedan
        /// afuera.
        /// </summary>
        public static bool IsCombatPayable(this RollActionKind kind)
            => kind == RollActionKind.Attack
               || kind == RollActionKind.Defense
               || kind == RollActionKind.Heal;
    }
}
