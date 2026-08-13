using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sirenix.Serialization;
using UnityEngine;

namespace Patterns.Save
{
    /// <summary>
    /// Sistema de save único del juego (TECHNICAL.md §15): cache en memoria
    /// <c>saveKey → serializedState</c> + flush a JSON por triggers configurables en
    /// <see cref="SaveSettingsSO"/>.
    /// <para>
    /// <b>WeakReferences.</b> El registro usa <c>WeakReference&lt;ISaveable&gt;</c> para
    /// que un saveable destruido sin <see cref="Unregister"/> no quede zombie — el
    /// primer <see cref="CaptureAll"/> posterior lo purga. Ojo: el purge NO reemplaza
    /// al <see cref="Unregister"/> explícito para saveables run-scoped que se recrean
    /// por run — dos instancias vivas con la misma key harían que la vieja (índice
    /// menor, iteración en reverso) se capture última y pise el cache.
    /// </para>
    /// <para>
    /// <b>Serialización.</b> Odin <c>SerializationUtility</c> (JSON): el cache guarda
    /// valores polimórficos (ints boxeados, <c>Dictionary&lt;,&gt;</c>, DTOs) que
    /// <c>JsonUtility</c> no puede round-trippear.
    /// </para>
    /// </summary>
    public static class SaveSystem
    {
        private const string LogPrefix = "[SaveSystem] ";

        /// <summary>
        /// Versión del schema del payload. Se bumpea a mano cuando un ISaveable cambia
        /// la forma de su estado de manera incompatible. Un mismatch al cargar descarta
        /// el save viejo con mensaje claro en vez de crashear al deserializar — no hay
        /// migration paths en fase de diseño (§15.3).
        /// </summary>
        // v2: RunContext pasó de boxed int a RunContextSnapshot {FloorIndex, RunId}.
        // v3: persistencia de dungeon (run.dungeon_state) + combate en curso
        //     (run.combat_state) — sala/posición/enemigos/turno (Feature#0028).
        public const int CurrentSchemaVersion = 3;

        private static readonly Dictionary<string, object> _cache = new();
        private static readonly List<WeakReference<ISaveable>> _registered = new();
        private static readonly HashSet<string> _dirty = new();
        private static readonly object _flushLock = new();

        private static ISaveFileStore _store = new FileSaveStore();

        // ====================================================================
        // Registration
        // ====================================================================

        public static void Register(ISaveable s)
        {
            if (s == null) return;

            foreach (var wr in _registered)
                if (wr.TryGetTarget(out var existing) && ReferenceEquals(existing, s))
                    return;

            _registered.Add(new WeakReference<ISaveable>(s));

            // Con estado previo en cache (scene reload mid-run, load desde disco),
            // el registro re-hidrata automáticamente.
            if (_cache.TryGetValue(s.SaveKey, out var state))
                s.RestoreState(state);
        }

        public static void Unregister(ISaveable s)
        {
            if (s == null) return;

            // Capturamos antes de soltar para que el cache retenga el estado final.
            for (int i = _registered.Count - 1; i >= 0; i--)
            {
                if (_registered[i].TryGetTarget(out var existing) && ReferenceEquals(existing, s))
                {
                    _cache[s.SaveKey] = s.CaptureState();
                    _registered.RemoveAt(i);
                    break;
                }
            }
        }

        public static void MarkDirty(ISaveable s)
        {
            if (s != null) _dirty.Add(s.SaveKey);
        }

        // ====================================================================
        // Capture / Restore
        // ====================================================================

        /// <summary>
        /// Recorre todos los ISaveable y actualiza el cache. Purga in-place las
        /// WeakReferences cuyo target fue recolectado por el GC.
        /// </summary>
        public static void CaptureAll()
        {
            for (int i = _registered.Count - 1; i >= 0; i--)
            {
                if (_registered[i].TryGetTarget(out var s))
                    _cache[s.SaveKey] = s.CaptureState();
                else
                    _registered.RemoveAt(i);
            }
            _dirty.Clear();
            EventManager.Trigger(EventName.OnCaptureRequested);
        }

        /// <summary>Sólo captura los marcados dirty. Más eficiente si el grafo es grande.</summary>
        public static void CaptureDirty()
        {
            for (int i = _registered.Count - 1; i >= 0; i--)
            {
                if (_registered[i].TryGetTarget(out var s))
                {
                    if (_dirty.Contains(s.SaveKey))
                        _cache[s.SaveKey] = s.CaptureState();
                }
                else
                {
                    _registered.RemoveAt(i);
                }
            }
            _dirty.Clear();
        }

        /// <summary>Restaura todos los ISaveable registrados desde el cache.</summary>
        public static void RestoreAll()
        {
            for (int i = _registered.Count - 1; i >= 0; i--)
            {
                if (_registered[i].TryGetTarget(out var s))
                {
                    if (_cache.TryGetValue(s.SaveKey, out var state))
                        s.RestoreState(state);
                }
                else
                {
                    _registered.RemoveAt(i);
                }
            }
            EventManager.Trigger(EventName.OnRestoreCompleted);
        }

        // ====================================================================
        // Flush / Load
        // ====================================================================

        /// <summary>
        /// Escribe el cache a disco si el trigger está habilitado en
        /// <see cref="SaveSettingsSO"/>. Bajo el threshold escribe sincrono en el
        /// frame; sobre el threshold despacha a <c>Task.Run</c> (§15.3.1).
        /// <c>Exit</c> siempre corre sincrono — es el último write y el lock compartido
        /// lo hace esperar a cualquier flush async en vuelo.
        /// </summary>
        public static void Flush(SaveTrigger trigger)
        {
            if (!ServiceLocator.TryGetService<SaveSettingsSO>(out var settings) || settings == null)
            {
                Debug.LogWarning(LogPrefix + $"Flush({trigger}) sin SaveSettingsSO registrado — skip.");
                return;
            }

            if (!settings.ShouldFlushOn(trigger)) return;

            var path = settings.GetSavePath();
            var payload = new SavePayload
            {
                SchemaVersion = CurrentSchemaVersion,
                Data = _cache,
            };
            var json = SerializationUtility.SerializeValue(payload, DataFormat.JSON);

            bool forceSync = trigger == SaveTrigger.Exit || json.Length < settings.AsyncFlushThresholdBytes;
            if (forceSync)
            {
                lock (_flushLock)
                {
                    _store.Write(path, json);
                }
                if (settings.LogFlushes)
                    Debug.Log(LogPrefix + $"Sync flush on {trigger} → {path} ({json.Length} B)");
            }
            else
            {
                FlushAsync(path, json, trigger, settings.LogFlushes);
            }
        }

        private static void FlushAsync(string path, byte[] json, SaveTrigger trigger, bool log)
        {
            Task.Run(() =>
            {
                // El lock serializa writes al mismo path — dos triggers back-to-back
                // no producen interleaving; el store garantiza atomicidad (tmp+rename).
                lock (_flushLock)
                {
                    _store.Write(path, json);
                }
                if (log)
                    Debug.Log(LogPrefix + $"Async flush on {trigger} → {path} ({json.Length} B)");
            });
        }

        /// <summary>
        /// Carga el save del slot activo y re-hidrata los ISaveable registrados.
        /// Defensivo: archivo ausente/corrupto o schema incompatible degradan a
        /// "sin save" en vez de crashear.
        /// </summary>
        public static void LoadFromDisk()
        {
            if (!ServiceLocator.TryGetService<SaveSettingsSO>(out var settings) || settings == null)
            {
                Debug.LogWarning(LogPrefix + "LoadFromDisk sin SaveSettingsSO registrado — skip.");
                return;
            }

            var path = settings.GetSavePath();
            if (!_store.Exists(path)) return;

            SavePayload payload;
            try
            {
                var json = _store.Read(path);
                payload = SerializationUtility.DeserializeValue<SavePayload>(json, DataFormat.JSON);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(LogPrefix + $"Save corrupt, starting fresh: {ex.Message}");
                return;
            }

            if (payload == null || payload.Data == null)
            {
                Debug.LogWarning(LogPrefix + "Save vacío o ilegible — arrancando sin save.");
                return;
            }

            if (payload.SchemaVersion != CurrentSchemaVersion)
            {
                Debug.LogWarning(LogPrefix +
                    $"Save schema v{payload.SchemaVersion} incompatible with current " +
                    $"v{CurrentSchemaVersion} — discarding save.");
                return;
            }

            _cache.Clear();
            foreach (var kvp in payload.Data) _cache[kvp.Key] = kvp.Value;

            RestoreAll();
        }

        /// <summary>
        /// Vacía cache y dirty set. Se invoca al inicio de cada run nueva
        /// (<c>RunBootstrapper.StartRun</c>) para que <see cref="Register"/> no
        /// re-hidrate estado de la run anterior.
        /// </summary>
        public static void Clear()
        {
            _cache.Clear();
            _dirty.Clear();
        }

        // ====================================================================
        // Queries (menú / resume)
        // ====================================================================

        /// <summary>
        /// <c>true</c> si existe un save en disco para el slot activo. Pasa por el
        /// store (no <c>File</c> directo) para poder testearse sin filesystem.
        /// </summary>
        public static bool HasSave()
        {
            if (!ServiceLocator.TryGetService<SaveSettingsSO>(out var settings) || settings == null)
                return false;
            return _store.Exists(settings.GetSavePath());
        }

        /// <summary>
        /// Peek del cache sin restaurar nada — para leer la identidad de la run
        /// (héroe, build, RunId) después de <see cref="LoadFromDisk"/> y antes de
        /// arrancar el resume.
        /// </summary>
        public static bool TryGetCached(string key, out object state)
        {
            return _cache.TryGetValue(key, out state);
        }

        /// <summary>
        /// Borra el save del slot activo y vacía el cache. Lo invoca
        /// <c>RunBootstrapper.EndRun</c> cuando la run terminó de verdad
        /// (victoria/derrota) — el quit mid-run NO pasa por acá.
        /// </summary>
        public static void DeleteSave()
        {
            if (ServiceLocator.TryGetService<SaveSettingsSO>(out var settings) && settings != null)
            {
                _store.Delete(settings.GetSavePath());
            }
            Clear();
        }

        // ====================================================================
        // Test hooks
        // ====================================================================

        /// <summary>Inyecta un store in-memory para EditMode tests. <c>null</c> restaura el default.</summary>
        public static void SetStoreForTests(ISaveFileStore store)
        {
            _store = store ?? new FileSaveStore();
        }

        /// <summary>Reset completo (cache + dirty + registrations + store) para teardown de tests.</summary>
        public static void ResetForTests()
        {
            _cache.Clear();
            _dirty.Clear();
            _registered.Clear();
            _store = new FileSaveStore();
        }

        /// <summary>Cantidad de registrations vivas — asserts de purge en tests.</summary>
        public static int RegisteredCountForTests => _registered.Count;
    }
}
