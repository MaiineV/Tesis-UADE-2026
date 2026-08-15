using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Threat;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Croupier
{
    /// <summary>
    /// Gira la ruleta parenteada al prefab del Croupier cada vez que cambia el número en el aire, y la
    /// deja parada en el sector cantado: el ángulo de la rueda <b>es</b> el número.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué el ángulo mapea al número y no es un giro decorativo.</b> El jefe canta un número que
    /// hoy sólo se lee en el overlay del paño. Atar el ángulo al sector le da al prop una segunda
    /// lectura del mismo dato (y gratis: el mismo tween sirve de feedback). Un giro random sería ruido
    /// que compite con el telegraph en vez de reforzarlo.
    /// </para>
    /// <para>
    /// <b>Canta vs. corrimiento.</b> <see cref="ICroupierWheelService.NumbersChanged"/> es un solo canal
    /// para los dos, así que se clasifican por forma: si todos los números avanzaron exactamente +1
    /// respecto del evento anterior, fue el corrimiento (un click corto); cualquier otra cosa es un
    /// canto nuevo (giro largo con vueltas de floreo). Sin la distinción, correr la rueda daría el mismo
    /// giro enorme que el canto y las dos cosas dejarían de diferenciarse.
    /// </para>
    /// <para>
    /// <b>Nunca retrocede.</b> El objetivo siempre se busca hacia adelante: una rueda que vuelve atrás
    /// se lee como un bug de tween, no como un sorteo — incluso cuando el número nuevo es menor.
    /// </para>
    /// <para>
    /// <b>Binding tardío y sin crear el servicio.</b> El servicio lo crea el nodo que canta
    /// (<see cref="CroupierWheelService.ResolveOrCreate"/>) en su primer tick, que puede caer después
    /// del <c>Awake</c> de este componente. Se resuelve por <c>Update</c> (un lookup de diccionario) en
    /// vez de crearlo desde acá: la vista no tiene por qué instanciar estado de combate, y si el jefe
    /// nunca aparece este componente no deja un servicio registrado de más. Al enganchar se sincroniza
    /// con <see cref="ICroupierWheelService.SungNumbers"/> para no perderse el primer canto, que ocurre
    /// en el mismo tick en el que el servicio nace.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CroupierWheelSpinVisual : MonoBehaviour
    {
        /// <summary>Nombre con el que el builder parentea la ruleta al wrapper.</summary>
        public const string DefaultWheelChildName = "Wheel";

        [Header("Rig")]
        [Tooltip("Transform de la ruleta. Si queda vacío se busca un hijo llamado \"Wheel\".")]
        [SerializeField] private Transform _wheel;

        [Tooltip("Eje de giro en el espacio local de la ruleta. El disco del prop Ruletav03 mira a ±Z, " +
                 "así que su eje es Z. Negarlo invierte el sentido del giro.")]
        [SerializeField] private Vector3 _spinAxis = Vector3.forward;

        [Header("Mapeo número → ángulo")]
        [Tooltip("Cuántos sectores tiene la rueda. 0 = usar el conteo de sectores de sala del juego.")]
        [SerializeField] private int _sectorCount;

        [Header("Canto")]
        [Tooltip("Vueltas completas de más al cantar un número nuevo, antes de frenar en el sector.")]
        [SerializeField] private float _singTurns = 1.5f;

        [Tooltip("Segundos que tarda el giro del canto.")]
        [SerializeField] private float _singDuration = 0.85f;

        [Header("Corrimiento (el jugador cerró el turno en el sector)")]
        [Tooltip("Vueltas completas de más al correr la rueda. 0 = sólo el click de un sector.")]
        [SerializeField] private float _nudgeTurns;

        [Tooltip("Segundos que tarda el click del corrimiento.")]
        [SerializeField] private float _nudgeDuration = 0.35f;

        private ICroupierWheelService _service;
        private readonly List<int> _lastNumbers = new List<int>(2);

        private Quaternion _baseRotation = Quaternion.identity;
        private Vector3 _axis = Vector3.forward;

        private float _angle;
        private float _fromAngle;
        private float _toAngle;
        private float _elapsed;
        private float _duration;
        private bool _spinning;

        private int SectorCount => _sectorCount > 0 ? _sectorCount : ThreatAreaShape.RoomSectorCount;

        // ======================================================================
        // Ciclo de vida
        // ======================================================================

        private void Awake()
        {
            if (_wheel == null) _wheel = transform.Find(DefaultWheelChildName);
            if (_wheel == null)
            {
                // Sin rueda no hay nada que girar: apagarse evita un Update que sólo loguearía.
                Debug.LogWarning($"[CroupierWheelSpinVisual] '{name}' no tiene rueda asignada ni un hijo " +
                                 $"'{DefaultWheelChildName}' — el componente queda apagado.");
                enabled = false;
                return;
            }

            // La rotación de autor del prop es el cero del giro: el tween la compone, no la pisa.
            _baseRotation = _wheel.localRotation;
            _axis = _spinAxis.sqrMagnitude > Mathf.Epsilon ? _spinAxis.normalized : Vector3.forward;
        }

        private void OnDisable() => Unbind();

        private void Update()
        {
            EnsureBound();
            AdvanceSpin(Time.deltaTime);
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
            if (_service == null) return;

            _service.NumbersChanged += OnNumbersChanged;

            // El primer canto viaja en el mismo tick en el que el nodo crea el servicio, así que su
            // evento ya pasó cuando llegamos acá: se lee el estado en vez de esperar el próximo.
            var inAir = _service.SungNumbers;
            if (inAir != null && inAir.Count > 0) OnNumbersChanged(inAir);
        }

        private void Unbind()
        {
            if (_service == null) return;

            _service.NumbersChanged -= OnNumbersChanged;
            _service = null;
            _lastNumbers.Clear();
        }

        private void OnNumbersChanged(IReadOnlyList<int> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                // Detonó (o terminó el combate): la rueda se queda donde cayó — es el "salió el 4".
                _lastNumbers.Clear();
                return;
            }

            bool nudged = IsSingleStepFrom(_lastNumbers, numbers);
            CacheNumbers(numbers);

            SpinTo(
                numbers[0],
                nudged ? _nudgeTurns : _singTurns,
                nudged ? _nudgeDuration : _singDuration);
        }

        // ======================================================================
        // Tween
        // ======================================================================

        private void SpinTo(int sector, float extraTurns, float duration)
        {
            int count = Mathf.Max(1, SectorCount);
            int index = ((sector - 1) % count + count) % count;
            float aligned = index * (360f / count);

            // Mathf.Repeat del delta = "la próxima vez que el ángulo actual pase por el sector", así el
            // giro siempre va hacia adelante aunque el número nuevo sea menor que el anterior.
            _fromAngle = _angle;
            _toAngle = _angle + Mathf.Repeat(aligned - _angle, 360f) + Mathf.Max(0f, extraTurns) * 360f;
            _duration = Mathf.Max(0f, duration);
            _elapsed = 0f;
            _spinning = true;

            if (_duration <= 0f) SnapToTarget();
        }

        private void AdvanceSpin(float deltaTime)
        {
            if (!_spinning) return;

            _elapsed += deltaTime;
            float t = _duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / _duration);

            _angle = Mathf.Lerp(_fromAngle, _toAngle, EaseOut(t));
            Apply();

            if (t >= 1f) SnapToTarget();
        }

        private void SnapToTarget()
        {
            // El ángulo se normaliza al frenar: acumular vueltas toda la pelea le come precisión al
            // float y el sector dejaría de caer donde tiene que caer.
            _angle = Mathf.Repeat(_toAngle, 360f);
            _fromAngle = _angle;
            _toAngle = _angle;
            _spinning = false;
            Apply();
        }

        private void Apply()
        {
            if (_wheel == null)
            {
                enabled = false;
                return;
            }

            _wheel.localRotation = _baseRotation * Quaternion.AngleAxis(_angle, _axis);
        }

        /// <summary>Ease-out cúbico: arranca rápido y se sienta en el número, como una rueda con roce.</summary>
        private static float EaseOut(float t)
        {
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        // ======================================================================
        // Clasificación canto / corrimiento
        // ======================================================================

        private void CacheNumbers(IReadOnlyList<int> numbers)
        {
            _lastNumbers.Clear();
            for (int i = 0; i < numbers.Count; i++) _lastNumbers.Add(numbers[i]);
        }

        /// <summary>
        /// <c>true</c> si <paramref name="current"/> es <paramref name="previous"/> corrido un sector:
        /// la firma del corrimiento. En fase 1 (el único momento en que la rueda se corre) hay un solo
        /// número en el aire, así que "todos +1" y "el que se corrió" son lo mismo.
        /// </summary>
        private bool IsSingleStepFrom(List<int> previous, IReadOnlyList<int> current)
        {
            if (previous.Count == 0 || previous.Count != current.Count) return false;

            int count = Mathf.Max(1, SectorCount);
            for (int i = 0; i < current.Count; i++)
            {
                if (current[i] != Wrap(previous[i] + 1, count)) return false;
            }
            return true;
        }

        /// <summary>
        /// Mismo wrap que <c>CroupierWheelService</c> (privado allá): es una rueda, del último sector se
        /// vuelve al primero.
        /// </summary>
        private static int Wrap(int number, int count)
        {
            int wrapped = ((number - 1) % count + count) % count;
            return wrapped + 1;
        }
    }
}
