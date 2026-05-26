using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Feedback
{
    /// <summary>
    /// Bootstrap SO — registra una instancia de <see cref="PawnRegistry"/> como
    /// <see cref="IPawnRegistry"/>. Priority 20 (antes de <see cref="FeedbackManagerBootstrap"/>).
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Feedback/Pawn Registry Bootstrap",
        fileName = "PawnRegistryBootstrap")]
    public sealed class PawnRegistryBootstrap : ScriptableObject, IPreloadableService
    {
        private PawnRegistry _instance;

        public int Priority => 20;

        public void Register()
        {
            if (_instance != null) return;
            _instance = new PawnRegistry();
            ServiceLocator.AddService<IPawnRegistry>(_instance, ServiceScope.Global);
        }
    }
}
