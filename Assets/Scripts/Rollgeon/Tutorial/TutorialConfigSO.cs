using Patterns;
using Rollgeon.Heroes;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.Shop;
using UnityEngine;

namespace Rollgeon.Tutorial
{
    /// <summary>
    /// Config central del Tutorial Mode: héroe fijo, piso autorado y overrides de
    /// tienda/economía. Es su propio bootstrap (<see cref="IPreloadableService"/>):
    /// agregarlo a <c>ServiceBootstrapSO.ExtraServices</c> y queda registrado como
    /// <c>TutorialConfigSO</c> en <see cref="ServiceScope.Global"/>. Sin este asset
    /// registrado, el juego degrada al flujo normal (sin tutorial).
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Tutorial/Tutorial Config", fileName = "TutorialConfig")]
    public sealed class TutorialConfigSO : ScriptableObject, IPreloadableService
    {
        [Header("Run del tutorial")]
        [Tooltip("Héroe fijo con el que se juega el tutorial (clone del Warrior con " +
                 "AD_ForceDoor_Tutorial en el slot ForceDoor). Saltea class/build selection.")]
        [SerializeField] private ClassHeroSO _tutorialHero;

        [Tooltip("Layout fijo de 5 salas: A Start(0,0), B Combat(0,1), C Combat(0,2), " +
                 "D Shop(-1,1) StartLocked, E Enchant(1,2) con puerta exit.")]
        [SerializeField] private TutorialFloorPlanSO _floorPlan;

        [Tooltip("RulesetId para StartRun. Vacío = mismo default que el flujo normal.")]
        [SerializeField] private string _rulesetId = string.Empty;

        [Header("Overrides de tienda")]
        [Tooltip("ShopConfig del tutorial (MaxItemSlots=1).")]
        [SerializeField] private ShopConfigSO _shopConfig;

        [Tooltip("Pool con exactamente 1 entry: el ComboPassive de daño de Par.")]
        [SerializeField] private ShopPoolSO _shopPool;

        [Header("Economía")]
        [Tooltip("Oro con el que arranca el tutorial. El resto lo dropean los enemigos " +
                 "(tuneados para financiar la compra y exactamente 1 encantamiento).")]
        [SerializeField] private int _tutorialStartingGold = 0;

        [Tooltip("Oro de la fresh run post-tutorial. Espeja el startingGold del EconomyBootstrap.")]
        [SerializeField] private int _postTutorialStartingGold = 10;

        public ClassHeroSO TutorialHero => _tutorialHero;
        public TutorialFloorPlanSO FloorPlan => _floorPlan;
        public string RulesetId => _rulesetId;
        public ShopConfigSO ShopConfig => _shopConfig;
        public ShopPoolSO ShopPool => _shopPool;
        public int TutorialStartingGold => _tutorialStartingGold;
        public int PostTutorialStartingGold => _postTutorialStartingGold;

        /// <summary>Config completa para poder lanzar el tutorial.</summary>
        public bool IsLaunchable => _tutorialHero != null && _floorPlan != null;

        // ---------------------------------------------------------------- IPreloadableService

        public int Priority => 61;

        public void Register()
        {
            ServiceLocator.AddService<TutorialConfigSO>(this, ServiceScope.Global);
        }
    }
}
