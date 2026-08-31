using System;
using System.Collections.Generic;
using Patterns;
using PrimeTween;
using Rollgeon.Entities.Portraits;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Sub-view del Combat HUD que renderiza la cola de turnos como carrusel en
    /// loop (estilo Expedition 33): el actor activo a la izquierda de todo y más
    /// grande, y a su derecha los próximos turnos dimmed. Escucha
    /// <see cref="EventName.OnTurnQueueBuilt"/>, <see cref="EventName.OnTurnStarted"/>,
    /// <see cref="EventName.OnTurnFinished"/> y <see cref="EventName.OnEntityDestroyed"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Los slots NO mapean 1:1 con entidades: son 5 instancias por posición de
    /// display (0 = activo, +1..+4 = próximos). Con menos de 5 participantes la
    /// ventana repite actores (la cola es circular). El estado por entidad
    /// (portrait, isPlayer/isBoss, destroyed) vive en un modelo interno
    /// guid→<see cref="EntityInfo"/> y se aplica al slot que toque mostrarlo.
    /// </para>
    /// <para>
    /// Al avanzar el turno (<c>OnTurnStarted</c> con cursor+1) todo corre una
    /// posición a la izquierda con PrimeTween; el activo sale por la izquierda
    /// y se recicla como el último próximo entrando por la derecha. Un
    /// <c>OnTurnQueueBuilt</c> con la misma secuencia (wrap de round) es no-op
    /// visual, así el loop no salta.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Turn Queue View")]
    public class TurnQueueView : MonoBehaviour
    {
        private const string LogPrefix = "[TurnQueueView] ";

        [Title("Turn Queue — Widget refs")]
        [Required("Arrastrar el prefab de TurnSlotView (instructivo §8.2).")]
        [SerializeField]
        [Tooltip("Prefab que se instancia una vez por posición de la ventana (5).")]
        private TurnSlotView _slotPrefab;

        [Required("Arrastrar el Transform padre donde se anidan los slots.")]
        [SerializeField]
        [Tooltip("Container de los slots. El layout es manual (sin LayoutGroup).")]
        private Transform _container;

        [Title("Turn Queue — Carrusel")]
        [SerializeField]
        [Tooltip("Escalas/alphas/espaciado del carrusel.")]
        private TurnCarouselConfig _carousel = TurnCarouselConfig.Default;

        [SerializeField]
        [Tooltip("Duración del shift al avanzar un turno.")]
        private float _transitionSeconds = 0.28f;

        [SerializeField]
        [Tooltip("Distancia extra que recorre el slot que sale por la izquierda / entra por la derecha.")]
        private float _exitSlideDistance = 60f;

        [SerializeField]
        [Tooltip("Padding entre el borde izquierdo del container y el slot activo.")]
        private float _leftPadding = 10f;

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        [ShowInInspector, ReadOnly]
        private int _cursor;

        private readonly List<Guid> _order = new List<Guid>();
        private readonly Dictionary<Guid, EntityInfo> _entities = new Dictionary<Guid, EntityInfo>();

        // Índice i → posición relativa i (0 = activo). Vacía hasta el primer build.
        private readonly List<TurnSlotView> _window = new List<TurnSlotView>();

        private sealed class EntityInfo
        {
            public Sprite Portrait;
            public bool IsPlayer;
            public bool IsBoss;
            public bool Destroyed;
            public bool Stunned;
        }

        /// <summary>Suscribe al bus y guarda el playerGuid (frames del player en el carrusel).</summary>
        public void Bind(Guid playerGuid)
        {
            if (_bound) Unbind();

            _playerGuid = playerGuid;
            EventManager.Subscribe(EventName.OnTurnQueueBuilt, HandleTurnQueueBuilt);
            EventManager.Subscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.Subscribe(EventName.OnTurnFinished, HandleTurnFinished);
            EventManager.Subscribe(EventName.OnEntityDestroyed, HandleEntityDestroyed);
            EventManager.Subscribe(EventName.OnStunApplied, HandleStunApplied);
            EventManager.Subscribe(EventName.OnStunExpired, HandleStunExpired);
            _bound = true;
        }

        /// <summary>Desuscribe del bus. Idempotente. Deja los slots vivos (se limpian en OnDisable / re-build).</summary>
        public void Unbind()
        {
            if (!_bound) return;
            EventManager.UnSubscribe(EventName.OnTurnQueueBuilt, HandleTurnQueueBuilt);
            EventManager.UnSubscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.UnSubscribe(EventName.OnTurnFinished, HandleTurnFinished);
            EventManager.UnSubscribe(EventName.OnEntityDestroyed, HandleEntityDestroyed);
            EventManager.UnSubscribe(EventName.OnStunApplied, HandleStunApplied);
            EventManager.UnSubscribe(EventName.OnStunExpired, HandleStunExpired);
            _bound = false;
        }

        private void OnDisable()
        {
            if (_bound) Unbind();
        }

        /// <summary>RectTransform del container de slots — anchor del overlay del
        /// tutorial. Falso mientras la cola está vacía (no hay nada que señalar).</summary>
        public bool TryGetQueueRect(out RectTransform rect)
        {
            rect = _container as RectTransform;
            return rect != null && _window.Count > 0;
        }

        // ======================================================================
        // API publica (hooks para tests y tooling T95T — plan §7)
        // ======================================================================

        /// <summary>
        /// Re-construye modelo y ventana a partir de <paramref name="order"/>. Expuesto
        /// publico para tooling T95T sin tener que disparar el evento. Snap sin animación.
        /// </summary>
        public void RebuildQueue(IReadOnlyList<Guid> order)
        {
            if (order == null || order.Count == 0)
            {
                ClearSlots();
                _order.Clear();
                _entities.Clear();
                _cursor = 0;
                return;
            }

            // Si el actor activo sigue en la nueva cola, el cursor lo persigue
            // (Append / RestoreState no deben mover al activo de la izquierda).
            Guid activeGuid = _order.Count > 0 && _cursor < _order.Count
                ? _order[_cursor]
                : Guid.Empty;

            _order.Clear();
            _order.AddRange(order);
            RefreshEntities();

            int idx = activeGuid != Guid.Empty ? _order.IndexOf(activeGuid) : -1;
            _cursor = idx >= 0 ? idx : 0;

            RebuildWindow();
        }

        /// <summary>Destruye todos los slots de la ventana.</summary>
        public void ClearSlots()
        {
            for (int i = 0; i < _window.Count; i++)
            {
                var slot = _window[i];
                if (slot == null) continue;
                if (Application.isPlaying) Destroy(slot.gameObject);
                else DestroyImmediate(slot.gameObject);
            }
            _window.Clear();
        }

        /// <summary>
        /// Primer slot visible que muestra este guid (con pocas entidades puede
        /// haber más de uno — la ventana repite). Null si no está en pantalla.
        /// </summary>
        public TurnSlotView FindSlot(Guid guid)
        {
            for (int i = 0; i < _window.Count; i++)
            {
                var slot = _window[i];
                if (slot != null && slot.SlotGuid == guid) return slot;
            }
            return null;
        }

        // Mismo criterio que EffForceDoor.IsBossRoom: la sala actual define si el
        // combate es de boss (sin IDungeonService, ej. tests, degrada a false).
        private static bool IsBossCombat()
        {
            return ServiceLocator.TryGetService<Rollgeon.Dungeon.IDungeonService>(out var dungeon)
                   && dungeon?.CurrentRoom != null
                   && dungeon.CurrentRoom.Type == Rollgeon.Dungeon.RoomType.Boss;
        }

        // ======================================================================
        // Modelo y ventana
        // ======================================================================

        private void RefreshEntities()
        {
            bool bossCombat = IsBossCombat();
            ServiceLocator.TryGetService<IEntityPortraitResolver>(out var portraits);

            // Conservar el flag Destroyed de guids que persisten (Append mid-round
            // no debe "revivir" visualmente a un muerto todavía listado).
            var stale = new List<Guid>(_entities.Keys);
            for (int i = 0; i < _order.Count; i++)
            {
                var guid = _order[i];
                if (!_entities.TryGetValue(guid, out var info))
                {
                    info = new EntityInfo();
                    _entities[guid] = info;
                }
                bool isPlayer = guid == _playerGuid;
                info.IsPlayer = isPlayer;
                info.IsBoss = bossCombat && !isPlayer;
                info.Portrait = portraits != null && portraits.TryGetPortrait(guid, out var sprite)
                    ? sprite
                    : null;
                // Seed desde el servicio: un rebuild mid-stun (Append, resume) no
                // debe perder la cruz — el evento OnStunApplied ya pasó (BUG-87).
                info.Stunned = ServiceLocator.TryGetService<Rollgeon.Combat.Status.IStunService>(out var stun)
                               && stun != null && stun.IsStunned(guid);
            }
            for (int i = 0; i < stale.Count; i++)
            {
                if (!_order.Contains(stale[i])) _entities.Remove(stale[i]);
            }
        }

        private void RebuildWindow()
        {
            ClearSlots();

            if (_slotPrefab == null)
            {
                Debug.LogWarning(LogPrefix + "_slotPrefab no esta cableado — skip rebuild.", this);
                return;
            }
            if (_container == null)
            {
                Debug.LogWarning(LogPrefix + "_container no esta cableado — skip rebuild.", this);
                return;
            }

            for (int i = 0; i < TurnQueueCarouselLayout.WindowSize; i++)
            {
                _window.Add(Instantiate(_slotPrefab, _container));
            }
            SnapWindow();
        }

        /// <summary>Re-bindea y posiciona toda la ventana sin animación.</summary>
        private void SnapWindow()
        {
            for (int relPos = 0; relPos < _window.Count; relPos++)
            {
                var guid = TurnQueueCarouselLayout.GuidAt(_order, _cursor, relPos);
                BindSlotContent(_window[relPos], guid, relPos);
                ApplyPoseSnap(_window[relPos], relPos);
            }
        }

        private void BindSlotContent(TurnSlotView slot, Guid guid, int relPos)
        {
            if (slot == null) return;
            _entities.TryGetValue(guid, out var info);

            bool isPlayer = info?.IsPlayer ?? guid == _playerGuid;
            slot.Bind(guid, isPlayer, relPos, isBoss: info?.IsBoss ?? false);
            slot.SetPortrait(info?.Portrait);
            // Solo los próximos llevan su orden relativo (+1..+4); el activo sin número.
            slot.SetLabel(relPos > 0 ? relPos.ToString() : null);
            slot.SetDestroyed(info?.Destroyed ?? false);
            slot.SetStunned(info?.Stunned ?? false);
            slot.SetActive(relPos == 0);
        }

        /// <summary>
        /// X del slot en coordenadas del container (anchors centro): la fila
        /// arranca pegada al borde izquierdo, con el activo a _leftPadding.
        /// </summary>
        private float TargetX(int relPos)
        {
            float originX = 0f;
            if (_container is RectTransform rect)
            {
                originX = -rect.rect.width * 0.5f + _leftPadding
                          + _carousel.SlotWidth * _carousel.ActiveScale * 0.5f;
            }
            return originX + TurnQueueCarouselLayout.GetX(relPos, _carousel);
        }

        private void ApplyPoseSnap(TurnSlotView slot, int relPos)
        {
            if (slot == null) return;
            slot.Rect.anchoredPosition = new Vector2(TargetX(relPos), 0f);
            float scale = TurnQueueCarouselLayout.GetScale(relPos, _carousel);
            slot.Rect.localScale = new Vector3(scale, scale, 1f);
            slot.Group.alpha = TurnQueueCarouselLayout.GetAlpha(relPos, _carousel);
        }

        // ======================================================================
        // Event handlers
        // ======================================================================

        private void HandleTurnQueueBuilt(params object[] args)
        {
            if (args == null || args.Length < 1)
            {
                Debug.LogWarning(LogPrefix + "OnTurnQueueBuilt args malformed (len < 1).", this);
                return;
            }
            if (!(args[0] is IReadOnlyList<Guid> order))
            {
                Debug.LogWarning(LogPrefix + "OnTurnQueueBuilt args[0] is not IReadOnlyList<Guid>.", this);
                return;
            }

            // Wrap de round: misma secuencia → no-op visual, el loop del carrusel
            // sigue suave (la transición la maneja el próximo OnTurnStarted).
            if (SameSequence(order) && _window.Count == TurnQueueCarouselLayout.WindowSize)
            {
                return;
            }

            RebuildQueue(order);
        }

        private bool SameSequence(IReadOnlyList<Guid> order)
        {
            if (order.Count != _order.Count) return false;
            for (int i = 0; i < order.Count; i++)
            {
                if (order[i] != _order[i]) return false;
            }
            return true;
        }

        private void HandleTurnStarted(params object[] args)
        {
            if (!TryReadGuid(args, "OnTurnStarted", out var guid)) return;
            if (_order.Count == 0) return;

            int newCursor = _order.IndexOf(guid);
            if (newCursor < 0)
            {
                Debug.LogWarning(LogPrefix + "OnTurnStarted guid fuera de la cola — skip.", this);
                return;
            }

            if (_window.Count != TurnQueueCarouselLayout.WindowSize)
            {
                _cursor = newCursor;
                RebuildWindow();
                return;
            }

            if (newCursor == _cursor)
            {
                // Primer turno post-build: la ventana ya arranca acá.
                SnapWindow();
                return;
            }

            bool singleStep = _order.Count > 1 && newCursor == (_cursor + 1) % _order.Count;
            _cursor = newCursor;

            bool reducedMotion = DiceAnim.DiceUiMotionPrefs.ReducedMotion;
            if (!Application.isPlaying || !singleStep || reducedMotion)
            {
                // Saltos no consecutivos (muerte mid-round que corre el cursor,
                // resume) no tienen transición natural — snap directo.
                CompleteWindowTweens();
                SnapWindow();
                return;
            }

            AnimateShift();
        }

        /// <summary>
        /// Shift de una posición a la izquierda: los próximos tweenean hacia su
        /// nueva pose (el +1 llega al lugar del activo y crece), el activo sale
        /// por la izquierda con fade y se recicla como el último próximo entrando
        /// por la derecha.
        /// </summary>
        private void AnimateShift()
        {
            CompleteWindowTweens();

            var exiting = _window[0];
            _window.RemoveAt(0);
            _window.Add(exiting);

            // Supervivientes: contenido de estado inmediato (frames/label),
            // pose animada hacia la posición nueva.
            for (int relPos = 0; relPos < _window.Count - 1; relPos++)
            {
                var slot = _window[relPos];
                if (slot == null) continue;

                var guid = TurnQueueCarouselLayout.GuidAt(_order, _cursor, relPos);
                BindSlotContent(slot, guid, relPos);

                float scale = TurnQueueCarouselLayout.GetScale(relPos, _carousel);
                Tween.UIAnchoredPosition(slot.Rect, new Vector2(TargetX(relPos), 0f),
                    _transitionSeconds, Ease.OutCubic);
                Tween.Scale(slot.Rect, new Vector3(scale, scale, 1f),
                    _transitionSeconds, Ease.OutCubic);
                Tween.Alpha(slot.Group,
                    TurnQueueCarouselLayout.GetAlpha(relPos, _carousel),
                    _transitionSeconds, Ease.OutCubic);
            }

            if (exiting == null) return;

            // El que actuó se pierde por la izquierda…
            float exitX = TargetX(0) - _exitSlideDistance;
            Tween.UIAnchoredPosition(exiting.Rect, new Vector2(exitX, 0f),
                _transitionSeconds, Ease.InCubic);
            Tween.Alpha(exiting.Group, 0f, _transitionSeconds, Ease.InQuad)
                .OnComplete(() =>
                {
                    if (this == null || exiting == null) return;
                    if (_window.Count != TurnQueueCarouselLayout.WindowSize) return;

                    // …y reaparece como el último próximo entrando desde la derecha.
                    const int enterRel = TurnQueueCarouselLayout.UpcomingCount;
                    var enterGuid = TurnQueueCarouselLayout.GuidAt(_order, _cursor, enterRel);
                    BindSlotContent(exiting, enterGuid, enterRel);

                    float targetX = TargetX(enterRel);
                    float scale = TurnQueueCarouselLayout.GetScale(enterRel, _carousel);
                    exiting.Rect.anchoredPosition = new Vector2(targetX + _exitSlideDistance, 0f);
                    exiting.Rect.localScale = new Vector3(scale, scale, 1f);
                    exiting.Group.alpha = 0f;

                    Tween.UIAnchoredPosition(exiting.Rect, new Vector2(targetX, 0f),
                        _transitionSeconds, Ease.OutCubic);
                    Tween.Alpha(exiting.Group,
                        TurnQueueCarouselLayout.GetAlpha(enterRel, _carousel),
                        _transitionSeconds, Ease.OutQuad);
                });
        }

        private void CompleteWindowTweens()
        {
            // PrimeTween solo corre en play mode — en EditMode (tests) no hay
            // tweens vivos y el manager puede no existir.
            if (!Application.isPlaying) return;
            for (int i = 0; i < _window.Count; i++)
            {
                var slot = _window[i];
                if (slot == null) continue;
                Tween.CompleteAll(onTarget: slot.Rect);
                Tween.CompleteAll(onTarget: slot.Group);
            }
        }

        private void HandleTurnFinished(params object[] args)
        {
            if (!TryReadGuid(args, "OnTurnFinished", out var guid)) return;
            // Apagar el frame activo (la rotación llega con el próximo start).
            if (_window.Count == TurnQueueCarouselLayout.WindowSize
                && _order.Count > 0
                && _order[_cursor] == guid)
            {
                var active = _window[0];
                if (active != null) active.SetActive(false);
            }
        }

        private void HandleEntityDestroyed(params object[] args)
        {
            if (!TryReadGuid(args, "OnEntityDestroyed", out var guid)) return;

            if (_entities.TryGetValue(guid, out var info)) info.Destroyed = true;

            for (int i = 0; i < _window.Count; i++)
            {
                var slot = _window[i];
                if (slot == null || slot.SlotGuid != guid) continue;
                slot.SetDestroyed(true);
                slot.SetActive(false);
            }
        }

        // BUG-87: cruz de stun — mismo patrón que destroyed (la ventana puede
        // repetir el guid con pocas entidades; BindSlotContent re-aplica al rotar).
        private void HandleStunApplied(params object[] args)
        {
            if (!TryReadGuid(args, "OnStunApplied", out var guid)) return;
            ApplyStunned(guid, true);
        }

        private void HandleStunExpired(params object[] args)
        {
            if (!TryReadGuid(args, "OnStunExpired", out var guid)) return;
            ApplyStunned(guid, false);
        }

        private void ApplyStunned(Guid guid, bool stunned)
        {
            if (_entities.TryGetValue(guid, out var info)) info.Stunned = stunned;

            for (int i = 0; i < _window.Count; i++)
            {
                var slot = _window[i];
                if (slot == null || slot.SlotGuid != guid) continue;
                slot.SetStunned(stunned);
            }
        }

        private bool TryReadGuid(object[] args, string evName, out Guid guid)
        {
            guid = Guid.Empty;
            if (args == null || args.Length < 1)
            {
                Debug.LogWarning(LogPrefix + evName + " args malformed (len < 1).", this);
                return false;
            }
            if (!(args[0] is Guid g))
            {
                Debug.LogWarning(LogPrefix + evName + " args[0] is not Guid.", this);
                return false;
            }
            guid = g;
            return true;
        }
    }
}
