namespace Rollgeon.Entities.Traits
{
    /// <summary>
    /// Familia de combate de un enemigo: lo que su panel dice de él antes de cualquier número.
    /// </summary>
    /// <remarks>
    /// No es <see cref="AIPersonality"/>. Eso es cuánto peligro acepta pisar el pathing, y aunque
    /// tenga un valor que se llama igual, mezclarlos vuelve indescribible un support agresivo: son
    /// dos ejes y cada uno necesita su campo.
    /// </remarks>
    public enum EnemyArchetype
    {
        /// <summary>
        /// Nadie le puso familia. El panel <b>no dibuja la fila</b> en vez de adivinar: el default
        /// lo heredan los enemigos que nadie va a autorar, y un bicho mal etiquetado miente sobre
        /// cómo se lo pelea. Que el valor sea 0 es lo que deja los <c>.asset</c> de hoy intactos.
        /// </summary>
        Unset = 0,

        Melee = 1,
        Ranged = 2,
        Support = 3,
    }
}
