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

        [Tooltip("Textura de patrón del estado (rayado / sólido / punteado / damero). Se aplica al " +
                 "material compartido del par (estado, matiz). Null = quad plano, sin patrón.")]
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
    /// El material que comparten todos los quads de un par (estado, matiz). Es la unidad del latido:
    /// el alpha sale de <see cref="ThreatOverlayStateStyle.AlphaAt"/>, que depende solo del estilo,
    /// así que dentro del grupo es idéntico para todos los quads.
    /// </summary>
    public sealed class ThreatOverlayMaterialGroup
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int PulseAlphaId = Shader.PropertyToID("_PulseAlpha");

        /// <summary>El matiz que abrió el grupo. Su alpha no se dibuja: lo pisa el latido.</summary>
        public Color Tint;

        public ThreatOverlayStateStyle Style;

        public Material Material { get; private set; }

        // NaN y no 0: ningún alpha real es NaN, así que el primer Pulse siempre escribe.
        private float _lastAlpha = float.NaN;

        // Se resuelve al bindear y no por frame — ver Pulse.
        private bool _alphaInColor;

        public void Bind(Material material)
        {
            Material = material;

            // La ruta degradada (Sprites/Default, cuando falta el shader del proyecto) no tiene
            // _PulseAlpha, así que ahí el latido tiene que viajar en el alpha de _Color.
            _alphaInColor = material != null && !material.HasProperty(PulseAlphaId);

            _lastAlpha = float.NaN;
        }

        /// <summary>
        /// Reescribe matiz, patrón y alpha. Hace falta además del latido porque el estilo se muta en
        /// caliente (<see cref="ThreatTelegraphOverlay.ApplyStyle"/>) y porque un grupo que estuvo
        /// parkeado puede venir con el patrón viejo.
        /// </summary>
        public void Repaint()
        {
            if (Material == null) return;

            Material.SetColor(ColorId, Tint);

            // El != null convierte el fake-null de una textura ya destruida en null real, que es lo
            // que hace que el material caiga al default "white" del shader.
            var pattern = Style != null && Style.Pattern != null ? Style.Pattern : null;
            Material.SetTexture(MainTexId, pattern);

            _lastAlpha = float.NaN;
            Pulse(Time.time);
        }

        /// <summary>Escribe el alpha del latido si se movió.</summary>
        /// <returns><c>true</c> si tocó el material.</returns>
        public bool Pulse(float time)
        {
            if (Material == null || Style == null) return false;

            float alpha = Style.AlphaAt(time);

            // Comparación exacta a propósito: AlphaAt es determinista, así que un estilo sin latido
            // (PulseSpeed 0) devuelve el mismo float siempre y el grupo deja de escribir del todo.
            if (alpha == _lastAlpha) return false;
            _lastAlpha = alpha;

            if (_alphaInColor)
            {
                var color = Tint;
                color.a = alpha;
                Material.SetColor(ColorId, color);
            }
            else
            {
                Material.SetFloat(PulseAlphaId, alpha);
            }
            return true;
        }
    }

    /// <summary>
    /// Un quad del telegraph. El matiz y el patrón viven en el material de su <see cref="Group"/>,
    /// compartido por todos los quads del mismo par (estado, matiz), así que dos amenazas
    /// simultáneas pueden verse distintas.
    /// </summary>
    public sealed class ThreatOverlayQuad
    {
        public GameObject Go;
        public Renderer Renderer;

        /// <summary>El tint explícito del Show o, si no vino, el del estado.</summary>
        public Color Tint;

        public ThreatOverlayStateStyle Style;

        /// <summary>El material compartido que le tocó por su par (estado, matiz).</summary>
        public ThreatOverlayMaterialGroup Group;

        public ThreatOverlayState State => Style?.State ?? ThreatOverlayState.Marked;
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

        // < 1 para que se lea la grilla.
        public float QuadScale = 0.92f;

        private readonly Dictionary<Guid, List<ThreatOverlayQuad>> _activeBySource =
            new Dictionary<Guid, List<ThreatOverlayQuad>>();
        private readonly Stack<ThreatOverlayQuad> _free = new Stack<ThreatOverlayQuad>();

        private readonly Dictionary<ThreatOverlayState, ThreatOverlayStateStyle> _styles = DefaultStyles();

        /// <summary>Un material por par (estado, matiz): ni uno por quad ni uno solo para todo.</summary>
        private readonly Dictionary<(ThreatOverlayState State, int Rgb), ThreatOverlayMaterialGroup> _groups =
            new Dictionary<(ThreatOverlayState State, int Rgb), ThreatOverlayMaterialGroup>();

        /// <summary>Los grupos con al menos un quad activo: es lo único que recorre el pulso.</summary>
        private readonly List<ThreatOverlayMaterialGroup> _liveGroups =
            new List<ThreatOverlayMaterialGroup>();

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
            for (int i = 0; i < _liveGroups.Count; i++)
                _liveGroups[i].Repaint();
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

            // Todos los materiales del cache, no uno: cada par (estado, matiz) tiene el suyo, y
            // dejarlos vivos leakea un material por par y por run.
            foreach (var group in _groups.Values)
            {
                DestroyCompat(group.Material);
                group.Bind(null);
            }
            _groups.Clear();
            _liveGroups.Clear();
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

            // Repinta el grupo acá y no en el primer Update del pulso: un Show fuera de play mode, o
            // el frame en que se marca, mostraría el look del grupo anterior.
            var group = GroupFor(style, resolvedTint);
            group.Repaint();

            var quads = new List<ThreatOverlayQuad>();
            float scale = Mathf.Max(grid.TileSize, 0.01f) * QuadScale;
            foreach (var coord in tiles)
            {
                var quad = NextFreeQuad();
                quad.Go.transform.position = grid.GridToWorld(coord) + Vector3.up * YOffset;
                quad.Go.transform.localScale = new Vector3(scale, scale, 1f);

                quad.Style = style;
                quad.Tint = resolvedTint;
                quad.Group = group;

                // El quad sale del pool con el material de su grupo anterior.
                if (quad.Renderer != null) quad.Renderer.sharedMaterial = group.Material;

                quad.Go.SetActive(true);
                quads.Add(quad);
            }

            if (quads.Count > 0) _activeBySource[sourceGuid] = quads;
            RebuildLiveGroups();
        }

        public void Clear(Guid sourceGuid)
        {
            if (!ParkQuadsOf(sourceGuid)) return;
            RebuildLiveGroups();
        }

        public void ClearAll()
        {
            var sources = new List<Guid>(_activeBySource.Keys);
            foreach (var source in sources)
                ParkQuadsOf(source);

            RebuildLiveGroups();
        }

        /// <returns><c>false</c> si la fuente no tenía overlay.</returns>
        private bool ParkQuadsOf(Guid sourceGuid)
        {
            if (!_activeBySource.TryGetValue(sourceGuid, out var quads)) return false;

            foreach (var quad in quads)
            {
                if (quad?.Go == null) continue;
                quad.Go.SetActive(false);
                _free.Push(quad);
            }
            _activeBySource.Remove(sourceGuid);
            return true;
        }

        // ======================================================================
        // Materiales
        // ======================================================================

        /// <summary>
        /// Ruta en <c>Resources</c> del shader del overlay. Se carga por <c>Resources.Load</c> y no
        /// por <c>Shader.Find</c> porque el shader no está en Always Included Shaders: en un build no
        /// hay material serializado que lo referencie, se strippea, y el Find devolvería null.
        /// </summary>
        private const string QuadShaderResourcePath = "ThreatOverlayQuad";

        // Cacheado por proceso: el Load es una búsqueda de asset, no algo para hacer por material.
        private static Shader _quadShader;
        private static bool _quadShaderMissingLogged;

        private static Shader QuadShader
        {
            get
            {
                if (_quadShader != null) return _quadShader;

                _quadShader = Resources.Load<Shader>(QuadShaderResourcePath);
                if (_quadShader != null) return _quadShader;

                if (!_quadShaderMissingLogged)
                {
                    _quadShaderMissingLogged = true;
                    Debug.LogError(
                        "[ThreatTelegraphOverlay] No se encontró el shader en " +
                        $"Resources/{QuadShaderResourcePath} — el overlay cae a Sprites/Default: " +
                        "se sigue viendo igual pero deja de batchear (~112 SetPass calls por frame). " +
                        "Revisar que Assets/Shaders/Resources/ThreatOverlayQuad.shader importe.");
                }

                // No se cachea en _quadShader para que un import tardío todavía pueda recuperar la
                // ruta buena.
                return Shader.Find("Sprites/Default");
            }
        }

        /// <summary>
        /// Clave de matiz del cache: RGB a 8 bits, sin alpha. Sin alpha porque el latido lo pisa; a 8
        /// bits porque una clave de floats exactos hace que un tint interpolado abra un material
        /// nuevo por frame en un cache que vive lo que dura la run.
        /// </summary>
        private static int RgbKey(Color tint)
        {
            var quantized = (Color32)tint;
            return (quantized.r << 16) | (quantized.g << 8) | quantized.b;
        }

        private ThreatOverlayMaterialGroup GroupFor(ThreatOverlayStateStyle style, Color tint)
        {
            var key = (style.State, RgbKey(tint));
            if (!_groups.TryGetValue(key, out var group))
            {
                group = new ThreatOverlayMaterialGroup { Tint = tint };
                _groups[key] = group;
            }

            group.Style = style;

            // El material se rehace acá y no al crear el grupo: un domain reload lo deja en fake-null
            // sin tocar la entrada del cache, así que se recupera perezosamente en el próximo Show.
            if (group.Material == null) group.Bind(NewOverlayMaterial(style.State, group.Tint));

            return group;
        }

        private static Material NewOverlayMaterial(ThreatOverlayState state, Color tint) =>
            new Material(QuadShader)
            {
                name = $"ThreatOverlay {state} #{ColorUtility.ToHtmlStringRGB(tint)} (runtime)",
            };

        private void RebuildLiveGroups()
        {
            _liveGroups.Clear();
            foreach (var quads in _activeBySource.Values)
                for (int i = 0; i < quads.Count; i++)
                {
                    var group = quads[i]?.Group;
                    if (group == null || _liveGroups.Contains(group)) continue;
                    _liveGroups.Add(group);
                }
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
                    _liveGroups.Clear();

                    _root = new GameObject("ThreatTelegraphOverlay");
                    var pulse = _root.AddComponent<ThreatOverlayPulse>();

                    // La MISMA colección, no una copia: el pulso tiene que ver los grupos que
                    // aparezcan después.
                    pulse.Groups = _liveGroups;
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

    /// <summary>Late el material de cada par (estado, matiz) vivo. Sin grupos es no-op.</summary>
    /// <remarks>
    /// <see cref="Groups"/> es la <b>misma</b> colección que mantiene
    /// <see cref="ThreatTelegraphOverlay"/>, no una copia.
    /// </remarks>
    public sealed class ThreatOverlayPulse : MonoBehaviour
    {
        public List<ThreatOverlayMaterialGroup> Groups;

        private void Update()
        {
            if (Groups == null || Groups.Count == 0) return;

            float time = Time.time;
            for (int i = 0; i < Groups.Count; i++)
                Groups[i].Pulse(time);
        }
    }
}
