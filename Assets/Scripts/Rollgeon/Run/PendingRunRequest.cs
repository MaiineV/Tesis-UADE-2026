using System;
using Rollgeon.Dice;
using Rollgeon.Heroes;

namespace Rollgeon.Run
{
    /// <summary>
    /// Carrier estatico para los datos de la run pendiente entre
    /// <c>01_MainMenu</c> (BuildSelectionScreen) y <c>02_Gameplay</c>
    /// (GameplayBootstrapper). No se registra en ServiceLocator porque la
    /// scope <see cref="Patterns.ServiceScope.Run"/> aun no existe cuando
    /// cruzamos escenas — <see cref="RunBootstrapper.StartRun"/> la crea
    /// recien en <c>GameplayBootstrapper.Start</c>.
    /// </summary>
    public static class PendingRunRequest
    {
        public static ClassHeroSO SelectedHero { get; private set; }
        public static Guid RunId { get; private set; }
        public static string RulesetId { get; private set; }
        public static DiceBagSO BuiltDiceBag { get; private set; }
        public static bool HasRequest { get; private set; }

        public static void Set(ClassHeroSO hero, Guid runId, string rulesetId, DiceBagSO builtDiceBag = null)
        {
            SelectedHero = hero;
            RunId = runId;
            RulesetId = rulesetId;
            BuiltDiceBag = builtDiceBag;
            HasRequest = true;
        }

        public static void Clear()
        {
            SelectedHero = null;
            RunId = Guid.Empty;
            RulesetId = null;
            BuiltDiceBag = null;
            HasRequest = false;
        }
    }
}
