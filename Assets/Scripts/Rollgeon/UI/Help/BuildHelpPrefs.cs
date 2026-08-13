using UnityEngine;

namespace Rollgeon.UI.Help
{
    /// <summary>
    /// Persistencia en PlayerPrefs de "ya vi la guía de armado de bolsa".
    /// <para>
    /// Va en PlayerPrefs y no en el meta save, igual que <c>AnalyticsPrefs</c>: es un
    /// flag de UI, no progresión, y meterlo en el snapshot obligaría a tocar el formato
    /// de save por un bool. Como contrapartida no lo borra "Borrar partida" — por eso
    /// <c>OptionsScreen</c> llama <see cref="Clear"/> explícitamente al resetear, para
    /// que un jugador que empieza de cero vuelva a ver la guía.
    /// </para>
    /// </summary>
    public sealed class BuildHelpPrefs : IBuildHelpSeenStore
    {
        public const string SeenKey = "rollgeon.help.build_seen";

        public bool HasSeen => PlayerPrefs.GetInt(SeenKey, 0) == 1;

        public void MarkSeen()
        {
            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();
        }

        /// <summary>Vuelve al estado "nunca visto". Lo usan el reset de progreso y los tests.</summary>
        public void Clear() => PlayerPrefs.DeleteKey(SeenKey);

        /// <summary>Atajo estático para callers que no quieren instanciar (reset de progreso).</summary>
        public static void ClearSeen() => PlayerPrefs.DeleteKey(SeenKey);
    }
}
