namespace Rollgeon.Heroes
{
    // Serializado por valor en los ClassHeroSO (Odin) y usado como orden canónico
    // por GetBehaviorsForPhase — solo appendear valores nuevos, jamás reordenar.
    public enum HeroBehaviorSlot
    {
        Movement = 0,
        BaseAttack = 1,
        ClassSkill = 2,   // ex SpecialAttack (Feature#0055) — mismo valor, no reordenar

        Healing = 3,
        ForceDoor = 4,
        Defense = 5
    }
}
