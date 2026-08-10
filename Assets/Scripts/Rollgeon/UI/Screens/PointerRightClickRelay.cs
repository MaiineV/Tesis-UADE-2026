using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Reenvía el click derecho de un GameObject de UI a un callback. Pensado para
    /// convivir con un <see cref="UnityEngine.UI.Button"/> en el MISMO GameObject.
    /// </summary>
    /// <remarks>
    /// Existe por cómo rutea uGUI: el evento se entrega al primer ANCESTRO que maneje
    /// <see cref="IPointerClickHandler"/>, y el Button ya lo maneja — un handler puesto
    /// en el root de la fila nunca se enteraría del click sobre el sprite. En cambio,
    /// sobre un mismo GameObject uGUI ejecuta el functor en TODOS los componentes que
    /// implementan la interfaz, así que acá el Button se queda con el izquierdo y este
    /// relay con el derecho. Se agrega por código para no tocar prefabs ya autorados.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class PointerRightClickRelay : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>Quien lo agrega es dueño del callback y lo limpia al desbindear.</summary>
        public Action OnRightClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Right) return;
            OnRightClick?.Invoke();
        }

        private void OnDestroy() => OnRightClick = null;
    }
}
