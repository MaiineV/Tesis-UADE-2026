using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy
{
    /// <summary>
    /// Agrega el trigger <c>Teleport</c> y la transición que lo dispara en
    /// <c>AnimCon_SunkedGrand</c> (<c>Rollgeon → Enemies → Wire SunkedGrand Teleport</c>).
    /// Idempotente.
    /// </summary>
    /// <remarks>
    /// El estado ya existía pero sólo lo alcanzaba el bool <c>Movement</c>, que es el blink del
    /// pathing: el salto de <c>AINode_TeleportAwayToEdge</c> no anima ningún path, así que sin
    /// trigger propio el jefe cambiaba de casilla sin gesto. La vuelta a Idle la resuelve la cadena
    /// que ya estaba —<c>Teleport → Teleport_2 → Idle</c>—, que es el par desvanecerse/aparecer del
    /// rig; por eso no se agrega una salida nueva, que competiría con ésa.
    /// </remarks>
    public static class SunkedGrandTeleportWiring
    {
        private const string LogPrefix = "[SunkedGrandTeleportWiring] ";

        private const string ControllerPath =
            "Assets/Art/3D/Animations/Enemies/SunkedGrand/AnimCon_SunkedGrand.controller";

        private const string TriggerName = "Teleport";
        private const string IdleStateName = "Idle";
        private const string TeleportStateName = "Teleport";

        // Los mismos valores que las transiciones de Attack_Melee y Attack_Range del controller.
        private const float TransitionDuration = 0.01f;
        private const float TransitionExitTime = 0.6875f;

        [MenuItem("Rollgeon/Enemies/Wire SunkedGrand Teleport")]
        public static void Wire()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError(LogPrefix + $"No se encontró '{ControllerPath}'.");
                return;
            }

            var machine = controller.layers[0].stateMachine;
            var idle = FindState(machine, IdleStateName);
            var teleport = FindState(machine, TeleportStateName);
            if (idle == null || teleport == null)
            {
                Debug.LogError(LogPrefix + $"'{controller.name}' no tiene los estados " +
                               $"'{IdleStateName}' y '{TeleportStateName}'.");
                return;
            }

            bool changed = false;

            if (!DeclaresTrigger(controller))
            {
                controller.AddParameter(TriggerName, AnimatorControllerParameterType.Trigger);
                changed = true;
            }

            if (!HasTriggerTransition(idle, teleport))
            {
                var transition = idle.AddTransition(teleport);
                transition.hasExitTime = false;
                transition.exitTime = TransitionExitTime;
                transition.hasFixedDuration = true;
                transition.duration = TransitionDuration;
                transition.offset = 0f;
                transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
                transition.orderedInterruption = true;
                transition.canTransitionToSelf = true;
                transition.AddCondition(AnimatorConditionMode.If, 0f, TriggerName);
                changed = true;
            }

            if (!changed)
            {
                Debug.Log(LogPrefix + "Ya estaba cableado — nada que hacer.");
                return;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log(LogPrefix + $"'{TriggerName}' cableado: {IdleStateName} → {TeleportStateName}.");
        }

        private static bool DeclaresTrigger(AnimatorController controller) =>
            controller.parameters.Any(p => p.name == TriggerName
                                           && p.type == AnimatorControllerParameterType.Trigger);

        private static bool HasTriggerTransition(AnimatorState from, AnimatorState to) =>
            from.transitions.Any(t => t.destinationState == to
                                      && t.conditions.Any(c => c.parameter == TriggerName));

        private static AnimatorState FindState(AnimatorStateMachine machine, string name)
        {
            foreach (var child in machine.states)
                if (child.state != null && child.state.name == name) return child.state;
            return null;
        }
    }
}
