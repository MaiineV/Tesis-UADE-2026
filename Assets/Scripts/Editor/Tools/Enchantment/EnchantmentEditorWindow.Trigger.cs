using System.Collections.Generic;
using System.Linq;
using Rollgeon.Combos;
using Rollgeon.Editor.Tools.Item;
using Rollgeon.Editor.Tools.Polymorphic;
using Rollgeon.Upgrades.Dice;
using Rollgeon.Upgrades.Dice.Triggers;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enchantment
{
    /// <summary>
    /// La sección "Cuándo" del panel del encantamiento: cuándo dispara, en castellano y editable.
    /// </summary>
    /// <remarks>
    /// El "cuándo" vive repartido en <c>Event</c> + <c>Filter.Mode</c> + <c>ComboIds</c> +
    /// <c>RequireCarrierParticipates</c> dentro del nodo del trigger en el grafo — para leerlo hay
    /// que seleccionar el nodo y saber que <c>ComboMatched</c> es preview. Acá se ve armado y de
    /// entrada, con los nombres y las trampas (BUG-017) de <see cref="EnchantmentTriggerCatalog"/>.
    /// </remarks>
    public sealed partial class EnchantmentEditorWindow
    {
        static string[] _triggerLabels;
        List<string> _knownComboIds;

        static string[] TriggerLabels =>
            _triggerLabels ??= EnchantmentTriggerCatalog.All.Select(o => o.DisplayName).ToArray();

        /// <summary>Los combos del proyecto. Cachean: resolverlos escanea assets y esto corre por repaint.</summary>
        List<string> KnownComboIds =>
            _knownComboIds ??= BaseComboSO.GetKnownComboIds().OrderBy(id => id).ToList();

        partial void OnTriggerAssetsRefreshed()
        {
            _knownComboIds = null;
        }

        /// <summary>
        /// Hace que el nodo del trigger en el grafo diga "Cuando jugás cualquier combo" en vez de
        /// volcar sus campos.
        /// </summary>
        /// <remarks>
        /// Se registra en el <c>static</c> del tipo y no al abrir la ventana: el grafo se dibuja
        /// con nodos de triggers también cuando el registro no corrió (otra ventana, un dominio
        /// recién recargado), y el describer no depende de que haya una instancia viva.
        /// </remarks>
        static EnchantmentEditorWindow()
        {
            Polymorphic.Graph.BlockNodeDescription.Register<ExecuteEffectsOnDiceEvent>(
                EnchantmentTriggerCatalog.Describe);
        }

        // La sección de id vive en EnchantmentEditorWindow.Create.cs (es asunto de CRUD). Como
        // DrawRootExtras es un override y solo puede existir una vez por clase, entra por acá —
        // mismo patrón de dispatch que OnTriggerAssetsRefreshed.
        partial void DrawIdExtras(EnchantmentSO asset);

        protected override void DrawRootExtras(EnchantmentSO asset)
        {
            if (asset == null) return;

            EditorGUILayout.Space(2);
            if (DrawExtrasSection("Cuándo")) DrawTriggerSection(asset);
            DrawIdExtras(asset);
        }

        /// <summary>
        /// Cabecera plegable con el mismo aspecto y la misma persistencia que las categorías que
        /// vienen del <c>[Title]</c> de Odin — estas secciones no tienen <c>[Title]</c> del que
        /// colgarse, así que dibujan su propio título por el mismo helper.
        /// </summary>
        static bool DrawExtrasSection(string title)
        {
            var key = PolymorphicBlockDrawer.SectionKeyOf(nameof(EnchantmentSO), title);
            bool expanded = EditorPrefs.GetBool(key, true);
            bool next = PolymorphicBlockDrawer.SectionToggle(title, expanded, drawOwnTitle: true);
            if (next != expanded) EditorPrefs.SetBool(key, next);
            return next;
        }

        void DrawTriggerSection(EnchantmentSO asset)
        {
            var triggers = asset.Triggers;
            if (triggers == null || triggers.Count == 0)
            {
                // Info y no warning: un encantamiento de solo-FaceFilter o solo-capabilities es
                // válido — la salud del catálogo ya marca los que no hacen nada de nada.
                EditorGUILayout.HelpBox(
                    "Sin disparadores: solo actúan el filtro de caras, las capabilities y los " +
                    "stat grants.", MessageType.Info);
                if (GUILayout.Button("Agregar disparador")) AddTrigger(asset);
                return;
            }

            for (int i = 0; i < triggers.Count; i++)
            {
                if (triggers[i] == null) continue;
                if (i > 0) EditorGUILayout.Space(6);

                int ordinal = triggers.Count > 1 ? i + 1 : 0;
                if (triggers[i] is ExecuteEffectsOnDiceEvent bridge)
                {
                    DrawBridgeTrigger(bridge, ordinal);
                }
                else
                {
                    // Concretes legacy pre-puente: el catálogo no los describe, se editan en el grafo.
                    if (ordinal > 0)
                        EditorGUILayout.LabelField($"Disparador {ordinal}", EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField(triggers[i].GetType().Name);
                    DrawHelp("Trigger dedicado (no es el puente genérico): su configuración se " +
                             "edita en su nodo del grafo.");
                }
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Agregar disparador")) AddTrigger(asset);
        }

        void DrawBridgeTrigger(ExecuteEffectsOnDiceEvent bridge, int ordinal)
        {
            var matched = EnchantmentTriggerCatalog.Match(bridge);

            if (ordinal > 0)
                EditorGUILayout.LabelField($"Disparador {ordinal}", EditorStyles.miniBoldLabel);

            EditorGUILayout.LabelField("Dispara cuando", EditorStyles.miniBoldLabel);

            // El desplegable va a lo ancho y con el título arriba: las frases del catálogo son
            // oraciones enteras y en la columna de valor de un panel angosto entraba la mitad.
            int current = matched.HasValue ? IndexOfTrigger(matched.Value.Id) : -1;
            int next = EditorGUILayout.Popup(current, TriggerLabels);
            if (next != current && next >= 0)
            {
                var option = EnchantmentTriggerCatalog.All[next];
                Context.Mutate("Change Enchantment Trigger",
                    () => EnchantmentTriggerCatalog.Apply(bridge, option));
            }

            if (!matched.HasValue)
            {
                EditorGUILayout.HelpBox(
                    $"Configuración fuera del catálogo ({bridge.Event}, Filter={bridge.Filter?.Mode}). " +
                    "Elegí una opción de la lista para normalizarla.", MessageType.Error);
                return;
            }

            var opt = matched.Value;
            DrawHelp(opt.Help);

            if (opt.ScratchOnly)
                EditorGUILayout.HelpBox(
                    "Hook de preview (BUG-017): re-dispara en cada toggle de hold. Solo efectos " +
                    "scratch-writer (EffAddComboBonus y afines) — un apply directo (oro, escudo, " +
                    "curación) acá es farmeable infinito y la auditoría lo rechaza.",
                    MessageType.Warning);

            bool isComboHook = opt.Event == EnchantmentHookEvent.ComboMatched
                            || opt.Event == EnchantmentHookEvent.ComboPlayed;
            if (!isComboHook) return;

            if (opt.UsesComboIds) DrawComboPicker(bridge);
            DrawCarrierToggle(bridge);
        }

        /// <summary>
        /// Los combos que escucha el trigger, en el desplegable de selección múltiple compartido
        /// con el canal items (<see cref="ItemComboPicker"/> — trabaja sobre ids pelados, no sabe
        /// nada de items).
        /// </summary>
        void DrawComboPicker(ExecuteEffectsOnDiceEvent bridge)
        {
            bridge.Filter ??= new ComboFilter();
            bridge.Filter.ComboIds ??= new List<string>();

            var ids = KnownComboIds;
            if (ids.Count == 0)
            {
                EditorGUILayout.HelpBox("No se encontró ningún combo en el proyecto.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Combos", EditorStyles.miniBoldLabel);

            var selected = bridge.Filter.ComboIds;

            // Se escribe con RecordUndo + MarkDirty pero SIN Notify: notificar reconstruye el
            // grafo, y hacerlo en cada tilde con el popup abierto es churn que además puede
            // robarle el foco y cerrarlo. El grafo se entera una sola vez, al cerrar.
            ItemComboPicker.Draw(ids, selected,
                (id, on) =>
                {
                    Context.RecordUndo(on ? "Add Enchantment Trigger Combo" : "Remove Enchantment Trigger Combo");
                    if (on) { if (!selected.Contains(id)) selected.Add(id); }
                    else selected.Remove(id);
                    Context.MarkDirty();
                },
                all =>
                {
                    Context.RecordUndo("Set Enchantment Trigger Combos");
                    selected.Clear();
                    if (all) selected.AddRange(ids);
                    Context.MarkDirty();
                },
                () => Context.Notify());

            if (selected.Count == 0)
                EditorGUILayout.HelpBox(
                    "Sin ningún combo elegido no dispara nunca.", MessageType.Warning);
        }

        /// <summary>
        /// El gate del carrier, con nombre de diseño.
        /// </summary>
        /// <remarks>
        /// No es cosmético: los efectos que leen la cara del dado portador (<c>PcCarrierFace</c>)
        /// exigen este flag — sin él, el gate no filtra por el combo real y la auditoría lo marca
        /// como error.
        /// </remarks>
        void DrawCarrierToggle(ExecuteEffectsOnDiceEvent bridge)
        {
            bool current = bridge.RequireCarrierParticipates;
            bool next = EditorGUILayout.ToggleLeft(
                "Sólo si el dado encantado participa del combo", current);
            if (next != current)
                Context.Mutate("Toggle Carrier Participates",
                    () => bridge.RequireCarrierParticipates = next);
        }

        void AddTrigger(EnchantmentSO asset)
        {
            Context.Mutate("Add Enchantment Trigger", () =>
            {
                var bridge = new ExecuteEffectsOnDiceEvent();
                EnchantmentTriggerCatalog.Apply(bridge, EnchantmentTriggerCatalog.All[0]);
                asset.EditorAddTrigger(bridge);
            });
        }

        /// <summary>
        /// La línea de ayuda, a todo el ancho y envolviendo — en la columna de valor de un panel
        /// angosto, <c>miniLabel</c> sin wrap se corta a la mitad de la primera frase.
        /// </summary>
        static void DrawHelp(string text)
        {
            _triggerHelpStyle ??= new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            EditorGUILayout.LabelField(text, _triggerHelpStyle);
        }

        static GUIStyle _triggerHelpStyle;

        static int IndexOfTrigger(string optionId)
        {
            for (int i = 0; i < EnchantmentTriggerCatalog.All.Count; i++)
                if (EnchantmentTriggerCatalog.All[i].Id == optionId) return i;
            return -1;
        }
    }
}
