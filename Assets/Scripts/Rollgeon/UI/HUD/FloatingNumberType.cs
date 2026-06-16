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
}
