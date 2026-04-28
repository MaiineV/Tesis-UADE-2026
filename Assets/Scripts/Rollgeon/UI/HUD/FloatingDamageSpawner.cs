using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Entities;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Spawner de numeros flotantes ligado al canal <see cref="TypedEvent{T}"/> de
    /// <see cref="DamageResolvedPayload"/>. Tambien escucha el legacy
    /// <see cref="EventName.OnFloatingNumberRequested"/> para heals/shields/status ticks.
    /// Plan §3.8.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Posicion</b>: resuelve world pos via <see cref="IEntityPositionResolver"/>
    /// (opcional). Fallback: centro de la pantalla con un offset vertical.
    /// </para>
    /// <para>
    /// <b>Tint</b>: el <c>SourceGuid</c> se compara con el player para pintar rojo
    /// (incoming) o verde (outgoing). Los ticks de heal del evento legacy usan un
    /// tint separado.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Floating Damage Spawner")]
    public class FloatingDamageSpawner : MonoBehaviour
    {
        private const string LogPrefix = "[FloatingDamageSpawner] ";

        [Title("Floating Damage — Prefab + canvas")]
        [Required("Arrastrar el prefab de FloatingDamageInstance (instructivo §8.3).")]
        [SerializeField]
        private FloatingDamageInstance _instancePrefab;

        [Required("Arrastrar el RectTransform del canvas worldspace / overlay donde se " +
                  "instancian los numeros.")]
        [SerializeField]
        private RectTransform _overlayContainer;

        [SerializeField]
        [Tooltip("Camera que convierte world->screen position. Si null, se usa " +
                 "Camera.main. Solo necesaria si el canvas es ScreenSpace-Camera/Worldspace.")]
        private Camera _uiCamera;

        [Title("Floating Damage — Tints")]
        [SerializeField]
        [Tooltip("Color del numero cuando el player hace dano (outgoing).")]
        private Color _outgoingTint = new Color(1f, 0.94f, 0.27f, 1f);

        [SerializeField]
        [Tooltip("Color del numero cuando el player recibe dano (incoming).")]
        private Color _incomingTint = new Color(1f, 0.25f, 0.25f, 1f);

        [SerializeField]
        [Tooltip("Color del numero para heals / status ticks positivos.")]
        private Color _healTint = new Color(0.35f, 1f, 0.45f, 1f);

        [SerializeField]
        [Tooltip("Offset en pixeles (screen) del punto de spawn. Suele ser un poco encima " +
                 "del sprite del target.")]
        private Vector3 _screenOffset = new Vector3(0f, 60f, 0f);

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        private Action<DamageResolvedPayload> _onDamageResolved;

        // Cache de la última worldPos conocida por GUID. Cuando un damage es lethal,
        // el CombatDeathWatcher despawnea al target antes de que este handler corra
        // — sin cache, el último hit caería al centro. Con cache, lo mostramos sobre
        // la última posición conocida del target.
        private readonly Dictionary<Guid, Vector3> _lastKnownWorldPos = new Dictionary<Guid, Vector3>();

        public void Bind(Guid playerGuid)
        {
            if (_bound) Unbind();
            _playerGuid = playerGuid;

            // Si el combate anterior se cerró con animaciones en curso, las coroutines
            // quedaron suspendidas en GOs residuales del container — al reactivar el HUD
            // aparecen visibles. Limpiamos antes de bindear para garantizar estado limpio.
            ClearActiveInstances();
            _lastKnownWorldPos.Clear();

            _onDamageResolved = HandleDamageResolved;
            TypedEvent<DamageResolvedPayload>.Subscribe(_onDamageResolved);

            EventManager.Subscribe(EventName.OnFloatingNumberRequested, HandleFloatingNumberRequested);
            _bound = true;
        }

        public void Unbind()
        {
            if (!_bound) return;

            if (_onDamageResolved != null)
            {
                TypedEvent<DamageResolvedPayload>.Unsubscribe(_onDamageResolved);
                _onDamageResolved = null;
            }

            EventManager.UnSubscribe(EventName.OnFloatingNumberRequested, HandleFloatingNumberRequested);
            ClearActiveInstances();
            _bound = false;
        }

        private void ClearActiveInstances()
        {
            if (_overlayContainer == null) return;
            var instances = _overlayContainer.GetComponentsInChildren<FloatingDamageInstance>(includeInactive: true);
            for (int i = 0; i < instances.Length; i++)
            {
                if (instances[i] != null) Destroy(instances[i].gameObject);
            }
        }

        private void OnDisable()
        {
            if (_bound) Unbind();
        }

        // ======================================================================
        // API publica (tests / tooling)
        // ======================================================================

        /// <summary>
        /// Spawnea una instancia manualmente. Publico para tests/tooling que no
        /// quieran pasar por el bus de eventos.
        /// </summary>
        public FloatingDamageInstance SpawnAt(string text, Color tint, Vector3 screenPos)
        {
            if (_instancePrefab == null)
            {
                Debug.LogWarning(LogPrefix + "_instancePrefab no esta cableado — skip spawn.", this);
                return null;
            }
            if (_overlayContainer == null)
            {
                Debug.LogWarning(LogPrefix + "_overlayContainer no esta cableado — skip spawn.", this);
                return null;
            }

            var instance = Instantiate(_instancePrefab, _overlayContainer);
            instance.Play(text, tint, screenPos + _screenOffset);
            return instance;
        }

        // ======================================================================
        // Handlers
        // ======================================================================

        private void HandleDamageResolved(DamageResolvedPayload payload)
        {
            // Tint: si el player fue source -> outgoing; si fue target -> incoming;
            // otro caso (enemy vs enemy) -> outgoing default (amarillo).
            Color tint = _outgoingTint;
            if (payload.TargetGuid == _playerGuid) tint = _incomingTint;
            else if (payload.SourceGuid == _playerGuid) tint = _outgoingTint;

            // Posicion: resolver world pos del target; si no, fallback center.
            var screenPos = ResolveScreenPos(payload.TargetGuid);

            string text = payload.FinalDamage.ToString();
            if (payload.WeaknessHit) text += "!";
            SpawnAt(text, tint, screenPos);
        }

        private void HandleFloatingNumberRequested(params object[] args)
        {
            // schema: [Guid targetGuid, FloatingNumberType type, float value, Vector3 offset]
            // No tenemos enum FloatingNumberType importado; leemos value+target y usamos
            // un tint neutral (heal) por default.
            if (args == null || args.Length < 3) return;
            if (!(args[0] is Guid target)) return;
            float value = args[2] is float f ? f : 0f;

            var screenPos = ResolveScreenPos(target);
            SpawnAt(value.ToString("0"), _healTint, screenPos);
        }

        private Vector3 ResolveScreenPos(Guid entityGuid)
        {
            Vector3? worldPos = null;
            if (ServiceLocator.TryGetService<IEntityPositionResolver>(out var resolver) && resolver != null)
            {
                worldPos = resolver.TryGetWorldPosition(entityGuid);
                if (worldPos.HasValue)
                    _lastKnownWorldPos[entityGuid] = worldPos.Value;
            }

            // Fallback al cache: si el target ya fue despawneado (lethal hit), usamos
            // la última posición conocida en vez del centro de pantalla.
            if (!worldPos.HasValue && _lastKnownWorldPos.TryGetValue(entityGuid, out var cached))
                worldPos = cached;

            if (worldPos.HasValue)
            {
                var cam = _uiCamera != null ? _uiCamera : Camera.main;
                if (cam != null)
                {
                    // Cámara renderiza al RT del pipeline pixel-art: WorldToScreenPoint
                    // devuelve coords en el espacio del RT (cam.pixelWidth/Height), no
                    // en Screen space. El canvas overlay del HUD usa Screen — escalamos.
                    var rtPos = cam.WorldToScreenPoint(worldPos.Value);
                    float sx = cam.pixelWidth > 0 ? rtPos.x / cam.pixelWidth * Screen.width : rtPos.x;
                    float sy = cam.pixelHeight > 0 ? rtPos.y / cam.pixelHeight * Screen.height : rtPos.y;
                    return new Vector3(sx, sy, rtPos.z);
                }
            }

            // Fallback final: centro de la pantalla.
            return new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        }
    }
}
