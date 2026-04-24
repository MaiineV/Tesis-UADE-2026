using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Feedback
{
    /// <summary>
    /// Registra el <see cref="FeedbackManager"/> como <see cref="IFeedbackService"/>.
    /// Crea un GameObject persistente en <c>DontDestroyOnLoad</c> que dueña las coroutines
    /// del pipeline §10. TECHNICAL.md §10.1.
    /// </summary>
    /// <remarks>
    /// <b>Setup.</b> Crear el asset desde <c>Assets / Create / Rollgeon / Feedback / Feedback Manager Bootstrap</c>,
    /// asignar la <see cref="FeedbackDBSO"/> y agregar el asset a
    /// <c>ServiceBootstrapSO.ExtraServices</c>. No puede coexistir con
    /// <see cref="FeedbackServiceStubBootstrap"/> — uno de los dos se tiene que elegir.
    /// </remarks>
    [CreateAssetMenu(
        menuName = "Rollgeon/Feedback/Feedback Manager Bootstrap",
        fileName = "FeedbackManagerBootstrap")]
    public sealed class FeedbackManagerBootstrap : ScriptableObject, IPreloadableService
    {
        [SerializeField, Tooltip("DB autoral con las FeedbackEntry. Required para feedbacks por id.")]
        private FeedbackDBSO _db;

        private FeedbackManager _instance;

        public int Priority => 55;

        public void Register()
        {
            if (_instance != null) return;

            var go = new GameObject("[FeedbackManager]");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<FeedbackManager>();
            _instance.Configure(_db);

            ServiceLocator.AddService<IFeedbackService>(_instance, ServiceScope.Global);
        }
    }
}
