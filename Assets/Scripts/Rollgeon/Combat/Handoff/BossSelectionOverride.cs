using Patterns;
using Rollgeon.Entities;

namespace Rollgeon.Combat.Handoff
{
    /// <summary>
    /// Implementación default de <see cref="IBossSelectionOverride"/>: un solo slot
    /// pendiente que se limpia al consumirse. La registra la dev console (Editor /
    /// development builds) — en release el servicio no existe y el resolver rolea el pool.
    /// </summary>
    public sealed class BossSelectionOverride : IBossSelectionOverride
    {
        private EnemyDataSO _pending;

        /// <summary>Boss pendiente (para la UI/log del comando). <c>null</c> = sin pedido.</summary>
        public EnemyDataSO Pending => _pending;

        /// <summary>Factory: registra la instancia en <see cref="ServiceScope.Global"/>.</summary>
        public static BossSelectionOverride CreateAndRegister()
        {
            var instance = new BossSelectionOverride();
            ServiceLocator.AddService<IBossSelectionOverride>(instance, ServiceScope.Global);
            return instance;
        }

        public void ForceNext(EnemyDataSO boss) => _pending = boss;

        public bool TryConsume(out EnemyDataSO boss)
        {
            boss = _pending;
            _pending = null;
            return boss != null;
        }
    }
}
