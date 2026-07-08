namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Categorías de número flotante. Lo consume el <see cref="FloatingDamageSpawner"/>
    /// para elegir tint + formato del texto. Pasado como <c>args[1]</c> del evento
    /// <c>EventName.OnFloatingNumberRequested</c>.
    /// </summary>
    public enum FloatingNumberType
    {
        Damage = 0,
        Heal = 1,
        Shield = 2,
        Gold = 3,
        Status = 4,
    }

    /// <summary>
    /// Curva de movimiento de un floating number. <see cref="Rise"/> es el default (sube
    /// derecho) — daño, heal, shield. <see cref="Arc"/> agrega drift lateral alternado
    /// (oro y otros drops que "saltan" en vez de subir derecho). Ver
    /// <see cref="FloatingNumberFormat"/> / <see cref="FloatingDamageInstance"/>.
    /// </summary>
    public enum FloatingMotion
    {
        Rise = 0,
        Arc = 1,
    }
}
