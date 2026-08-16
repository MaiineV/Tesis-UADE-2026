using System.Collections.Generic;
using Rollgeon.Entities;
using Rollgeon.Entities.Visuals;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy
{
    /// <summary>
    /// Pone <see cref="EntityPawn.LocomotionStyle.Blink"/> en los prefabs cuyo rig se teletransporta
    /// (<c>Rollgeon → Enemies → Apply Teleport Locomotion</c>). Idempotente.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El criterio sale del rig, no de una lista a mano.</b> Un prefab se marca Blink si su
    /// <c>AnimatorController</c> declara algún clip de teletransporte: eso es precisamente lo que
    /// significa "su animación de movimiento es un TP", y así el día que arte le autore un clip de
    /// caminata a un rig, re-correr esto lo devuelve a Walk solo.
    /// </para>
    /// <para>
    /// Hoy caen el Healer (<c>Anim_Healer_Teleport_1/_2</c>) y el Sunked Grand
    /// (<c>Anim_SunkedGrand_Teleport_1/_2</c>) — y con él el Tahúr, que viste su mismo rig.
    /// </para>
    /// </remarks>
    public static class EnemyLocomotionInstaller
    {
        private const string LogPrefix = "[EnemyLocomotionInstaller] ";

        /// <summary>Marca en el nombre del clip que delata un rig de teletransporte.</summary>
        public const string TeleportClipMarker = "Teleport";

        [MenuItem("Rollgeon/Enemies/Apply Teleport Locomotion")]
        public static void Apply()
        {
            var visited = new HashSet<string>();
            int blink = 0;
            int walk = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDataSO"))
            {
                var data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (data?.VisualPrefab == null) continue;

                var prefabPath = AssetDatabase.GetAssetPath(data.VisualPrefab);
                if (string.IsNullOrEmpty(prefabPath) || !visited.Add(prefabPath)) continue;

                var style = Teleports(data.VisualPrefab)
                    ? EntityPawn.LocomotionStyle.Blink
                    : EntityPawn.LocomotionStyle.Walk;

                if (!ApplyTo(prefabPath, style)) continue;

                if (style == EntityPawn.LocomotionStyle.Blink)
                {
                    blink++;
                    Debug.Log(LogPrefix + $"'{data.EntityId}' ({data.VisualPrefab.name}) → Blink: su " +
                              "clip de movimiento es un teletransporte.");
                }
                else walk++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log(LogPrefix + $"Listo — {blink} prefab(s) en Blink, {walk} en Walk.");
        }

        /// <summary>
        /// <c>true</c> si el rig tiene algún clip de teletransporte. Público y estático para que el
        /// test pueda afirmar la regla sin abrir prefabs.
        /// </summary>
        public static bool HasTeleportClip(RuntimeAnimatorController controller)
        {
            if (controller == null) return false;
            foreach (var clip in controller.animationClips)
            {
                if (clip == null) continue;
                if (clip.name.IndexOf(TeleportClipMarker, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static bool Teleports(GameObject visualPrefab)
        {
            var animator = visualPrefab.GetComponentInChildren<Animator>(true);
            return animator != null && HasTeleportClip(animator.runtimeAnimatorController);
        }

        private static bool ApplyTo(string prefabPath, EntityPawn.LocomotionStyle style)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            if (contents == null) return false;

            try
            {
                var pawn = contents.GetComponentInChildren<EntityPawn>(true);
                if (pawn == null) return false;

                var so = new SerializedObject(pawn);
                var prop = so.FindProperty("_locomotion");
                if (prop == null)
                {
                    Debug.LogWarning(LogPrefix + "EntityPawn no expone '_locomotion' — ¿se renombró?");
                    return false;
                }

                // Sin cambio no se reescribe: SaveAsPrefabAsset renumera fileIDs internos y ensuciaría
                // el diff de todos los prefabs en cada corrida.
                if (prop.enumValueIndex == (int)style) return false;

                prop.enumValueIndex = (int)style;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
