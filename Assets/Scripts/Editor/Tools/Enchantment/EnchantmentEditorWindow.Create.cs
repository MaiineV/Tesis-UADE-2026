using System;
using System.Collections.Generic;
using System.Linq;
using Rollgeon.Combos;
using Rollgeon.Dice;
using Rollgeon.Editor.Tools.Item;
using Rollgeon.Upgrades.Dice;
using Rollgeon.Upgrades.Dice.Triggers;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enchantment
{
    /// <summary>
    /// Asistente de alta (espejo del de items, §6.2 del item-editor-spec adaptado al canal dados).
    /// Intercepta Create/Duplicate/Delete del shell para que todo camino de escritura pase por
    /// <see cref="EnchantmentAuthoring"/>: un alta siempre nace con Display Name real, id derivado
    /// con colisión chequeada, categoría obligatoria y entry del pool — nunca un stub
    /// <c>Ench_New</c> vacío ni una copia que comparte id con su fuente.
    /// </summary>
    public sealed partial class EnchantmentEditorWindow
    {
        // ---- BlockEditorWindow<EnchantmentSO> hooks ---------------------------------------------

        protected override bool TryBeginCreate()
        {
            EnchantmentCreationWizard.Open(this, null);
            return true;
        }

        /// <summary>
        /// Duplicar copia el id textual — la única forma confiable de terminar con dos assets
        /// compartiendo id. En vez de copiar el archivo, abre el asistente precargado con los
        /// datos de la fuente y el nombre vacío: la estructura de efectos no viaja (se autora en
        /// el grafo después), pero el id, el catálogo, el pool y la localización nacen bien.
        /// </summary>
        protected override bool TryBeginDuplicate(EnchantmentSO source)
        {
            if (source == null) return false;
            EnchantmentCreationWizard.Open(this, source);
            return true;
        }

        /// <summary>
        /// El delete del shell solo borra el archivo; el alta escribió además catálogo, pool y dos
        /// claves de localización, y huérfanas quedan invisibles hasta la próxima auditoría. Todo
        /// pasa por <see cref="EnchantmentAuthoring.DeleteEnchantment"/>, que limpia en el orden
        /// correcto (catálogo y pool localizan la entry por referencia — con el asset borrado ya
        /// no hay con qué encontrarla).
        /// </summary>
        protected override bool TryBeginDelete(EnchantmentSO selected)
        {
            if (selected == null) return true;

            if (!EditorUtility.DisplayDialog(
                    "Borrar encantamiento",
                    $"¿Borrar '{LabelOf(selected)}'?\n\nNO se puede deshacer (Ctrl+Z no lo trae de " +
                    "vuelta). Además del asset se limpian: la entrada del EnchantmentCatalog, la " +
                    "entry del pool del altar y las dos claves de localización (name/desc).",
                    "Borrar", "Cancelar")) return true;

            var result = EnchantmentAuthoring.DeleteEnchantment(selected);
            if (!result.Success)
            {
                EditorUtility.DisplayDialog("No se pudo borrar", result.ErrorMessage, "OK");
                return true;
            }

            RefreshAndSelect(null);
            return true;
        }

        // ---- wizard callbacks -------------------------------------------------------------------

        /// <summary>Devuelve el control al shell cuando <see cref="EnchantmentAuthoring.CreateEnchantment"/> terminó bien.</summary>
        internal void OnWizardEnchantmentCreated(EnchantmentSO enchantment)
        {
            RefreshAndSelect(enchantment);
            Focus();
            LogUndoCaveat();
        }

        /// <summary>El id cambió en disco y en las tablas de localización: rebuild + reselección.</summary>
        internal void OnIdRenamed(EnchantmentSO enchantment)
        {
            RefreshAndSelect(enchantment);
            Focus();
        }

        /// <summary>
        /// Límite medido del undo group del alta (item-editor-spec §7.1): Ctrl+Z revierte catálogo,
        /// pool y claves de localización, pero Unity nunca pone <c>AssetDatabase.CreateAsset</c> en
        /// el stack — el <c>.asset</c> sobrevive huérfano y hay que borrarlo a mano.
        /// </summary>
        static void LogUndoCaveat() =>
            Debug.Log(
                "[Enchantment Editor] Creado. Ojo: Ctrl+Z deshace las escrituras de " +
                "catálogo/pool/localización pero NO borra el archivo .asset (limitación de Unity) " +
                "— deshacer un alta deja un asset huérfano para borrar a mano.");

        // ---- sección Id (root extras) -----------------------------------------------------------

        /// <summary>
        /// El id, visible y renombrable con su flujo propio — editarlo suelto en Raw Data dejaría
        /// las claves de localización apuntando al id viejo.
        /// </summary>
        partial void DrawIdExtras(EnchantmentSO asset)
        {
            if (!DrawExtrasSection("Id")) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    "UpgradeId",
                    string.IsNullOrEmpty(asset.UpgradeId) ? "(vacío)" : asset.UpgradeId);
                if (GUILayout.Button("Renombrar…", GUILayout.Width(92f)))
                    EnchantmentRenameIdPrompt.Open(this, asset);
            }

            DrawHelp("El id es clave de save: renombrarlo rompe las partidas guardadas que lo tengan.");
        }
    }

    // ==============================================================================================
    // id preview compartido
    // ==============================================================================================

    /// <summary>
    /// Feedback vivo del id (espejo de <c>ItemIdPreview</c>): mientras se tipea el Display Name
    /// muestra el id que va a derivar y marca al instante si ya está tomado, nombrando al dueño.
    /// </summary>
    static class EnchantmentIdPreview
    {
        // Snapshot de ids tomado al abrir el asistente, más un memo del último nombre consultado:
        // consultar disco por tecla es un FindAssets + carga de cada EnchantmentSO por repaint.
        // El alta revalida contra disco antes de escribir, que es donde la respuesta tiene que ser
        // correcta — este aviso puede quedar viejo unos segundos sin consecuencia.
        static Dictionary<string, EnchantmentSO> _idOwners;
        static string _lastDisplayName;
        static string _lastId;
        static bool _lastAvailable;
        static string _lastOwnerLabel;

        /// <summary>Vuelve a tomar la foto de ids. La llaman los asistentes al abrirse.</summary>
        public static void Refresh()
        {
            _idOwners = EnchantmentAuthoring.BuildIdOwnerSnapshot();
            _lastDisplayName = null;
        }

        public static void Draw(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                EditorGUILayout.HelpBox("Type a Display Name to see the id it will get.", MessageType.None);
                return;
            }

            if (displayName != _lastDisplayName)
            {
                _lastDisplayName = displayName;
                if (_idOwners == null) Refresh();

                _lastId = EnchantmentIdSlug.FromDisplayName(displayName);

                EnchantmentSO owner = null;
                _lastAvailable = !string.IsNullOrEmpty(_lastId)
                                 && !_idOwners.TryGetValue(_lastId, out owner);
                _lastOwnerLabel = owner == null ? "<unknown>" : EnchantmentQuery.LabelOf(owner);
            }

            if (string.IsNullOrEmpty(_lastId))
            {
                EditorGUILayout.HelpBox(
                    "This Display Name doesn't derive a usable id (only separators/symbols).",
                    MessageType.Error);
                return;
            }

            if (_lastAvailable)
            {
                EditorGUILayout.HelpBox($"Id: {_lastId}", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox($"Id '{_lastId}' is already used by '{_lastOwnerLabel}'.", MessageType.Error);
        }
    }

    // ==============================================================================================
    // "+ Create" / "Duplicate" wizard
    // ==============================================================================================

    /// <summary>
    /// Pide nombre, descripción (es/en), icono, categoría, dados compatibles, peso/piso del pool y
    /// el "cuándo". Confirm llama a <see cref="EnchantmentAuthoring.CreateEnchantment"/> una sola
    /// vez — las cuatro escrituras caen en el undo group del servicio, esta ventana nunca toca
    /// assets por su cuenta.
    /// </summary>
    /// <remarks>
    /// Deltas contra el asistente de items: no hay rareza/precio (el dial de balance del altar es
    /// peso + piso mínimo), no hay familias (la agrupación es la categoría del GDD, obligatoria —
    /// la auditoría rechaza <c>None</c>), y el disparador admite "sin disparador" porque un
    /// encantamiento de solo-FaceFilter o solo-capabilities es válido.
    /// </remarks>
    sealed class EnchantmentCreationWizard : EditorWindow
    {
        EnchantmentEditorWindow _owner;

        string _displayName = string.Empty;
        string _description = string.Empty;

        // Inglés opcional. Los tests de localización exigen valor en los dos idiomas Y que
        // difieran: sembrar el español en ambos deja la suite roja hasta traducir. Completarlo acá
        // evita esa deuda de entrada; dejarlo vacío sigue siendo válido y el test lo recuerda.
        bool _showEnglish;
        string _displayNameEn = string.Empty;
        string _descriptionEn = string.Empty;

        Sprite _icon;
        EnchantmentCategory _category = EnchantmentCategory.None;
        readonly List<DiceType> _allowedDiceTypes = new List<DiceType>();
        float _poolWeight = 1f;
        int _minFloorDepth;
        string _targetFolder;

        // null = sin disparador (válido: solo-FaceFilter / solo-capabilities).
        string _triggerId = EnchantmentTriggerCatalog.All[0].Id;
        readonly List<string> _triggerComboIds = new List<string>();
        bool _requireCarrierParticipates;

        List<string> _errors;
        Vector2 _scroll;

        /// <param name="prefillFrom">
        /// Null para un "+ Create" fresco. No-null para Duplicate: copia descripción, icono,
        /// categoría, dados, entry del pool y el disparador como punto de partida, pero deja el
        /// Display Name vacío — un duplicado no se confirma sin nombre nuevo.
        /// </param>
        public static void Open(EnchantmentEditorWindow owner, EnchantmentSO prefillFrom)
        {
            EnchantmentIdPreview.Refresh();
            var w = CreateInstance<EnchantmentCreationWizard>();
            w.titleContent = new GUIContent(prefillFrom == null ? "New Enchantment" : "Duplicate → New Enchantment");
            w._owner = owner;
            w._targetFolder = EnchantmentAuthoring.DefaultFolder;

            if (prefillFrom != null) w.Prefill(prefillFrom);

            w.minSize = new Vector2(460f, 480f);
            w.ShowUtility();
        }

        void Prefill(EnchantmentSO source)
        {
            _description = source.Description ?? string.Empty;
            _icon = source.Icon;
            _category = source.Category;
            if (source.AllowedDiceTypes != null) _allowedDiceTypes.AddRange(source.AllowedDiceTypes);

            var folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(source))?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder)) _targetFolder = folder;

            var pool = EnchantmentPoolBridge.LoadDefaultPool();
            if (pool != null && EnchantmentPoolBridge.TryGetWeight(pool, source, out float weight))
            {
                _poolWeight = weight;
                if (EnchantmentPoolBridge.TryGetMinFloorDepth(pool, source, out int minFloor))
                    _minFloorDepth = minFloor;
            }

            // Solo el primer puente: la estructura de efectos no viaja por el asistente (se autora
            // en el grafo), pero el "cuándo" sí es punto de partida útil.
            _triggerId = null;
            if (source.Triggers != null)
            {
                foreach (var trigger in source.Triggers)
                {
                    if (trigger is not ExecuteEffectsOnDiceEvent bridge) continue;
                    var matched = EnchantmentTriggerCatalog.Match(bridge);
                    if (matched == null) continue;

                    _triggerId = matched.Value.Id;
                    if (matched.Value.UsesComboIds && bridge.Filter?.ComboIds != null)
                        _triggerComboIds.AddRange(bridge.Filter.ComboIds);
                    _requireCarrierParticipates = bridge.RequireCarrierParticipates;
                    break;
                }
            }
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.Space(4);

            _displayName = EditorGUILayout.TextField("Display Name", _displayName);
            EnchantmentIdPreview.Draw(_displayName);

            EditorGUILayout.LabelField("Description");
            _description = EditorGUILayout.TextArea(_description, GUILayout.MinHeight(48f));

            DrawEnglishFields();

            _icon = (Sprite)EditorGUILayout.ObjectField("Icon", _icon, typeof(Sprite), false);

            DrawCategory();
            DrawAllowedDiceTypes();
            DrawPoolFields();
            DrawTrigger();
            DrawFolderField();

            DrawErrors();

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6);
            DrawFooter();
        }

        /// <summary>
        /// Plegado por defecto para no estorbar el camino rápido. El aviso de que queda por
        /// traducir es deliberado: dejarlo vacío es una decisión con consecuencia (el test de
        /// localización lo marca), no un descuido silencioso.
        /// </summary>
        void DrawEnglishFields()
        {
            _showEnglish = EditorGUILayout.Foldout(_showEnglish, "English (optional)", true);
            if (!_showEnglish)
            {
                if (string.IsNullOrWhiteSpace(_displayNameEn) && string.IsNullOrWhiteSpace(_descriptionEn))
                    EditorGUILayout.LabelField(" ", "Sin traducir — se usa el texto en español.", EditorStyles.miniLabel);
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                _displayNameEn = EditorGUILayout.TextField("Display Name (EN)", _displayNameEn);
                EditorGUILayout.LabelField("Description (EN)");
                _descriptionEn = EditorGUILayout.TextArea(_descriptionEn, GUILayout.MinHeight(40f));

                if (string.IsNullOrWhiteSpace(_displayNameEn) || string.IsNullOrWhiteSpace(_descriptionEn))
                    EditorGUILayout.HelpBox(
                        "Lo que dejes vacío se siembra con el texto en español y queda como deuda "
                        + "de traducción: el test de localización lo va a marcar.",
                        MessageType.Info);
            }
        }

        // None no se ofrece: solo existe como estado inicial que bloquea el botón — la auditoría
        // (AllEnchantmentAssets_HaveACategoryAssigned) rechaza assets sin categoría, así que el
        // formulario no deja ni elegirla.
        static EnchantmentCategory[] _categoryValues;
        static string[] _categoryLabels;

        static EnchantmentCategory[] CategoryValues =>
            _categoryValues ??= ((EnchantmentCategory[])Enum.GetValues(typeof(EnchantmentCategory)))
                .Where(c => c != EnchantmentCategory.None)
                .ToArray();

        static string[] CategoryLabels =>
            _categoryLabels ??= CategoryValues
                .Select(EnchantmentEditorWindow.CategoryLabelOf)
                .ToArray();

        void DrawCategory()
        {
            int current = Array.IndexOf(CategoryValues, _category); // -1 mientras siga en None
            int next = EditorGUILayout.Popup("Categoría", current, CategoryLabels);
            if (next >= 0) _category = CategoryValues[next];

            if (_category == EnchantmentCategory.None)
                EditorGUILayout.HelpBox(
                    "Elegí una categoría — es obligatoria (la auditoría rechaza None).",
                    MessageType.Info);
        }

        void DrawAllowedDiceTypes()
        {
            EditorGUILayout.LabelField("Dados compatibles", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (DiceType type in Enum.GetValues(typeof(DiceType)))
                {
                    bool on = _allowedDiceTypes.Contains(type);
                    bool next = GUILayout.Toggle(on, type.ToString(), EditorStyles.miniButton);
                    if (next == on) continue;
                    if (next) _allowedDiceTypes.Add(type);
                    else _allowedDiceTypes.Remove(type);
                }
            }
            EditorGUILayout.LabelField(
                "Ninguno marcado = aplica a todos los tipos.", EditorStyles.miniLabel);
        }

        void DrawPoolFields()
        {
            // El eje económico del canal: acá no hay precio por asset (el costo del altar es
            // global), el dial de balance es peso de aparición + piso mínimo.
            _poolWeight = Mathf.Max(0f, EditorGUILayout.FloatField("Peso en el pool", _poolWeight));
            if (Mathf.Approximately(_poolWeight, 0f))
                EditorGUILayout.LabelField(
                    " ", "0 = registrado pero deshabilitado (no se ofrece).", EditorStyles.miniLabel);

            _minFloorDepth = Mathf.Max(0, EditorGUILayout.IntField("Piso mínimo", _minFloorDepth));
        }

        // "(Sin disparador)" delante del catálogo: un encantamiento de solo-FaceFilter o
        // solo-capabilities nace válido sin triggers, y la spec lo admite con TriggerId vacío.
        static string[] _triggerLabels;
        static string[] TriggerLabels =>
            _triggerLabels ??= new[] { "Sin disparador (solo caras / capabilities)" }
                .Concat(EnchantmentTriggerCatalog.All.Select(o => o.DisplayName))
                .ToArray();

        List<string> _knownCombos;
        List<string> KnownComboIds =>
            _knownCombos ??= BaseComboSO.GetKnownComboIds().OrderBy(id => id).ToList();

        /// <summary>
        /// El "cuándo" al crear, no después: el autor ya decidió cuándo quiere que dispare, y
        /// mandarlo al grafo a armar el trigger a mano era el paso que el asistente vino a borrar.
        /// </summary>
        void DrawTrigger()
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Dispara cuando", EditorStyles.miniBoldLabel);

            int current = _triggerId == null ? 0 : IndexOfTrigger(_triggerId) + 1;
            int next = EditorGUILayout.Popup(current, TriggerLabels);
            if (next != current && next >= 0)
            {
                _triggerId = next == 0 ? null : EnchantmentTriggerCatalog.All[next - 1].Id;
                _triggerComboIds.Clear();
                _requireCarrierParticipates = false;
            }

            if (_triggerId == null)
            {
                EditorGUILayout.LabelField(
                    "Nace sin triggers: solo actúan el filtro de caras y las capabilities que se " +
                    "autoren después.", HelpStyle);
                return;
            }

            var option = EnchantmentTriggerCatalog.All[IndexOfTrigger(_triggerId)];
            EditorGUILayout.LabelField(option.Help, HelpStyle);

            if (option.ScratchOnly)
                EditorGUILayout.HelpBox(
                    "Hook de preview (BUG-017): re-dispara en cada toggle de hold. Solo efectos " +
                    "scratch-writer (EffAddComboBonus y afines) — un apply directo (oro, escudo, " +
                    "curación) acá es farmeable infinito y la auditoría lo rechaza.",
                    MessageType.Warning);

            if (option.UsesComboIds)
            {
                var ids = KnownComboIds;
                ItemComboPicker.Draw(ids, _triggerComboIds,
                    (id, on) => { if (on) _triggerComboIds.Add(id); else _triggerComboIds.Remove(id); },
                    all => { _triggerComboIds.Clear(); if (all) _triggerComboIds.AddRange(ids); });

                if (_triggerComboIds.Count == 0)
                    EditorGUILayout.HelpBox(
                        "Sin ningún combo elegido no dispara nunca.", MessageType.Warning);
            }

            bool isComboHook = option.Event == EnchantmentHookEvent.ComboMatched
                            || option.Event == EnchantmentHookEvent.ComboPlayed;
            if (isComboHook)
                _requireCarrierParticipates = EditorGUILayout.ToggleLeft(
                    "Sólo si el dado encantado participa del combo", _requireCarrierParticipates);
        }

        static GUIStyle _helpStyle;
        static GUIStyle HelpStyle =>
            _helpStyle ??= new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };

        static int IndexOfTrigger(string id)
        {
            for (int i = 0; i < EnchantmentTriggerCatalog.All.Count; i++)
                if (EnchantmentTriggerCatalog.All[i].Id == id) return i;
            return -1;
        }

        void DrawFolderField()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _targetFolder = EditorGUILayout.TextField("Target Folder", _targetFolder);
                if (GUILayout.Button("...", GUILayout.Width(28f)))
                {
                    var picked = EditorUtility.OpenFolderPanel("Target Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(picked) && picked.StartsWith(Application.dataPath))
                        _targetFolder = "Assets" + picked.Substring(Application.dataPath.Length);
                }
            }
        }

        void DrawErrors()
        {
            if (_errors == null) return;
            foreach (var e in _errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        }

        void DrawFooter()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cancel", GUILayout.Height(24f))) Close();

                bool canConfirm = !string.IsNullOrWhiteSpace(_displayName)
                                  && _category != EnchantmentCategory.None;

                using (new EditorGUI.DisabledScope(!canConfirm))
                {
                    if (GUILayout.Button("Create", GUILayout.Height(24f))) Confirm();
                }
            }
        }

        /// <remarks>
        /// Arma la spec y se la da a <see cref="EnchantmentAuthoring"/> — sin validación duplicada
        /// más allá de lo que bloquea el botón (nombre vacío, categoría None): el servicio es la
        /// única fuente de verdad de derivación de id, unicidad y carpeta, y devuelve
        /// <c>Errors</c> para todo eso.
        /// </remarks>
        void Confirm()
        {
            _errors = null;

            var spec = new EnchantmentCreationSpec
            {
                DisplayName = _displayName,
                Description = _description,
                DisplayNameEn = _displayNameEn,
                DescriptionEn = _descriptionEn,
                Icon = _icon,
                Category = _category,
                AllowedDiceTypes = _allowedDiceTypes.Count > 0
                    ? new List<DiceType>(_allowedDiceTypes)
                    : null,
                PoolWeight = _poolWeight,
                MinFloorDepth = _minFloorDepth,
                TargetFolder = string.IsNullOrWhiteSpace(_targetFolder) ? null : _targetFolder,
                TriggerId = _triggerId,
                TriggerComboIds = _triggerComboIds.Count > 0
                    ? new List<string>(_triggerComboIds)
                    : null,
                RequireCarrierParticipates = _requireCarrierParticipates,
            };

            var result = EnchantmentAuthoring.CreateEnchantment(spec);
            if (!result.Success)
            {
                _errors = new List<string>(result.Errors);
                return;
            }

            _owner.OnWizardEnchantmentCreated(result.Enchantment);
            Close();
        }
    }

    // ==============================================================================================
    // rename de id
    // ==============================================================================================

    /// <summary>
    /// Prompt para renombrar el <c>UpgradeId</c>. Acción explícita y separada de editar el Display
    /// Name porque también mueve las dos claves de localización — y porque rompe saves, cosa que
    /// el diálogo de confirmación dice antes de comprometerse.
    /// </summary>
    sealed class EnchantmentRenameIdPrompt : EditorWindow
    {
        EnchantmentEditorWindow _owner;
        EnchantmentSO _asset;
        string _newId;
        string _error;

        public static void Open(EnchantmentEditorWindow owner, EnchantmentSO asset)
        {
            var w = CreateInstance<EnchantmentRenameIdPrompt>();
            w.titleContent = new GUIContent("Renombrar id");
            w._owner = owner;
            w._asset = asset;
            w._newId = asset.UpgradeId ?? EnchantmentIdSlug.Prefix;
            w.minSize = new Vector2(420f, 170f);
            w.ShowUtility();
        }

        void OnGUI()
        {
            if (_asset == null) { Close(); return; }

            EditorGUILayout.LabelField("Id actual", _asset.UpgradeId ?? "(vacío)");
            _newId = EditorGUILayout.TextField("Nuevo id", _newId);
            EditorGUILayout.LabelField(
                $"Debe conservar el prefijo de canal '{EnchantmentIdSlug.Prefix}'.",
                EditorStyles.miniLabel);

            if (_error != null) EditorGUILayout.HelpBox(_error, MessageType.Error);

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cancelar", GUILayout.Height(24f))) Close();

                bool canConfirm = !string.IsNullOrWhiteSpace(_newId) && _newId != _asset.UpgradeId;
                using (new EditorGUI.DisabledScope(!canConfirm))
                    if (GUILayout.Button("Renombrar", GUILayout.Height(24f))) Confirm();
            }
        }

        /// <remarks>
        /// La validación fina (prefijo, unicidad) es del servicio — acá solo se avisa lo que el
        /// servicio no puede: qué se rompe. El diccionario de <c>EnchantmentCategoryAssigner</c> y
        /// las definiciones de meta-unlock apuntan por id y quedan a cargo de quien renombra.
        /// </remarks>
        void Confirm()
        {
            _error = null;

            if (!EditorUtility.DisplayDialog(
                    "Renombrar id",
                    $"¿Renombrar '{_asset.UpgradeId}' → '{_newId}'?\n\nROMPE los saves existentes: " +
                    "los slots del RuntimeDiceBag se restauran por id y descartan los " +
                    "desconocidos.\n\nTambién hay que actualizar a mano el diccionario de " +
                    "EnchantmentCategoryAssigner y las definiciones de meta-unlock que apunten al " +
                    "id viejo.",
                    "Renombrar", "Cancelar")) return;

            var result = EnchantmentAuthoring.RenameEnchantmentId(_asset, _newId);
            if (!result.Success)
            {
                _error = result.ErrorMessage;
                return;
            }

            _owner.OnIdRenamed(_asset);
            Close();
        }
    }
}
