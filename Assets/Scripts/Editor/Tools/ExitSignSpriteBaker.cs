using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools
{
    /// <summary>
    /// Bakea el cartel 3D de salida a un sprite 2D para el <c>ExitSignIndicator</c>
    /// (el indicador es UI screen-space pero el visual tiene que ser el cartel del
    /// juego, no la flecha del tutorial). La fuente es el child <c>ExitSign</c> del
    /// <c>DoorBoss.prefab</c> — NO el FBX crudo — para heredar la pose/overrides con
    /// que el usuario lo colgó en la puerta. El subtree se extrae bajo un parent
    /// inactivo y el resto de la puerta se destruye antes de activar nada, así
    /// ningún Awake de DoorController/tooltips corre en EditMode. Render
    /// ortográfico frontal en una posición remota de la escena abierta (y = -5000,
    /// el frustum no agarra nada más), con doble pasada negro/blanco para recuperar
    /// el alpha — los shaders opacos de URP no garantizan un canal alpha usable en
    /// el RT. Re-ejecutable: pisa el PNG y reimporta.
    /// </summary>
    public static class ExitSignSpriteBaker
    {
        private const string DoorBossPath = "Assets/Prefabs/Tiles/DoorBoss.prefab";
        private const string SignChildName = "ExitSign";
        private const string OutputDir = "Assets/Art/UI/ExitSign";
        public const string OutputPath = OutputDir + "/ExitSignBaked.png";

        // Cara que se captura: la que mira a -Z del cartel en la pose del prefab.
        // Si el bake sale espejado o de canto, ajustar y re-correr.
        private static readonly Vector3 ViewDirection = Vector3.forward;

        private const int TargetHeightPx = 256;
        private const float FramePadding = 1.05f;

        /// <summary>Bakea y devuelve el sprite importado, o null si falló.</summary>
        public static Sprite Bake()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorBossPath);
            if (prefab == null)
            {
                Debug.LogError($"[ExitSignSpriteBaker] No se pudo cargar '{DoorBossPath}'.");
                return null;
            }

            // Parent inactivo: los Awake de los scripts de la puerta no corren nunca
            // (el door completo se destruye antes de activar el subtree del cartel).
            var stage = new GameObject("[ExitSignBakeStage]");
            stage.SetActive(false);
            stage.transform.position = new Vector3(0f, -5000f, 0f);

            var camGo = new GameObject("[ExitSignBakeCamera]");
            var lightGo = new GameObject("[ExitSignBakeLight]");
            try
            {
                var door = Object.Instantiate(prefab, stage.transform);
                Transform sign = null;
                foreach (var t in door.GetComponentsInChildren<Transform>(includeInactive: true))
                {
                    if (t != door.transform && t.name == SignChildName) { sign = t; break; }
                }
                if (sign == null)
                {
                    Debug.LogError($"[ExitSignSpriteBaker] '{DoorBossPath}' no tiene un hijo '{SignChildName}'.");
                    return null;
                }

                sign.SetParent(stage.transform, worldPositionStays: false);
                Object.DestroyImmediate(door);
                sign.gameObject.SetActive(true);
                stage.SetActive(true);

                var renderers = sign.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0)
                {
                    Debug.LogError("[ExitSignSpriteBaker] El modelo no tiene renderers.");
                    return null;
                }

                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);

                // Luz propia mirando con la cámara: el bake no debe depender de la
                // iluminación de la escena que esté abierta al correrlo.
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                lightGo.transform.rotation = Quaternion.LookRotation(ViewDirection + Vector3.down * 0.6f);

                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.orthographicSize = bounds.extents.y * FramePadding;
                cam.transform.position = bounds.center - ViewDirection * (bounds.extents.magnitude + 2f);
                cam.transform.rotation = Quaternion.LookRotation(ViewDirection);
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = bounds.extents.magnitude * 2f + 4f;

                float aspect = Mathf.Max(0.05f, bounds.extents.x / Mathf.Max(0.001f, bounds.extents.y));
                cam.aspect = aspect;
                int height = TargetHeightPx;
                int width = Mathf.Max(8, Mathf.RoundToInt(height * aspect));

                var onBlack = RenderToTexture(cam, width, height, Color.black);
                var onWhite = RenderToTexture(cam, width, height, Color.white);
                try
                {
                    var final = RecoverAlpha(onBlack, onWhite);
                    try
                    {
                        Directory.CreateDirectory(OutputDir);
                        File.WriteAllBytes(OutputPath, final.EncodeToPNG());
                    }
                    finally
                    {
                        Object.DestroyImmediate(final);
                    }
                }
                finally
                {
                    Object.DestroyImmediate(onBlack);
                    Object.DestroyImmediate(onWhite);
                }

                ImportAsSprite(OutputPath);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(OutputPath);
                Debug.Log($"[ExitSignSpriteBaker] Sprite bakeado en '{OutputPath}' ({width}x{height}).");
                return sprite;
            }
            finally
            {
                Object.DestroyImmediate(stage);
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(lightGo);
            }
        }

        private static Texture2D RenderToTexture(Camera cam, int width, int height, Color background)
        {
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var prevActive = RenderTexture.active;
            try
            {
                cam.backgroundColor = background;
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                return tex;
            }
            finally
            {
                cam.targetTexture = null;
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        // Difference matting clásico: un pixel 100% fondo pasa de negro a blanco
        // (alpha 0); uno 100% modelo no cambia (alpha 1); los bordes anti-aliased
        // caen proporcionalmente. El color se toma de la pasada sobre negro
        // des-premultiplicado por el alpha recuperado.
        private static Texture2D RecoverAlpha(Texture2D onBlack, Texture2D onWhite)
        {
            var black = onBlack.GetPixels();
            var white = onWhite.GetPixels();
            var result = new Color[black.Length];

            for (int i = 0; i < black.Length; i++)
            {
                float alpha = 1f - ((white[i].r - black[i].r)
                                    + (white[i].g - black[i].g)
                                    + (white[i].b - black[i].b)) / 3f;
                alpha = Mathf.Clamp01(alpha);
                result[i] = alpha <= 0.001f
                    ? Color.clear
                    : new Color(black[i].r / alpha, black[i].g / alpha, black[i].b / alpha, alpha);
            }

            var tex = new Texture2D(onBlack.width, onBlack.height, TextureFormat.RGBA32, mipChain: false);
            tex.SetPixels(result);
            tex.Apply();
            return tex;
        }

        private static void ImportAsSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
