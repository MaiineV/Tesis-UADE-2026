using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Partículas UI de los dados arrojables 2D: pool de Images simuladas a mano
    /// (velocidad + gravedad + shrink/fade). El canvas es Screen Space Overlay — un
    /// ParticleSystem no puede renderizar encima del HUD, así que las "partículas"
    /// son cuadraditos de UI instanciados desde un prefab autorado. Corre en tiempo
    /// ESCALADO a propósito: el hitstop del crit las congela junto con el juego.
    /// Gateado por reduced motion; campos opcionales: sin wiring, no-op.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Dice Throw Impact Burst")]
    public sealed class DiceThrowImpactBurst : MonoBehaviour
    {
        [SerializeField, Optional, Tooltip("Prefab del cuadradito (Image sin raycast). Sin wiring, no-op.")]
        private Image _particlePrefab;

        [SerializeField, Optional, Tooltip("Espacio/padre de las partículas — wirear al DiceThrowLayer. " +
                 "Null = el RectTransform de este GO.")]
        private RectTransform _container;

        [Header("Tuning")]
        [SerializeField, Tooltip("Partículas por burst a intensidad 0 → 1.")]
        private Vector2Int _countRange = new Vector2Int(4, 10);

        [SerializeField, Tooltip("Velocidad inicial (px/s) a intensidad 0 → 1.")]
        private Vector2 _speedRange = new Vector2(260f, 720f);

        [SerializeField, Tooltip("Apertura total del cono alrededor de la dirección (grados).")]
        private float _spreadDegrees = 140f;

        [SerializeField, Tooltip("Gravedad (px/s²) hacia abajo del canvas.")]
        private float _gravity = 1500f;

        [SerializeField, Tooltip("Vida (s) mín/máx por partícula.")]
        private Vector2 _lifeRange = new Vector2(0.22f, 0.45f);

        [SerializeField, Tooltip("Tamaño (px) mín/máx por partícula.")]
        private Vector2 _sizeRange = new Vector2(4f, 9f);

        [SerializeField, Tooltip("Tope de partículas vivas — los bursts extra se recortan.")]
        private int _maxAlive = 48;

        private sealed class Particle
        {
            public Image Img;
            public RectTransform Rect;
            public Vector2 Vel;
            public float Life;
            public float MaxLife;
        }

        private readonly List<Particle> _alive = new List<Particle>();
        private readonly Stack<Particle> _pool = new Stack<Particle>();

        /// <summary>
        /// Explota en <paramref name="pos"/> (espacio local del contenedor) abanicando
        /// alrededor de <paramref name="dir"/>, escalado por <paramref name="intensity01"/>.
        /// </summary>
        public void Burst(Vector2 pos, Vector2 dir, float intensity01)
        {
            if (_particlePrefab == null) return;
            if (DiceAnim.DiceUiMotionPrefs.ReducedMotion) return;

            var container = _container != null ? _container : (RectTransform)transform;
            if (dir == Vector2.zero) dir = Vector2.up;
            dir.Normalize();
            float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float t = Mathf.Clamp01(intensity01);
            int count = Mathf.RoundToInt(Mathf.Lerp(_countRange.x, _countRange.y, t));

            for (int i = 0; i < count && _alive.Count < _maxAlive; i++)
            {
                var p = _pool.Count > 0 ? _pool.Pop() : CreateParticle(container);

                float angle = (baseAngle + Random.Range(-_spreadDegrees, _spreadDegrees) * 0.5f)
                              * Mathf.Deg2Rad;
                float speed = Mathf.Lerp(_speedRange.x, _speedRange.y, t) * Random.Range(0.55f, 1.25f);
                p.Vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                p.MaxLife = Random.Range(_lifeRange.x, _lifeRange.y);
                p.Life = 0f;

                float size = Random.Range(_sizeRange.x, _sizeRange.y);
                p.Rect.anchoredPosition = pos;
                p.Rect.sizeDelta = new Vector2(size, size);
                p.Rect.localScale = Vector3.one;
                p.Rect.SetAsLastSibling(); // por encima de ghosts spawneados antes
                var c = p.Img.color; c.a = 1f; p.Img.color = c;
                p.Img.gameObject.SetActive(true);
                _alive.Add(p);
            }
        }

        private Particle CreateParticle(RectTransform container)
        {
            var img = Instantiate(_particlePrefab, container);
            img.raycastTarget = false;
            return new Particle { Img = img, Rect = (RectTransform)img.transform };
        }

        private void Update()
        {
            if (_alive.Count == 0) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return; // hitstop: congeladas junto con el juego, a propósito

            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                var p = _alive[i];
                p.Life += dt;
                float life01 = p.Life / p.MaxLife;
                if (life01 >= 1f || p.Img == null)
                {
                    Recycle(i, p);
                    continue;
                }
                p.Vel.y -= _gravity * dt;
                p.Rect.anchoredPosition += p.Vel * dt;
                float fade = 1f - life01;
                p.Rect.localScale = new Vector3(fade, fade, 1f);
                var c = p.Img.color; c.a = fade; p.Img.color = c;
            }
        }

        private void Recycle(int index, Particle p)
        {
            _alive.RemoveAt(index);
            if (p.Img == null) return; // destruida desde afuera (teardown de escena)
            p.Img.gameObject.SetActive(false);
            _pool.Push(p);
        }

        private void OnDisable()
        {
            // Corte seco: son puro chrome, no sobreviven al layer.
            for (int i = _alive.Count - 1; i >= 0; i--) Recycle(i, _alive[i]);
        }
    }
}
