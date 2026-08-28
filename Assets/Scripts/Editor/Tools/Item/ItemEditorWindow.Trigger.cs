using System.Collections.Generic;
using System.Linq;
using Rollgeon.Combat.Rolls;
using Rollgeon.Combos;
using Rollgeon.Items;
using Rollgeon.Upgrades.Dice;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Item
{
    /// <summary>
    /// La sección "Cuándo" del panel del ítem: cuándo se ejecuta, en castellano y editable.
    /// </summary>
    /// <remarks>
    /// Es lo único que un diseñador nombró como bloqueante de la tool. El "cuándo" está repartido en
    /// cuatro campos (<c>Kind</c>, <c>TriggerEvent</c>, <c>ComboFilter</c>, <c>ActionKindFilter</c>)
    /// que aparecen y desaparecen con <c>ShowIf</c> según lo que elijas, dentro del nodo del hook en
    /// el grafo — o sea que para leer cuándo dispara un ítem había que seleccionar el nodo, entender
    /// que <c>EventBus</c> significa "evento del combate" y traducir <c>OnDamageIncoming</c>. Acá se
    /// ve armado y de entrada, con los nombres de <see cref="ItemTriggerCatalog"/>.
    /// </remarks>
    public sealed partial class ItemEditorWindow
    {
        static string[] _triggerLabels;
        List<string> _knownComboIds;

        static string[] TriggerLabels =>
            _triggerLabels ??= ItemTriggerCatalog.All.Select(o => o.DisplayName).ToArray();

        /// <summary>Los combos del proyecto. Cachean: resolverlos escanea assets y esto corre por repaint.</summary>
        List<string> KnownComboIds =>
            _knownComboIds ??= BaseComboSO.GetKnownComboIds().OrderBy(id => id).ToList();

        partial void OnTriggerAssetsRefreshed()
        {
            _knownComboIds = null;
        }

        /// <summary>
        /// Hace que el nodo del hook en el grafo diga "Cuando te pegan" en vez de volcar sus campos.
        /// </summary>
        /// <remarks>
        /// Se registra en el <c>static</c> del tipo y no al abrir la ventana: el grafo se dibuja con
        /// nodos de hooks también cuando el registro no corrió (otra ventana, un dominio recién
        /// recargado), y el describer no depende de que haya una instancia viva.
        /// </remarks>
        static ItemEditorWindow()
        {
            Polymorphic.Graph.BlockNodeDescription.Register<PassiveItemHook>(
                ItemTriggerCatalog.Describe);
        }

        partial void DrawTriggerExtras(ItemSO asset)
        {
            if (asset == null || asset.Type != ItemType.Passive) return;
            if (!DrawExtrasSection("Cuándo")) return;

            var hooks = asset.PassiveHooks;
            if (hooks == null || hooks.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Este ítem no tiene ningún disparador: nunca se ejecuta.", MessageType.Warning);
                if (GUILayout.Button("Agregar disparador")) AddHook(asset);
                return;
            }

            for (int i = 0; i < hooks.Count; i++)
            {
                if (hooks[i] == null) continue;
                if (i > 0) EditorGUILayout.Space(6);
                DrawHookTrigger(hooks[i], hooks.Count > 1 ? i + 1 : 0);
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Agregar disparador")) AddHook(asset);
        }

        void DrawHookTrigger(PassiveItemHook hook, int ordinal)
        {
            var matched = ItemTriggerCatalog.Match(hook);

            if (ordinal > 0)
                EditorGUILayout.LabelField($"Disparador {ordinal}", EditorStyles.miniBoldLabel);

            // Un hook que solo lleva modificadores persistentes no escucha nada: rinde mientras el
            // item este en el inventario. Ofrecer el desplegable ahi seria mentir — elegir un evento
            // no cambiaria cuando se aplica.
            if (ItemTriggerCatalog.IsPermanent(hook))
            {
                EditorGUILayout.LabelField("Dispara cuando", "Mientras lo tengas en el inventario");
                EditorGUILayout.LabelField(" ",
                    "Es un modificador permanente: se aplica al conseguir el ítem y se saca al " +
                    "perderlo. No usa evento.", EditorStyles.miniLabel);
                return;
            }

            // El índice -1 deja el popup en blanco en vez de mentir mostrando la primera opción,
            // que es justo el error que este catálogo viene a hacer visible.
            int current = matched.HasValue ? IndexOf(matched.Value.Id) : -1;
            int next = EditorGUILayout.Popup("Dispara cuando", current, TriggerLabels);
            if (next != current && next >= 0)
            {
                var option = ItemTriggerCatalog.All[next];
                Context.Mutate("Change Item Trigger", () => ItemTriggerCatalog.Apply(hook, option));
            }

            if (!matched.HasValue)
            {
                EditorGUILayout.HelpBox(
                    $"Hoy dispara con '{hook.TriggerEvent}', que ningún ítem puede escuchar: " +
                    "nunca se va a ejecutar. Elegí uno de la lista.", MessageType.Error);
                return;
            }

            var opt = matched.Value;
            EditorGUILayout.LabelField(" ", opt.Help, EditorStyles.miniLabel);

            if (!opt.FiltersByEntity)
                EditorGUILayout.HelpBox(
                    "Este evento no dice a quién le pasó, así que el ítem no puede filtrar por " +
                    "el jugador.", MessageType.Info);

            if (opt.Kind != PassiveHookKind.ComboPlayed) return;

            if (opt.UsesComboIds) DrawComboPicker(hook);
            DrawActionKindFilter(hook);
        }

        /// <summary>
        /// Los combos que escucha el hook, como casillas.
        /// </summary>
        /// <remarks>
        /// Odin lo dibuja como una lista de strings con un <c>ValueDropdown</c>: para sumar un combo
        /// hay que apretar "+", abrir el desplegable del elemento nuevo y elegir. Acá los combos del
        /// proyecto ya están todos a la vista y se prenden de a uno.
        /// </remarks>
        void DrawComboPicker(PassiveItemHook hook)
        {
            hook.ComboFilter ??= new ComboFilter();
            hook.ComboFilter.ComboIds ??= new List<string>();

            var ids = KnownComboIds;
            if (ids.Count == 0)
            {
                EditorGUILayout.HelpBox("No se encontró ningún combo en el proyecto.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Combos", EditorStyles.miniBoldLabel);

            if (hook.ComboFilter.ComboIds.Count == 0)
                EditorGUILayout.HelpBox(
                    "Sin ningún combo tildado no dispara nunca.", MessageType.Warning);

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (var id in ids)
                {
                    bool on = hook.ComboFilter.ComboIds.Contains(id);
                    bool next = EditorGUILayout.ToggleLeft(ShortComboLabel(id), on);
                    if (next == on) continue;

                    var captured = id;
                    Context.Mutate(next ? "Add Item Trigger Combo" : "Remove Item Trigger Combo", () =>
                    {
                        if (next) hook.ComboFilter.ComboIds.Add(captured);
                        else hook.ComboFilter.ComboIds.Remove(captured);
                    });
                }
            }
        }

        /// <summary>
        /// A qué tipo de acción se limita el hook.
        /// </summary>
        /// <remarks>
        /// No es cosmético: ataque, defensa y curación comparten el mismo play scratch, así que un
        /// bono de daño sin restringir se filtra a la curación (BUG-080). El campo existía y se
        /// llamaba <c>ActionKindFilter</c> con <c>Unknown</c> como "sin filtro" — dos cosas que
        /// nadie que no haya escrito el motor va a adivinar.
        /// </remarks>
        void DrawActionKindFilter(PassiveItemHook hook)
        {
            var kinds = new[]
            {
                RollActionKind.Unknown, RollActionKind.Attack,
                RollActionKind.Defense, RollActionKind.Heal,
            };
            var labels = new[] { "Cualquier acción", "Sólo ataques", "Sólo defensas", "Sólo curaciones" };

            int current = System.Array.IndexOf(kinds, hook.ActionKindFilter);
            if (current < 0) current = 0;

            int next = EditorGUILayout.Popup("Limitado a", current, labels);
            if (next == current) return;

            var picked = kinds[next];
            Context.Mutate("Change Item Trigger Action Kind", () => hook.ActionKindFilter = picked);
        }

        void AddHook(ItemSO asset)
        {
            Context.Mutate("Add Item Trigger", () =>
            {
                asset.PassiveHooks ??= new List<PassiveItemHook>();
                var hook = new PassiveItemHook();
                ItemTriggerCatalog.Apply(hook, ItemTriggerCatalog.All[0]);
                asset.PassiveHooks.Add(hook);
            });
        }

        static int IndexOf(string optionId)
        {
            for (int i = 0; i < ItemTriggerCatalog.All.Count; i++)
                if (ItemTriggerCatalog.All[i].Id == optionId) return i;
            return -1;
        }

        /// <summary>"combo.full_house" → "full house". El prefijo es igual en todos y sólo gasta ancho.</summary>
        static string ShortComboLabel(string id) =>
            string.IsNullOrEmpty(id) ? id : id.Replace("combo.", string.Empty).Replace('_', ' ');
    }
}
