namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Evento del canal Dice en el que dispara un <see cref="Triggers.Concretes.ModifyResourceTrigger"/>.
    /// El <see cref="ComboFilter"/> solo se consulta cuando <see cref="ComboMatched"/>.
    /// </summary>
    public enum TriggerWhen
    {
        ComboMatched,
        RollResolved,
        DiceRolled,
    }
}
