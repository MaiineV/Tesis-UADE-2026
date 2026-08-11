using System.Collections.Generic;
using UnityEngine;

namespace Rollgeon.UI.HUD.Breakdown
{
    /// <summary>
    /// Pool de <see cref="FlyingValueView"/> sobre el FlyingValueLayer (vuelos y ghosts
    /// comparten tipo y pool). Prewarm en Awake; <see cref="Clear"/> en el teardown del HUD.
    /// </summary>
    public sealed class FlyingValuePool : MonoBehaviour
    {
        [SerializeField] private FlyingValueView _prefab;

        [SerializeField]
        [Tooltip("Instancias precalentadas (vuelo + sus ghosts solapados).")]
        private int _prewarm = 8;

        private readonly Stack<FlyingValueView> _pool = new Stack<FlyingValueView>();

        private void Awake()
        {
            for (int i = 0; i < _prewarm; i++)
            {
                var view = CreateInstance();
                if (view == null) break;
                _pool.Push(view);
            }
        }

        public FlyingValueView Rent()
        {
            var view = _pool.Count > 0 ? _pool.Pop() : CreateInstance();
            return view;
        }

        public void Release(FlyingValueView view)
        {
            if (view == null) return;
            _pool.Push(view);
        }

        public void SpawnGhost(string text, Sprite icon, Vector2 anchoredPosition, Color? tint = null)
        {
            var ghost = Rent();
            if (ghost == null) return;
            ghost.PlayAsGhost(text, icon, anchoredPosition, tint);
        }

        private FlyingValueView CreateInstance()
        {
            if (_prefab == null) return null;
            var view = Instantiate(_prefab, transform);
            view.SetPool(this);
            view.gameObject.SetActive(false);
            return view;
        }
    }
}
