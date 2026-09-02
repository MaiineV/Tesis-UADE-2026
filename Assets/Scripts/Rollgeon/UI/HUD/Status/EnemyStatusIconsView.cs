using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Entities;
using Rollgeon.Entities.Traits;
// El Design de EnemyDataSO trae otro EnemyArchetype (planilla editor-only, Rollgeon.Entities):
// acá el nombre pelado es SIEMPRE la familia runtime del panel.
using EnemyArchetype = Rollgeon.Entities.Traits.EnemyArchetype;
using Rollgeon.Entities.Visuals;
using Rollgeon.Localization;
using Rollgeon.Tiles;
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
    /// El panel las pide partidas en dos —lo que va a hacer arriba, lo que le pasa como fila de
    /// íconos al pie— porque son dos zonas; la fila que flota las dibuja juntas, porque es una
    /// sola fila.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Enemy Status Icons View")]
    public sealed class EnemyStatusIconsView : MonoBehaviour
    {
        private readonly List<IStatusIconProvider> _providers = new();
        private readonly List<StatusIconState> _applied = new();
        private readonly List<StatusIconState> _panelCards = new();
        private readonly List<StatusIconState> _bottomIcons = new();
        private readonly List<StatusEffectIconView> _slots = new();
        private readonly List<AIIntent> _standing = new();
        private readonly List<AIIntent> _next = new();

        /// <summary>Key de UI de la etiqueta "Próximo turno" del ataque.</summary>
        public const string NextTurnKey = "enemy.panel.next_turn";

        /// <summary>Key de UI de la etiqueta del bloque de maldición del jefe.</summary>
        public const string PlayerCurseKey = "enemy.panel.player_curse";

        /// <summary>Key de UI de la etiqueta del bloque de la casilla que el bicho pisa.</summary>
        public const string OnTheFloorKey = "enemy.panel.on_the_floor";

        private Guid _entityGuid;
        private EnemyDataSO _data;
        private EnemyKitStatusProvider _kit;
        private StatusEffectIconView _iconPrefab;
        private StatusIconCatalogSO _catalog;
        private RectTransform _container;
        private Vector3 _offset;
        private Transform _healthBar;
        private float _liftAboveBar = 1f;
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
            // El prefab del slot está autorado en píxeles de HUD: sin esta escala un píxel es
            // una unidad de mundo, y el badge de turnos de una bomba tapaba la pantalla.
            rect.localScale = Vector3.one * settings.WorldScale;
            rect.localRotation = Quaternion.identity;

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = settings.Spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            // Sin controlar el tamaño: el root del slot no tiene LayoutElement y controlarlo lo
            // colapsa a 0 — quedaba visible sólo el badge de turnos, que tiene rect fijo.
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var view = go.AddComponent<EnemyStatusIconsView>();
            view._container = rect;
            view._offset = settings.Offset;
            view._liftAboveBar = settings.LiftAboveBar;
            view._referenceZoom = settings.ReferenceZoom;
            view._baseScale = rect.localScale;

            // Sobre la barra de vida del pawn y no a una altura global: cada bicho la lleva
            // a otra altura. Sin barra, el Offset del settings.
            var bar = pawnRoot.GetComponentInChildren<WorldSpaceHealthBar>(includeInactive: true);
            view._healthBar = bar != null ? bar.transform : null;
            rect.localPosition = view.RowPosition(1f);

            WorldSpaceOverlayMaterials.Apply(go);
            return view;
        }

        public void Initialize(Guid entityGuid, StatusEffectIconView iconPrefab,
                               StatusIconCatalogSO catalog, EnemyDataSO data = null)
        {
            if (_bound) Teardown();

            _entityGuid = entityGuid;
            _iconPrefab = iconPrefab;
            _catalog = catalog;
            _data = data;
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
        /// Lo que <b>le pasa</b> y lo que mantiene en el paño, con todo su texto. El panel ya no
        /// dibuja esta lista: usa <see cref="CollectBottomIcons"/>. Sigue viva porque es la fuente
        /// de la fila de abajo y de la fila que flota sobre la cabeza.
        /// </summary>
        public IReadOnlyList<StatusIconState> CollectApplied()
        {
            Recollect();
            return _applied;
        }

        /// <summary>
        /// La fila de estados al pie del panel: lo que le pasa y lo que mantiene en el paño,
        /// reducido a lo que tiene arte — los slots son sólo ícono y un estado sin sprite sería
        /// una placa vacía.
        /// </summary>
        public IReadOnlyList<StatusIconState> CollectBottomIcons()
        {
            Recollect();
            AppendBottomIcons(_applied, _bottomIcons);
            return _bottomIcons;
        }

        /// <summary>
        /// El filtro de la fila de abajo. Estático y público por lo mismo que
        /// <see cref="AddIfOwn"/>: el preview de editor arma este mismo panel sin combate.
        /// </summary>
        public static void AppendBottomIcons(List<StatusIconState> applied,
                                             List<StatusIconState> into)
        {
            into.Clear();
            if (applied == null) return;
            foreach (var state in applied)
                if (state.Icon != null) into.Add(state);
        }

        /// <summary>
        /// La columna principal del panel: el bloque de próximo turno. Se recalcula en cada
        /// hover, igual que el costado.
        /// </summary>
        public IReadOnlyList<StatusIconState> CollectPanelCards()
        {
            Recollect();
            return _panelCards;
        }

        // Se recalcula en cada hover; recorrer el árbol por hover es barato porque SetHover
        // dispara en el flanco del mouse, no por frame.
        private void Recollect()
        {
            _applied.Clear();
            _panelCards.Clear();

            if (ServiceLocator.TryGetService<IEnemyIntentService>(out var intents) && intents != null
                && intents.TryRead(_entityGuid, _standing, _next))
            {
                string promotedKey = AppendNextTurnCard(_next, _standing, _entityGuid, _catalog,
                                                        _panelCards, Family());
                AppendStandingCards(_standing, promotedKey, _entityGuid, _catalog, _applied);
            }

            // Sólo cuando opera: la del Croupier arranca con su fase 2, y el bloque antes de eso
            // promete un castigo que no existe. El preview de editor llama AppendCurseCard
            // directo y la muestra siempre — ahí se diseña la tarjeta, no la pelea.
            var curse = _data != null ? _data.Curse : null;
            if (curse != null && curse.IsActive(_entityGuid))
                AppendCurseCard(curse, _catalog, _panelCards);

            // El hover de la celda es uno solo aunque la pisen dos cosas (decisión del 30/08):
            // el panel del bicho suma la casilla como una caja más al final del stack, en vez de
            // disputarle el mouse al tooltip de la casilla.
            AppendGroundCards(_entityGuid, _under, _panelCards);

            foreach (var provider in _providers) provider.Collect(_entityGuid, _applied);
        }

        private readonly List<SpecialTileInfo> _under = new();

        /// <summary>
        /// La casilla especial bajo el bicho, como tarjeta EN EL PISO al final de la columna.
        /// Título + su precio como dato; el detalle completo vive en el hover de otra celda de la
        /// misma casilla. Estático y público por lo mismo que <see cref="AddIfOwn"/>.
        /// </summary>
        public static void AppendGroundCards(Guid owner, List<SpecialTileInfo> underScratch,
                                             List<StatusIconState> into)
        {
            if (underScratch == null || into == null) return;
            if (!ServiceLocator.TryGetService<ISpecialTileService>(out var tiles) || tiles == null)
                return;

            tiles.CollectUnder(owner, underScratch);
            if (underScratch.Count == 0) return;

            // La etiqueta subraya el bloque entero: la lleva sólo la primera caja, como EFECTO.
            string eyebrow = LocalizedContent.Ui(OnTheFloorKey, "En el piso");
            foreach (var info in underScratch)
            {
                var def = info.Definition;
                if (def == null) continue;

                string id = string.IsNullOrEmpty(def.NameKey) ? def.TileId : def.NameKey;

                // El precio que le importa a quien lee al bicho parado ahí es el de quedarse
                // (turn start); si la casilla sólo cobra al entrar, ese.
                int? price = def.TurnStartDamage > 0 ? def.TurnStartDamage
                    : def.EnterDamage > 0 ? def.EnterDamage : (int?)null;

                into.Add(new StatusIconState(
                    "ground." + id,
                    LocalizedContent.Name(id, def.DisplayName ?? def.TileId),
                    description: null, icon: null, active: true,
                    style: StatusCardStyle.Terrain,
                    damage: price,
                    eyebrow: eyebrow));
                eyebrow = null;
            }
        }

        public void Refresh()
        {
            if (_container == null || _iconPrefab == null) return;

            Recollect();

            // La fila sigue siendo UNA aunque el panel tenga dos columnas: el ícono que flota
            // sobre el bicho y el de su tarjeta tienen que ser el mismo sprite, que es lo que hace
            // que el sistema se entienda sin tutorial. Su ataque primero, que es lo que se lee.
            int shown = Draw(_panelCards, 0);
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

        /// <summary>
        /// La tarjeta del bloque de próximo turno: el próximo tiempo del ciclo o, en un árbol sin
        /// ciclo —el bestiario común, el golpe marcado de un jefe telegraph—, lo primero que
        /// tickea todos los turnos. Devuelve la key promovida para que la columna del costado no
        /// la repita, o <c>null</c> si no había nada que afirmar (el bloque no se dibuja).
        /// </summary>
        /// <remarks>
        /// Estático y público por lo mismo que <see cref="AddIfOwn"/>: el preview de editor arma
        /// este mismo panel sin combate.
        /// </remarks>
        public static string AppendNextTurnCard(List<AIIntent> next, List<AIIntent> standing,
                                                Guid owner, StatusIconCatalogSO catalog,
                                                List<StatusIconState> into,
                                                EnemyArchetype archetype = EnemyArchetype.Unset)
        {
            if (!TryPickOwn(next, owner, out var intent)
                && !TryPickOwn(standing, owner, out intent))
                return null;

            into.Add(ToNextTurnState(intent, catalog, archetype));
            return intent.LabelKey;
        }

        /// <summary>
        /// Lo que el bicho mantiene en el paño, para la columna del costado — menos lo que ya se
        /// promovió al bloque de próximo turno, que saldría dos veces.
        /// </summary>
        public static void AppendStandingCards(List<AIIntent> standing, string promotedKey,
                                               Guid owner, StatusIconCatalogSO catalog,
                                               List<StatusIconState> into)
        {
            if (standing == null) return;
            foreach (var intent in standing)
            {
                if (intent.LabelKey == promotedKey) continue;
                AddIfOwn(intent, owner, catalog, into);
            }
        }

        private static bool TryPickOwn(List<AIIntent> intents, Guid owner, out AIIntent picked)
        {
            picked = default;
            if (intents == null) return false;
            foreach (var intent in intents)
            {
                if (intent.SubjectGuid != Guid.Empty && intent.SubjectGuid != owner) continue;
                picked = intent;
                return true;
            }
            return false;
        }

        // El tipo de ataque va en el título sólo acá: las tarjetas del costado hablan de efectos
        // y terreno, no de ataques, y un " · Básico" en ellas no calificaría nada.
        private static StatusIconState ToNextTurnState(in AIIntent intent, StatusIconCatalogSO catalog,
                                                      EnemyArchetype archetype = EnemyArchetype.Unset)
        {
            var state = ToState(intent, catalog, NextTurnEyebrow());
            return new StatusIconState(
                state.Id,
                AttackKindText.ComposeTitle(TitleOf(intent, archetype), intent.Kind),
                state.Description,
                state.Icon,
                active: true,
                remainingTurns: state.RemainingTurns,
                damage: state.Damage,
                eyebrow: state.Eyebrow);
        }

        /// <summary>
        /// El título de la tarjeta. El nodo genérico del bestiario rotula todo igual —"Golpe"—
        /// porque describe un <c>EffDealDamage</c> y no sabe de qué bicho cuelga: <c>AIContext</c>
        /// no lleva la ficha. El panel sí la tiene, y la familia ya está impresa dos renglones más
        /// arriba, así que la palabra sale de ahí: un tirador que pega desde cinco casillas no
        /// "golpea".
        /// </summary>
        /// <remarks>
        /// Sólo se pisa la key genérica. Un título autorado —el disparo de un jefe, la mecha de una
        /// bomba— manda siempre: es una decisión de autoría y la familia no la conoce.
        /// </remarks>
        private static string TitleOf(in AIIntent intent, EnemyArchetype archetype)
            => intent.LabelKey == AIIntentTextKeys.Attack && archetype == EnemyArchetype.Ranged
                ? LocalizedContent.Name(AIIntentTextKeys.RangedShot,
                                        AIIntentTextKeys.RangedShotFallback)
                : LocalizedContent.Name(intent.LabelKey, intent.LabelFallback);

        /// <summary>La familia de la ficha, o <c>Unset</c> sin ficha — que no pisa ningún título.</summary>
        private EnemyArchetype Family() => _data != null ? _data.Archetype : EnemyArchetype.Unset;

        // Lo que le pertenece a otra cosa del paño se lee en ESA cosa. Las bombas del Croupier
        // publican una cruz cada una y el jefe las tickea a todas, así que sin esto su columna
        // eran tres tarjetas de bombas y su próximo ataque perdido al final.
        //
        // Estático y público porque el preview de editor arma este mismo panel sin combate: con
        // su propia copia de esto, el panel que mirás para diseñar dejaría de ser el que el juego
        // dibuja en cuanto una de las dos cambie.
        public static void AddIfOwn(in AIIntent intent, Guid owner, StatusIconCatalogSO catalog,
                                    List<StatusIconState> into, string eyebrow = null)
        {
            if (intent.SubjectGuid != Guid.Empty && intent.SubjectGuid != owner) return;
            into.Add(ToState(intent, catalog, eyebrow));
        }

        private void AddIfOwn(in AIIntent intent, List<StatusIconState> into, string eyebrow = null)
            => AddIfOwn(intent, _entityGuid, _catalog, into, eyebrow);

        /// <summary>La etiqueta de fecha del próximo ataque, ya localizada.</summary>
        public static string NextTurnEyebrow()
            => LocalizedContent.Ui(NextTurnKey, "Próximo turno");

        /// <summary>La etiqueta del bloque de maldición, ya localizada.</summary>
        public static string PlayerCurseEyebrow()
            => LocalizedContent.Ui(PlayerCurseKey, "Maldición");

        /// <summary>
        /// La maldición del jefe sobre el jugador, como tarjeta de la columna principal debajo
        /// del próximo turno. Sin <paramref name="curse"/> autorado no agrega nada — el bloque
        /// no existe y no deja hueco. Estático y público por lo mismo que <see cref="AddIfOwn"/>:
        /// el preview de editor arma este mismo panel sin combate.
        /// </summary>
        public static void AppendCurseCard(BossCurseSO curse, StatusIconCatalogSO catalog,
                                           List<StatusIconState> into)
        {
            if (curse == null || into == null) return;

            // Sin título a propósito (mockup del spec): la tarjeta es label + regla — "PLAYER
            // CURSE / Te traba un dado." El nombre del curse vive en su ícono y su regla.
            //
            // Trait y no Unit: la maldición es de la pelea, no un estado transitorio del bicho.
            // Con Unit + ícono entraría a la fila que flota sobre su cabeza — la moneda del
            // Cajero clavada ahí todo el combate, con un tooltip de slot sin encabezado.
            into.Add(new StatusIconState(
                curse.CurseId ?? string.Empty,
                null,
                LocalizedContent.Description(curse.CurseId, curse.Description),
                curse.Icon != null ? curse.Icon
                    : catalog != null ? catalog.Resolve(curse.CurseId) : null,
                active: true,
                style: StatusCardStyle.Trait,
                eyebrow: PlayerCurseEyebrow()));
        }

        // El badge cuenta turnos hasta que pase, no turnos restantes de un estado: TurnsAway 0 es
        // "en su próximo turno", que para el jugador es un turno de distancia.
        //
        // Daño 0 viaja como null y no como cero: una intención que no pega por sí misma —el
        // estallido, que lo que cobra es el fuego que deja— no tiene que mostrar un "0".
        public static StatusIconState ToState(in AIIntent intent, StatusIconCatalogSO catalog,
                                              string eyebrow = null)
            => new StatusIconState(
                intent.LabelKey,
                LocalizedContent.Name(intent.LabelKey, intent.LabelFallback),
                AIIntentText.Describe(intent),
                catalog != null ? catalog.Resolve(intent.LabelKey) : null,
                active: true,
                remainingTurns: intent.TurnsAway + 1,
                damage: intent.Damage > 0 ? intent.Damage : (int?)null,
                eyebrow: eyebrow);

        // Punto de extensión: un jefe nuevo es un IStatusIconProvider más, sin tocar nada de UI.
        private void BuildProviders()
        {
            _providers.Clear();

            _kit = _data != null ? new EnemyKitStatusProvider(_catalog, _data) : null;

            // El teleport del kit va a la fila de abajo con los demás; la debilidad no está en
            // el panel (mockup del spec: header, próximo turno, maldición, estados — nada más).
            if (_kit != null) _providers.Add(_kit);

            // El candado de dados ya no es un provider de esta fila: era la maldición del jefe
            // dicha ad-hoc (aparecía recién al primer bloqueo), y ahora la dice el bloque
            // PLAYER CURSE desde el turno 1 (AppendCurseCard, con el BossCurseSO del jefe).
            _providers.Add(new PoisonStatusProvider(_catalog));
            _providers.Add(new StunStatusProvider(_catalog));
            _providers.Add(new TileStandStatusProvider(_catalog));

            // Lo que el bicho dejó ardiendo tambien es de su columna: los otros cuatro dicen lo que
            // le pasa A el, este dice lo que el mantiene en el paño.
            _providers.Add(new OwnedTilesStatusProvider(_catalog));
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
            float scale = 1f;
            var cam = Camera.main;
            if (cam != null)
            {
                transform.forward = cam.transform.forward;

                if (cam.orthographic)
                {
                    scale = WorldSpaceHealthBar.ComputeZoomScale(cam.orthographicSize, _referenceZoom);
                    transform.localScale = _baseScale * scale;
                }
            }

            transform.localPosition = RowPosition(scale);
        }

        // La y de la barra se lee viva: la barra re-aplica su propio offset cada frame.
        private Vector3 RowPosition(float zoomScale)
        {
            var pos = _offset;
            if (_healthBar != null && transform.parent != null)
                pos.y = transform.parent.InverseTransformPoint(_healthBar.position).y
                        + _liftAboveBar * zoomScale;
            return pos;
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
