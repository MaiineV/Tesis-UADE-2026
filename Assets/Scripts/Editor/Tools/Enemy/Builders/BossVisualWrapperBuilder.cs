using System.Collections.Generic;
using System.IO;
using Rollgeon.Entities.Visuals;
using Rollgeon.Feedback;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.Editor.Tools.Enemy.Builders
{
    /// <summary>
    /// Fundación compartida para "vestir" un jefe: toma un prefab/FBX de <b>arte</b> y escupe el
    /// prefab de <b>gameplay</b> que lo anida y le cuelga los componentes que el combate espera
    /// (<see cref="EntityPawn"/>, <see cref="PawnRegistryBinding"/>, <see cref="HitImpulseConsumer"/>,
    /// <see cref="PawnMaterialFeedback"/>, collider y barra de vida world-space).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué existe.</b> Los prefabs de enemigos son dos capas: el de arte (rig + animator, lo
    /// exporta el artista y se reimporta seguido) y el de gameplay que lo anida. Hacer el de gameplay
    /// a mano por cada jefe es donde se filtran los errores que no explotan hasta el playtest — un
    /// <c>PawnRegistryBinding</c> que falta y el jefe no recibe hit flash, un collider mal dimensionado
    /// y el cursor no lo puede targetear. Esta utility centraliza ese armado.
    /// </para>
    /// <para>
    /// <b>Sin <c>[MenuItem]</c> a propósito</b>: la consumen los builders de cada jefe, que son los que
    /// saben qué arte, qué props y qué tinte le toca a cada uno.
    /// </para>
    /// <para>
    /// <b>Idempotente</b>: si <see cref="BossWrapperSpec.OutputPrefabPath"/> ya existe, se reescribe
    /// sobre el mismo path — <see cref="PrefabUtility.SaveAsPrefabAsset(GameObject, string)"/> preserva
    /// el GUID del asset, así que las referencias de los <c>EnemyDataSO</c> a este prefab sobreviven al
    /// rebuild. Lo mismo vale para los materiales clonados.
    /// </para>
    /// </remarks>
    public static class BossVisualWrapperBuilder
    {
        // ======================================================================
        // Constantes de estructura — replican GeneralDirector.prefab
        // ======================================================================

        /// <summary>Sprite atlas de la barra: 3 sub-sprites (marco, fondo, relleno).</summary>
        public const string HealthBarAtlasPath = "Assets/Art/UI/EnemiesHealthBar/EnemiesHealthbarv2.png";

        /// <summary>Marco exterior — sub-sprite <c>_0</c> (94×18).</summary>
        public const string HealthBarFrameSpriteName = "EnemiesHealthbarv2_0";

        /// <summary>Canaleta vacía — sub-sprite <c>_4</c> (85×3).</summary>
        public const string HealthBarBackgroundSpriteName = "EnemiesHealthbarv2_4";

        /// <summary>Barra que se consume — sub-sprite <c>_5</c> (85×3).</summary>
        public const string HealthBarFillSpriteName = "EnemiesHealthbarv2_5";

        /// <summary>Fuente pixel del proyecto — la misma que usa GeneralDirector.prefab.</summary>
        public const string HealthBarFontPath = "Assets/Fonts/m6x11plus SDF.asset";

        public const string DefaultMaterialsRoot = "Assets/Rollgeon/Enemies/Materials";

        private const string ShaderName = "Rollgeon/PaletteCelLit";

        // Medidas de la barra, en unidades de mundo (el canvas es World Space y escala 1).
        private static readonly Vector2 CanvasSize = new Vector2(3f, 1f);
        private static readonly Vector2 FrameSize = new Vector2(3f, 0.5f);
        private static readonly Vector2 BarSize = new Vector2(2.7982f, 0.2227f);
        private static readonly Vector2 BarOffset = new Vector2(0.0067f, 0.0735f);

        // Fallback de collider cuando el arte no reporta bounds usables (rig sin bake, prefab
        // vacío): mismas medidas que el capsule a mano de GeneralDirector.prefab.
        private const float FallbackColliderRadius = 0.5f;
        private const float FallbackColliderHeight = 2f;

        // ======================================================================
        // API pública
        // ======================================================================

        /// <summary>
        /// Construye (o reconstruye) el prefab wrapper descrito por <paramref name="spec"/> y devuelve
        /// el asset guardado. <c>null</c> si el spec es inválido o el arte no existe.
        /// </summary>
        public static GameObject BuildWrapper(BossWrapperSpec spec)
        {
            if (!ValidateSpec(spec)) return null;

            var artAsset = AssetDatabase.LoadAssetAtPath<GameObject>(spec.ArtPrefabPath);
            if (artAsset == null)
            {
                Debug.LogError($"[BossVisualWrapperBuilder] No hay prefab de arte en " +
                               $"'{spec.ArtPrefabPath}' — no se construye '{spec.OutputPrefabPath}'.");
                return null;
            }

            string bossName = ResolveBossName(spec);

            var root = new GameObject(Path.GetFileNameWithoutExtension(spec.OutputPrefabPath));
            try
            {
                // Identidad explícita: los bounds del arte se leen en world space y se guardan como
                // local del root. Con el root fuera del origen, el collider quedaría corrido.
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one;

                var art = PrefabUtility.InstantiatePrefab(artAsset) as GameObject;
                if (art == null)
                {
                    Debug.LogError($"[BossVisualWrapperBuilder] No se pudo instanciar el arte " +
                                   $"'{spec.ArtPrefabPath}'.");
                    return null;
                }

                art.name = string.IsNullOrEmpty(spec.ArtChildName) ? "Art" : spec.ArtChildName;
                art.transform.SetParent(root.transform, worldPositionStays: false);
                art.transform.localPosition = Vector3.zero;
                art.transform.localRotation = Quaternion.identity;
                art.transform.localScale = Vector3.one;

                var artRenderers = CollectRenderers(art);
                if (artRenderers.Count == 0)
                {
                    Debug.LogWarning($"[BossVisualWrapperBuilder] '{spec.ArtPrefabPath}' no tiene " +
                                     $"Mesh/SkinnedMeshRenderers: el collider usa el fallback y el hit " +
                                     $"flash no va a tener nada que tintar.");
                }

                Retint(artRenderers, spec, bossName);

                var propRenderers = InstantiateProps(root, spec);

                AddCollider(root, artRenderers, spec.Collider);

                var healthBar = spec.AddHealthBar
                    ? BuildHealthBar(root, spec.HealthBarOffset)
                    : null;

                WireGameplayComponents(root, artRenderers, propRenderers, healthBar, spec);

                return SavePrefab(root, spec.OutputPrefabPath);
            }
            finally
            {
                // El root vive en la escena abierta sólo para poder guardarlo como prefab.
                if (root != null) Object.DestroyImmediate(root);
            }
        }

        // ======================================================================
        // Validación
        // ======================================================================

        private static bool ValidateSpec(BossWrapperSpec spec)
        {
            if (spec == null)
            {
                Debug.LogError("[BossVisualWrapperBuilder] spec null.");
                return false;
            }
            if (string.IsNullOrEmpty(spec.ArtPrefabPath))
            {
                Debug.LogError("[BossVisualWrapperBuilder] spec.ArtPrefabPath vacío.");
                return false;
            }
            if (string.IsNullOrEmpty(spec.OutputPrefabPath))
            {
                Debug.LogError("[BossVisualWrapperBuilder] spec.OutputPrefabPath vacío.");
                return false;
            }
            if (!spec.OutputPrefabPath.EndsWith(".prefab"))
            {
                Debug.LogError($"[BossVisualWrapperBuilder] OutputPrefabPath tiene que terminar en " +
                               $"'.prefab' — llegó '{spec.OutputPrefabPath}'.");
                return false;
            }
            return true;
        }

        private static string ResolveBossName(BossWrapperSpec spec)
        {
            if (!string.IsNullOrEmpty(spec.BossName)) return spec.BossName;

            // "PF_Boss_Croupier.prefab" → "Croupier": el nombre sirve de carpeta y de prefijo de
            // material, y arrastrar el "PF_Boss_" ahí sólo hace ruido.
            var leaf = Path.GetFileNameWithoutExtension(spec.OutputPrefabPath);
            foreach (var prefix in new[] { "PF_Boss_", "PF_Enemy_", "PF_", "Boss_" })
            {
                if (leaf.StartsWith(prefix)) return leaf.Substring(prefix.Length);
            }
            return leaf;
        }

        // ======================================================================
        // Renderers
        // ======================================================================

        /// <summary>
        /// Mesh y SkinnedMesh renderers del arte. Filtra a propósito los de partículas/trails: no
        /// tienen bounds estables para dimensionar el collider ni materiales que valga la pena
        /// clonar para el retinte.
        /// </summary>
        private static List<Renderer> CollectRenderers(GameObject go)
        {
            var result = new List<Renderer>();
            foreach (var r in go.GetComponentsInChildren<Renderer>(includeInactive: true))
            {
                if (r is MeshRenderer || r is SkinnedMeshRenderer) result.Add(r);
            }
            return result;
        }

        // ======================================================================
        // Retinte
        // ======================================================================

        /// <summary>
        /// Clona a assets nuevos los materiales listados en <see cref="BossWrapperSpec.Retints"/> y los
        /// swapea en los renderers del arte.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Nunca toca los materiales originales</b>: <c>Mat_Gold</c> y compañía los comparten todos
        /// los enemigos del juego, así que retintarlos in-place repintaría medio casino.
        /// </para>
        /// <para>
        /// <b>Un clon por material único, no por renderer.</b> El arte reusa el mismo material en varios
        /// renderers y submeshes (en <c>Healer_Animated</c>, <c>Mat_Red</c> aparece en tres). Clonar por
        /// slot dejaría N copias divergentes del mismo material y rompería el batching.
        /// </para>
        /// <para>
        /// Los materiales que no están en el diccionario se dejan <b>compartidos</b>: clonar todo por si
        /// acaso llenaría el proyecto de assets idénticos al original.
        /// </para>
        /// </remarks>
        private static void Retint(List<Renderer> renderers, BossWrapperSpec spec, string bossName)
        {
            if (spec.Retints == null || spec.Retints.Count == 0) return;

            string folder = string.IsNullOrEmpty(spec.MaterialsFolder)
                ? $"{DefaultMaterialsRoot}/{bossName}"
                : spec.MaterialsFolder;

            var clones = new Dictionary<Material, Material>();
            var seenSources = new HashSet<string>();

            foreach (var renderer in renderers)
            {
                var shared = renderer.sharedMaterials;
                if (shared == null || shared.Length == 0) continue;

                bool changed = false;
                for (int i = 0; i < shared.Length; i++)
                {
                    var src = shared[i];
                    if (src == null) continue;

                    seenSources.Add(src.name);
                    if (!spec.Retints.TryGetValue(src.name, out var retint) || retint == null) continue;

                    if (!clones.TryGetValue(src, out var clone))
                    {
                        clone = CloneMaterial(src, folder, bossName);
                        if (clone == null) continue;
                        ApplyRetint(clone, retint);
                        clones[src] = clone;
                    }

                    shared[i] = clone;
                    changed = true;
                }

                if (changed) renderer.sharedMaterials = shared;
            }

            // Una key que no matcheó ningún material del arte es casi siempre un typo, y el síntoma
            // (el jefe sale con el color de fábrica) no grita nada en el editor.
            foreach (var key in spec.Retints.Keys)
            {
                if (!seenSources.Contains(key))
                {
                    Debug.LogWarning($"[BossVisualWrapperBuilder] El retinte pide '{key}' pero " +
                                     $"'{spec.ArtPrefabPath}' no usa ningún material con ese nombre. " +
                                     $"Materiales del arte: {string.Join(", ", seenSources)}.");
                }
            }

            if (clones.Count > 0) AssetDatabase.SaveAssets();
        }

        private static Material CloneMaterial(Material src, string folder, string bossName)
        {
            EnsureFolder(folder);

            // "Mat_Gold" → "Mat_Croupier_Gold"; sin el prefijo, "Gold" → "Mat_Croupier_Gold" igual.
            string core = src.name.StartsWith("Mat_") ? src.name.Substring("Mat_".Length) : src.name;
            string path = $"{folder}/Mat_{bossName}_{core}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                // Rebuild determinista: se resetea al original y después se re-aplica el retinte, así
                // dos corridas con el mismo spec dan el mismo material, y el GUID no cambia.
                existing.shader = src.shader;
                existing.CopyPropertiesFromMaterial(src);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var clone = new Material(src) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(clone, path);
            return clone;
        }

        private static void ApplyRetint(Material material, MaterialRetint retint)
        {
            if (material.shader == null || material.shader.name != ShaderName)
            {
                Debug.LogWarning($"[BossVisualWrapperBuilder] '{material.name}' usa el shader " +
                                 $"'{material.shader?.name}' y no '{ShaderName}': el retinte de paleta " +
                                 $"no le va a hacer nada.");
            }

            bool hasDirect = retint.LightColor.HasValue
                             || retint.MidColor.HasValue
                             || retint.ShadowColor.HasValue;

            if (retint.PaletteSlot.HasValue && hasDirect)
            {
                Debug.LogWarning($"[BossVisualWrapperBuilder] '{material.name}' pide PaletteSlot y " +
                                 $"colores directos a la vez — ganan los colores directos.");
            }

            if (hasDirect)
            {
                // El shader ramea `_UsePalette > 0.5 ? _PaletteXColors[slot] : _XColor`: sin apagar el
                // toggle, los colores directos quedan escritos pero no se ven.
                material.SetFloat("_UsePalette", 0f);
                if (retint.LightColor.HasValue) material.SetColor("_LightColor", retint.LightColor.Value);
                if (retint.MidColor.HasValue) material.SetColor("_MidColor", retint.MidColor.Value);
                if (retint.ShadowColor.HasValue) material.SetColor("_ShadowColor", retint.ShadowColor.Value);
            }
            else if (retint.PaletteSlot.HasValue)
            {
                int slot = Mathf.Clamp(retint.PaletteSlot.Value, 0, PaletteSlots.MaxSlots - 1);
                if (slot != retint.PaletteSlot.Value)
                {
                    Debug.LogWarning($"[BossVisualWrapperBuilder] PaletteSlot " +
                                     $"{retint.PaletteSlot.Value} fuera de rango en '{material.name}' — " +
                                     $"clampeado a {slot}.");
                }
                material.SetFloat("_UsePalette", 1f);
                material.SetFloat("_PaletteSlot", slot);
            }

            EditorUtility.SetDirty(material);
        }

        // ======================================================================
        // Props
        // ======================================================================

        private static List<Renderer> InstantiateProps(GameObject root, BossWrapperSpec spec)
        {
            var renderers = new List<Renderer>();
            if (spec.Props == null || spec.Props.Count == 0) return renderers;

            foreach (var prop in spec.Props)
            {
                if (prop == null || string.IsNullOrEmpty(prop.PrefabPath)) continue;

                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prop.PrefabPath);
                if (asset == null)
                {
                    // Un prop que falta no invalida al jefe: sale sin la ruleta pero jugable.
                    Debug.LogWarning($"[BossVisualWrapperBuilder] Prop no encontrado en " +
                                     $"'{prop.PrefabPath}' — se saltea.");
                    continue;
                }

                var instance = PrefabUtility.InstantiatePrefab(asset) as GameObject;
                if (instance == null) continue;

                if (!string.IsNullOrEmpty(prop.Name)) instance.name = prop.Name;
                instance.transform.SetParent(root.transform, worldPositionStays: false);
                instance.transform.localPosition = prop.LocalPosition;
                instance.transform.localEulerAngles = prop.LocalEuler;
                instance.transform.localScale = prop.LocalScale;

                renderers.AddRange(CollectRenderers(instance));
            }

            return renderers;
        }

        // ======================================================================
        // Collider
        // ======================================================================

        /// <summary>
        /// Collider en el root dimensionado a los bounds del arte. Va en el root porque
        /// <c>PawnPicker</c> resuelve el pick con <c>GetComponentInParent</c> desde el collider: el
        /// cursor sólo puede targetear al jefe si hay un collider que cuelgue del mismo objeto que
        /// tiene el <see cref="EntityPawn"/>.
        /// </summary>
        private static void AddCollider(GameObject root, List<Renderer> renderers, ColliderKind kind)
        {
            if (kind == ColliderKind.None) return;

            bool hasBounds = TryGetBounds(renderers, out var bounds);

            if (kind == ColliderKind.Box)
            {
                var box = root.AddComponent<BoxCollider>();
                box.center = hasBounds
                    ? bounds.center
                    : new Vector3(0f, FallbackColliderHeight * 0.5f, 0f);
                box.size = hasBounds
                    ? bounds.size
                    : new Vector3(FallbackColliderRadius * 2f, FallbackColliderHeight, FallbackColliderRadius * 2f);
                return;
            }

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.direction = 1; // Y — los pawns están de pie.
            if (hasBounds)
            {
                capsule.center = bounds.center;
                capsule.height = bounds.size.y;
                capsule.radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            }
            else
            {
                capsule.center = new Vector3(0f, FallbackColliderHeight * 0.5f, 0f);
                capsule.height = FallbackColliderHeight;
                capsule.radius = FallbackColliderRadius;
            }
        }

        private static bool TryGetBounds(List<Renderer> renderers, out Bounds bounds)
        {
            bounds = default;
            bool any = false;

            foreach (var r in renderers)
            {
                if (r == null) continue;
                var b = r.bounds;
                if (any) bounds.Encapsulate(b);
                else { bounds = b; any = true; }
            }

            // Un rig recién instanciado puede reportar bounds degenerados; con size 0 el collider
            // quedaría inpickeable y el jefe no se podría targetear.
            if (!any || bounds.size.y <= Mathf.Epsilon) return false;
            return true;
        }

        // ======================================================================
        // Barra de vida
        // ======================================================================

        /// <summary>
        /// Replica la barra world-space de GeneralDirector.prefab: canvas en World Space con
        /// fondo + relleno (Image Filled horizontal) + marco + texto, y el
        /// <see cref="WorldSpaceHealthBar"/> cableado a sus piezas.
        /// </summary>
        /// <remarks>
        /// <b>Dos desvíos deliberados</b> respecto del prefab de referencia, ambos por el cursor:
        /// no se agrega <c>GraphicRaycaster</c> y las Images van con <c>raycastTarget = false</c>.
        /// En el original, la barra flota sobre la cabeza del jefe y es un raycast target válido, así
        /// que se come el hover que <c>CursorService</c> necesita para el targeting del pawn.
        /// </remarks>
        private static WorldSpaceHealthBar BuildHealthBar(GameObject root, Vector3 offset)
        {
            var canvasGo = new GameObject("Canvas");
            var canvasRect = canvasGo.AddComponent<RectTransform>();
            canvasRect.SetParent(root.transform, worldPositionStays: false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.referencePixelsPerUnit = 100f;
            scaler.dynamicPixelsPerUnit = 1f;

            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.zero;
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
            canvasRect.sizeDelta = CanvasSize;
            canvasRect.localScale = Vector3.one;
            canvasRect.localRotation = Quaternion.identity;
            // WorldSpaceHealthBar.LateUpdate sobreescribe esto con su _offset; se setea igual para que
            // la barra se vea en su lugar al abrir el prefab.
            canvasRect.localPosition = offset;

            var atlas = LoadHealthBarSprites();

            // Orden de hijos = orden de dibujado: canaleta, relleno, marco encima, texto arriba.
            CreateBarImage(canvasRect, "LifeBackground", atlas.background, filled: false);
            var fill = CreateBarImage(canvasRect, "LifeFill", atlas.fill, filled: true);
            CreateFrameImage(canvasRect, "Frame", atlas.frame);
            var text = CreateHealthText(canvasRect);

            var healthBar = canvasGo.AddComponent<WorldSpaceHealthBar>();
            var so = new SerializedObject(healthBar);
            SetRef(so, "_fillImage", fill);
            SetRef(so, "_hpText", text);
            SetString(so, "_textFormat", "{0}/{1}");
            SetRef(so, "_barRoot", canvasGo);
            SetVector3(so, "_offset", offset);
            so.ApplyModifiedPropertiesWithoutUndo();

            return healthBar;
        }

        private static Image CreateBarImage(RectTransform parent, string name, Sprite sprite, bool filled)
        {
            var go = new GameObject(name);
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, worldPositionStays: false);

            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = BarOffset;
            rect.sizeDelta = BarSize;
            rect.localScale = Vector3.one;

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            if (filled)
            {
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Horizontal;
                image.fillOrigin = (int)Image.OriginHorizontal.Left;
                image.fillAmount = 1f;
            }
            else
            {
                image.type = Image.Type.Simple;
            }

            return image;
        }

        private static Image CreateFrameImage(RectTransform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name);
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, worldPositionStays: false);

            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = FrameSize;
            rect.localScale = Vector3.one;

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateHealthText(RectTransform parent)
        {
            var go = new GameObject("HealthText");
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, worldPositionStays: false);

            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = FrameSize;
            rect.localScale = Vector3.one;

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = "0/0";
            text.fontSize = 0.5f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HealthBarFontPath);
            if (font != null) text.font = font;
            else
            {
                Debug.LogWarning($"[BossVisualWrapperBuilder] No se encontró la fuente " +
                                 $"'{HealthBarFontPath}' — el texto de HP queda con la default de TMP.");
            }

            return text;
        }

        private static (Sprite frame, Sprite background, Sprite fill) LoadHealthBarSprites()
        {
            Sprite frame = null, background = null, fill = null;

            var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(HealthBarAtlasPath);
            if (reps == null || reps.Length == 0)
            {
                Debug.LogWarning($"[BossVisualWrapperBuilder] '{HealthBarAtlasPath}' no tiene " +
                                 $"sub-sprites — la barra sale sin gráficos.");
                return (null, null, null);
            }

            foreach (var rep in reps)
            {
                if (!(rep is Sprite sprite)) continue;
                if (sprite.name == HealthBarFrameSpriteName) frame = sprite;
                else if (sprite.name == HealthBarBackgroundSpriteName) background = sprite;
                else if (sprite.name == HealthBarFillSpriteName) fill = sprite;
            }

            if (frame == null || background == null || fill == null)
            {
                Debug.LogWarning($"[BossVisualWrapperBuilder] Faltan sub-sprites en " +
                                 $"'{HealthBarAtlasPath}' (marco={frame != null}, " +
                                 $"fondo={background != null}, relleno={fill != null}). " +
                                 $"¿Se reimportó el atlas con otro slicing?");
            }

            return (frame, background, fill);
        }

        // ======================================================================
        // Componentes de gameplay
        // ======================================================================

        private static void WireGameplayComponents(
            GameObject root,
            List<Renderer> artRenderers,
            List<Renderer> propRenderers,
            WorldSpaceHealthBar healthBar,
            BossWrapperSpec spec)
        {
            var pawn = root.AddComponent<EntityPawn>();
            if (healthBar != null)
            {
                var pawnSo = new SerializedObject(pawn);
                SetRef(pawnSo, "_healthBar", healthBar);
                pawnSo.ApplyModifiedPropertiesWithoutUndo();
            }

            root.AddComponent<PawnRegistryBinding>();
            root.AddComponent<HitImpulseConsumer>();

            var feedback = root.AddComponent<PawnMaterialFeedback>();

            var targets = new List<Renderer>(artRenderers);
            if (spec.IncludePropRenderersInFeedback) targets.AddRange(propRenderers);

            // Se cablea explícito (y no se deja el auto-populate de PawnMaterialFeedback) para que el
            // prefab quede inspeccionable y el hit flash no dependa del orden de Awake.
            var feedbackSo = new SerializedObject(feedback);
            var prop = feedbackSo.FindProperty("_renderers");
            if (prop != null)
            {
                prop.arraySize = targets.Count;
                for (int i = 0; i < targets.Count; i++)
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = targets[i];
                feedbackSo.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[BossVisualWrapperBuilder] PawnMaterialFeedback no expone " +
                                 "'_renderers' — ¿se renombró el campo? Queda el auto-populate " +
                                 "de runtime.");
            }
        }

        // ======================================================================
        // Persistencia
        // ======================================================================

        private static GameObject SavePrefab(GameObject root, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path));

            // SaveAsPrefabAsset sobre un path existente reescribe el contenido preservando el GUID —
            // por eso no se borra el asset viejo primero: eso sí rompería las referencias.
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            if (!success || saved == null)
            {
                Debug.LogError($"[BossVisualWrapperBuilder] Falló el guardado de '{path}'.");
                return null;
            }

            AssetDatabase.SaveAssets();
            return saved;
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        private static void SetRef(SerializedObject so, string field, Object value)
        {
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[BossVisualWrapperBuilder] '{so.targetObject.GetType().Name}' no " +
                                 $"expone el campo serializado '{field}' — quedó sin cablear.");
                return;
            }
            prop.objectReferenceValue = value;
        }

        private static void SetString(SerializedObject so, string field, string value)
        {
            var prop = so.FindProperty(field);
            if (prop != null) prop.stringValue = value;
        }

        private static void SetVector3(SerializedObject so, string field, Vector3 value)
        {
            var prop = so.FindProperty(field);
            if (prop != null) prop.vector3Value = value;
        }

        /// <summary>Crea la cadena de carpetas de <paramref name="folder"/> si falta alguna.</summary>
        public static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return;

            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder)) return;

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            var leaf = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }

    // ==========================================================================
    // Spec
    // ==========================================================================

    /// <summary>Ficha de armado de un wrapper de jefe. Ver <see cref="BossVisualWrapperBuilder"/>.</summary>
    public sealed class BossWrapperSpec
    {
        /// <summary>Prefab o FBX de arte a anidar. Obligatorio.</summary>
        public string ArtPrefabPath;

        /// <summary>Destino del wrapper, ej. <c>Assets/Prefabs/Enemies/Bosses/PF_Boss_Croupier.prefab</c>.</summary>
        public string OutputPrefabPath;

        /// <summary>
        /// Nombre para carpeta y prefijo de materiales. Si queda vacío se deriva del nombre del
        /// output sacándole el prefijo (<c>PF_Boss_Croupier</c> → <c>Croupier</c>).
        /// </summary>
        public string BossName;

        /// <summary>Nombre del hijo que envuelve el arte.</summary>
        public string ArtChildName = "Art";

        public bool AddHealthBar = true;

        /// <summary>Altura de la barra sobre el pivot del jefe.</summary>
        public Vector3 HealthBarOffset = new Vector3(0f, 3f, 0f);

        public ColliderKind Collider = ColliderKind.Capsule;

        /// <summary>
        /// Retinte por material: la key es el <b>nombre del material fuente</b> del arte
        /// (ej. <c>"Mat_Gold"</c>). Los que no estén acá quedan compartidos, sin clonar.
        /// </summary>
        public Dictionary<string, MaterialRetint> Retints;

        /// <summary>Carpeta de los clones. Default: <c>Assets/Rollgeon/Enemies/Materials/&lt;BossName&gt;</c>.</summary>
        public string MaterialsFolder;

        /// <summary>Props parenteados al root del wrapper (la ruleta del Croupier, etc.).</summary>
        public List<BossPropSpec> Props;

        /// <summary>
        /// Si los renderers de los props entran al hit flash. Default false: el prefab de referencia
        /// sólo tintea el cuerpo, y un prop con materiales ajenos al shader de paleta no reacciona igual.
        /// </summary>
        public bool IncludePropRenderersInFeedback;
    }

    /// <summary>Prop anidado al wrapper con transform local explícito.</summary>
    public sealed class BossPropSpec
    {
        public string PrefabPath;
        public Vector3 LocalPosition = Vector3.zero;
        public Vector3 LocalEuler = Vector3.zero;
        public Vector3 LocalScale = Vector3.one;

        /// <summary>Opcional: renombra la instancia (default = nombre del prefab).</summary>
        public string Name;
    }

    /// <summary>
    /// Retinte de un material del arte. <b>Precedencia</b>: si hay colores directos, ganan sobre
    /// <see cref="PaletteSlot"/>.
    /// </summary>
    /// <remarks>
    /// El shader <c>Rollgeon/PaletteCelLit</c> <b>no usa ramp textures</b> (las
    /// <c>Assets/Art/2D/Ramps/CelRamp_*.png</c> son de un shader viejo): el color sale de un slot de
    /// la paleta global o de los tres colores directos del material.
    /// </remarks>
    public sealed class MaterialRetint
    {
        /// <summary>Slot de <c>PA_MainPalette</c>. Ver <see cref="PaletteSlots"/>.</summary>
        public int? PaletteSlot;

        public Color? LightColor;
        public Color? MidColor;
        public Color? ShadowColor;

        public static MaterialRetint FromSlot(int slot) => new MaterialRetint { PaletteSlot = slot };

        public static MaterialRetint FromColors(Color light, Color mid, Color shadow) =>
            new MaterialRetint { LightColor = light, MidColor = mid, ShadowColor = shadow };
    }

    public enum ColliderKind
    {
        Capsule = 0,
        Box = 1,
        None = 2,
    }

    /// <summary>
    /// Índices canónicos de slot de paleta, en el orden de <c>PaletteAsset.Presets</c> — el mismo que
    /// usan los <c>Mat_*.mat</c> del proyecto (<c>Mat_Gold</c> tiene <c>_PaletteSlot: 5</c> = Gold).
    /// </summary>
    /// <remarks>
    /// <b>Ojo</b>: los <c>label</c> guardados hoy en <c>PA_MainPalette.asset</c> están desalineados
    /// respecto de esta tabla (el slot 0 dice "Black, DarkBlue, Navy, Charcoal"), porque el asset se
    /// editó a mano después de generarse. Estas constantes siguen la tabla de presets del código, que
    /// es contra la que están autorados los materiales; el color final que se ve sale del asset. Si un
    /// retinte no da el color esperado, verificar el slot en el inspector del PaletteAsset.
    /// </remarks>
    public static class PaletteSlots
    {
        public const int MaxSlots = 32;

        public const int Black = 0;
        public const int Bone = 1;
        public const int Brown = 2;
        public const int DarkGray = 3;
        public const int DarkYellow = 4;
        public const int Gold = 5;
        public const int Gray = 6;
        public const int Green = 7;
        public const int LightBrown = 8;
        public const int LightGray = 9;
        public const int LightGreen = 10;
        public const int LightYellow = 11;
        public const int Red = 12;
        public const int Red1 = 13;
        public const int White = 14;
        public const int Yellow = 15;
        public const int Teal = 16;
        public const int Cyan = 17;
        public const int Purple = 18;
        public const int Pink = 19;
        public const int Orange = 20;
        public const int DarkRed = 21;
        public const int DarkBlue = 22;
        public const int LightBlue = 23;
        public const int Salmon = 24;
        public const int Olive = 25;
        public const int Lavender = 26;
        public const int Mint = 27;
        public const int Coral = 28;
        public const int Navy = 29;
        public const int Peach = 30;
        public const int Charcoal = 31;
    }
}
