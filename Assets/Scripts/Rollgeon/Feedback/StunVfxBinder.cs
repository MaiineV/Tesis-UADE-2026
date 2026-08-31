using System;
using System.Collections.Generic;
using Patterns;
using UnityEngine;

namespace Rollgeon.Feedback
{
    /// <summary>
    /// Feedback visual persistente de stun (BUG-87): mientras una entidad está
    /// stuneada, un <c>ParticleSystem</c> (estrellitas) orbita sobre su pawn.
    /// Escucha <see cref="EventName.OnStunApplied"/> / <see cref="EventName.OnStunExpired"/>
    /// y resuelve guid→transform vía <see cref="IPawnRegistry"/>.
    /// </summary>
    /// <remarks>
    /// POCO con auto-install (patrón <c>IceStunBinder</c>) y no una <c>FeedbackEntry</c>:
    /// el <c>FeedbackManager</c> es fire-and-forget con duración, y este VFX vive
    /// "mientras dure el estado". <c>StunService.ClearAll()</c> (fin de combate/run)
    /// NO emite <c>OnStunExpired</c> por entidad, así que el cleanup de scope se hace
    /// acá escuchando <c>OnCombatEnd</c>/<c>OnRunEnd</c>.
    /// </remarks>
    public sealed class StunVfxBinder : IDisposable
    {
        private const string VfxResourcePath = "VFX_StunStars";
        private static readonly Vector3 LocalOffset = new Vector3(0f, 1.8f, 0f);

        private readonly Dictionary<Guid, GameObject> _activeVfx = new Dictionary<Guid, GameObject>();

        private GameObject _prefab;
        private bool _prefabLookupFailed;

        private EventManager.EventReceiver _onStunAppliedHandler;
        private EventManager.EventReceiver _onStunExpiredHandler;
        private EventManager.EventReceiver _onEntityDestroyedHandler;
        private EventManager.EventReceiver _onCombatEndHandler;
        private EventManager.EventReceiver _onRunEndHandler;

        // ======================================================================
        // Lifecycle
        // ======================================================================

        /// <summary>Devuelve el binder registrado, creándolo si hace falta.</summary>
        public static StunVfxBinder ResolveOrCreate()
        {
            if (ServiceLocator.TryGetService<StunVfxBinder>(out var existing) && existing != null)
                return existing;

            var binder = new StunVfxBinder();
            binder.Register();
            return binder;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall() => ResolveOrCreate();

        /// <summary>Suscribe handlers y se registra en el locator.</summary>
        public void Register()
        {
            _onStunAppliedHandler = OnStunAppliedExternal;
            _onStunExpiredHandler = OnStunExpiredExternal;
            _onEntityDestroyedHandler = OnEntityDestroyedExternal;
            _onCombatEndHandler = OnScopeEndedExternal;
            _onRunEndHandler = OnScopeEndedExternal;

            EventManager.Subscribe(EventName.OnStunApplied, _onStunAppliedHandler);
            EventManager.Subscribe(EventName.OnStunExpired, _onStunExpiredHandler);
            EventManager.Subscribe(EventName.OnEntityDestroyed, _onEntityDestroyedHandler);
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEndHandler);
            EventManager.Subscribe(EventName.OnRunEnd, _onRunEndHandler);

            ServiceLocator.AddService<StunVfxBinder>(this, ServiceScope.Global);
        }

        public void Dispose()
        {
            if (_onStunAppliedHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnStunApplied, _onStunAppliedHandler);
                _onStunAppliedHandler = null;
            }
            if (_onStunExpiredHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnStunExpired, _onStunExpiredHandler);
                _onStunExpiredHandler = null;
            }
            if (_onEntityDestroyedHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnEntityDestroyed, _onEntityDestroyedHandler);
                _onEntityDestroyedHandler = null;
            }
            if (_onCombatEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatEnd, _onCombatEndHandler);
                _onCombatEndHandler = null;
            }
            if (_onRunEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnRunEnd, _onRunEndHandler);
                _onRunEndHandler = null;
            }

            ClearAllVfx();
        }

        // ======================================================================
        // Event handlers
        // ======================================================================

        private void OnStunAppliedExternal(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid) || guid == Guid.Empty) return;
            if (_activeVfx.ContainsKey(guid)) return;
            if (Rollgeon.UI.HUD.DiceAnim.DiceUiMotionPrefs.ReducedMotion) return;

            var pawn = FeedbackPositionResolver.ResolvePawnTransform(guid);
            if (pawn == null) return;

            var prefab = ResolvePrefab();
            if (prefab == null) return;

            var vfx = UnityEngine.Object.Instantiate(prefab, pawn);
            vfx.transform.localPosition = LocalOffset;
            _activeVfx[guid] = vfx;
        }

        private void OnStunExpiredExternal(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            RemoveVfx(guid);
        }

        private void OnEntityDestroyedExternal(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            // El GO muere con el pawn, pero si el pawn se poolea en vez de
            // destruirse el Destroy explícito lo cubre igual.
            RemoveVfx(guid);
        }

        private void OnScopeEndedExternal(params object[] args) => ClearAllVfx();

        // ======================================================================
        // Internals
        // ======================================================================

        private GameObject ResolvePrefab()
        {
            if (_prefab != null) return _prefab;
            if (_prefabLookupFailed) return null;

            _prefab = Resources.Load<GameObject>(VfxResourcePath);
            if (_prefab == null)
            {
                _prefabLookupFailed = true;
                Debug.LogWarning("[StunVfxBinder] Resources/" + VfxResourcePath + ".prefab no existe — " +
                                 "correr Rollgeon → VFX → Build Stun Stars VFX. El stun queda sin partículas.");
            }
            return _prefab;
        }

        private void RemoveVfx(Guid guid)
        {
            if (!_activeVfx.TryGetValue(guid, out var vfx)) return;
            _activeVfx.Remove(guid);
            DestroySafe(vfx);
        }

        private void ClearAllVfx()
        {
            foreach (var vfx in _activeVfx.Values)
            {
                DestroySafe(vfx);
            }
            _activeVfx.Clear();
        }

        private static void DestroySafe(GameObject vfx)
        {
            if (vfx == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(vfx);
            else UnityEngine.Object.DestroyImmediate(vfx);
        }
    }
}
