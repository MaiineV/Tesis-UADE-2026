using Rollgeon.Entities;

namespace Rollgeon.Combat.Handoff
{
    /// <summary>
    /// Seam opcional para forzar QUÉ boss spawnea en la próxima sala de boss, pisando el
    /// roll del <c>BossPoolSO</c> del piso. Lo registra la dev console (<c>boss &lt;id&gt;</c>);
    /// ausente del ServiceLocator = siempre rolea el pool. Mismo espíritu que
    /// <see cref="IEnemySpawnCoordOverride"/>.
    /// </summary>
    public interface IBossSelectionOverride
    {
        /// <summary>
        /// Deja <paramref name="boss"/> pedido para el próximo spawn de boss.
        /// <c>null</c> cancela un pedido pendiente.
        /// </summary>
        void ForceNext(EnemyDataSO boss);

        /// <summary>
        /// <c>true</c> + el boss pedido si había uno. Es <b>one-shot</b>: consumirlo lo
        /// limpia, así el override no se pega a todas las salas de boss de la run.
        /// </summary>
        bool TryConsume(out EnemyDataSO boss);
    }
}
