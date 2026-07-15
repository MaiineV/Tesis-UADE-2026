using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Effects.Concretes;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Debug-only overlay: self-bootstraps at runtime (no scene/prefab wiring needed) and
    /// draws an OnGUI box while <see cref="EffLowHpAttackBuff"/> is active on any entity.
    /// Exists to make the "low HP attack buff" passive visible for testing right now, while
    /// the real HUD badge (<see cref="PassiveBadgeView"/>) is still pending manual GameObject
    /// creation in the Canvas prefab (see docs/setup/warrior-passive-rework-setup.md, section 3).
    /// Safe to delete once that wiring is done and the real badge is in place.
    /// </summary>
    public class PassiveActiveDebugOverlay : MonoBehaviour
    {
        private static readonly HashSet<Guid> ActiveEntities = new HashSet<Guid>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("[Debug] PassiveActiveOverlay");
            DontDestroyOnLoad(go);
            go.AddComponent<PassiveActiveDebugOverlay>();
        }

        private void OnEnable()
        {
            EventManager.Subscribe(EventName.OnModifierAdded, OnModifierEvent);
            EventManager.Subscribe(EventName.OnModifierRemoved, OnModifierEvent);
        }

        private void OnDisable()
        {
            EventManager.UnSubscribe(EventName.OnModifierAdded, OnModifierEvent);
            EventManager.UnSubscribe(EventName.OnModifierRemoved, OnModifierEvent);
        }

        private void OnModifierEvent(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid entityGuid)) return;
            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null) return;

            if (EffLowHpAttackBuff.IsActiveFor(attrs, entityGuid)) ActiveEntities.Add(entityGuid);
            else ActiveEntities.Remove(entityGuid);
        }

        private void OnGUI()
        {
            if (ActiveEntities.Count == 0) return;

            var style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 18,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.yellow }
            };
            GUI.Box(new Rect(20, 220, 260, 60), "PASIVA ACTIVA\nFuria del Guerrero", style);
        }
    }
}
