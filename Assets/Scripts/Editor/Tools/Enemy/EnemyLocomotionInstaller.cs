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

        /// <summary>
        /// Fichas que van en Blink <b>aunque su rig no tenga clip de teletransporte</b>. Es un
        /// parche hasta que haya arte, no una decisión de diseño.
        /// </summary>
        /// <remarks>
        /// El Cajero se repliega todos los turnos (<c>AINode_KeepDistance</c>: su disparo a rango
        /// existe justo porque kitea) pero <c>AnimCon_GeneralDirector</c> sólo declara Idle y Attack
        /// — no hay ciclo de caminata. Con el lerp de siempre se desliza por el piso en pose de
        /// idle, que es el peor de los dos males. El salto seco al menos se lee como intencional.
        /// <para>
        /// Cuando arte entregue el ciclo de caminata, se saca de acá y vuelve a Walk solo.
        /// </para>
        /// </remarks>
        public static readonly HashSet<string> ForcedBlinkEntityIds = new HashSet<string>
        {
            "boss.cashier",
        };

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

        /// <summary>Hold por tramo cuando el rig SÍ tiene clip de desvanecerse — le da tiempo a correr.</summary>
        public const float ClipBlinkHold = 0.14f;

        /// <summary>Hold por tramo cuando no hay clip: apenas un beat, para que no parezca un tirón.</summary>
        public const float SnapBlinkHold = 0.05f;

        /// <summary>
        /// Resuelve y escribe el estilo de locomoción de un prefab. La consume
        /// <c>BossVisualWrapperBuilder</c> al final de cada armado, porque
        /// <c>SaveAsPrefabAsset</c> reescribe el <see cref="EntityPawn"/> entero y devuelve el flag
        /// a Walk: sin este llamado, cada <c>Build &lt;Jefe&gt;</c> deja al jefe deslizándose hasta
        /// que alguien se acuerde de correr el menú.
        /// </summary>
        /// <param name="entityId">
        /// Para consultar <see cref="ForcedBlinkEntityIds"/>. Vacío ⇒ manda sólo el rig, que es lo
        /// correcto para un prefab que todavía no tiene ficha.
        /// </param>
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

            // Sin clip de desvanecerse, un hold largo se lee como un tirón: el pawn se queda
            // quieto y después aparece. Corto, se lee como un salto intencional.
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
