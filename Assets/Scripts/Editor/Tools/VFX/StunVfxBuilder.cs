using Rollgeon.Feedback;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools.VFX
{
    /// <summary>
    /// Crea/actualiza <c>Assets/Resources/VFX_StunStars.prefab</c> — la corona de
    /// estrellas que gira sobre un pawn stuneado (BUG-87). Usa el modelo autorado
    /// <c>Art/3D/Models/Items/Stars.fbx</c> (que ya remapea a Mat_StarCrown en su
    /// import) como hijo de un root con <see cref="StunStarsSpin"/>.
    /// Idempotente: re-correrlo reconstruye el prefab desde cero.
    /// </summary>
    public static class StunVfxBuilder
    {
        private const string StarsModelPath = "Assets/Art/3D/Models/Items/Stars.fbx";
        // En Resources: StunVfxBinder lo carga por Resources.Load("VFX_StunStars").
        private const string StunVfxPrefabPath = "Assets/Resources/VFX_StunStars.prefab";

        [MenuItem("Rollgeon/VFX/Build Stun Stars VFX")]
        public static void Build()
        {
            var prefab = BuildStunStarsVfx();
            if (prefab != null)
                Debug.Log($"[StunVfxBuilder] OK — '{StunVfxPrefabPath}' listo (modelo Stars + spin).");
        }

        public static GameObject BuildStunStarsVfx()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(StarsModelPath);
            if (model == null)
            {
                Debug.LogWarning($"[StunVfxBuilder] No está el modelo en '{StarsModelPath}' — " +
                                 "el stun queda sin estrellas.");
                return null;
            }

            // Reconstrucción total: el contenido viejo (fuera el que fuere — la
            // primera versión era un ParticleSystem clonado) se descarta.
            var root = new GameObject("VFX_StunStars");
            try
            {
                root.AddComponent<StunStarsSpin>();

                var stars = (GameObject)PrefabUtility.InstantiatePrefab(model);
                stars.name = "Stars";
                stars.transform.SetParent(root.transform, worldPositionStays: false);
                stars.transform.localPosition = Vector3.zero;

                PrefabUtility.SaveAsPrefabAsset(root, StunVfxPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(StunVfxPrefabPath);
        }
    }
}
