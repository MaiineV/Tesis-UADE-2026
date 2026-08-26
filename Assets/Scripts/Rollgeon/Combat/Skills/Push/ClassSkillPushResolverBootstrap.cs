using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.Skills.Push
{
    /// <summary>
    /// Wrapper thin que arrastra el <see cref="ClassSkillPushResolver"/> al
    /// <c>ServiceBootstrapSO.ExtraServices</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combat/Class Skill Push Resolver Bootstrap",
        fileName = "ClassSkillPushResolverBootstrap")]
    public sealed class ClassSkillPushResolverBootstrap : ScriptableObject, IPreloadableService
    {
        private ClassSkillPushResolver _instance;

        public int Priority => 82;

        public void Register()
        {
            if (_instance != null) return;
            if (ServiceLocator.TryGetService<IClassSkillPushResolver>(out var existing) && existing != null) return;

            _instance = new ClassSkillPushResolver();
            _instance.Register();
        }
    }
}
