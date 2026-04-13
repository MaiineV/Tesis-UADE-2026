using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the ArtTestV2 scene from scratch, replicating the Godot
/// 3d-pixel-art-base-project scene with isometric camera, cel shading,
/// outlines, and billboarded foliage.
///
/// Run via: Tools > Art Test > Build ArtTestV2 Scene
/// </summary>
public static class ArtTestSceneBuilder
{
    // ─── paths ────────────────────────────────────────────────────────────────
    const string ScenePath   = "Assets/Scenes/ArtTestV2.unity";
    const string ArtFolder   = "Assets/Art/ArtTest";
    const string RampFolder  = "Assets/Resources/Stylized";
    const string ShaderPath  = "Assets/Shaders/GodotParityCelLit.shader";
    const string FolShaderP  = "Assets/Shaders/FoliageBillboard.shader";
    const string FolTexPath  = "Assets/Art/pixelart_foliage_spritesheet.png";

    // ─── Color input helpers ───────────────────────────────────────────────────
    //
    // GradientKey values are in LINEAR float space (0.0–1.0 linear) — the same
    // format Godot stores internally in .tscn PackedColorArray.
    //
    // TWO ways to enter colors:
    //
    //   A) LINEAR(r,g,b)  — paste directly from Godot's .tscn file  e.g. LINEAR(0.3191f, 0.1351f, 0.2816f)
    //   B) SRGB255(r,g,b) — paste from any color picker (0-255 sRGB) e.g. SRGB255( 163,   93,  144)
    //
    // Both produce the same internal linear Color. Use whichever source is handy.

    /// <summary>Linear 0-1 float: values from Godot .tscn PackedColorArray.</summary>
    static Color LINEAR(float r, float g, float b) => new Color(r, g, b, 1f);

    /// <summary>sRGB 0-255 integer: values from any screenshot color picker or Godot's editor.</summary>
    static Color SRGB255(int r, int g, int b) =>
        new Color(SRGBToLinear(r / 255f), SRGBToLinear(g / 255f), SRGBToLinear(b / 255f), 1f);

    static float SRGBToLinear(float c) =>
        c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);

    // ─── Godot gradient data (values from live Godot scene .tscn) ─────────────
    //
    // Position 0.0 = maximum shadow color
    // Position 1.0 = maximum highlight color
    // The number of stops = number of visible cel bands.
    //
    // To match Godot precisely: open the gradient in Godot's inspector, read the
    // RGB values from the color picker (0-255 mode), then use SRGB255() here.
    // OR read the raw float values from the .tscn and use LINEAR().

    // Dark: scales a linear Color toward black (for shadow stop derivation).
    // factor=0.38 ≈ halves the perceived sRGB brightness of the highlight color.
    static Color Dark(Color c, float factor = 0.38f) =>
        new Color(c.r * factor, c.g * factor, c.b * factor, 1f);

    // ── Ramp definitions ──────────────────────────────────────────────────────
    // 4 stops at 0.000 / 0.333 / 0.667 / 1.000 → 4 visible cel bands.
    // With step-function texture the bands transition abruptly at each stop position.
    // bandEval 0→1 maps: deepest shadow → mid shadow → mid light → brightest highlight.

    // Cube + Walls: warm beige (same ramp for both)
    static readonly GradientKey[] RampCubeWalls =
    {
        new GradientKey(0.000f, Dark(SRGB255(224, 200, 178), 0.28f)),
        new GradientKey(0.333f, Dark(SRGB255(224, 200, 178), 0.52f)),
        new GradientKey(0.667f, Dark(SRGB255(224, 200, 178), 0.76f)),
        new GradientKey(1.000f, SRGB255(224, 200, 178)),
    };

    // Floor: bright green
    static readonly GradientKey[] RampGreen =
    {
        new GradientKey(0.000f, Dark(SRGB255(141, 219, 100), 0.28f)),
        new GradientKey(0.333f, Dark(SRGB255(141, 219, 100), 0.52f)),
        new GradientKey(0.667f, Dark(SRGB255(141, 219, 100), 0.76f)),
        new GradientKey(1.000f, SRGB255(141, 219, 100)),
    };

    // Sphere: salmon/orange
    static readonly GradientKey[] RampSphere =
    {
        new GradientKey(0.000f, Dark(SRGB255(255, 136, 102), 0.28f)),
        new GradientKey(0.333f, Dark(SRGB255(255, 136, 102), 0.52f)),
        new GradientKey(0.667f, Dark(SRGB255(255, 136, 102), 0.76f)),
        new GradientKey(1.000f, SRGB255(255, 136, 102)),
    };

    // Grass: pink (shadow) → purple (highlight) — 4 bands interpolated between the two colors
    static readonly GradientKey[] RampGrass =
    {
        new GradientKey(0.000f, SRGB255(237, 121, 166)),          // deep shadow: pink
        new GradientKey(0.333f, SRGB255(220, 119, 192)),          // mid shadow: pink-purple
        new GradientKey(0.667f, SRGB255(204, 119, 216)),          // mid highlight: purple-pink
        new GradientKey(1.000f, SRGB255(187, 118, 237)),          // full highlight: purple
    };

    // ─── entry point ──────────────────────────────────────────────────────────
    [MenuItem("Tools/Art Test/Build ArtTestV2 Scene")]
    public static void Build()
    {
        // Open (or create) the ArtTestV2 scene
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // Clear all existing root objects except built-in settings
        foreach (var go in scene.GetRootGameObjects())
            Object.DestroyImmediate(go);

        // ── ensure folders ───────────────────────────────────────────────────
        EnsureFolder("Assets/Art", "ArtTest");

        // ── textures / materials ─────────────────────────────────────────────
        // Cel ramps: step-function (Point filter) → distinct flat bands
        var rampCubeWalls = LoadOrCreate_Ramp("CelRamp_CubeWalls.png", RampCubeWalls, stepBands: true);
        var rampGreen     = LoadOrCreate_Ramp("CelRamp_Green.png",     RampGreen,     stepBands: true);
        var rampSphere    = LoadOrCreate_Ramp("CelRamp_Sphere.png",    RampSphere,    stepBands: true);
        var rampGrass     = LoadOrCreate_Ramp("CelRamp_Grass.png",     RampGrass,     stepBands: true);
        // Specular ramp: smooth gradient (Bilinear) → soft highlight falloff
        var rampSpec      = LoadOrCreate_Ramp("CelSpecularRamp_Default.png", new[]
        {
            new GradientKey(0.0f, Color.black),
            new GradientKey(0.5f, Color.black),
            new GradientKey(1.0f, Color.white),
        }, stepBands: false);

        var matCube    = CreateCelMat("Mat_ArtTest_Cube",   rampCubeWalls, rampSpec);
        var matWalls   = CreateCelMat("Mat_ArtTest_Walls",  rampCubeWalls, rampSpec);
        var matFloor   = CreateCelMat("Mat_ArtTest_Floor",  rampGreen,     rampSpec);
        var matSphere  = CreateCelMat("Mat_ArtTest_Sphere", rampSphere,    rampSpec);
        var matFoliage = CreateFoliageMat("Mat_ArtTest_Foliage", rampGrass, rampGrass);

        // ── scene hierarchy ───────────────────────────────────────────────────
        var rigGO = BuildCameraRig();
        var sunGO = BuildSun();
        var world = BuildWorld(matCube, matWalls, matFloor, matSphere, matFoliage);
        BuildPostVolume();

        // ── ambient / environment ─────────────────────────────────────────────
        // Match Godot: disable skybox ambient, use a plain colour
        // Godot: ambient_light_disabled in cel shader → ambient contributes nothing.
        // Set to black so Unity's SH doesn't accidentally tint our custom cel lighting.
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.fog = false;

        // ── renderer feature: disable pixel-snap (camera RT is already 640×380) ──
        SetGodotParityPixelSize(1);

        // ── save ─────────────────────────────────────────────────────────────
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.Refresh();

        Debug.Log("[ArtTestSceneBuilder] ArtTestV2 built successfully.");
        Selection.activeGameObject = rigGO;
    }

    // ─── camera rig ───────────────────────────────────────────────────────────
    // Godot architecture: 3D scene → SubViewport (640×380) → SubViewportContainer (upscaled).
    // Unity equivalent : camera → RenderTexture (640×380) → Canvas RawImage (fullscreen).
    // Rendering at 640×380 means the cel-shader fragment runs at the same pixel density
    // as Godot, so smooth cel gradients naturally quantise into distinct visible bands.
    const int RT_W = 640;
    const int RT_H = 380;

    static GameObject BuildCameraRig()
    {
        // ── 640×380 render texture (matches Godot SubViewport size) ────────
        string rtPath = $"{ArtFolder}/PixelArtRT.renderTexture";
        var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(rtPath);
        if (rt == null)
        {
            rt = new RenderTexture(RT_W, RT_H, 24, RenderTextureFormat.DefaultHDR)
            {
                name        = "PixelArtRT",
                filterMode  = FilterMode.Point,   // nearest-neighbour – no blur between pixels
                antiAliasing = 1,
                wrapMode    = TextureWrapMode.Clamp,
            };
            AssetDatabase.CreateAsset(rt, rtPath);
        }
        else
        {
            rt.Release();
            rt.width       = RT_W;
            rt.height      = RT_H;
            rt.filterMode  = FilterMode.Point;
        }

        // ── CameraRig (yaw = 45°) ───────────────────────────────────────────
        var rig = new GameObject("CameraRig");
        rig.transform.position    = Vector3.zero;
        rig.transform.eulerAngles = new Vector3(0f, 45f, 0f);
        rig.AddComponent<CameraRig>();

        // ── Main Camera (pitch 30°, 20 units back) ──────────────────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.SetParent(rig.transform);
        camGO.transform.localPosition    = new Vector3(0f, 10f, -17.32f);
        camGO.transform.localEulerAngles = new Vector3(30f, 0f, 0f);

        var cam = camGO.AddComponent<Camera>();
        cam.orthographic     = true;
        cam.orthographicSize = 10f;
        cam.nearClipPlane    = 0.05f;
        cam.farClipPlane     = 50f;
        cam.clearFlags       = CameraClearFlags.SolidColor;
        cam.backgroundColor  = new Color(0.18f, 0.16f, 0.22f, 1f);
        cam.targetTexture    = rt;   // ← render to 640×380, not directly to screen

        camGO.AddComponent<AudioListener>();

        var urpData = camGO.GetComponent<UniversalAdditionalCameraData>()
                   ?? camGO.AddComponent<UniversalAdditionalCameraData>();
        urpData.renderPostProcessing = true;
        urpData.renderShadows        = true; // explicit – ensure shadow maps are requested for this camera

        // ── CameraDummy – renders nothing, exists only to silence the
        //    "Display 1 No Cameras Rendering" editor message.
        //    The Main Camera writes to a RenderTexture, not the display,
        //    so Unity considers Display 1 unoccupied without this camera.
        var dummyGO = new GameObject("CameraDummy");
        var dummyCam = dummyGO.AddComponent<Camera>();
        dummyCam.clearFlags    = CameraClearFlags.SolidColor;
        dummyCam.backgroundColor = Color.black;
        dummyCam.cullingMask   = 0;             // render nothing
        dummyCam.depth         = -10;           // below Main Camera
        dummyCam.targetTexture = null;          // renders to Display 1 → silences the message
        // Disable URP post-processing and shadows — it's a dummy
        var dummyUrp = dummyGO.AddComponent<UniversalAdditionalCameraData>();
        dummyUrp.renderPostProcessing = false;
        dummyUrp.renderShadows        = false;

        // ── DisplayCanvas – mirrors Godot's SubViewportContainer ────────────
        BuildDisplayCanvas(rt);

        return rig;
    }

    // Fullscreen canvas that displays the low-res RT (ScreenSpaceOverlay = no camera needed)
    static void BuildDisplayCanvas(RenderTexture rt)
    {
        var canvasGO = new GameObject("DisplayCanvas");

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        canvasGO.AddComponent<CanvasScaler>(); // default: constant pixel size
        canvasGO.AddComponent<GraphicRaycaster>();

        var imgGO = new GameObject("PixelDisplay");
        imgGO.transform.SetParent(canvasGO.transform, false);

        var img = imgGO.AddComponent<RawImage>();
        img.texture = rt;
        img.color   = Color.white;
        img.uvRect  = new Rect(0f, 0f, 1f, 1f);

        var rect = img.rectTransform;
        rect.anchorMin  = Vector2.zero;
        rect.anchorMax  = Vector2.one;
        rect.sizeDelta  = Vector2.zero;
        rect.offsetMin  = Vector2.zero;
        rect.offsetMax  = Vector2.zero;
    }

    // Finds the GodotParityFeature on the active URP renderer and sets pixelSize.
    // With camera rendering to a 640×380 RT, pixelSize=1 means no extra snapping —
    // the low render resolution already gives us the pixel-art quantisation.
    static void SetGodotParityPixelSize(int size)
    {
        var guids = AssetDatabase.FindAssets("t:UniversalRendererData");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (data == null) continue;
            foreach (var feature in data.rendererFeatures)
            {
                if (feature is GodotParityFeature gpf)
                {
                    gpf.pixelSize = size;
                    EditorUtility.SetDirty(data);
                    EditorUtility.SetDirty(gpf);
                }
            }
        }
        AssetDatabase.SaveAssets();
    }

    // ─── sun (directional light) ──────────────────────────────────────────────
    static GameObject BuildSun()
    {
        var sun = new GameObject("Sun");
        // Godot basis converted to Unity Euler:
        // Godot sun forward ≈ (0.5, -0.707, -0.5) → Unity pitch=45°, yaw=45°
        sun.transform.eulerAngles = new Vector3(45f, 45f, 0f);

        var light = sun.AddComponent<Light>();
        light.type             = LightType.Directional;
        light.color            = Color.white;
        light.intensity        = 1.0f;
        light.shadows          = LightShadows.Soft;
        light.shadowBias       = 0.05f;  // depth bias – keep low to avoid shadow acne
        light.shadowNormalBias = 0.05f;  // CRITICAL: default is 1.0 → shadows float 1m above surfaces (Peter Panning)

        return sun;
    }

    // ─── post-process volume (glow / bloom matching Godot environment) ───────
    static void BuildPostVolume()
    {
        var go = new GameObject("PostProcessing");
        var vol = go.AddComponent<UnityEngine.Rendering.Volume>();
        vol.isGlobal = true;

        // Save profile as asset so it serialises properly
        string profilePath = $"{ArtFolder}/ArtTestPostProfile.asset";
        var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }
        else
        {
            // Clear old overrides
            profile.components.Clear();
        }

        // Bloom – matches Godot: glow_intensity=0.3, glow_bloom=0.1, additive, hdr_threshold=1.0
        var bloom = profile.Add<UnityEngine.Rendering.Universal.Bloom>(true);
        bloom.threshold.value  = 1.0f;   // glow_hdr_threshold
        bloom.intensity.value  = 0.30f;  // glow_intensity
        bloom.scatter.value    = 0.50f;  // controls spread; no exact Godot equivalent
        bloom.highQualityFiltering.value = false;

        EditorUtility.SetDirty(profile);
        vol.sharedProfile = profile;
    }

    // ─── world ────────────────────────────────────────────────────────────────
    static GameObject BuildWorld(Material matCube, Material matWalls, Material matFloor,
                                 Material matSphere, Material matFoliage)
    {
        var world  = new GameObject("World");
        var meshes = new GameObject("Meshes");
        meshes.transform.SetParent(world.transform);

        // Floor – Godot PlaneMesh 12×12 → Unity plane (10×10) scaled 1.2
        CreateMeshObject("Floor", meshes.transform,
            PrimitiveMeshOf(PrimitiveType.Plane), matFloor,
            pos: new Vector3(0f, 0f, 0f),
            scale: new Vector3(1.2f, 1f, 1.2f));

        // Central cube – 1×1×1 at (1, 0.5, 0) rotated Y=15° (matches Godot)
        var cubeGO = CreateMeshObject("Cube", meshes.transform,
            PrimitiveMeshOf(PrimitiveType.Cube), matCube,
            pos: new Vector3(1f, 0.5f, 0f));
        cubeGO.transform.localEulerAngles = new Vector3(0f, 15f, 0f);

        // Sphere — Godot: (-0.6, 0.5, 0) → Unity: (-0.6, 0.5, 0)
        CreateMeshObject("Sphere", meshes.transform,
            PrimitiveMeshOf(PrimitiveType.Sphere), matSphere,
            pos: new Vector3(-0.6f, 0.5f, 0f));

        // 4 Walls — exact positions converted from Godot (negate Z, negate Y rot)
        // Godot BoxMesh(5,3,0.5). X-facing walls need scale(0.5,3,5), Z-facing need scale(5,3,0.5).
        float wallH = 1.5f;

        // Wall1 Godot(-5.8,1.5, 3.5) Y=-90° → Unity(-5.8,1.5,-3.5) scale(0.5,3,5)
        CreateMeshObject("Wall1", meshes.transform, PrimitiveMeshOf(PrimitiveType.Cube), matWalls,
            pos: new Vector3(-5.8f, wallH, -3.5f), scale: new Vector3(0.5f, 3f, 5f));
        // Wall2 Godot( 5.8,1.5, 3.5) Y=-90° → Unity( 5.8,1.5,-3.5) scale(0.5,3,5)
        CreateMeshObject("Wall2", meshes.transform, PrimitiveMeshOf(PrimitiveType.Cube), matWalls,
            pos: new Vector3(5.8f, wallH, -3.5f), scale: new Vector3(0.5f, 3f, 5f));
        // Wall3 Godot( 3.5,1.5, 5.8) Y=0°  → Unity( 3.5,1.5,-5.8) scale(5,3,0.5)
        CreateMeshObject("Wall3", meshes.transform, PrimitiveMeshOf(PrimitiveType.Cube), matWalls,
            pos: new Vector3(3.5f, wallH, -5.8f), scale: new Vector3(5f, 3f, 0.5f));
        // Wall4 Godot(-3.5,1.5,-5.8) Y=0°  → Unity(-3.5,1.5, 5.8) scale(5,3,0.5)
        CreateMeshObject("Wall4", meshes.transform, PrimitiveMeshOf(PrimitiveType.Cube), matWalls,
            pos: new Vector3(-3.5f, wallH, 5.8f), scale: new Vector3(5f, 3f, 0.5f));

        // ── Grass layer 1: pink/purple (original foliage color) ────────────
        var grassGO = new GameObject("Grass_Foliage");
        grassGO.transform.SetParent(world.transform);
        var gi = grassGO.AddComponent<GrassInstancer>();
        gi.count           = 2048;
        gi.areaSize        = 11f;
        gi.seed            = 0f;
        gi.quadMesh        = BuildQuadMesh();
        gi.foliageMaterial = matFoliage;

        // ── Grass layer 2: floor green (same ramp as floor, different seed) ──
        var rampGreenTex  = LoadOrCreate_Ramp("CelRamp_Green.png", RampGreen, stepBands: true);
        var grassGreenMat = CreateFoliageMat("Mat_ArtTest_Foliage_Green", rampGreenTex, rampGreenTex);
        var grassGO2 = new GameObject("Grass_Green");
        grassGO2.transform.SetParent(world.transform);
        var gi2 = grassGO2.AddComponent<GrassInstancer>();
        gi2.count           = 2048;
        gi2.areaSize        = 11f;
        gi2.seed            = 42f;  // different seed → different positions from layer 1
        gi2.quadMesh        = BuildQuadMesh();
        gi2.foliageMaterial = grassGreenMat;

        return world;
    }

    // ─── material helpers ──────────────────────────────────────────────────────
    static Material CreateCelMat(string name, Texture2D ramp, Texture2D specRamp,
                                  float specularStrength = 0f, float shininess = 32f)
    {
        string path   = $"{ArtFolder}/{name}.mat";
        var    shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null) { Debug.LogError($"Shader not found: {ShaderPath}"); return null; }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }

        mat.SetTexture("_CelRamp",        ramp);
        mat.SetTexture("_CelSpecularRamp", specRamp);
        mat.SetFloat("_LightWrap",                    0.3f);
        mat.SetFloat("_Steepness",                    1.0f);
        mat.SetFloat("_ShadowStrength",               1.0f);
        mat.SetFloat("_PointLightAttenuationCurve",   1.0f);
        mat.SetFloat("_SpecularShininess",            shininess);
        mat.SetFloat("_SpecularStrength",             specularStrength);
        mat.SetFloat("_UseDither",                    0f);
        mat.SetFloat("_DitherStrength",               0.1f);
        mat.SetFloat("_DitherDirectional",            0f);

        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material CreateFoliageMat(string name, Texture2D ramp, Texture2D rampAccent)
    {
        string path   = $"{ArtFolder}/{name}.mat";
        var    shader = AssetDatabase.LoadAssetAtPath<Shader>(FolShaderP);
        if (shader == null)
        {
            Debug.LogWarning($"Foliage shader not found at {FolShaderP}. Using cel shader as fallback.");
            shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        }
        if (shader == null) return null;

        var foliageTex = AssetDatabase.LoadAssetAtPath<Texture2D>(FolTexPath);

        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }

        if (foliageTex != null && mat.HasProperty("_BaseTexture"))
            mat.SetTexture("_BaseTexture", foliageTex);

        if (mat.HasProperty("_CelRamp"))        mat.SetTexture("_CelRamp",        ramp);
        if (mat.HasProperty("_CelRampAccent"))  mat.SetTexture("_CelRampAccent",  rampAccent);
        if (mat.HasProperty("_LightWrap"))      mat.SetFloat("_LightWrap",        0.3f);
        if (mat.HasProperty("_Steepness"))      mat.SetFloat("_Steepness",        1.0f);
        if (mat.HasProperty("_ShadowStrength")) mat.SetFloat("_ShadowStrength",   1.0f);
        if (mat.HasProperty("_AlphaScissor"))   mat.SetFloat("_AlphaScissor",     0.5f);
        if (mat.HasProperty("_VerticalOffset")) mat.SetFloat("_VerticalOffset",   0.25f);
        if (mat.HasProperty("_QuadScale"))      mat.SetFloat("_QuadScale",        1.0f);
        if (mat.HasProperty("_Framerate"))      mat.SetFloat("_Framerate",        5.0f);
        if (mat.HasProperty("_ViewSwaySpeed"))  mat.SetFloat("_ViewSwaySpeed",    0.1f);
        if (mat.HasProperty("_ViewSwayAngle"))  mat.SetFloat("_ViewSwayAngle",    10.0f);

        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ─── ramp texture helpers ─────────────────────────────────────────────────
    // stepBands=true  → step-function (distinct flat cel bands, Point filter)
    // stepBands=false → smooth Catmull-Rom curve (used for specular ramp)
    static Texture2D LoadOrCreate_Ramp(string filename, GradientKey[] keys,
                                        int width = 256, bool stepBands = true)
    {
        EnsureFolder("Assets/Resources", "Stylized");
        string path = $"{RampFolder}/{filename}";
        return CreateRampTexture(path, keys, width, stepBands);
    }

    static Texture2D CreateRampTexture(string assetPath, GradientKey[] keys,
                                        int width = 256, bool stepBands = true)
    {
        // Godot values are LINEAR; we convert to sRGB for storage so the GPU
        // can decode back to linear (matches Godot's source_color sampler hint).
        var tex = new Texture2D(width, 1, TextureFormat.RGBA32, false, true)
        {
            wrapMode   = TextureWrapMode.Clamp,
            filterMode = stepBands ? FilterMode.Point : FilterMode.Bilinear
        };

        for (int x = 0; x < width; x++)
        {
            float t = x / (float)(width - 1);
            Color c = stepBands
                ? EvaluateGradientStep(keys, t)   // flat bands, abrupt transitions
                : EvaluateGradient(keys, t);       // smooth Catmull-Rom for specular
            tex.SetPixel(x, 0, c);
        }
        tex.Apply(false, false);

        string abs = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        File.WriteAllBytes(abs, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(assetPath) is TextureImporter ti)
        {
            ti.mipmapEnabled      = false;
            ti.sRGBTexture        = true;    // GPU decodes sRGB→linear on sample
            ti.wrapMode           = TextureWrapMode.Clamp;
            ti.filterMode         = stepBands ? FilterMode.Point : FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.alphaSource        = TextureImporterAlphaSource.None;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    // ─── mesh / GO helpers ────────────────────────────────────────────────────
    static GameObject CreateMeshObject(string name, Transform parent, Mesh mesh,
                                        Material mat, Vector3 pos = default,
                                        Vector3 scale = default)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.localPosition = pos;
        go.transform.localScale    = (scale == default ? Vector3.one : scale);

        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mf.sharedMesh      = mesh;
        mr.sharedMaterial  = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        mr.receiveShadows    = true;
        return go;
    }

    static Mesh PrimitiveMeshOf(PrimitiveType type)
    {
        var temp = GameObject.CreatePrimitive(type);
        var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(temp);
        return mesh;
    }

    static Mesh BuildQuadMesh()
    {
        // A simple unit quad centred at origin, suitable for billboarding.
        // Vertices span [-0.5, 0.5] in X and [-0.5, 0.5] in Y.
        var mesh = new Mesh { name = "BillboardQuad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f),
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
        };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Save as asset so GrassInstancer serialises it properly
        string meshPath = $"{ArtFolder}/BillboardQuad.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (existing != null) return existing;
        AssetDatabase.CreateAsset(mesh, meshPath);
        return mesh;
    }

    static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            AssetDatabase.CreateFolder(parent, child);
    }

    // ─── gradient evaluation ───────────────────────────────────────────────────

    // Step-function: each stop occupies a flat-color region, no interpolation.
    // Returns sRGB-space color.  With FilterMode.Point on the texture this gives
    // the hard cel-shading bands visible in Godot's pixel-art render.
    static Color EvaluateGradientStep(GradientKey[] keys, float t)
    {
        if (keys == null || keys.Length == 0) return LinearToSRGB(Color.white);
        // Walk backwards: return the last stop whose position ≤ t
        for (int i = keys.Length - 1; i >= 0; i--)
        {
            if (t >= keys[i].t - 1e-5f)
                return LinearToSRGB(keys[i].color);
        }
        return LinearToSRGB(keys[0].color);
    }

    // Smooth Catmull-Rom in sRGB space (kept for specular ramp only).
    // Returns sRGB-space color (ready to be stored in a sRGB texture).
    static Color EvaluateGradient(GradientKey[] keys, float t)
    {
        if (keys == null || keys.Length == 0) return LinearToSRGB(Color.white);
        if (t <= keys[0].t)                   return LinearToSRGB(keys[0].color);
        if (t >= keys[keys.Length - 1].t)     return LinearToSRGB(keys[keys.Length - 1].color);

        for (int i = 0; i < keys.Length - 1; i++)
        {
            if (t >= keys[i].t && t <= keys[i + 1].t)
            {
                float u = Mathf.InverseLerp(keys[i].t, keys[i + 1].t, t);

                // Catmull-Rom in sRGB space (matches Godot interpolation_mode=1, color_space=0)
                Color p0 = LinearToSRGB(i > 0              ? keys[i - 1].color : keys[i].color);
                Color p1 = LinearToSRGB(keys[i].color);
                Color p2 = LinearToSRGB(keys[i + 1].color);
                Color p3 = LinearToSRGB(i + 2 < keys.Length ? keys[i + 2].color : keys[i + 1].color);

                return CatmullRomColor(p0, p1, p2, p3, u);
            }
        }
        return LinearToSRGB(keys[keys.Length - 1].color);
    }

    static Color CatmullRomColor(Color p0, Color p1, Color p2, Color p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        Color c = 0.5f * (
              p1 * 2f
            + (p2 - p0) * t
            + (p0 * 2f - p1 * 5f + p2 * 4f - p3) * t2
            + (p0 * -1f + p1 * 3f - p2 * 3f + p3) * t3
        );
        return new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f);
    }

    // Convert a linear-space Color to sRGB (IEC 61966-2-1)
    static Color LinearToSRGB(Color c) =>
        new Color(LinearToSRGB(c.r), LinearToSRGB(c.g), LinearToSRGB(c.b), c.a);

    static float LinearToSRGB(float x)
    {
        x = Mathf.Max(0f, x);
        return x <= 0.0031308f ? x * 12.92f : 1.055f * Mathf.Pow(x, 1f / 2.4f) - 0.055f;
    }

    readonly struct GradientKey
    {
        public readonly float t;
        public readonly Color color; // stored as LINEAR (matching Godot .tscn values)
        public GradientKey(float t, Color color) { this.t = t; this.color = color; }
    }
}
