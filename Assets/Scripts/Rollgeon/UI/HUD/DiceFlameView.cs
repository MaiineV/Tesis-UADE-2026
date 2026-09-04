using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Llama de combo armado sobre un dado, animada por frames de sprite en la <see cref="Image"/>
    /// del mismo GameObject (hoja <c>DiceFlames.png</c>). Al encenderse corre Born una vez y
    /// loopea Tier 1; con tier <see cref="ComboFlameTier.High"/> pasa por la transición una vez
    /// y loopea Tier 2. No hay animación inversa: bajar de tier corta a Tier 1 y apagar oculta.
    /// La máquina avanza en <see cref="Tick"/> (público) para que los tests EditMode la manejen
    /// sin Update. Todos los campos son opcionales: una fase sin frames se saltea.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Dice Flame View")]
    [RequireComponent(typeof(Image))]
    public sealed class DiceFlameView : MonoBehaviour
    {
        public enum Phase { Off, Born, Tier1, Transition, Tier2 }

        [Title("Frames (DiceFlames.png)")]
        [SerializeField, Tooltip("Nacimiento, una vez al encenderse. Vacío = arranca directo en el loop.")]
        private Sprite[] _born = System.Array.Empty<Sprite>();

        [SerializeField, Tooltip("Loop del tier bajo (mitad baja del catálogo de combos).")]
        private Sprite[] _tier1 = System.Array.Empty<Sprite>();

        [SerializeField, Tooltip("Transición Tier 1 → Tier 2, una vez. Vacío = salta directo a Tier 2.")]
        private Sprite[] _transition = System.Array.Empty<Sprite>();

        [SerializeField, Tooltip("Loop del tier alto (Full House o superior).")]
        private Sprite[] _tier2 = System.Array.Empty<Sprite>();

        [Title("Tuning")]
        [SerializeField, MinValue(0.01f), Tooltip("Frames por segundo, común a todas las fases.")]
        private float _fps = 12f;

        [SerializeField, MinValue(0.01f),
         Tooltip("Multiplicador sobre el texel entero (ancho del slot / frame más ancho). 1 = el frame más ancho llena el dado.")]
        private float _scale = 1f;

        private Image _image;
        private Phase _phase = Phase.Off;
        private int _target = ComboFlameTier.Off;
        private int _frame;
        private float _timer;
        private float _referenceWidth = -1f;

        /// <summary>Fase actual de la máquina (solo lectura, para DiceZoneJuice y tests).</summary>
        public Phase CurrentPhase => _phase;

        /// <summary>Índice del frame dentro de la fase actual (solo lectura, para tests).</summary>
        public int FrameIndex => _frame;

        // Lazy: EditMode no corre Awake y el prefab puede construirse por código.
        private Image Image => _image != null ? _image : (_image = GetComponent<Image>());

        // Tiempo escalado a propósito: el único que toca timeScale es DiceHitstop y el resto del
        // juice del slot se congela con él (misma decisión que PlayComboCelebrate).
        private void Update() => Tick(Time.deltaTime);

        private void OnDisable() => SetTier(ComboFlameTier.Off);

        /// <summary>
        /// Cambia el tier objetivo (0 = apagar). Idempotente: repetir el mismo tier no reinicia
        /// Born — el payload de combo llega en cada toggle de hold.
        /// </summary>
        public void SetTier(int tier)
        {
            tier = Mathf.Clamp(tier, ComboFlameTier.Off, ComboFlameTier.High);
            if (tier == _target) return;
            _target = tier;

            if (tier == ComboFlameTier.Off)
            {
                Enter(Phase.Off);
                return;
            }

            switch (_phase)
            {
                case Phase.Off:
                    Enter(Phase.Born);
                    break;
                case Phase.Born:
                    // Born se completa siempre; al terminar, NextAfter lee el target vigente.
                    break;
                case Phase.Tier1:
                    if (tier == ComboFlameTier.High) Enter(Phase.Transition);
                    break;
                case Phase.Transition:
                case Phase.Tier2:
                    if (tier == ComboFlameTier.Low) Enter(Phase.Tier1);
                    break;
            }
        }

        /// <summary>
        /// Avanza la animación <paramref name="dt"/> segundos. El while recupera frames tras un
        /// stall (hitstop, hitch) y hace el avance determinista en tests.
        /// </summary>
        public void Tick(float dt)
        {
            if (_phase == Phase.Off || _fps <= 0f) return;
            float frameSeconds = 1f / _fps;
            _timer += dt;
            while (_timer >= frameSeconds && _phase != Phase.Off)
            {
                _timer -= frameSeconds;
                Step();
            }
        }

        private void Step()
        {
            var frames = FramesOf(_phase);
            int next = _frame + 1;
            if (next < frames.Length)
            {
                _frame = next;
                Show(frames[next]);
                return;
            }
            if (IsLoop(_phase))
            {
                _frame = 0;
                Show(frames[0]);
                return;
            }
            Enter(NextAfter(_phase));
        }

        private void Enter(Phase phase)
        {
            // Fases sin frames autorados se saltean hacia adelante (Born vacío → loop, transición
            // vacía → Tier 2). Un loop vacío no tiene a dónde ir: se apaga.
            for (int guard = 0; guard < 4 && phase != Phase.Off && FramesOf(phase).Length == 0; guard++)
                phase = IsLoop(phase) ? Phase.Off : NextAfter(phase);

            _phase = phase;
            _frame = 0;
            _timer = 0f;

            if (phase == Phase.Off)
            {
                if (Image != null) Image.enabled = false;
                return;
            }
            Show(FramesOf(phase)[0]);
        }

        private Phase NextAfter(Phase phase)
        {
            switch (phase)
            {
                case Phase.Born:
                    return _target == ComboFlameTier.High ? Phase.Transition : Phase.Tier1;
                case Phase.Transition:
                    return Phase.Tier2;
                default:
                    return phase;
            }
        }

        private static bool IsLoop(Phase phase) => phase == Phase.Tier1 || phase == Phase.Tier2;

        private Sprite[] FramesOf(Phase phase)
        {
            switch (phase)
            {
                case Phase.Born: return _born ?? System.Array.Empty<Sprite>();
                case Phase.Tier1: return _tier1 ?? System.Array.Empty<Sprite>();
                case Phase.Transition: return _transition ?? System.Array.Empty<Sprite>();
                case Phase.Tier2: return _tier2 ?? System.Array.Empty<Sprite>();
                default: return System.Array.Empty<Sprite>();
            }
        }

        private void Show(Sprite sprite)
        {
            var image = Image;
            if (image == null) return;
            image.sprite = sprite;
            image.enabled = sprite != null;
            if (sprite != null) ApplySize(image.rectTransform, sprite);
        }

        // Texel entero compartido por todas las fases: el frame más ancho de la hoja llena el
        // ancho del dado y las fases más chicas quedan proporcionales (la llama "crece" de
        // verdad de Tier 1 a Tier 2). Con filtro Point, un texel fraccional titila entre frames.
        private void ApplySize(RectTransform rect, Sprite sprite)
        {
            var parent = rect.parent as RectTransform;
            if (parent == null) return;
            float parentWidth = parent.rect.width;
            float referenceWidth = ReferenceWidth();
            if (parentWidth <= 0f || referenceWidth <= 0f) return;

            float texel = Mathf.Max(1, Mathf.FloorToInt(parentWidth / referenceWidth)) * _scale;
            rect.sizeDelta = new Vector2(
                Mathf.Round(sprite.rect.width * texel),
                Mathf.Round(sprite.rect.height * texel));
        }

        private float ReferenceWidth()
        {
            if (_referenceWidth > 0f) return _referenceWidth;
            float widest = 0f;
            widest = Mathf.Max(widest, WidestOf(_born));
            widest = Mathf.Max(widest, WidestOf(_tier1));
            widest = Mathf.Max(widest, WidestOf(_transition));
            widest = Mathf.Max(widest, WidestOf(_tier2));
            _referenceWidth = widest;
            return widest;
        }

        private static float WidestOf(Sprite[] frames)
        {
            float widest = 0f;
            if (frames == null) return widest;
            for (int i = 0; i < frames.Length; i++)
                if (frames[i] != null) widest = Mathf.Max(widest, frames[i].rect.width);
            return widest;
        }
    }
}
