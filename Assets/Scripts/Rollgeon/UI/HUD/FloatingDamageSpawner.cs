using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Entities;
using Rollgeon.Feedback;
using Rollgeon.Patterns;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

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
    /// <b>Estilo</b>: <see cref="FloatingNumberFormat"/> resuelve texto/tint/escala/motion
    /// a partir del <see cref="DamageResolvedPayload"/> (incoming vs outgoing vs weakness)
    /// o del <see cref="FloatingNumberType"/> del evento legacy (heal/shield/gold/status).
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

        // Tints ex-[SerializeField] movidos a FloatingNumberFormat/FloatingNumberPalette
        // (código) — evita que dos prefabs/escenas terminen con paletas divergentes.

        [SerializeField]
        [Tooltip("Offset en pixeles (screen) del punto de spawn. Suele ser un poco encima " +
                 "del sprite del target.")]
        private Vector3 _screenOffset = new Vector3(0f, 60f, 0f);

        [SerializeField, MinValue(0f), MaxValue(1f)]
        [Tooltip("Mínimo entre el spawn de un floating y el siguiente (segundos). Si dos eventos " +
                 "pegan en el mismo frame (ej. damage + gold drop al matar enemigo), el segundo " +
                 "se posterga este tiempo para que sean legibles.")]
        private float _staggerSeconds = 0.4f;

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

        // Tiempo (Time.time) hasta el cual el siguiente spawn debe esperar antes de aparecer.
        // Cada spawn lo avanza por _staggerSeconds — así eventos que llegan al mismo frame
        // (damage + oro) se distribuyen en el tiempo y no se solapan visualmente.
        private float _nextSpawnTime;

        // Mientras los dados vuelan al centro (outro del confirm Classic), los números
        // se encolan y aparecen recién al aterrizar — el daño "llega" con los dados en
        // vez de pisarles la animación. La posición se resuelve al ENCOLAR (el target
        // puede despawnear si el golpe fue letal).
        private readonly List<Action> _deferredSpawns = new List<Action>();

        private bool DeferIfOutroPending(string text, Color tint, Vector3 screenPos,
            float scale, FloatingMotion motion)
        {
            if (!Rollgeon.UI.HUD.DiceAnim.DiceOutroGate.OutroPending
                && !Rollgeon.Feedback.BreakdownUiGate.Pending) return false;
            _deferredSpawns.Add(() => SpawnAt(text, tint, screenPos, scale, motion));
            return true;
        }

        private void FlushDeferredIfIdle()
        {
            if (Rollgeon.UI.HUD.DiceAnim.DiceOutroGate.OutroPending
                || Rollgeon.Feedback.BreakdownUiGate.Pending) return;
            if (_deferredSpawns.Count == 0) return;
            var pending = new List<Action>(_deferredSpawns);
            _deferredSpawns.Clear();
            foreach (var spawn in pending) spawn();
        }

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
            Rollgeon.UI.HUD.DiceAnim.DiceOutroGate.Changed += FlushDeferredIfIdle;
            Rollgeon.Feedback.BreakdownUiGate.Changed += FlushDeferredIfIdle;
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
            Rollgeon.UI.HUD.DiceAnim.DiceOutroGate.Changed -= FlushDeferredIfIdle;
            Rollgeon.Feedback.BreakdownUiGate.Changed -= FlushDeferredIfIdle;
            _deferredSpawns.Clear();
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
        public FloatingDamageInstance SpawnAt(string text, Color tint, Vector3 screenPos,
            float scale = 1f, FloatingMotion motion = FloatingMotion.Rise)
        {
            if (_instancePrefab == null)
            {
                Debug.LogWarning(LogPrefix + "_instancePrefab no esta cableado — skip spawn.", this);
                return null;
            }

            var parent = ResolveSpawnParent();
            if (parent == null)
            {
                Debug.LogWarning(LogPrefix + "no hay container donde instanciar — skip spawn.", this);
                return null;
            }

            // Auto-stagger: si todavía hay un spawn programado en el futuro, postergamos
            // este. Así dos eventos disparados en el mismo frame se ven secuencialmente.
            float now = Time.time;
            float scheduled = Mathf.Max(now, _nextSpawnTime);
            _nextSpawnTime = scheduled + _staggerSeconds;
            float delay = scheduled - now;

            // Bakeamos el offset acá para que el spawn diferido no dependa de este
            // componente (que puede destruirse al cerrar el CombatHUD).
            Vector3 finalPos = screenPos + _screenOffset;

            if (delay > 0f && Application.isPlaying)
            {
                // El spawn diferido (ej. el oro, staggered 0.4s tras el daño) corre en el
                // CoroutineHost persistente y apunta al canvas persistente — así aparece
                // aunque el combate ya haya terminado y el CombatHUD se haya destruido.
                // El caller tolera null cuando hay stagger.
                CoroutineHost.Run(DelayedSpawn(_instancePrefab, parent, text, tint, finalPos, delay, scale, motion));
                return null;
            }

            return SpawnInto(_instancePrefab, parent, text, tint, finalPos, scale, motion);
        }

        private static IEnumerator DelayedSpawn(FloatingDamageInstance prefab, Transform parent,
            string text, Color tint, Vector3 pos, float delay, float scale, FloatingMotion motion)
        {
            yield return new WaitForSeconds(delay);
            SpawnInto(prefab, parent, text, tint, pos, scale, motion);
        }

        private static FloatingDamageInstance SpawnInto(FloatingDamageInstance prefab, Transform parent,
            string text, Color tint, Vector3 pos, float scale, FloatingMotion motion)
        {
            if (prefab == null || parent == null) return null;
            var instance = Instantiate(prefab, parent);
            instance.Play(text, tint, pos, scale, motion);
            return instance;
        }

        // Padre donde se instancian los números. En runtime usa el canvas persistente
        // (DontDestroyOnLoad) para que sobrevivan al teardown del CombatHUD; copia el
        // scaler del canvas del HUD para mantener tamaño/posición idénticos. En EditMode/
        // tests (no isPlaying) cae a _overlayContainer — comportamiento de siempre.
        private Transform ResolveSpawnParent()
        {
            if (Application.isPlaying)
            {
                CanvasScaler reference = _overlayContainer != null
                    ? _overlayContainer.GetComponentInParent<CanvasScaler>()
                    : null;
                var persistent = PersistentUiOverlay.GetContainer(reference);
                if (persistent != null) return persistent;
            }
            return _overlayContainer;
        }

        // ======================================================================
        // Handlers
        // ======================================================================

        private void HandleDamageResolved(DamageResolvedPayload payload)
        {
            var screenPos = ResolveScreenPos(payload.TargetGuid);

            // Shield bloqueó todo: mensaje explícito en vez del "0" viejo (confundía,
            // parecía daño real de 0 en vez de "no pasó nada").
            if (payload.BlockedByShield)
            {
                var blocked = FloatingNumberFormat.ShieldBlocked();
                SpawnOrDefer(blocked.Text, blocked.Tint, screenPos, blocked.Scale, blocked.Motion);
                return;
            }

            bool incoming = payload.TargetGuid == _playerGuid;

            // Shield rompió en este hit Y queda daño residual: primero "Escudo roto" en
            // color shield, después el daño residual. El auto-stagger del SpawnAt los
            // separa en el tiempo automáticamente.
            if (payload.ShieldBroken && payload.FinalDamage > 0)
            {
                var broken = FloatingNumberFormat.ShieldBroken();
                SpawnOrDefer(broken.Text, broken.Tint, screenPos, broken.Scale, broken.Motion);
            }

            // El stage 3 recortó el golpe (la mesa de La Generala en pie): el porcentaje va antes del
            // número, mismo riel que "Escudo roto". Sin esto un golpe de 30 que hace 9 no tiene
            // explicación en pantalla y el jugador aprende "mis golpes no sirven" en vez de "rompé los
            // dados". El rango y no != 1: el payload es un struct y uno armado a mano trae 0.
            if (payload.IncomingMultiplier > 0f && payload.IncomingMultiplier < 1f)
            {
                var reduced = FloatingNumberFormat.DamageReduced(payload.IncomingMultiplier);
                SpawnOrDefer(reduced.Text, reduced.Tint, screenPos, reduced.Scale, reduced.Motion);
            }

            var style = FloatingNumberFormat.ForDamage(payload.FinalDamage, incoming, payload.WeaknessHit);
            SpawnOrDefer(style.Text, style.Tint, screenPos, style.Scale, style.Motion);
        }

        private void SpawnOrDefer(string text, Color tint, Vector3 screenPos, float scale, FloatingMotion motion)
        {
            if (DeferIfOutroPending(text, tint, screenPos, scale, motion)) return;
            SpawnAt(text, tint, screenPos, scale, motion);
        }

        private void HandleFloatingNumberRequested(params object[] args)
        {
            // schema: [Guid targetGuid, FloatingNumberType type, float value, Vector3 offset]
            if (args == null || args.Length < 3) return;
            if (!(args[0] is Guid target)) return;

            var type = args[1] is FloatingNumberType ft ? ft : FloatingNumberType.Heal;

            // El slot del valor acepta texto además de número: hay avisos que no son una cantidad
            // ("Soborno", "-1 escalón") y forzarlos a float los dejaría con el "+" del formato
            // numérico. El tint/escala/motion los sigue poniendo el tipo — ver ForText.
            var style = args[2] is string text
                ? FloatingNumberFormat.ForText(type, text)
                : FloatingNumberFormat.ForType(type, args[2] is float f ? f : (args[2] is int i ? i : 0f));

            var screenPos = ResolveScreenPos(target);
            SpawnOrDefer(style.Text, style.Tint, screenPos, style.Scale, style.Motion);
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

            // Fallback al PawnRegistry: el player suele estar en uno de los dos registries
            // (depende de qué prefab/sistema lo spawneó). Si EntityPositionResolver no lo
            // tiene, el PawnRegistry sí. Ambos apuntan al mismo Transform en runtime sano.
            if (!worldPos.HasValue
                && ServiceLocator.TryGetService<IPawnRegistry>(out var pawnReg) && pawnReg != null
                && pawnReg.TryGetTransform(entityGuid, out var pawnTransform) && pawnTransform != null)
            {
                worldPos = pawnTransform.position;
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
