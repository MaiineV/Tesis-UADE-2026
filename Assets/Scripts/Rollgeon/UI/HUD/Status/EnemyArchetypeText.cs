using Rollgeon.Entities.Traits;
using Rollgeon.Localization;

namespace Rollgeon.UI.HUD.Status
{
    /// <summary>
    /// La fila de familia del panel de un enemigo: <c>Rango</c>, o <c>Jefe · Rango</c>.
    /// </summary>
    /// <remarks>
    /// Un jefe no es una cuarta familia sino un prefijo, y sale de <c>EnemyDataSO.IsBoss</c>, que
    /// ya está en el dato. Así el Croupier se lee <c>Jefe · Rango</c> sin que nadie tenga que
    /// autorar dos cosas ni mantenerlas de acuerdo.
    /// </remarks>
    public static class EnemyArchetypeText
    {
        /// <summary>
        /// Cadena vacía = el panel no dibuja la fila. Sólo pasa cuando no es jefe y nadie le puso
        /// familia: un jefe siempre tiene algo verdadero que decir.
        /// </summary>
        public static string Describe(EnemyArchetype archetype, bool isBoss)
        {
            string key = EnemyArchetypeKeys.KeyFor(archetype);

            if (key == null)
                return isBoss ? Ui(EnemyArchetypeKeys.Boss) : string.Empty;

            string family = Ui(key);
            if (!isBoss) return family;

            return LocalizedContent.FromTableFormat(
                LocalizedContent.UITable, EnemyArchetypeKeys.BossFormat,
                EnemyArchetypeKeys.Fallback(EnemyArchetypeKeys.BossFormat), family);
        }

        private static string Ui(string key)
            => LocalizedContent.Ui(key, EnemyArchetypeKeys.Fallback(key));
    }
}
