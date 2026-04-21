using System;
using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Attributes
{
    /// <summary>
    /// <see cref="IPreloadableService"/> wrapper que instancia el
    /// <see cref="AttributesManager"/> global y lo registra en el
    /// <see cref="ServiceLocator"/> bajo <see cref="ServiceScope.Global"/>.
    /// Debe correr antes que cualquier service que resuelva
    /// <c>AttributesManager</c> en su propio <c>Register()</c> —
    /// <see cref="Rollgeon.Combat.Energy.EnergyService"/> (Priority 50),
    /// <see cref="Rollgeon.Combat.Pipelines.DamagePipeline"/>,
    /// <see cref="Rollgeon.Combat.Pipelines.HealPipeline"/>, etc.
    /// <para>
    /// Cierra el TODO heredado entre
    /// <c>docs/setup/Foundation#0003_AttributesAndModifiers.md</c> (delega el
    /// registro a Foundation#0005) y
    /// <c>docs/setup/Foundation#0005_CatalogsAndBootstrap.md</c> (no lo
    /// mencionaba). TECHNICAL.md §2.3.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class AttributesManagerBootstrap : IPreloadableService, IDisposable
    {
        /// <summary>
        /// Corre temprano: antes de <see cref="Rollgeon.Combat.Energy.EnergyService"/> (50)
        /// y cualquier otro dependiente.
        /// </summary>
        public int Priority => 5;

        private AttributesManager _instance;

        public void Register()
        {
            if (ServiceLocator.HasService<AttributesManager>())
            {
                Debug.LogWarning(
                    "[AttributesManagerBootstrap] AttributesManager ya estaba registrado — skip.");
                return;
            }

            _instance = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_instance, ServiceScope.Global);
        }

        public void Dispose()
        {
            _instance?.Dispose();
            _instance = null;
        }
    }
}
