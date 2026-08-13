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

        // Keyed by Color32, not Color: Color is four floats, so a tint that round-trips through the
        // Inspector can miss a Color-keyed lookup by a bit of float drift and leak a duplicate
        // material per Show. Quantizing to bytes makes "visually the same colour" hash the same.
        private readonly Dictionary<Color32, Material> _materialsByTint = new Dictionary<Color32, Material>();

        // Same Material objects as the dict, in a list the pulse component holds by reference so
        // materials created after the root exists still get pulsed.
        private readonly List<Material> _pulseTargets = new List<Material>();

        private GameObject _root;

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

            // One material per tint now, so teardown has to drain the whole cache — a missed entry
            // is a leaked Material that Unity reports as a leak in EditMode tests.
            foreach (var material in _materialsByTint.Values)
                DestroyCompat(material);
            _materialsByTint.Clear();
            _pulseTargets.Clear();
        }

        // ======================================================================
        // IThreatOverlayService
        // ======================================================================

        public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles) => Show(sourceGuid, tiles, DefaultTint);

        public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles, Color tint)
        {
            if (sourceGuid == Guid.Empty || tiles == null) return;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null)
            {
                Debug.LogWarning("[ThreatTelegraphOverlay] IGridManager no registrado — sin overlay.");
                return;
            }

            Clear(sourceGuid);

            var material = MaterialFor(tint);
            var quads = new List<GameObject>();
            float scale = Mathf.Max(grid.TileSize, 0.01f) * QuadScale;
            foreach (var coord in tiles)
            {
                var quad = NextFreeQuad();
                quad.transform.position = grid.GridToWorld(coord) + Vector3.up * YOffset;
                quad.transform.localScale = new Vector3(scale, scale, 1f);

                // Assigned here rather than at creation: quads are pooled across sources, so a
                // recycled one still carries the previous hazard's tint.
                var renderer = quad.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.sharedMaterial = material;

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
                    pulse.Targets = _pulseTargets;
                }
                return _root;
            }
        }

        /// <summary>El naranja de advertencia histórico — el look de todo telegraph antes de que los
        /// hazards pudieran tintarse por separado, y el default del overload sin color.</summary>
        public static readonly Color DefaultTint = new Color(1f, 0.45f, 0.1f, 0.55f);

        private Material MaterialFor(Color tint)
        {
            var key = (Color32)tint;
            if (_materialsByTint.TryGetValue(key, out var cached))
            {
                if (cached != null) return cached;

                // Fake-null: the material was destroyed under us (Dispose on another instance, or a
                // domain reload). Drop the corpse so the pulse list doesn't accumulate dead entries.
                _pulseTargets.Remove(cached);
            }

            // Sprites/Default: transparente y tinteable sin keywords de
            // pipeline. El día que arte quiera un sprite/material propio,
            // se reemplaza acá o se expone override por bootstrap.
            var material = new Material(Shader.Find("Sprites/Default"))
            {
                name = $"ThreatTelegraphOverlay {key} (runtime)",
                color = tint,
            };

            _materialsByTint[key] = material;
            _pulseTargets.Add(material);
            return material;
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
            // No material here — Show assigns the caller's tint, including on pooled reuse.
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
    /// Pulso de alpha de los materiales del overlay — todos los quads laten
    /// juntos, sea cual sea su tint. Vive en el root del overlay; sin targets
    /// es no-op.
    /// </summary>
    /// <remarks>
    /// <see cref="Targets"/> es la <b>misma</b> lista que mantiene
    /// <see cref="ThreatTelegraphOverlay"/>, no una copia: los materiales se
    /// crean por demanda (uno por tint) y el pulso tiene que agarrar también
    /// los que aparezcan después de que este componente exista.
    /// </remarks>
    public sealed class ThreatOverlayPulse : MonoBehaviour
    {
        public List<Material> Targets;
        public float Speed = 2.5f;
        [Range(0f, 1f)] public float MinAlpha = 0.35f;
        [Range(0f, 1f)] public float MaxAlpha = 0.65f;

        private void Update()
        {
            if (Targets == null || Targets.Count == 0) return;

            // Banda de alpha absoluta (no proporcional al alpha del tint): así el naranja histórico
            // late exactamente igual que antes de que existieran los tints por hazard.
            float alpha = Mathf.Lerp(MinAlpha, MaxAlpha, (Mathf.Sin(Time.time * Speed) + 1f) * 0.5f);
            for (int i = 0; i < Targets.Count; i++)
            {
                var target = Targets[i];
                if (target == null) continue; // Destruido por un Dispose en curso.

                var color = target.color;
                color.a = alpha;
                target.color = color;
            }
        }
    }
}
