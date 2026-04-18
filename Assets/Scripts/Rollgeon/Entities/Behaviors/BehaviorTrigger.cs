namespace Rollgeon.Entities.Behaviors
{
    /// <summary>
    /// Eventos que pueden disparar un <see cref="BaseBehavior"/>. TECHNICAL.md §7.2.
    /// <para>
    /// Regla de estabilidad: no renumerar — los valores pueden quedar persistidos en
    /// assets de data. Solo agregar nuevos al final.
    /// </para>
    /// </summary>
    public enum BehaviorTrigger
    {
        None = 0,
        OnTurnStart = 1,
        OnTurnEnd = 2,
        OnEvent = 3,
        OnDamaged = 4,
        OnEntered = 5,
        OnPlayerInRange = 6,
        OnInteract = 7,
    }
}
