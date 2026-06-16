using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Feedback
{
    /// <summary>
    /// SO wrapper que registra <see cref="FeedbackServiceStub"/> como <see cref="IFeedbackService"/>.
    /// Sirve como fallback test/editor. Para combate jugable usar
    /// <see cref="FeedbackManagerBootstrap"/> — los dos son mutuamente exclusivos.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Feedback/Feedback Service Stub Bootstrap",
        fileName = "FeedbackServiceStubBootstrap")]
    public sealed class FeedbackServiceStubBootstrap : ScriptableObject, IPreloadableService
    {
        private FeedbackServiceStub _instance;

        public int Priority => 55;

        public void Register()
        {
            if (_instance != null) return;
            _instance = new FeedbackServiceStub();
            ServiceLocator.AddService<IFeedbackService>(_instance, ServiceScope.Global);
        }
    }
}
