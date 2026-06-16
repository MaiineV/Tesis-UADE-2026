using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Grid;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Combat.Threat
{
    /// <summary>
    /// Implementación pooled de <see cref="IThreatOverlayService"/>: un quad
    /// semitransparente por casilla amenazada, parented a un root propio y con
    /// pulso de alpha (ver <see cref="ThreatOverlayPulse"/>). Mismo ciclo de
    /// vida que <see cref="ThreatenedAreaService"/>: Global, y los visuales se
    /// apagan en <c>OnCombatEnd</c> / <c>OnRunEnd</c>.
    /// </summary>
    public sealed class ThreatTelegraphOverlay : IThreatOverlayService, IDisposable
    {
        // Levantado apenas del piso para no pelear z con el tinte del tile.
        public float YOffset = 0.06f;

        // < 1 para que se lea la grilla (mismo criterio que el ghost del editor).
        public float QuadScale = 0.92f;

        private readonly Dictionary<Guid, List<GameObject>> _activeBySource =
            new Dictionary<Guid, List<GameObject>>();
        private readonly Stack<GameObject> _free = new Stack<GameObject>();

        private GameObject _root;
        private Material _material;

        private EventManager.EventReceiver _onCombatEndHandler;
        private EventManager.EventReceiver _onRunEndHandler;

        /// <summary>Cantidad de quads visibles — para asserts de tests y debugging.</summary>
        public int ActiveQuadCount
        {
            get
            {
                int count = 0;
                foreach (var list in _activeBySource.Values)
                    foreach (var quad in list)
                        if (quad != null && quad.activeSelf) count++;
                return count;
            }
        }

        /// <summary>
        /// Devuelve el service registrado o crea + registra uno (Global). Lazy
        /// para no depender de wiring manual en <c>ServiceBootstrap.ExtraServices</c>.
        /// </summary>
        public static IThreatOverlayService ResolveOrCreate()
        {
            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var existing) && existing != null)
                return existing;

            var created = new ThreatTelegraphOverlay();
            created.RegisterGlobal();
            return created;
        }

        private void RegisterGlobal()
        {
            _onCombatEndHandler = OnScopeEndedExternal;
            _onRunEndHandler = OnScopeEndedExternal;
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEndHandler);
            EventManager.Subscribe(EventName.OnRunEnd, _onRunEndHandler);

            ServiceLocator.AddService<IThreatOverlayService>(this, ServiceScope.Global);
        }

        public void Dispose()
        {
            if (_onCombatEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatEnd, _onCombatEndHandler);
                _onCombatEndHandler = null;
            }
            if (_onRunEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnRunEnd, _onRunEndHandler);
                _onRunEndHandler = null;
            }

            _activeBySource.Clear();
            _free.Clear();
            DestroyCompat(_root);
            _root = null;
            if (_material != null)
            {
                DestroyCompat(_material);
                _material = null;
            }
        }

        // ======================================================================
        // IThreatOverlayService
        // ======================================================================

        public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles)
        {
            if (sourceGuid == Guid.Empty || tiles == null) return;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null)
            {
                Debug.LogWarning("[ThreatTelegraphOverlay] IGridManager no registrado — sin overlay.");
                return;
            }

            Clear(sourceGuid);

            var quads = new List<GameObject>();
            float scale = Mathf.Max(grid.TileSize, 0.01f) * QuadScale;
            foreach (var coord in tiles)
            {
                var quad = NextFreeQuad();
                quad.transform.position = grid.GridToWorld(coord) + Vector3.up * YOffset;
                quad.transform.localScale = new Vector3(scale, scale, 1f);
                quad.SetActive(true);
                quads.Add(quad);
            }

            if (quads.Count > 0) _activeBySource[sourceGuid] = quads;
        }

        public void Clear(Guid sourceGuid)
        {
            if (!_activeBySource.TryGetValue(sourceGuid, out var quads)) return;

            foreach (var quad in quads)
            {
                if (quad == null) continue;
                quad.SetActive(false);
                _free.Push(quad);
            }
            _activeBySource.Remove(sourceGuid);
        }

        public void ClearAll()
        {
            var sources = new List<Guid>(_activeBySource.Keys);
            foreach (var source in sources)
                Clear(source);
        }

        // ======================================================================
        // Pool / visuales
        // ======================================================================

        private GameObject Root
        {
            get
            {
                // == null también cubre el fake-null de Unity tras un cambio de
                // escena: el root murió con la escena y hay que rearmar el pool.
                if (_root == null)
                {
                    _activeBySource.Clear();
                    _free.Clear();

                    _root = new GameObject("ThreatTelegraphOverlay");
                    var pulse = _root.AddComponent<ThreatOverlayPulse>();
                    pulse.Target = Material;
                }
                return _root;
            }
        }

        private Material Material
        {
            get
            {
                if (_material == null)
                {
                    // Sprites/Default: transparente y tinteable sin keywords de
                    // pipeline. El día que arte quiera un sprite/material propio,
                    // se reemplaza acá o se expone override por bootstrap.
                    _material = new Material(Shader.Find("Sprites/Default"))
                    {
                        name = "ThreatTelegraphOverlay (runtime)",
                        color = new Color(1f, 0.45f, 0.1f, 0.55f),
                    };
                }
                return _material;
            }
        }

        private GameObject NextFreeQuad()
        {
            while (_free.Count > 0)
            {
                var pooled = _free.Pop();
                if (pooled != null) return pooled;
            }
            return CreateQuad();
        }

        private GameObject CreateQuad()
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "ThreatTile";

            // Sin collider: no debe interceptar los raycasts del TileClickHandler.
            var collider = quad.GetComponent<Collider>();
            if (collider != null) DestroyCompat(collider);

            quad.transform.SetParent(Root.transform, worldPositionStays: false);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.GetComponent<MeshRenderer>().sharedMaterial = Material;
            quad.SetActive(false);
            return quad;
        }

        private static void DestroyCompat(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }

        private void OnScopeEndedExternal(params object[] args) => ClearAll();
    }

    /// <summary>
    /// Pulso de alpha del material compartido del overlay — todos los quads
    /// laten juntos. Vive en el root del overlay; sin target es no-op.
    /// </summary>
    public sealed class ThreatOverlayPulse : MonoBehaviour
    {
        public Material Target;
        public float Speed = 2.5f;
        [Range(0f, 1f)] public float MinAlpha = 0.35f;
        [Range(0f, 1f)] public float MaxAlpha = 0.65f;

        private void Update()
        {
            if (Target == null) return;
            var color = Target.color;
            color.a = Mathf.Lerp(MinAlpha, MaxAlpha, (Mathf.Sin(Time.time * Speed) + 1f) * 0.5f);
            Target.color = color;
        }
    }
}
