using System;
using Patterns;
using Rollgeon.Grid;
using UnityEngine;
using UnityEngine.Events;

namespace Rollgeon.Tiles.Visuals
{
    /// <summary>
    /// Puente opcional entre el estado lógico de una casilla especial y su visual: el
    /// prefab de arte engancha animaciones/VFX a estos UnityEvents en el Inspector y el
    /// swap del modelo 3D no toca una línea de código. Lo bindea
    /// <see cref="SpecialTileService"/> al instanciar el <c>VisualPrefab</c> por celda.
    /// </summary>
    [AddComponentMenu("Rollgeon/Tiles/Special Tile Visual Binding")]
    public sealed class SpecialTileVisualBinding : MonoBehaviour
    {
        [Tooltip("La celda se armó (Pinchos: triángulos rojos, listo para pinchar).")]
        public UnityEvent OnArmed;

        [Tooltip("La celda se desarmó tras disparar (Pinchos: triángulos negros).")]
        public UnityEvent OnDisarmed;

        [Tooltip("La casilla resolvió su efecto sobre una unidad (flash, subida en Y...).")]
        public UnityEvent OnTriggered;

        [Tooltip("La instancia expiró — despedida antes del Destroy (la llama se apaga).")]
        public UnityEvent OnExpiring;

        private Guid _instanceId;
        private GridCoord _coord;
        private bool _bound;

        private EventManager.EventReceiver _onStateChanged;
        private EventManager.EventReceiver _onTriggered;
        private EventManager.EventReceiver _onExpired;

        /// <summary>Llamado por el servicio al instanciar el visual de la celda.</summary>
        public void Bind(Guid instanceId, GridCoord coord)
        {
            Unbind();

            _instanceId = instanceId;
            _coord = coord;

            _onStateChanged = HandleStateChanged;
            _onTriggered = HandleTriggered;
            _onExpired = HandleExpired;
            EventManager.Subscribe(EventName.OnSpecialTileStateChanged, _onStateChanged);
            EventManager.Subscribe(EventName.OnSpecialTileTriggered, _onTriggered);
            EventManager.Subscribe(EventName.OnSpecialTileExpired, _onExpired);
            _bound = true;
        }

        private void OnDestroy() => Unbind();

        /// <remarks>Red de seguridad para el visual que alguien apague por fuera del pool.
        /// <b>El pool no se apoya en esto:</b> llama <see cref="Unbind"/> explícito al estacionar,
        /// porque en EditMode Unity no dispara OnEnable/OnDisable en clones instanciados en runtime
        /// y el desenganche quedaría sin ocurrir justo donde se lo testea.</remarks>
        private void OnDisable() => Unbind();

        /// <summary>
        /// Suelta los eventos. Público porque el pool tiene que poder soltarlos <b>determinísticamente</b>
        /// al estacionar el clon: <c>SetActive(false)</c> no llama <c>OnDestroy</c>, y el
        /// <c>OnDisable</c> no corre en EditMode. Idempotente — <see cref="Bind"/> arranca llamándolo.
        /// </summary>
        public void Unbind()
        {
            if (!_bound) return;
            EventManager.UnSubscribe(EventName.OnSpecialTileStateChanged, _onStateChanged);
            EventManager.UnSubscribe(EventName.OnSpecialTileTriggered, _onTriggered);
            EventManager.UnSubscribe(EventName.OnSpecialTileExpired, _onExpired);
            _bound = false;
        }

        private void HandleStateChanged(params object[] args)
        {
            if (args == null || args.Length < 3) return;
            if (!(args[0] is Guid instanceId) || instanceId != _instanceId) return;
            if (!(args[1] is GridCoord coord) || !coord.Equals(_coord)) return;
            if (!(args[2] is bool armed)) return;

            if (armed) OnArmed?.Invoke();
            else OnDisarmed?.Invoke();
        }

        private void HandleTriggered(params object[] args)
        {
            if (args == null || args.Length < 1) return;
            if (!(args[0] is Guid instanceId) || instanceId != _instanceId) return;
            OnTriggered?.Invoke();
        }

        private void HandleExpired(params object[] args)
        {
            if (args == null || args.Length < 1) return;
            if (!(args[0] is Guid instanceId) || instanceId != _instanceId) return;
            OnExpiring?.Invoke();
        }
    }
}
