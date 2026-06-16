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
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<CoroutineHost>();
                return _instance;
            }
        }

        public static Coroutine Run(IEnumerator routine)
        {
            return Instance.StartCoroutine(routine);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
