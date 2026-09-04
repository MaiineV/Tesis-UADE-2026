using UnityEngine;

namespace Rollgeon.Rendering
{
    /// <summary>
    /// Hace flotar el visual 3D de un ítem sobre su pedestal. Va en la raíz del
    /// prefab de <c>ItemSO.WorldPrefab</c>.
    /// </summary>
    /// <remarks>
    /// Va por transform y no por el shader <c>Rollgeon/PaletteCelItemFloat</c>
    /// (que hace lo mismo por vértice) porque los ítems usan los materiales
    /// compartidos de <c>PA_MainPalette</c> — los mismos que las paredes y los
    /// pisos. Meterles el shader flotante haría flotar el dungeon entero; la
    /// alternativa sería duplicar la paleta en variantes float, que es mucho
    /// asset para 3-6 objetos por sala.
    /// </remarks>
    [AddComponentMenu("Rollgeon/Rendering/Pedestal Item Float")]
    public sealed class PedestalItemFloat : MonoBehaviour
    {
        [Tooltip("Cuánto sube y baja respecto de su posición de reposo, en unidades de mundo.")]
        [Range(0f, 0.5f)] public float Amplitude = 0.05f;

        [Tooltip("Velocidad del seno, en radianes por segundo.")]
        [Range(0f, 10f)] public float Speed = 1.6f;

        private Vector3 _basePosition;
        private float _phase;
        private bool _hasBase;

        // Última posición que escribió ESTE componente. Si al arrancar el frame
        // difiere, alguien lo movió desde afuera (el spawn de la tienda reposiciona
        // el visual DESPUÉS de instanciarlo, y el Inspector en Play Mode también) y
        // hay que adoptar eso como la nueva base en vez de tironear hacia la vieja.
        // Mismo criterio que TorchFlicker.
        private Vector3 _lastWrittenPosition;

        private void OnDisable()
        {
            // Devolver el transform a su reposo ANTES de soltar la base: si el
            // objeto se reusa desde un pool, capturar de nuevo sobre la posición
            // ya desplazada haría que el bob se acumule y el ítem se vaya subiendo
            // un poco más en cada reuso.
            if (_hasBase) transform.localPosition = _basePosition;
            _hasBase = false;
        }

        // LateUpdate y no Update: el spawn del pedestal escribe localPosition en su
        // propio paso, y capturar la base antes de eso dejaría el ítem flotando
        // alrededor del origen del prefab.
        private void LateUpdate()
        {
            if (!_hasBase)
            {
                CaptureBase();
                return;
            }

            if (transform.localPosition != _lastWrittenPosition)
                _basePosition = transform.localPosition;

            float offset = PedestalItemFloatMath.VerticalOffset(Time.time, _phase, Speed, Amplitude);
            transform.localPosition = _basePosition + new Vector3(0f, offset, 0f);
            _lastWrittenPosition = transform.localPosition;
        }

        private void CaptureBase()
        {
            _basePosition = transform.localPosition;
            _lastWrittenPosition = _basePosition;
            _phase = PedestalItemFloatMath.PhaseFromWorldXZ(transform.position.x, transform.position.z);
            _hasBase = true;
        }
    }
}
