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
    /// El criterio sale del rig y no de una lista a mano: se marca Blink si el
    /// <c>AnimatorController</c> declara algún clip de teletransporte, así que el día que arte le
    /// autore una caminata, re-correr esto lo devuelve a Walk solo.
    /// </remarks>
    public static class EnemyLocomotionInstaller
    {
        private const string LogPrefix = "[EnemyLocomotionInstaller] ";

        /// <summary>Marca en el nombre del clip que delata un rig de teletransporte.</summary>
        public const string TeleportClipMarker = "Teleport";

        /// <summary>
        /// Fichas que van en Blink <b>aunque su rig no tenga clip de teletransporte</b>: un parche
        /// hasta que haya arte. Hoy vacía; se deja en pie para que el próximo rig sin ciclo de
        /// caminata entre acá y no en una rama nueva.
        /// </summary>
        public static readonly HashSet<string> ForcedBlinkEntityIds = new HashSet<string>();

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

                if (!ApplyTo(prefabPath, data.EntityId, out var style)) continue;

                if (style == EntityPawn.LocomotionStyle.Blink)
                {
                    blink++;
                    Debug.Log(LogPrefix + $"'{data.EntityId}' ({data.VisualPrefab.name}) → Blink: " +
                              (Teleports(data.VisualPrefab)
                                  ? "su clip de movimiento es un teletransporte."
                                  : "PARCHE — su rig no tiene ciclo de caminata (ver ForcedBlinkEntityIds)."));
                }
                else walk++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log(LogPrefix + $"Listo — {blink} prefab(s) en Blink, {walk} en Walk.");
        }

        /// <summary>Público para que el test pueda afirmar la regla sin abrir prefabs.</summary>
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

        /// <summary>Hold por tramo cuando el rig SÍ tiene clip de desvanecerse — le da tiempo a correr.</summary>
        public const float ClipBlinkHold = 0.14f;

        /// <summary>Hold por tramo cuando no hay clip: apenas un beat, para que no parezca un tirón.</summary>
        public const float SnapBlinkHold = 0.05f;

        /// <summary>
        /// Resuelve y escribe el estilo de locomoción. La consume <c>BossVisualWrapperBuilder</c> al
        /// final de cada armado porque <c>SaveAsPrefabAsset</c> reescribe el <see cref="EntityPawn"/>
        /// entero y devuelve el flag a Walk.
        /// </summary>
        /// <param name="entityId">Consulta <see cref="ForcedBlinkEntityIds"/>; vacío ⇒ manda el rig.</param>
        /// <returns><c>true</c> si el prefab se reescribió.</returns>
        public static bool ApplyTo(string prefabPath, string entityId,
                                   out EntityPawn.LocomotionStyle style)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            bool rigTeleports = prefab != null && Teleports(prefab);
            bool forced = !string.IsNullOrEmpty(entityId) && ForcedBlinkEntityIds.Contains(entityId);

            style = rigTeleports || forced
                ? EntityPawn.LocomotionStyle.Blink
                : EntityPawn.LocomotionStyle.Walk;

            // Sin clip de desvanecerse, un hold largo se lee como un tirón; corto, como un salto.
            return ApplyTo(prefabPath, style, rigTeleports ? ClipBlinkHold : SnapBlinkHold);
        }

        private static bool ApplyTo(string prefabPath, EntityPawn.LocomotionStyle style, float hold)
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

                var outProp = so.FindProperty("_blinkOutSeconds");
                var inProp = so.FindProperty("_blinkInSeconds");

                bool styleChanged = prop.enumValueIndex != (int)style;
                bool holdChanged = style == EntityPawn.LocomotionStyle.Blink
                                   && outProp != null && inProp != null
                                   && (!Mathf.Approximately(outProp.floatValue, hold)
                                       || !Mathf.Approximately(inProp.floatValue, hold));

                // Sin cambio no se reescribe: SaveAsPrefabAsset renumera fileIDs internos y ensuciaría
                // el diff de todos los prefabs en cada corrida.
                if (!styleChanged && !holdChanged) return false;

                prop.enumValueIndex = (int)style;
                if (style == EntityPawn.LocomotionStyle.Blink)
                {
                    if (outProp != null) outProp.floatValue = hold;
                    if (inProp != null) inProp.floatValue = hold;
                }
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
