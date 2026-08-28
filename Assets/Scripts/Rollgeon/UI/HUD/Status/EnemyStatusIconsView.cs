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
    /// Las dos salen del MISMO tick, y eso es el punto: el ícono que ves flotando sobre el bicho y
    /// el de la tarjeta que se abre al pasar el mouse son el mismo sprite, que es lo que hace que el
    /// sistema se entienda sin tutorial.
    /// <para>
    /// El panel las pide partidas en dos —lo que va a hacer y lo que le pasa— porque son dos
    /// columnas; la fila que flota las dibuja juntas, porque es una sola fila.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Enemy Status Icons View")]
    public sealed class EnemyStatusIconsView : MonoBehaviour
    {
        private readonly List<IStatusIconProvider> _providers = new();
        private readonly List<StatusIconState> _attack = new();
        private readonly List<StatusIconState> _applied = new();
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
        /// Lo que el enemigo <b>va a hacer</b>: la columna de arriba del panel.
        /// </summary>
        /// <remarks>
        /// Se recalcula en cada llamada, así que el tooltip siempre está al día en el momento del
        /// hover, cubra o no cubra un evento lo que cambió.
        /// </remarks>
        public IReadOnlyList<StatusIconState> CollectAttack()
        {
            Recollect();
            return _attack;
        }

        /// <summary>
        /// Lo que <b>le pasa</b> y lo que mantiene en el paño: la columna del costado, para que
        /// aturdirlo no estire el panel hacia abajo.
        /// </summary>
        public IReadOnlyList<StatusIconState> CollectApplied()
        {
            Recollect();
            return _applied;
        }

        // Las dos listas salen del mismo tick, así que se llenan juntas. Recorrer el árbol dos
        // veces por hover es barato: SetHover dispara en el flanco del mouse, no por frame.
        private void Recollect()
        {
            _attack.Clear();
            _applied.Clear();

            CollectIntents();
            foreach (var provider in _providers) provider.Collect(_entityGuid, _applied);
        }

        public void Refresh()
        {
            if (_container == null || _iconPrefab == null) return;

            Recollect();

            // La fila sigue siendo UNA aunque el panel tenga dos columnas: el ícono que flota
            // sobre el bicho y el de su tarjeta tienen que ser el mismo sprite, que es lo que hace
            // que el sistema se entienda sin tutorial. Su ataque primero, que es lo que se lee.
            int shown = Draw(_attack, 0);
            shown = Draw(_applied, shown);

            for (int i = shown; i < _slots.Count; i++)
                _slots[i].gameObject.SetActive(false);
        }

        // La fila muestra SOLO lo que tiene ícono y habla de la unidad. Una tarjeta de terreno
        // describe el suelo, que ya se ve en el paño, y una sin arte dejaría un cuadrado vacío
        // flotando sobre el bicho.
        private int Draw(List<StatusIconState> states, int shown)
        {
            for (int i = 0; i < states.Count; i++)
            {
                if (!IsFloatable(states[i])) continue;
                EnsureSlots(shown + 1);
                _slots[shown].gameObject.SetActive(true);
                _slots[shown].Show(states[i]);
                shown++;
            }
            return shown;
        }

        private static bool IsFloatable(in StatusIconState state)
            => state.Style == StatusCardStyle.Unit && state.Icon != null;

        private void CollectIntents()
        {
            if (!ServiceLocator.TryGetService<IEnemyIntentService>(out var intents) || intents == null)
                return;
            if (!intents.TryRead(_entityGuid, _standing, _next)) return;

            // El próximo tiempo del ciclo ES su ataque. Lo que el árbol tickea TODOS los turnos
            // no es un segundo ataque sino lo que el jefe mantiene en el paño —el fuego del Pleno,
            // el candado del 70%—, y eso es un estado. Mezclarlos en una sola columna es lo que
            // hacía que el panel mostrara dos tarjetas de ataque y el jugador tuviera que adivinar
            // cuál de las dos iba a pasar.
            foreach (var intent in _next) AddIfOwn(intent, _attack);
            foreach (var intent in _standing) AddIfOwn(intent, _applied);
        }

        // Lo que le pertenece a otra cosa del paño se lee en ESA cosa. Las bombas del Croupier
        // publican una cruz cada una y el jefe las tickea a todas, así que sin esto su columna
        // eran tres tarjetas de bombas y su próximo ataque perdido al final.
        private void AddIfOwn(in AIIntent intent, List<StatusIconState> into)
        {
            if (intent.SubjectGuid != Guid.Empty && intent.SubjectGuid != _entityGuid) return;
            into.Add(ToState(intent));
        }

        // El badge cuenta turnos hasta que pase, no turnos restantes de un estado: TurnsAway 0 es
        // "en su próximo turno", que para el jugador es un turno de distancia.
        //
        // Daño 0 viaja como null y no como cero: una intención que no pega por sí misma —el
        // estallido, que lo que cobra es el fuego que deja— no tiene que mostrar un "0".
        private StatusIconState ToState(in AIIntent intent)
            => new StatusIconState(
                intent.LabelKey,
                LocalizedContent.Name(intent.LabelKey, intent.LabelFallback),
                AIIntentText.Describe(intent),
                _catalog != null ? _catalog.Resolve(intent.LabelKey) : null,
                active: true,
                remainingTurns: intent.TurnsAway + 1,
                damage: intent.Damage > 0 ? intent.Damage : (int?)null);

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
