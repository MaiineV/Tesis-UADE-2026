using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Entities.Visuals;
using Rollgeon.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD.Status
{
    /// <summary>
    /// La fila de estados que flota sobre la cabeza de un enemigo, y la fuente de la columna de
    /// tarjetas que abre su tooltip.
    /// </summary>
    /// <remarks>
    /// Las dos salen de la MISMA lista, y eso es el punto: el ícono que ves flotando sobre el bicho
    /// y el de la tarjeta que se abre al pasar el mouse son el mismo sprite, que es lo que hace que
    /// el sistema se entienda sin tutorial.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Enemy Status Icons View")]
    public sealed class EnemyStatusIconsView : MonoBehaviour
    {
        private readonly List<IStatusIconProvider> _providers = new();
        private readonly List<StatusIconState> _states = new();
        private readonly List<StatusEffectIconView> _slots = new();
        private readonly List<AIIntent> _standing = new();
        private readonly List<AIIntent> _next = new();

        private Guid _entityGuid;
        private StatusEffectIconView _iconPrefab;
        private StatusIconCatalogSO _catalog;
        private RectTransform _container;
        private Vector3 _offset;
        private float _referenceZoom = 9f;
        private Vector3 _baseScale = Vector3.one;
        private bool _bound;

        /// <summary>
        /// Arma la fila world-space sobre <paramref name="pawnRoot"/> y la devuelve sin bindear.
        /// </summary>
        /// <remarks>
        /// Por código y no autorada en cada prefab de enemigo: es información, no arte — un enemigo
        /// nuevo la trae por existir y no hay forma de olvidarse de cablearla. Mismo argumento que
        /// hace <c>EntityVisualService.AttachTooltip</c> con el tooltip.
        /// </remarks>
        public static EnemyStatusIconsView Create(Transform pawnRoot, EnemyStatusRowSettingsSO settings)
        {
            if (pawnRoot == null || settings == null) return null;

            var go = new GameObject("StatusRow");
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(pawnRoot, worldPositionStays: false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.referencePixelsPerUnit = 100f;
            scaler.dynamicPixelsPerUnit = 1f;

            // Sin GraphicRaycaster y con todo en raycastTarget = false: una fila que intercepte el
            // mouse se come el hover del pawn, que es de donde salen el cursor de targeting Y este
            // mismo tooltip. Misma razón por la que la barra de vida tampoco lo lleva.
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(settings.IconSize * 5f, settings.IconSize);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.localPosition = settings.Offset;

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = settings.Spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var view = go.AddComponent<EnemyStatusIconsView>();
            view._container = rect;
            view._offset = settings.Offset;
            view._referenceZoom = settings.ReferenceZoom;
            view._baseScale = rect.localScale;

            WorldSpaceOverlayMaterials.Apply(go);
            return view;
        }

        public void Initialize(Guid entityGuid, StatusEffectIconView iconPrefab,
                               StatusIconCatalogSO catalog)
        {
            if (_bound) Teardown();

            _entityGuid = entityGuid;
            _iconPrefab = iconPrefab;
            _catalog = catalog;
            BuildProviders();

            EventManager.Subscribe(EventName.OnTurnStarted, HandleRefreshEvent);
            EventManager.Subscribe(EventName.OnTurnFinished, HandleRefreshEvent);
            EventManager.Subscribe(EventName.OnSpecialTilePlaced, HandleRefreshEvent);
            EventManager.Subscribe(EventName.OnSpecialTileExpired, HandleRefreshEvent);
            EventManager.Subscribe(EventName.OnEntityDestroyed, HandleEntityDestroyed);

            _bound = true;
            Refresh();
        }

        public void Teardown()
        {
            if (!_bound) return;

            EventManager.UnSubscribe(EventName.OnTurnStarted, HandleRefreshEvent);
            EventManager.UnSubscribe(EventName.OnTurnFinished, HandleRefreshEvent);
            EventManager.UnSubscribe(EventName.OnSpecialTilePlaced, HandleRefreshEvent);
            EventManager.UnSubscribe(EventName.OnSpecialTileExpired, HandleRefreshEvent);
            EventManager.UnSubscribe(EventName.OnEntityDestroyed, HandleEntityDestroyed);

            _bound = false;
        }

        private void OnDisable() => Teardown();

        /// <summary>
        /// Todo lo que este enemigo tiene en juego, en orden: primero lo que va a hacer, después
        /// los estados que lo afectan.
        /// </summary>
        /// <remarks>
        /// Se recalcula en cada llamada, así que el tooltip siempre está al día en el momento del
        /// hover, cubra o no cubra un evento lo que cambió.
        /// </remarks>
        public IReadOnlyList<StatusIconState> Collect()
        {
            _states.Clear();
            CollectIntents();
            foreach (var provider in _providers) provider.Collect(_entityGuid, _states);
            return _states;
        }

        public void Refresh()
        {
            if (_container == null || _iconPrefab == null) return;

            Collect();

            // La fila muestra SOLO lo que tiene ícono y habla de la unidad. Una tarjeta de terreno
            // describe el suelo, que ya se ve en el paño, y una sin arte dejaría un cuadrado vacío
            // flotando sobre el bicho.
            int shown = 0;
            for (int i = 0; i < _states.Count; i++)
            {
                if (!IsFloatable(_states[i])) continue;
                EnsureSlots(shown + 1);
                _slots[shown].gameObject.SetActive(true);
                _slots[shown].Show(_states[i]);
                shown++;
            }

            for (int i = shown; i < _slots.Count; i++)
                _slots[i].gameObject.SetActive(false);
        }

        private static bool IsFloatable(in StatusIconState state)
            => state.Style == StatusCardStyle.Unit && state.Icon != null;

        private void CollectIntents()
        {
            if (!ServiceLocator.TryGetService<IEnemyIntentService>(out var intents) || intents == null)
                return;
            if (!intents.TryRead(_entityGuid, _standing, _next)) return;

            // Su siguiente ataque primero: es lo que el jugador vino a leer.
            foreach (var intent in _next) _states.Add(ToState(intent));
            foreach (var intent in _standing) _states.Add(ToState(intent));
        }

        // El badge cuenta turnos hasta que pase, no turnos restantes de un estado: TurnsAway 0 es
        // "en su próximo turno", que para el jugador es un turno de distancia.
        private StatusIconState ToState(in AIIntent intent)
            => new StatusIconState(
                intent.LabelKey,
                LocalizedContent.Name(intent.LabelKey, intent.LabelFallback),
                AIIntentText.Describe(intent),
                _catalog != null ? _catalog.Resolve(intent.LabelKey) : null,
                active: true,
                remainingTurns: intent.TurnsAway + 1);

        // Punto de extensión: un jefe nuevo es un IStatusIconProvider más, sin tocar nada de UI.
        private void BuildProviders()
        {
            _providers.Clear();
            _providers.Add(new PoisonStatusProvider(_catalog));
            _providers.Add(new StunStatusProvider(_catalog));
            _providers.Add(new TileStandStatusProvider(_catalog));
            _providers.Add(new DiceBlockStatusProvider(_catalog));
        }

        private void EnsureSlots(int needed)
        {
            while (_slots.Count < needed)
            {
                var slot = Instantiate(_iconPrefab, _container);
                foreach (var graphic in slot.GetComponentsInChildren<Graphic>(includeInactive: true))
                    graphic.raycastTarget = false;
                _slots.Add(slot);
            }
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                transform.forward = cam.transform.forward;

                if (cam.orthographic)
                {
                    float scale = WorldSpaceHealthBar.ComputeZoomScale(cam.orthographicSize, _referenceZoom);
                    transform.localScale = _baseScale * scale;
                }
            }

            transform.localPosition = _offset;
        }

        // Sin filtro por guid: los eventos de casilla traen un instanceId, no una entidad, así que
        // no hay contra qué filtrar. Es lo mismo que hace la fila del player.
        private void HandleRefreshEvent(params object[] args) => Refresh();

        private void HandleEntityDestroyed(params object[] args)
        {
            if (args == null || args.Length < 1) return;
            if (!(args[0] is Guid guid) || guid != _entityGuid) return;
            gameObject.SetActive(false);
        }
    }
}
