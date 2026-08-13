using System.Collections;
using UnityEngine;

namespace Rollgeon.Patterns
{
    /// <summary>
    /// MonoBehaviour singleton always-active (en <c>DontDestroyOnLoad</c>) para hostear
    /// coroutines disparadas desde código no-MonoBehaviour. Se crea perezosamente la
    /// primera vez que alguien lo pide.
    /// </summary>
    /// <remarks>
    /// Útil cuando un servicio (clase plana) necesita correr una coroutine pero no
    /// puede asumir que algún UI está activo, ej. <c>CombatDeathWatcher</c> que delaya
    /// el despawn del enemigo para que los floating numbers terminen de mostrarse antes
    /// de que el HUD se desmonte por la transición a Victory.
    /// </remarks>
    [DefaultExecutionOrder(-1000)]
    public sealed class CoroutineHost : MonoBehaviour
    {
        private static CoroutineHost _instance;

        public static CoroutineHost Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("[CoroutineHost]");
                // DontDestroyOnLoad explota fuera de play mode — los tests EditMode
                // llegan acá vía eventos de fase (perma-move arma el movimiento con
                // una corrutina). En EditMode el host es un GO común de la escena.
                if (Application.isPlaying) DontDestroyOnLoad(go);
                _instance = go.AddComponent<CoroutineHost>();
                return _instance;
            }
        }

        public static Coroutine Run(IEnumerator routine)
        {
            return Instance.StartCoroutine(routine);
        }

        /// <summary>
        /// Detiene una coroutine lanzada con <see cref="Run"/>. Usa el backing field
        /// directamente: si el host no existe, no hay nada corriendo que detener y
        /// crearlo solo para esto sería un side-effect gratuito.
        /// </summary>
        public static void Stop(Coroutine routine)
        {
            if (routine == null || _instance == null) return;
            _instance.StopCoroutine(routine);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
