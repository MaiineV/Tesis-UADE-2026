using System.Collections.Generic;
using System.Text;
using Rollgeon.Entities;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy
{
    /// <summary>
    /// Audita el rig visual de cada <see cref="EnemyDataSO"/>: si su prefab tiene malla, si tiene
    /// <see cref="Animator"/> con controller, y qué clips declara
    /// (<c>Rollgeon → Enemies → Audit Rigs</c>).
    /// </summary>
    /// <remarks>
    /// Existe porque el YAML miente: los wrappers de jefe anidan el prefab de arte, así que sus
    /// componentes aparecen como <c>stripped</c> y un grep sobre el <c>.prefab</c> dice "no hay
    /// Animator" cuando sí lo hay. La única fuente confiable es cargar el prefab y preguntarle.
    /// </remarks>
    public static class EnemyRigAudit
    {
        private const string LogPrefix = "[EnemyRigAudit] ";

        [MenuItem("Rollgeon/Enemies/Audit Rigs")]
        public static void Audit()
        {
            var guids = AssetDatabase.FindAssets("t:EnemyDataSO");
            int missingVisual = 0;
            int missingAnimator = 0;
            int missingController = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(path);
                if (data == null) continue;

                var line = new StringBuilder();
                line.Append(LogPrefix).Append(data.EntityId ?? data.name).Append("  |  ");

                if (data.VisualPrefab == null)
                {
                    missingVisual++;
                    Debug.LogWarning(line.Append("SIN VisualPrefab").ToString());
                    continue;
                }

                line.Append(data.VisualPrefab.name).Append("  |  ");

                int skinned = data.VisualPrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
                int meshes = data.VisualPrefab.GetComponentsInChildren<MeshRenderer>(true).Length;
                line.Append($"malla: {skinned} skinned + {meshes} mesh  |  ");

                var animator = data.VisualPrefab.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    missingAnimator++;
                    line.Append("SIN Animator");
                    Debug.LogWarning(line.ToString());
                    continue;
                }

                var controller = animator.runtimeAnimatorController;
                if (controller == null)
                {
                    missingController++;
                    line.Append("Animator SIN controller");
                    Debug.LogWarning(line.ToString());
                    continue;
                }

                line.Append(controller.name).Append(" [");
                line.Append(string.Join(", ", ClipNames(controller))).Append(']');

                // El bool que gatea Idle <-> Run en EntityPawn. Un rig sin él se queda quieto
                // mientras camina, que es la mitad de "las animaciones no andan".
                line.Append(HasMovementParam(controller) ? "  + Movement" : "  SIN param Movement");

                Debug.Log(line.ToString());
            }

            Debug.Log(LogPrefix + $"{guids.Length} fichas auditadas — " +
                      $"{missingVisual} sin visual, {missingAnimator} sin Animator, " +
                      $"{missingController} sin controller.");
        }

        private static IEnumerable<string> ClipNames(RuntimeAnimatorController controller)
        {
            var names = new List<string>();
            foreach (var clip in controller.animationClips)
                if (clip != null && !names.Contains(clip.name)) names.Add(clip.name);
            return names;
        }

        private static bool HasMovementParam(RuntimeAnimatorController controller)
        {
            if (!(controller is AnimatorController ac)) return false;
            foreach (var p in ac.parameters)
                if (p.name == "Movement") return true;
            return false;
        }
    }
}
