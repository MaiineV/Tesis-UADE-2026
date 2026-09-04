using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Audio;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.HUD.Status
{
    /// <summary>
    /// Fila de estados del player (arriba a la izquierda). Instancia un
    /// <see cref="StatusEffectIconView"/> por estado que publiquen sus
    /// <see cref="IStatusIconProvider"/>. Hoy el único es la pasiva de clase; el sistema de
    /// efectos de estado que viene se suma agregando un provider en
    /// <see cref="BuildProviders"/>, sin tocar esta vista.
    /// </summary>
    /// <remarks>
    /// Vive en <c>Canvas_PlayerStatus</c>, que está siempre activo — se ve igual en
    /// exploración y en combate. Lo bindea <c>ExplorationHUDView</c>, que es la screen base
    /// y nunca se popea durante la run.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Player Status Icons View")]
    public class PlayerStatusIconsView : MonoBehaviour
    {
        [Title("Wiring")]
        [SerializeField, Required]
        [Tooltip("Prefab de un ícono de estado (StatusEffectIcon).")]
        private StatusEffectIconView _iconPrefab;

        [SerializeField, Required]
        [Tooltip("Contenedor de la fila — lleva el HorizontalLayoutGroup.")]
        private RectTransform _container;

        [SerializeField]
        [Tooltip("Catálogo id → sprite para los estados sin asset propio (veneno, stun). " +
                 "Null = los slots muestran solo el marco hasta que llegue el arte.")]
        private StatusIconCatalogSO _statusIconCatalog;

        [SerializeField, Tooltip("SFX cuando un estado pasa de inactivo a ACTIVO (ej. la " +
                 "pasiva de clase prendiéndose). No suena en el primer Refresh de un Bind " +
                 "ni al apagarse. Null = mudo.")]
        private AudioClip _activateClip;

        [SerializeField, Range(0f, 1f), Tooltip("Volumen del SFX de activación.")]
        private float _activateVolume = 0.9f;

        [ShowInInspector, ReadOnly] private Guid _playerGuid;
        [ShowInInspector, ReadOnly] private bool _bound;

        private readonly List<IStatusIconProvider> _providers = new();
        private readonly List<StatusIconState> _states = new();
        private readonly List<StatusEffectIconView> _slots = new();

        // Último Active visto por estado (key = StatusIconState.Id) para detectar el
        // flanco apagado→prendido. Sobrevive Unbind/Bind a propósito: un rebind por
        // toggle del canvas no debe re-anunciar un estado que ya venía activo.
        private readonly Dictionary<string, bool> _lastActiveById = new();
        private bool _announceReady;

        public void Bind(Guid playerGuid)
        {
            if (_bound) Unbind();

            _playerGuid = playerGuid;
            BuildProviders();

            // La pasiva se prende y apaga por modifiers sobre atributos, así que estos tres
            // cubren todo lo que hoy la puede cambiar. OnTurnStarted es el resync barato para
            // estados futuros que dependan de turno y no toquen modifiers.
            EventManager.Subscribe(EventName.OnModifierAdded, HandleOwnerEvent);
            EventManager.Subscribe(EventName.OnModifierRemoved, HandleOwnerEvent);
            EventManager.Subscribe(EventName.OnAttributeChanged, HandleOwnerEvent);
            EventManager.Subscribe(EventName.OnTurnStarted, HandleOwnerEvent);
            // Estados con duración (veneno/stun): sus eventos de aplicar/tickear/expirar
            // llevan el guid del afectado en args[0], igual que el resto.
            EventManager.Subscribe(EventName.OnStunApplied, HandleOwnerEvent);
            EventManager.Subscribe(EventName.OnStunExpired, HandleOwnerEvent);
            EventManager.Subscribe(EventName.OnPoisonApplied, HandleOwnerEvent);
            EventManager.Subscribe(EventName.OnPoisonTicked, HandleOwnerEvent);
            EventManager.Subscribe(EventName.OnPoisonExpired, HandleOwnerEvent);
            EventManager.Subscribe(EventName.OnTeleportCooldownApplied, HandleOwnerEvent);
            EventManager.Subscribe(EventName.OnTeleportCooldownTicked, HandleOwnerEvent);
            EventManager.Subscribe(EventName.OnTeleportCooldownExpired, HandleOwnerEvent);
            // Lifecycle de casillas: args[0] es el instanceId, no el guid del player, así que
            // no pasa por HandleOwnerEvent. Refresh incondicional — un FireTemp que expira
            // bajo los pies debe apagar el ícono sin que el jugador se mueva.
            EventManager.Subscribe(EventName.OnSpecialTilePlaced, HandleTileLifecycleEvent);
            EventManager.Subscribe(EventName.OnSpecialTileExpired, HandleTileLifecycleEvent);
            TryHookMovement();
            _bound = true;

            // Primer repintado en silencio: si el estado ya venía activo (rebind, load
            // de save), el "se activó" ya sonó — o nunca correspondió — en su momento.
            _announceReady = false;
            Refresh();
            _announceReady = true;
        }

        public void Unbind()
        {
            if (!_bound) return;

            EventManager.UnSubscribe(EventName.OnModifierAdded, HandleOwnerEvent);
            EventManager.UnSubscribe(EventName.OnModifierRemoved, HandleOwnerEvent);
            EventManager.UnSubscribe(EventName.OnAttributeChanged, HandleOwnerEvent);
            EventManager.UnSubscribe(EventName.OnTurnStarted, HandleOwnerEvent);
            EventManager.UnSubscribe(EventName.OnStunApplied, HandleOwnerEvent);
            EventManager.UnSubscribe(EventName.OnStunExpired, HandleOwnerEvent);
            EventManager.UnSubscribe(EventName.OnPoisonApplied, HandleOwnerEvent);
            EventManager.UnSubscribe(EventName.OnPoisonTicked, HandleOwnerEvent);
            EventManager.UnSubscribe(EventName.OnPoisonExpired, HandleOwnerEvent);
            EventManager.UnSubscribe(EventName.OnTeleportCooldownApplied, HandleOwnerEvent);
            EventManager.UnSubscribe(EventName.OnTeleportCooldownTicked, HandleOwnerEvent);
            EventManager.UnSubscribe(EventName.OnTeleportCooldownExpired, HandleOwnerEvent);
            EventManager.UnSubscribe(EventName.OnSpecialTilePlaced, HandleTileLifecycleEvent);
            EventManager.UnSubscribe(EventName.OnSpecialTileExpired, HandleTileLifecycleEvent);
            UnhookMovement();
            _bound = false;
        }

        private void OnEnable()
        {
            // A diferencia del PassiveBadgeView viejo, re-bindeamos solos: este canvas puede
            // apagarse y prenderse sin que la screen que nos bindeó vuelva a correr su Bind,
            // y sin esto la fila quedaba congelada en el último estado que vio.
            if (!_bound && _playerGuid != Guid.Empty) Bind(_playerGuid);
        }

        private void OnDisable()
        {
            if (_bound) Unbind();
        }

        // Todos los eventos que escuchamos traen el guid del dueño en args[0]; los de otras
        // entidades no nos mueven la fila.
        private void HandleOwnerEvent(params object[] args)
        {
            // El MovementService es run-scoped y puede registrarse después de nuestro Bind:
            // reintento barato en cada evento hasta engancharlo (mismo criterio que
            // SpecialTileService.EnsureMovementSubscription).
            TryHookMovement();

            if (args == null || args.Length < 1) return;
            if (!(args[0] is Guid entityGuid) || entityGuid != _playerGuid) return;
            Refresh();
        }

        private void HandleTileLifecycleEvent(params object[] args) => Refresh();

        // ======================================================================
        // Hook de movimiento — los estados "parado sobre" cambian al caminar, y el
        // movimiento no viaja por el EventManager sino por delegados C#.
        // ======================================================================

        private Rollgeon.Movement.IMovementService _movementHooked;
        private Rollgeon.Movement.IPathedMovementService _teleportHooked;

        private void TryHookMovement()
        {
            if (_movementHooked != null) return;
            if (!ServiceLocator.TryGetService<Rollgeon.Movement.IMovementService>(out var movement)
                || movement == null)
            {
                return;
            }

            movement.OnEntityMoved += HandleEntityMoved;
            _movementHooked = movement;

            if (movement is Rollgeon.Movement.IPathedMovementService pathed)
            {
                // Teleport NO dispara OnEntityMoved por spec — sin este segundo hook el
                // ícono de tp-delay aparecería recién en el próximo evento de turno.
                pathed.OnEntityTeleported += HandleEntityTeleported;
                _teleportHooked = pathed;
            }
        }

        // Desuscribir SIEMPRE de la instancia cacheada: re-resolver por ServiceLocator
        // podría devolver una más nueva y leakear la vieja (gotcha _movementSubscribedTo
        // de SpecialTileService).
        private void UnhookMovement()
        {
            if (_movementHooked != null)
            {
                _movementHooked.OnEntityMoved -= HandleEntityMoved;
                _movementHooked = null;
            }
            if (_teleportHooked != null)
            {
                _teleportHooked.OnEntityTeleported -= HandleEntityTeleported;
                _teleportHooked = null;
            }
        }

        private void HandleEntityMoved(Guid entity, Rollgeon.Grid.GridCoord from,
            Rollgeon.Grid.GridCoord to, IReadOnlyList<Rollgeon.Grid.GridCoord> path)
        {
            if (entity == _playerGuid) Refresh();
        }

        private void HandleEntityTeleported(Guid entity, Rollgeon.Grid.GridCoord from,
            Rollgeon.Grid.GridCoord to)
        {
            if (entity == _playerGuid) Refresh();
        }

        /// <summary>Relee el estado real y repinta. Idempotente y barato.</summary>
        public void Refresh()
        {
            if (_container == null || _iconPrefab == null) return;

            _states.Clear();
            foreach (var provider in _providers)
                provider?.Collect(_playerGuid, _states);

            AnnounceActivations();
            EnsureSlots(_states.Count);

            for (int i = 0; i < _slots.Count; i++)
            {
                bool used = i < _states.Count;
                _slots[i].gameObject.SetActive(used);
                if (used) _slots[i].Show(_states[i]);
            }
        }

        // Flanco apagado→prendido por estado: acompaña con sonido a la aparición del
        // arte "activo" del ícono (la pasiva de clase prendiéndose). El repintado en sí
        // sigue siendo idempotente — solo el flanco anuncia.
        private void AnnounceActivations()
        {
            for (int i = 0; i < _states.Count; i++)
            {
                var state = _states[i];
                if (string.IsNullOrEmpty(state.Id)) continue;
                bool wasActive = _lastActiveById.TryGetValue(state.Id, out bool prev) && prev;
                if (_announceReady && state.Active && !wasActive) PlayActivateSfx();
                _lastActiveById[state.Id] = state.Active;
            }
        }

        private void PlayActivateSfx()
        {
            if (!Application.isPlaying || _activateClip == null) return;
            if (ServiceLocator.TryGetService<IAudioService>(out var audio) && audio != null)
                audio.PlaySfx2D(_activateClip, _activateVolume, isImportant: true);
        }

        // Los slots se reusan y solo se apagan: la fila se repinta en cada cambio de HP y
        // destruir/instanciar por refresh sería churn de GC en pleno combate.
        private void EnsureSlots(int needed)
        {
            while (_slots.Count < needed)
                _slots.Add(Instantiate(_iconPrefab, _container));
        }

        private void BuildProviders()
        {
            _providers.Clear();
            _providers.Add(new ClassPassiveStatusProvider());
            _providers.Add(new PoisonStatusProvider(_statusIconCatalog));
            _providers.Add(new BleedStatusProvider(_statusIconCatalog));
            _providers.Add(new BloodD6StatusProvider(_statusIconCatalog));
            _providers.Add(new StunStatusProvider(_statusIconCatalog));
            _providers.Add(new TileStandStatusProvider(_statusIconCatalog));
            _providers.Add(new TeleportCooldownStatusProvider(_statusIconCatalog));
        }
    }
}
