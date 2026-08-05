using System;
using System.Text;
using Patterns;
using Rollgeon.Tutorial;
using Rollgeon.Upgrades.Dice;
using Rollgeon.Upgrades.Dice.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools.Repro
{
    /// <summary>
    /// Herramienta temporal de repro para el bug "mesa de encantamientos sin
    /// información" (softlock del tutorial). Borrar cuando el bug esté cerrado.
    /// </summary>
    public static class EnchantAltarRepro
    {
        [MenuItem("Rollgeon/Repro/1 - Launch Tutorial (play mode)")]
        private static void LaunchTutorial()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[Repro] Solo en play mode."); return; }
            bool ok = TutorialLauncher.Launch();
            Debug.Log($"[Repro] TutorialLauncher.Launch() => {ok}");
        }

        [MenuItem("Rollgeon/Repro/2 - Open Altar Panel (fake event)")]
        private static void OpenAltarPanel()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[Repro] Solo en play mode."); return; }
            EventManager.Trigger(EventName.OnEnchantmentAltarActivated, Guid.Empty, Guid.NewGuid(), 15);
            Debug.Log("[Repro] OnEnchantmentAltarActivated disparado.");
        }

        [MenuItem("Rollgeon/Repro/4 - Push CombatHUD")]
        private static void PushCombatHud()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[Repro] Solo en play mode."); return; }
            if (ServiceLocator.TryGetService<Rollgeon.UI.IScreenManager>(out var screens))
            {
                screens.PushByStringId("CombatHUD");
                Debug.Log("[Repro] CombatHUD pusheado.");
            }
        }

        [MenuItem("Rollgeon/Repro/5 - Pop Current Screen")]
        private static void PopScreen()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[Repro] Solo en play mode."); return; }
            if (ServiceLocator.TryGetService<Rollgeon.UI.IScreenManager>(out var screens))
            {
                screens.PopCurrent();
                Debug.Log("[Repro] Pop del top del stack.");
            }
        }

        [MenuItem("Rollgeon/Repro/6 - Force Tutorial Step Enchant")]
        private static void ForceStepEnchant()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[Repro] Solo en play mode."); return; }
            if (!ServiceLocator.TryGetService<TutorialFlowController>(out var flow) || flow == null)
            {
                Debug.LogWarning("[Repro] TutorialFlowController no registrado.");
                return;
            }
            var flowType = typeof(TutorialFlowController);
            var stepField = flowType.GetField("_step",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var stepType = typeof(TutorialLauncher).Assembly.GetType("Rollgeon.Tutorial.TutorialStep");
            stepField.SetValue(flow, Enum.Parse(stepType, "Enchant"));
            Debug.Log("[Repro] _step = Enchant forzado.");
        }

        [MenuItem("Rollgeon/Repro/7 - Overlay Continue Click")]
        private static void OverlayContinue()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[Repro] Solo en play mode."); return; }
            var ovType = typeof(TutorialLauncher).Assembly.GetType("Rollgeon.Tutorial.UI.TutorialOverlay");
            var ov = UnityEngine.Object.FindFirstObjectByType(ovType, FindObjectsInactive.Include);
            if (ov == null) { Debug.LogWarning("[Repro] TutorialOverlay no encontrado."); return; }
            ovType.GetMethod("HandleContinueClick",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(ov, null);
            Debug.Log("[Repro] HandleContinueClick invocado.");
        }

        [MenuItem("Rollgeon/Repro/8 - Enter Room E + Altar Diagnostics")]
        private static void EnterRoomEAndDiagnose()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[Repro] Solo en play mode."); return; }
            if (!ServiceLocator.TryGetService<Rollgeon.Dungeon.IDungeonService>(out var dungeon) || dungeon == null)
            {
                Debug.LogWarning("[Repro] IDungeonService no registrado."); return;
            }

            Guid roomE = Guid.Empty;
            foreach (var kv in dungeon.GetAllRoomInstances())
            {
                if (kv.Value.Template != null && kv.Value.Template.Type == Rollgeon.Dungeon.RoomType.Enchantment)
                {
                    roomE = kv.Key;
                    break;
                }
            }
            if (roomE == Guid.Empty) { Debug.LogWarning("[Repro] No hay sala Enchantment."); return; }

            if (dungeon.CurrentRoomInstance == null || dungeon.CurrentRoomInstance.InstanceId != roomE)
            {
                var dm = dungeon as Rollgeon.Dungeon.DungeonManager;
                var mi = dungeon.GetType().GetMethod("TransitionTo",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                mi.Invoke(dungeon, new object[] { roomE });
                Debug.Log("[Repro] TransitionTo(roomE) invocado.");
            }

            var altar = UnityEngine.Object.FindFirstObjectByType<EnchantmentAltarInteractable>(FindObjectsInactive.Include);
            if (altar == null) { Debug.LogWarning("[Repro] Altar no instanciado tras entrar a E."); return; }

            var sb = new StringBuilder("[Repro] === Altar Diagnostics ===\n");
            sb.AppendLine($"altar GO active={altar.gameObject.activeInHierarchy} pos={altar.transform.position}");
            var t = typeof(EnchantmentAltarInteractable);
            float range = (float)t.GetField("_interactRange",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(altar);
            bool panelOpen = (bool)t.GetField("_panelOpen",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(altar);
            object svc = t.GetField("_service",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(altar);
            sb.AppendLine($"_interactRange={range} _panelOpen={panelOpen} _service={(svc != null ? "OK" : "NULL")}");

            if (ServiceLocator.TryGetService<Rollgeon.Grid.IGridManager>(out var grid)
                && ServiceLocator.TryGetService<Rollgeon.Player.IPlayerService>(out var player)
                && player != null && player.PlayerGuid != Guid.Empty)
            {
                bool hasPos = grid.TryGetPosition(player.PlayerGuid, out var coord);
                sb.AppendLine($"player coord: {(hasPos ? coord.ToString() : "NO REGISTRADO")}");
                if (hasPos)
                {
                    var pw = grid.GridToWorld(coord);
                    float distSq = (pw - altar.transform.position).sqrMagnitude;
                    sb.AppendLine($"playerWorld={pw} distSq={distSq:F2} rangeSq={range * range:F2} inRange={distSq <= range * range}");
                }
                var altarCoord = grid.WorldToGrid(altar.transform.position);
                foreach (var (dx, dy, tag) in new[] { (0, 1, "N"), (0, -1, "S"), (1, 0, "E"), (-1, 0, "W"), (1, 1, "NE"), (-1, -1, "SW") })
                {
                    var c = new Rollgeon.Grid.GridCoord(altarCoord.X + dx, altarCoord.Y + dy);
                    var w = grid.GridToWorld(c);
                    float dsq = (w - altar.transform.position).sqrMagnitude;
                    sb.AppendLine($"  tile {tag} {c}: distSq={dsq:F2} inRange={dsq <= range * range}");
                }
            }
            Debug.Log(sb.ToString());

            altar.Interact();
            Debug.Log("[Repro] altar.Interact() invocado.");
        }

        [MenuItem("Rollgeon/Repro/3 - Dump Altar State")]
        private static void DumpAltarState()
        {
            var sb = new StringBuilder("[Repro] === Dump Altar State ===\n");

            bool hasSvc = ServiceLocator.TryGetService<IDiceEnchantmentService>(out var svc);
            sb.AppendLine($"IDiceEnchantmentService: found={hasSvc} ready={(hasSvc && svc != null ? svc.IsReady.ToString() : "-")} " +
                          $"bag={(hasSvc && svc?.Bag != null ? svc.Bag.Dice.Count + " dados" : "null")}");

            var views = UnityEngine.Object.FindObjectsByType<EnchantmentAltarView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            sb.AppendLine($"EnchantmentAltarView instances: {views.Length}");
            foreach (var v in views)
            {
                sb.AppendLine($"- view GO '{GetPath(v.transform)}' activeSelf={v.gameObject.activeSelf} activeInHierarchy={v.gameObject.activeInHierarchy} enabled={v.enabled}");
                var panel = v.transform.Find("EnchantmentAltarPanel");
                if (panel == null && v.transform.childCount > 0) panel = v.transform.GetChild(0);
                if (panel != null)
                {
                    sb.AppendLine($"  panel '{panel.name}' active={panel.gameObject.activeSelf}");
                    DumpChildren(panel, sb, 1);
                }
            }
            var ovType = typeof(TutorialLauncher).Assembly.GetType("Rollgeon.Tutorial.UI.TutorialOverlay");
            var ov = UnityEngine.Object.FindFirstObjectByType(ovType, FindObjectsInactive.Include) as MonoBehaviour;
            if (ov != null)
            {
                var isVisible = ovType.GetProperty("IsVisible").GetValue(ov);
                var dim = (UnityEngine.UI.Image)ovType
                    .GetField("_dim", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .GetValue(ov);
                var dimMat = (Material)ovType
                    .GetField("_dimMaterial", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .GetValue(ov);
                var popupText = (TextMeshProUGUI)ovType
                    .GetField("_popupText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .GetValue(ov);
                sb.AppendLine($"TutorialOverlay: GO active={ov.gameObject.activeSelf} IsVisible={isVisible} " +
                              $"dimRaycast={(dim != null ? dim.raycastTarget.ToString() : "-")} " +
                              $"show={(dimMat != null ? dimMat.GetFloat("_Show").ToString("F2") : "-")} " +
                              $"cutoutR={(dimMat != null ? dimMat.GetFloat("_CutoutRadius").ToString("F0") : "-")} " +
                              $"popup='{Trunc(popupText != null ? popupText.text : "")}' " +
                              $"popupFont={(popupText != null && popupText.font != null ? popupText.font.name : "NULL")}");
            }
            else
            {
                sb.AppendLine("TutorialOverlay: (no existe)");
            }
            Debug.Log(sb.ToString());
        }

        private static void DumpChildren(Transform t, StringBuilder sb, int depth)
        {
            if (depth > 4) return;
            foreach (Transform c in t)
            {
                var rt = c as RectTransform;
                string extra = rt != null ? $" rect={rt.rect.size} pos={rt.anchoredPosition} scale={c.localScale}" : "";
                var tmp = c.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    extra += $" TMP text='{Trunc(tmp.text)}' font={(tmp.font != null ? tmp.font.name : "NULL")} " +
                             $"mat={(tmp.fontSharedMaterial != null ? tmp.fontSharedMaterial.name : "NULL")} " +
                             $"color={tmp.color} alpha={tmp.alpha}";
                }
                var cg = c.GetComponent<CanvasGroup>();
                if (cg != null) extra += $" canvasGroup(alpha={cg.alpha})";
                sb.AppendLine($"{new string(' ', depth * 2)}{c.name} active={c.gameObject.activeSelf}{extra}");
                DumpChildren(c, sb, depth + 1);
            }
        }

        private static string Trunc(string s) => string.IsNullOrEmpty(s) ? "" : (s.Length > 40 ? s.Substring(0, 40) + "…" : s);

        private static string GetPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
            return path;
        }
    }
}
