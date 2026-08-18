using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Grid;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Combat.Threat
{
    /// <summary>
    /// Look de un <see cref="ThreatOverlayState"/>. Igualar <see cref="MinAlpha"/> con
    /// <see cref="MaxAlpha"/>, o <see cref="PulseSpeed"/> en 0, apaga el latido.
    /// </summary>
    [Serializable]
    public sealed class ThreatOverlayStateStyle
    {
        [Tooltip("Estado al que aplica este estilo.")]
        public ThreatOverlayState State = ThreatOverlayState.Marked;

        [Tooltip("Color del overlay cuando el Show no trae tint propio. El alpha lo pisa el pulso: " +
                 "acá lo que manda es el matiz.")]
        public Color Tint = Color.white;

        [Tooltip("Textura de patrón del estado (rayado / sólido / punteado / damero). Se aplica por " +
                 "MaterialPropertyBlock, mismo criterio que los estilos con textura de " +
                 "TileHighlightService. Null = quad plano, sin patrón.")]
        public Texture2D Pattern;

        [Tooltip("Alpha mínimo del latido.")]
        [Range(0f, 1f)] public float MinAlpha = 0.35f;

        [Tooltip("Alpha máximo del latido. Igualarlo al mínimo deja el estado quieto.")]
        [Range(0f, 1f)] public float MaxAlpha = 0.65f;

        [Tooltip("Velocidad del latido. 0 = sin latido, se pinta fijo en el alpha máximo.")]
        [Min(0f)] public float PulseSpeed = 2.5f;

        public float AlphaAt(float time)
        {
            if (PulseSpeed <= 0f || Mathf.Approximately(MinAlpha, MaxAlpha)) return MaxAlpha;
            return Mathf.Lerp(MinAlpha, MaxAlpha, (Mathf.Sin(time * PulseSpeed) + 1f) * 0.5f);
        }

        /// <remarks>Copia en vez de reemplazar la instancia: los quads ya pintados guardan una
        /// referencia a su estilo, y sustituir el objeto dejaría a las amenazas vivas con el look
        /// viejo hasta el próximo Show.</remarks>
        public void CopyFrom(ThreatOverlayStateStyle other)
        {
            if (other == null) return;
            Tint = other.Tint;
            Pattern = other.Pattern;
            MinAlpha = other.MinAlpha;
            MaxAlpha = other.MaxAlpha;
            PulseSpeed = other.PulseSpeed;
        }
    }

    /// <summary>
    /// El color y el patrón viven acá y se aplican por <see cref="MaterialPropertyBlock"/>, así que
    /// dos amenazas simultáneas pueden verse distintas compartiendo el mismo material.
    /// </summary>
    public sealed class ThreatOverlayQuad
    {
        // Sprites/Default lee _Color y _MainTex; _BaseMap se setea además para que un reemplazo por
        // material URP siga recibiendo el patrón (setear una propiedad no declarada es no-op).
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        public GameObject Go;
        public Renderer Renderer;

        /// <summary>El tint explícito del Show o, si no vino, el del estado.</summary>
        public Color Tint;

        public ThreatOverlayStateStyle Style;

        public ThreatOverlayState State => Style?.State ?? ThreatOverlayState.Marked;

        /// <remarks>El bloque se limpia entero antes de escribir: los quads son pooled, y sin el
        /// Clear la textura del estado anterior queda pegada al reciclar el quad para un estado sin
        /// patrón.</remarks>
        public void Paint(MaterialPropertyBlock block, float alpha)
        {
            if (Renderer == null || block == null) return;

            var color = Tint;
            color.a = alpha;

            block.Clear();
            block.SetColor(ColorId, color);

            var pattern = Style != null ? Style.Pattern : null;
            if (pattern != null)
            {
                block.SetTexture(MainTexId, pattern);
                block.SetTexture(BaseMapId, pattern);
            }

            Renderer.SetPropertyBlock(block);
        }
    }

    /// <summary>
    /// Implementación pooled de <see cref="IThreatOverlayService"/>: un quad semitransparente por
    /// casilla amenazada, con pulso de alpha (<see cref="ThreatOverlayPulse"/>). Global; los
    /// visuales se apagan en <c>OnCombatEnd</c> / <c>OnRunEnd</c>.
    /// </summary>
    public sealed class ThreatTelegraphOverlay : IThreatOverlayService, IDisposable
    {
        // Levantado apenas del piso para no pelear z con el tinte del tile.
        public float YOffset = 0.06f;

        // < 1 para que se lea la grilla (mismo criterio que el ghost del editor).
        public float QuadScale = 0.92f;

        private readonly Dictionary<Guid, List<ThreatOverlayQuad>> _activeBySource =
            new Dictionary<Guid, List<ThreatOverlayQuad>>();
        private readonly Stack<ThreatOverlayQuad> _free = new Stack<ThreatOverlayQuad>();

        private readonly Dictionary<ThreatOverlayState, ThreatOverlayStateStyle> _styles = DefaultStyles();

        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

        // Un único material para todos los quads: uno por tint ataría el latido de dos hazards del
        // mismo matiz.
        private Material _material;

        private GameObject _root;

        private EventManager.EventReceiver _onCombatEndHandler;
        private EventManager.EventReceiver _onRunEndHandler;

        public int ActiveQuadCount
        {
            get
            {
                int count = 0;
                foreach (var list in _activeBySource.Values)
                    foreach (var quad in list)
                        if (quad?.Go != null && quad.Go.activeSelf) count++;
                return count;
            }
        }

        /// <summary>Los quads activos de <paramref name="sourceGuid"/>, vacío si no tiene overlay.</summary>
        public IReadOnlyList<ThreatOverlayQuad> ActiveQuadsOf(Guid sourceGuid) =>
            _activeBySource.TryGetValue(sourceGuid, out var quads)
                ? (IReadOnlyList<ThreatOverlayQuad>)quads
                : Array.Empty<ThreatOverlayQuad>();

        /// <summary>Estilo vigente de <paramref name="state"/>. Nunca null: un estado desconocido
        /// (índice viejo en un asset) cae en <see cref="ThreatOverlayState.Marked"/>.</summary>
        public ThreatOverlayStateStyle StyleOf(ThreatOverlayState state) =>
            _styles.TryGetValue(state, out var style) ? style : _styles[ThreatOverlayState.Marked];

        /// <summary>Pisa el estilo de <c>style.State</c> con los valores de autoría.</summary>
        public void ApplyStyle(ThreatOverlayStateStyle style)
        {
            if (style == null) return;

            StyleOf(style.State).CopyFrom(style);

            // Repinta ya: fuera de play mode no hay pulso que alcance a una amenaza viva, y quedaría
            // con el look viejo hasta el próximo Show.
            RepaintActive();
        }

        /// <inheritdoc cref="ApplyStyle"/>
        public void ApplyStyles(IEnumerable<ThreatOverlayStateStyle> styles)
        {
            if (styles == null) return;
            foreach (var style in styles) ApplyStyle(style);
        }

        private void RepaintActive()
        {
            float time = Time.time;
            foreach (var quads in _activeBySource.Values)
                for (int i = 0; i < quads.Count; i++)
                {
                    var quad = quads[i];
                    if (quad?.Style == null) continue;

                    quad.Paint(_block, quad.Style.AlphaAt(time));
                }
        }

        /// <summary>Lazy para no depender de wiring en <c>ServiceBootstrap.ExtraServices</c>.</summary>
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

            DestroyCompat(_material);
            _material = null;
        }

        // ======================================================================
        // IThreatOverlayService
        // ======================================================================

        public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles) =>
            Show(sourceGuid, tiles, ThreatOverlayState.Marked, null);

        public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles, Color tint) =>
            Show(sourceGuid, tiles, ThreatOverlayState.Marked, tint);

        public void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles, ThreatOverlayState state,
            Color? tint = null)
        {
            if (sourceGuid == Guid.Empty || tiles == null) return;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null)
            {
                Debug.LogWarning("[ThreatTelegraphOverlay] IGridManager no registrado — sin overlay.");
                return;
            }

            Clear(sourceGuid);

            var style = StyleOf(state);
            var resolvedTint = tint ?? style.Tint;
            float alpha = style.AlphaAt(Time.time);

            var quads = new List<ThreatOverlayQuad>();
            float scale = Mathf.Max(grid.TileSize, 0.01f) * QuadScale;
            foreach (var coord in tiles)
            {
                var quad = NextFreeQuad();
                quad.Go.transform.position = grid.GridToWorld(coord) + Vector3.up * YOffset;
                quad.Go.transform.localScale = new Vector3(scale, scale, 1f);

                quad.Style = style;
                quad.Tint = resolvedTint;

                // Acá y no al crear el quad: el material puede haber muerto con un domain reload.
                if (quad.Renderer != null) quad.Renderer.sharedMaterial = SharedMaterial;

                // Sin esperar al primer Update del pulso: un Show fuera de play mode, o el frame en
                // que se marca, mostraría el look del quad anterior.
                quad.Paint(_block, alpha);

                quad.Go.SetActive(true);
                quads.Add(quad);
            }

            if (quads.Count > 0) _activeBySource[sourceGuid] = quads;
        }

        public void Clear(Guid sourceGuid)
        {
            if (!_activeBySource.TryGetValue(sourceGuid, out var quads)) return;

            foreach (var quad in quads)
            {
                if (quad?.Go == null) continue;
                quad.Go.SetActive(false);
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
                // == null cubre también el fake-null de Unity tras un cambio de escena: el root
                // murió con ella y hay que rearmar el pool.
                if (_root == null)
                {
                    _activeBySource.Clear();
                    _free.Clear();

                    _root = new GameObject("ThreatTelegraphOverlay");
                    var pulse = _root.AddComponent<ThreatOverlayPulse>();

                    // La MISMA colección, no una copia: el pulso tiene que ver los quads que
                    // aparezcan después.
                    pulse.Targets = _activeBySource;
                }
                return _root;
            }
        }

        /// <summary>Naranja de advertencia: el default del overload sin color.</summary>
        public static readonly Color DefaultTint = new Color(1f, 0.45f, 0.1f, 0.55f);

        /// <summary>Cian de la zona segura (paleta de las fichas de jefe).</summary>
        public static readonly Color SafeTint = new Color(0.227f, 0.525f, 0.784f, 0.5f);

        /// <summary>
        /// Los tres avisos comparten matiz porque el <i>cuándo</i> lo lee la opacidad, así que las
        /// bandas no se pueden solapar: Incoming (máx 0.30) &lt; Marked (mín 0.35), Marked
        /// (máx 0.65) &lt; Detonating (0.85).
        /// </summary>
        private static Dictionary<ThreatOverlayState, ThreatOverlayStateStyle> DefaultStyles() =>
            new Dictionary<ThreatOverlayState, ThreatOverlayStateStyle>
            {
                {
                    ThreatOverlayState.Marked, new ThreatOverlayStateStyle
                    {
                        State = ThreatOverlayState.Marked,
                        Tint = DefaultTint,
                        MinAlpha = 0.35f, MaxAlpha = 0.65f, PulseSpeed = 2.5f,
                    }
                },
                {
                    ThreatOverlayState.Detonating, new ThreatOverlayStateStyle
                    {
                        State = ThreatOverlayState.Detonating,
                        Tint = DefaultTint,
                        MinAlpha = 0.85f, MaxAlpha = 0.85f, PulseSpeed = 0f,
                    }
                },
                {
                    ThreatOverlayState.Incoming, new ThreatOverlayStateStyle
                    {
                        State = ThreatOverlayState.Incoming,
                        Tint = DefaultTint,
                        MinAlpha = 0.18f, MaxAlpha = 0.30f, PulseSpeed = 1.2f,
                    }
                },
                {
                    ThreatOverlayState.Safe, new ThreatOverlayStateStyle
                    {
                        State = ThreatOverlayState.Safe,
                        Tint = SafeTint,
                        MinAlpha = 0.45f, MaxAlpha = 0.45f, PulseSpeed = 0f,
                    }
                },
            };

        private Material SharedMaterial
        {
            get
            {
                if (_material != null) return _material;

                // Sprites/Default: transparente y tinteable sin keywords de pipeline.
                _material = new Material(Shader.Find("Sprites/Default"))
                {
                    name = "ThreatTelegraphOverlay (runtime)",
                };
                return _material;
            }
        }

        private ThreatOverlayQuad NextFreeQuad()
        {
            while (_free.Count > 0)
            {
                var pooled = _free.Pop();
                if (pooled?.Go != null) return pooled;
            }
            return CreateQuad();
        }

        private ThreatOverlayQuad CreateQuad()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "ThreatTile";

            // Sin collider: no debe interceptar los raycasts del TileClickHandler.
            var collider = go.GetComponent<Collider>();
            if (collider != null) DestroyCompat(collider);

            go.transform.SetParent(Root.transform, worldPositionStays: false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.SetActive(false);

            return new ThreatOverlayQuad
            {
                Go = go,
                Renderer = go.GetComponent<MeshRenderer>(),
            };
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
    /// Cada quad late según la banda de su <see cref="ThreatOverlayState"/>. Sin targets es no-op.
    /// </summary>
    /// <remarks>
    /// <see cref="Targets"/> es la <b>misma</b> colección que mantiene
    /// <see cref="ThreatTelegraphOverlay"/>, no una copia. Escribe por
    /// <see cref="MaterialPropertyBlock"/> y nunca sobre el <see cref="Material"/> compartido.
    /// </remarks>
    public sealed class ThreatOverlayPulse : MonoBehaviour
    {
        public Dictionary<Guid, List<ThreatOverlayQuad>> Targets;

        private MaterialPropertyBlock _block;

        private void Update()
        {
            if (Targets == null || Targets.Count == 0) return;

            // Perezoso y no en el field initializer: los ctors de MonoBehaviour corren también desde
            // el thread de carga de escena, donde tocar recursos nativos es ilegal.
            if (_block == null) _block = new MaterialPropertyBlock();

            float time = Time.time;
            foreach (var quads in Targets.Values)
            {
                for (int i = 0; i < quads.Count; i++)
                {
                    var quad = quads[i];
                    if (quad?.Style == null || quad.Renderer == null) continue;

                    quad.Paint(_block, quad.Style.AlphaAt(time));
                }
            }
        }
    }
}
