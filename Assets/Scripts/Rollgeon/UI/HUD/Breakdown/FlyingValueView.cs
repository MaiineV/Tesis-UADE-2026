using System;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD.Breakdown
{
    /// <summary>
    /// Un valor que vuela punto-a-punto ("+2", sprite de item + "+3") con curva bezier y
    /// ghost trail (copias del pool con fade — sin TrailRenderer ni shaders). Vive pooled
    /// bajo el FlyingValueLayer; el mismo tipo hace de ghost vía <see cref="PlayAsGhost"/>.
    /// </summary>
    public sealed class FlyingValueView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private Image _icon;
        [SerializeField] private CanvasGroup _group;

        [SerializeField]
        [Tooltip("Segundos entre ghosts del trail. 0 = sin trail.")]
        private float _ghostIntervalSeconds = 0.035f;

        [SerializeField] private float _ghostFadeSeconds = 0.15f;

        private FlyingValuePool _pool;
        private Tween _flight;
        private Tween _ghostFade;
        private Tween _spawnScale;
        private Vector2 _p0, _p1, _p2;
        private float _nextGhostAt;
        private Action _onArrive;
        private Color? _tint;

        public bool IsFlying { get; private set; }

        public RectTransform Rect => (RectTransform)transform;

        public void SetPool(FlyingValuePool pool) => _pool = pool;

        /// <summary>
        /// Vuela desde <paramref name="from"/> hasta <paramref name="to"/> (anclas en
        /// cualquier canvas — se proyectan al espacio del layer). <paramref name="arc"/>
        /// desplaza el punto de control perpendicular al segmento (+ = izquierda del avance).
        /// </summary>
        public void Fly(RectTransform from, RectTransform to, string text, Sprite icon,
            float duration, float arc, Action onArrive, Color? tint = null, float startScale = 1f)
        {
            var layer = (RectTransform)transform.parent;
            _p0 = ProjectToLayer(layer, from);
            _p2 = ProjectToLayer(layer, to);
            var mid = (_p0 + _p2) * 0.5f;
            var dir = (_p2 - _p0).normalized;
            _p1 = mid + new Vector2(-dir.y, dir.x) * arc;

            Setup(text, icon, 1f, tint);
            Rect.anchoredPosition = _p0;
            Rect.localScale = Vector3.one * Mathf.Max(0.1f, startScale);
            _onArrive = onArrive;
            _nextGhostAt = 0f;
            IsFlying = true;
            gameObject.SetActive(true);

            if (_spawnScale.isAlive) _spawnScale.Stop();
            if (startScale > 1.001f)
                _spawnScale = Tween.Scale(transform, 1f, duration * 0.4f, Ease.OutQuad);

            if (_flight.isAlive) _flight.Stop();
            _flight = Tween.Custom(this, 0f, 1f, duration,
                (view, t) => view.OnFlightTick(t), Ease.InQuad)
                .OnComplete(this, view => view.Arrive());
        }

        /// <summary>Skip: salta al destino y dispara el arribo ya mismo.</summary>
        public void CompleteInstantly()
        {
            if (!IsFlying) return;
            if (_flight.isAlive) _flight.Stop();
            Rect.anchoredPosition = _p2;
            Arrive();
        }

        /// <summary>Copia efímera del trail: fade + shrink y de vuelta al pool.</summary>
        public void PlayAsGhost(string text, Sprite icon, Vector2 anchoredPosition, Color? tint = null)
        {
            Setup(text, icon, 0.55f, tint);
            Rect.anchoredPosition = anchoredPosition;
            Rect.localScale = Vector3.one;
            IsFlying = false;
            gameObject.SetActive(true);

            if (_ghostFade.isAlive) _ghostFade.Stop();
            Tween.Scale(transform, 0.7f, _ghostFadeSeconds, Ease.OutQuad);
            _ghostFade = Tween.Custom(this, 0.55f, 0f, _ghostFadeSeconds,
                (view, a) => { if (view._group != null) view._group.alpha = a; })
                .OnComplete(this, view => view.ReturnToPool());
        }

        // Proyecta un ancla de cualquier canvas al espacio local del layer (los orígenes
        // viven en Canvas_ActionRoll; el layer es full-stretch en el mismo canvas, pero la
        // proyección vía screen-point también banca anclas de otros canvas, ej. la espada).
        private static Vector2 ProjectToLayer(RectTransform layer, RectTransform anchor)
        {
            var canvas = layer.GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, anchor.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, cam, out var local);
            return local;
        }

        public static Vector2 EvaluateBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            float u = 1f - t;
            return u * u * p0 + 2f * u * t * p1 + t * t * p2;
        }

        private void OnFlightTick(float t)
        {
            Rect.anchoredPosition = EvaluateBezier(_p0, _p1, _p2, t);

            if (_ghostIntervalSeconds <= 0f || _pool == null) return;
            if (Time.unscaledTime < _nextGhostAt) return;
            _nextGhostAt = Time.unscaledTime + _ghostIntervalSeconds;
            _pool.SpawnGhost(_label != null ? _label.text : string.Empty,
                _icon != null && _icon.enabled ? _icon.sprite : null,
                Rect.anchoredPosition, _tint);
        }

        private void Arrive()
        {
            IsFlying = false;
            var onArrive = _onArrive;
            _onArrive = null;
            ReturnToPool();
            onArrive?.Invoke();
        }

        private void Setup(string text, Sprite icon, float alpha, Color? tint)
        {
            _tint = tint;
            if (_label != null)
            {
                _label.text = text;
                _label.color = tint ?? Color.white;
            }
            if (_icon != null)
            {
                _icon.sprite = icon;
                _icon.enabled = icon != null;
            }
            if (_group != null) _group.alpha = alpha;
        }

        private void ReturnToPool()
        {
            if (_flight.isAlive) _flight.Stop();
            if (_ghostFade.isAlive) _ghostFade.Stop();
            if (_spawnScale.isAlive) _spawnScale.Stop();
            IsFlying = false;
            gameObject.SetActive(false);
            _pool?.Release(this);
        }

        private void OnDisable()
        {
            if (_flight.isAlive) _flight.Stop();
            if (_ghostFade.isAlive) _ghostFade.Stop();
            if (_spawnScale.isAlive) _spawnScale.Stop();
        }
    }
}
