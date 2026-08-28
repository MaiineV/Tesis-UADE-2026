using System;
using System.Collections.Generic;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Entities;
using Rollgeon.Localization;
using Rollgeon.Tiles;
using Rollgeon.UI.HUD.Status;
using Rollgeon.UI.Tooltips;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rollgeon.EditorTools.HUD
{
    /// <summary>
    /// Dibuja el panel de hover de un enemigo en el Game view sin entrar a play mode.
    /// </summary>
    /// <remarks>
    /// Existe porque el panel sólo se veía llegando al bicho en una run, y el Croupier está al
    /// final del piso 1: iterar el diseño costaba una partida por cambio.
    /// <para>
    /// Lo único inventado es el contexto de IA. El nombre, la familia, los intents, su reparto
    /// entre columna y costado, el texto de cada tarjeta y su daño salen del mismo dato y del
    /// mismo código que corre en combate — <see cref="AIIntentWalker"/> y
    /// <see cref="EnemyStatusIconsView.AddIfOwn"/>. Si el preview miente, miente el juego.
    /// </para>
    /// </remarks>
    public static class EnemyPanelPreviewTools
    {
        private const string CroupierPath = "Assets/Rollgeon/Enemies/ED_Boss_Croupier.asset";
        private const string TooltipPrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_Tooltip.prefab";

        // Temp/ está en el .gitignore: es un volcado de diagnóstico, no un asset.
        private const string DumpPath = "Temp/tooltip-layout.txt";

        [MenuItem("Rollgeon/Tooltips/Preview Enemy Panel")]
        public static void Preview() => Preview(withSampleStates: false);

        /// <summary>
        /// El panel con la columna del costado poblada.
        /// </summary>
        /// <remarks>
        /// Hace falta un menú aparte porque los estados aplicados los publican providers que
        /// preguntan por servicios vivos —<c>IStunService</c>, las casillas del piso— y fuera de
        /// combate ninguno tiene nada que decir. Sin esto la columna que motivó todo el trabajo
        /// es la única parte del panel que no se puede mirar sin llegar al jefe en una run.
        /// </remarks>
        [MenuItem("Rollgeon/Tooltips/Preview Enemy Panel + Standing Effects")]
        public static void PreviewWithStates() => Preview(withSampleStates: true);

        private static void Preview(bool withSampleStates)
        {
            var data = Selection.activeObject as EnemyDataSO
                       ?? AssetDatabase.LoadAssetAtPath<EnemyDataSO>(CroupierPath);
            if (data == null)
            {
                Debug.LogWarning("[EnemyPanelPreview] No hay EnemyDataSO seleccionado y falta " +
                                 CroupierPath + ".");
                return;
            }

            var controller = ResolveController();
            if (controller == null)
            {
                Debug.LogWarning("[EnemyPanelPreview] No hay TooltipController en la escena y no " +
                                 "se pudo cargar " + TooltipPrefabPath + ".");
                return;
            }

            var attack = new List<StatusIconState>();
            var applied = new List<StatusIconState>();
            CollectCards(data, attack, applied);
            if (withSampleStates) AddSampleStates(data, applied);

            var content = BuildContent(data, attack, applied);

            // Las tarjetas de la vuelta anterior sobreviven en el canvas y no son instancias de
            // prefab: sin tirarlas, editar la tarjeta y volver acá muestra las de antes.
            controller.EditorPreviewResetCards();
            controller.EditorPreview(
                content,
                new Vector2(Screen.width * 0.5f, Screen.height * 0.55f),
                TooltipPlacementMode.AutoFit);

            // Fuera de play mode nadie tira un frame solo: sin esto el Game view se queda con el
            // dibujo viejo y el panel parece no haber cambiado.
            EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

            // El resumen va al log además del Game view: si la ventana no está abierta, cuántas
            // tarjetas quedaron en cada columna sigue siendo la respuesta a "esto cambió o no".
            Debug.Log("[EnemyPanelPreview] " + content.Name + " — tipo '" + content.Type + "' — " +
                      attack.Count + " tarjeta(s) de ataque, " + applied.Count + " al costado" +
                      (withSampleStates ? " (dos de muestra)." : "."));

            if (attack.Count == 0)
                Debug.LogWarning("[EnemyPanelPreview] Sin tarjeta de ataque: el árbol de " +
                                 data.name + " no tiene un Alternate con próximo tiempo, o sus " +
                                 "nodos no saben describirse.");
        }

        /// <summary>
        /// Escupe el panel dibujado — cada nodo con su rect y su texto — al Console.
        /// </summary>
        /// <remarks>
        /// Sirve para discutir lo que se ve sin tener que describirlo: qué quedó prendido, dónde,
        /// de qué tamaño y con qué adentro.
        /// </remarks>
        [MenuItem("Rollgeon/Tooltips/Preview Enemy Panel - Dump Layout")]
        public static void DumpLayout()
        {
            var found = CollectControllers();
            if (found.Count == 0)
            {
                Debug.LogWarning("[EnemyPanelPreview] No hay TooltipController en la escena.");
                return;
            }

            if (found.Count > 1)
                Debug.LogWarning("[EnemyPanelPreview] Hay " + found.Count + " paneles en la " +
                                 "escena: lo que ves son varios superpuestos.");

            var controller = found[0];

            var sb = new System.Text.StringBuilder();
            Dump(controller.transform, 0, sb);

            // A archivo y no sólo al Console: el árbol entero no entra en una línea de log y la
            // consola lo recorta justo donde está lo que se quiere mirar.
            System.IO.File.WriteAllText(DumpPath, sb.ToString());
            Debug.Log("[EnemyPanelPreview] Layout escrito en " + DumpPath + "\n" + sb);
        }

        private static void Dump(Transform t, int depth, System.Text.StringBuilder sb)
        {
            var rect = t as RectTransform;
            sb.Append(new string(' ', depth * 2)).Append(t.name);

            if (!t.gameObject.activeSelf) sb.Append(" [OFF]");
            if (rect != null)
                sb.Append(" size=").Append(Mathf.Round(rect.rect.width))
                  .Append('x').Append(Mathf.Round(rect.rect.height))
                  .Append(" pos=").Append(Mathf.Round(rect.anchoredPosition.x))
                  .Append(',').Append(Mathf.Round(rect.anchoredPosition.y));

            var label = t.GetComponent<TMPro.TMP_Text>();
            if (label != null)
                sb.Append(" text='").Append(label.text).Append("' fs=").Append(label.fontSize);

            var image = t.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
                sb.Append(" img=").Append(image.sprite != null ? image.sprite.name : "<null>");

            sb.Append('\n');

            // Los apagados se listan igual pero no se abren: lo que importa de una rama muerta es
            // que está muerta.
            if (!t.gameObject.activeSelf) return;
            for (int i = 0; i < t.childCount; i++) Dump(t.GetChild(i), depth + 1, sb);
        }

        [MenuItem("Rollgeon/Tooltips/Preview Enemy Panel - Hide")]
        public static void Hide()
        {
            foreach (var controller in CollectControllers())
            {
                var root = controller.transform.root.gameObject;
                if (root.hideFlags == HideFlags.DontSave)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                    continue;
                }

                controller.EditorPreviewHide();
            }
        }

        private static TooltipContent BuildContent(EnemyDataSO data,
                                                   List<StatusIconState> attack,
                                                   List<StatusIconState> applied)
        {
            string id = data.EntityId;
            string name = string.IsNullOrEmpty(id)
                ? data.DisplayName
                : LocalizedContent.Name(id, data.DisplayName);

            // Sin vitales, igual que el panel de verdad: la barra de vida ya está sobre la cabeza
            // del bicho. Un preview que muestre una fila que el juego no dibuja no sirve.
            return new TooltipContent(
                name: name,
                type: EnemyArchetypeText.Describe(data.Archetype, data.IsBoss),
                cards: attack, sideCards: applied);
        }

        // El árbol real, leído por el walker real. Lo único de mentira es el contexto: fuera de
        // combate no hay grilla ni player, y los campos de AIContext son nullables a propósito
        // porque cada nodo tolera que le falte el servicio.
        private static void CollectCards(EnemyDataSO data, List<StatusIconState> attack,
                                         List<StatusIconState> applied)
        {
            if (data.AIRoot == null) return;

            var owner = Guid.NewGuid();
            var context = new AIContext
            {
                SelfGuid = owner,
                SelfMaxHp = data.ResolveMaxHP(1),
                RoundIndex = 1,

                // Semilla fija: un preview que cambia solo entre dos clicks no sirve para comparar.
                Rng = new System.Random(0),
            };

            var standing = new List<AIIntent>();
            var next = new List<AIIntent>();
            try
            {
                AIIntentWalker.Collect(data.AIRoot, context, standing, next);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[EnemyPanelPreview] Un nodo de " + data.name + " necesita un " +
                                 "servicio que fuera de combate no existe, así que el panel va " +
                                 "sin tarjetas: " + e.Message);
                return;
            }

            var settings = Resources.Load<EnemyStatusRowSettingsSO>(
                EnemyStatusRowSettingsSO.ResourcePath);
            var catalog = settings != null ? settings.Catalog : null;

            foreach (var intent in next)
                EnemyStatusIconsView.AddIfOwn(intent, owner, catalog, attack);
            foreach (var intent in standing)
                EnemyStatusIconsView.AddIfOwn(intent, owner, catalog, applied);
        }

        // Lo simulado es que el fuego este puesto, no lo que dice: la definicion es la que este
        // enemigo deja de verdad, y la tarjeta la arma el MISMO BurnState que usa el provider en
        // combate. Lo que se mide aca es la tarjeta real, no una maqueta.
        private static void AddSampleStates(EnemyDataSO data, List<StatusIconState> into)
        {
            var fire = FindFireDefinition(data);
            if (fire == null)
            {
                Debug.LogWarning("[EnemyPanelPreview] " + data.name + " no deja fuego, asi que no " +
                                 "hay nada propio que mostrar al costado.");
                return;
            }

            var settings = Resources.Load<EnemyStatusRowSettingsSO>(
                EnemyStatusRowSettingsSO.ResourcePath);
            var catalog = settings != null ? settings.Catalog : null;

            into.Add(TileStandStatusProvider.BurnState(
                fire,
                catalog != null ? catalog.Resolve(TileStandStatusProvider.BurnId) : null,
                StatusCardStyle.Terrain,
                remainingRounds: fire.DefaultDurationRounds > 0
                    ? fire.DefaultDurationRounds
                    : (int?)null));
        }

        // Por dependencias del asset y no caminando el arbol a mano: el fuego cuelga de un nodo
        // adentro del SO, y repetir aca el recorrido de AIIntentWalker seria una segunda copia que
        // se desincroniza en cuanto alguien mueva un nodo de lugar.
        private static SpecialTileDefinitionSO FindFireDefinition(EnemyDataSO data)
        {
            string path = AssetDatabase.GetAssetPath(data);
            if (string.IsNullOrEmpty(path)) return null;

            foreach (var dependency in AssetDatabase.GetDependencies(path, recursive: true))
            {
                var definition = AssetDatabase.LoadAssetAtPath<SpecialTileDefinitionSO>(dependency);
                if (definition == null) continue;
                if (definition.TileType == SpecialTileType.Fire ||
                    definition.TileType == SpecialTileType.FireTemp) return definition;
            }
            return null;
        }

        // La escena abierta puede no tener el canvas del tooltip (00_Bootstrap no lo tiene). En
        // vez de mandarte a abrir otra escena, se lo trae y lo marca para que no se guarde con ella.
        private static TooltipController ResolveController()
        {
            var found = CollectControllers();

            // Los prestados de vueltas anteriores se tiran SIEMPRE, y por eso esto no puede usar
            // FindObjectsByType: no devuelve un objeto marcado DontSave. Con eso, cada preview no
            // encontraba el canvas de la vuelta anterior, colgaba otro encima, y lo que se veía
            // eran dos paneles superpuestos — el viejo tapando al que se acababa de armar.
            for (int i = found.Count - 1; i >= 0; i--)
            {
                var root = found[i].transform.root.gameObject;
                if (root.hideFlags != HideFlags.DontSave) continue;

                found.RemoveAt(i);
                UnityEngine.Object.DestroyImmediate(root);
            }

            if (found.Count > 0) return found[0];

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TooltipPrefabPath);
            if (prefab == null) return null;

            var borrowed = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (borrowed == null) return null;

            foreach (var t in borrowed.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.DontSave;

            return borrowed.GetComponentInChildren<TooltipController>(true);
        }

        // Recorre las raíces de las escenas cargadas en vez de preguntarle a FindObjectsByType,
        // que se saltea todo lo marcado DontSave — que es justo lo que cuelga este tool.
        private static List<TooltipController> CollectControllers()
        {
            var found = new List<TooltipController>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                foreach (var root in scene.GetRootGameObjects())
                    found.AddRange(root.GetComponentsInChildren<TooltipController>(true));
            }
            return found;
        }
    }
}
