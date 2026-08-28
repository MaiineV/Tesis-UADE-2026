using System;
using System.Collections.Generic;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Entities;
using Rollgeon.Localization;
using Rollgeon.UI.HUD.Status;
using Rollgeon.UI.Tooltips;
using UnityEditor;
using UnityEngine;

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

        // El canvas que el preview tuvo que traerse porque la escena abierta no tenía ninguno.
        private static GameObject _borrowed;

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
        [MenuItem("Rollgeon/Tooltips/Preview Enemy Panel + Applied States")]
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
            if (withSampleStates) AddSampleStates(applied);

            var content = BuildContent(data, attack, applied);
            controller.EditorPreview(
                content,
                new Vector2(Screen.width * 0.5f, Screen.height * 0.55f),
                TooltipPlacementMode.AutoFit);

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

        [MenuItem("Rollgeon/Tooltips/Preview Enemy Panel - Hide")]
        public static void Hide()
        {
            var controller = UnityEngine.Object.FindFirstObjectByType<TooltipController>(
                FindObjectsInactive.Include);
            if (controller != null) controller.EditorPreviewHide();

            if (_borrowed == null) return;
            UnityEngine.Object.DestroyImmediate(_borrowed);
            _borrowed = null;
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

        // Lo simulado es que estén puestos, no lo que dicen: la key, el sprite del catálogo y el
        // texto localizado son los mismos que publican StunStatusProvider y TileStandStatusProvider
        // en combate, así que lo que se mide acá es el ancho real de la columna.
        private static void AddSampleStates(List<StatusIconState> into)
        {
            var settings = Resources.Load<EnemyStatusRowSettingsSO>(
                EnemyStatusRowSettingsSO.ResourcePath);
            var catalog = settings != null ? settings.Catalog : null;

            into.Add(new StatusIconState(
                StunStatusProvider.StateId,
                LocalizedContent.Name(StunStatusProvider.StateId, "Aturdido"),
                LocalizedContent.Description(StunStatusProvider.StateId, "Perdés tu próximo turno."),
                catalog != null ? catalog.Resolve(StunStatusProvider.StateId) : null,
                active: true,
                remainingTurns: 1));

            into.Add(new StatusIconState(
                TileStandStatusProvider.BurnId,
                LocalizedContent.Name(TileStandStatusProvider.BurnId, "Quemándose"),
                LocalizedContent.DescriptionFormat(TileStandStatusProvider.BurnId,
                    "<b>{0}</b> al entrar en una casilla. <b>{1}</b> si empezás tu turno sobre ella.",
                    6, 10),
                catalog != null ? catalog.Resolve(TileStandStatusProvider.BurnId) : null,
                active: true));
        }

        // La escena abierta puede no tener el canvas del tooltip (00_Bootstrap no lo tiene). En
        // vez de mandarte a abrir otra escena, se lo trae y lo marca para que no se guarde con ella.
        private static TooltipController ResolveController()
        {
            var found = UnityEngine.Object.FindFirstObjectByType<TooltipController>(
                FindObjectsInactive.Include);
            if (found != null) return found;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TooltipPrefabPath);
            if (prefab == null) return null;

            _borrowed = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (_borrowed == null) return null;

            foreach (var t in _borrowed.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.DontSave;

            return _borrowed.GetComponentInChildren<TooltipController>(true);
        }
    }
}
