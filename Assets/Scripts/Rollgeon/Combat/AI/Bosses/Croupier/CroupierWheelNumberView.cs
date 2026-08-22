using System.Collections.Generic;
using System.Text;
using Patterns;
using TMPro;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Croupier
{
    /// <summary>
    /// El número cantado (<see cref="ICroupierWheelService.SungNumbers"/>), escrito en el centro de la
    /// ruleta.
    /// </summary>
    /// <remarks>
    /// El label es hijo del root del wrapper y no de <c>Wheel</c>: colgarlo de la rueda lo haría girar
    /// con ella y sería ilegible justo en el momento del canto.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CroupierWheelNumberView : MonoBehaviour
    {
        /// <summary>Nombre con el que el builder parentea el label al wrapper.</summary>
        public const string DefaultLabelChildName = "WheelNumber";

        /// <summary>
        /// Separador entre los dos números de fase 2. Barra y no interpunto: la pixel font del HUD
        /// (<c>m6x11plus</c>) no tiene <c>·</c> (U+00B7) en su atlas y el glifo saldría como cuadradito.
        /// </summary>
        public const string DefaultSeparator = " / ";

        [Header("Rig")]
        [Tooltip("Label del número. Si queda vacío se busca un hijo llamado \"WheelNumber\".")]
        [SerializeField] private TMP_Text _label;

        [Tooltip("Gira el label para que siempre encare a la cámara. Con la cámara fija del juego es " +
                 "un no-op; existe para que el número no quede espejado si algún día la cámara rota.")]
        [SerializeField] private bool _faceCamera = true;

        private ICroupierWheelService _service;

        // ======================================================================
        // Ciclo de vida
        // ======================================================================

        private void Awake()
        {
            if (_label == null)
            {
                var child = transform.Find(DefaultLabelChildName);
                if (child != null) _label = child.GetComponent<TMP_Text>();
            }

            if (_label == null)
            {
                // Sin label no hay nada que escribir: apagarse evita un Update que sólo loguearía.
                Debug.LogWarning($"[CroupierWheelNumberView] '{name}' no tiene label asignado ni un hijo " +
                                 $"'{DefaultLabelChildName}' — el componente queda apagado y el número " +
                                 "cantado no se ve.");
                enabled = false;
                return;
            }

            Publish(null);
        }

        private void OnDisable() => Unbind();

        private void Update()
        {
            EnsureBound();
            FaceCamera();
        }

        // ======================================================================
        // Servicio
        // ======================================================================

        private void EnsureBound()
        {
            ServiceLocator.TryGetService<ICroupierWheelService>(out var current);
            if (ReferenceEquals(current, _service)) return;

            Unbind();

            _service = current;
            if (_service == null)
            {
                // El servicio se fue (fin de combate): el número no puede quedar colgado en pantalla.
                Publish(null);
                return;
            }

            _service.NumbersChanged += Publish;
            Publish(_service.SungNumbers);
        }

        private void Unbind()
        {
            if (_service == null) return;

            _service.NumbersChanged -= Publish;
            _service = null;
        }

        // ======================================================================
        // Pintado
        // ======================================================================

        private void Publish(IReadOnlyList<int> numbers)
        {
            if (_label == null) return;

            string text = Format(numbers, DefaultSeparator);
            _label.text = text;

            // Se apaga el GameObject y no sólo el texto: un label vacío sigue pagando su draw call y,
            // con outline autorado, deja un halo visible sobre el hub.
            _label.gameObject.SetActive(text.Length > 0);
        }

        /// <summary>
        /// Los números en el aire, listos para escribir. Vacío si no hay ninguno.
        /// </summary>
        public static string Format(IReadOnlyList<int> numbers, string separator)
        {
            if (numbers == null || numbers.Count == 0) return string.Empty;
            if (numbers.Count == 1) return numbers[0].ToString();

            var builder = new StringBuilder();
            for (int i = 0; i < numbers.Count; i++)
            {
                if (i > 0) builder.Append(separator);
                builder.Append(numbers[i]);
            }
            return builder.ToString();
        }

        private void FaceCamera()
        {
            if (!_faceCamera || _label == null) return;

            var cam = Camera.main;
            if (cam == null) return;

            // Se copia la rotación de la cámara en vez de mirar hacia ella: con LookAt, dos labels a
            // distinta distancia quedan con inclinaciones distintas y el texto se lee torcido.
            _label.transform.rotation = cam.transform.rotation;
        }
    }
}
