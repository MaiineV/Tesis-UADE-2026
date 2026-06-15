using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Meta
{
    /// <summary>
    /// Implementación runtime de <see cref="IMetaProgressionService"/> (#164).
    /// <para>
    /// Clase plana registrada en <c>ServiceScope.Global</c> vía la lista
    /// <c>ExtraServices</c> del <c>ServiceBootstrapSO</c>. En <see cref="Register"/>
    /// lee el save file y deja los pools actualizados <b>antes</b> de que el jugador
    /// interactúe con ningún menú (corre durante <c>00_Bootstrap</c>).
    /// </para>
    /// <para>
    /// <b>Prioridad.</b> <see cref="DefaultPriority"/> = 10 — temprano, para que
    /// cualquier servicio downstream que consulte disponibilidad ya vea el estado
    /// hidratado. El <see cref="UnlockCatalogSO"/> se resuelve lazy porque los
    /// Catalogs se registran antes que los ExtraServices en <c>RegisterAll()</c>.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class MetaProgressionService : IMetaProgressionService, IPreloadableService, IDisposable
    {
        private const string LogPrefix = "[MetaProgressionService] ";

        public const int DefaultPriority = 10;

        [NonSerialized] private MetaProgressionState _state;
        [NonSerialized] private IMetaSaveStore _store;
        [NonSerialized] private UnlockCatalogSO _catalog;
        [NonSerialized] private List<UnlockDefinitionSO> _definitionsCache;

        /// <inheritdoc />
        public int Priority => DefaultPriority;

        /// <summary>Estado persistente expuesto para el SaveSystem (§15) y tests.</summary>
        public MetaProgressionState State => _state ??= new MetaProgressionState();

        // ====================================================================
        // IPreloadableService
        // ====================================================================

        /// <inheritdoc />
        public void Register()
        {
            _store ??= new FileMetaSaveStore();
            _state ??= new MetaProgressionState();

            var snapshot = _store.Load();
            if (snapshot != null)
            {
                _state.RestoreState(snapshot);
                Debug.Log(LogPrefix + $"Save cargado: {_state.UnlockedTargetKeys.Count} unlocks, " +
                          $"streak={_state.ConsecutiveWins}, clases={_state.ClassesPlayed.Count}.");
            }

            ServiceLocator.TryGetService<UnlockCatalogSO>(out _catalog);
            ServiceLocator.AddService<IMetaProgressionService>(this, ServiceScope.Global);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _definitionsCache = null;
        }

        // ====================================================================
        // Test hooks
        // ====================================================================

        /// <summary>Hook para EditMode tests — inyecta store y catálogo sin pasar por el ServiceLocator.</summary>
        public void ConfigureForTests(IMetaSaveStore store, UnlockCatalogSO catalog)
        {
            _store = store;
            _catalog = catalog;
            _definitionsCache = null;
            _state ??= new MetaProgressionState();
        }

        // ====================================================================
        // IMetaProgressionService
        // ====================================================================

        /// <inheritdoc />
        public bool IsAvailable(UnlockableCategory category, string targetId)
        {
            if (string.IsNullOrEmpty(targetId)) return false;

            var key = UnlockDefinitionSO.MakeTargetKey(category, targetId);
            if (State.UnlockedTargetKeys.Contains(key)) return true;

            // Pool base: sin definición que lo gatee, el elemento está disponible.
            return !IsGated(category, targetId);
        }

        /// <inheritdoc />
        public bool IsDefinitionCompleted(UnlockDefinitionSO definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.UnlockId)) return false;
            return State.CompletedUnlockIds.Contains(definition.UnlockId);
        }

        /// <inheritdoc />
        public IReadOnlyList<UnlockDefinitionSO> Definitions
        {
            get
            {
                if (_definitionsCache != null) return _definitionsCache;

                if (_catalog == null)
                {
                    ServiceLocator.TryGetService<UnlockCatalogSO>(out _catalog);
                }
                if (_catalog == null)
                {
                    // Sin catálogo todavía — no cachear, puede registrarse después.
                    return Array.Empty<UnlockDefinitionSO>();
                }

                _definitionsCache = new List<UnlockDefinitionSO>();
                if (_catalog.Entries != null)
                {
                    foreach (var def in _catalog.Entries)
                    {
                        if (def != null) _definitionsCache.Add(def);
                    }
                }
                return _definitionsCache;
            }
        }

        /// <inheritdoc />
        public bool TryUnlock(UnlockDefinitionSO definition, bool duringRun)
        {
            if (definition == null || string.IsNullOrEmpty(definition.UnlockId)) return false;
            if (!State.CompletedUnlockIds.Add(definition.UnlockId)) return false;

            State.UnlockedTargetKeys.Add(definition.TargetKey);
            SaveNow();

            Debug.Log(LogPrefix + $"Unlock '{definition.UnlockId}' → {definition.TargetKey} " +
                      $"({(duringRun ? "mid-run" : "fin de run")}).");

            TypedEvent<UnlockAchievedPayload>.Raise(new UnlockAchievedPayload
            {
                UnlockId = definition.UnlockId,
                Category = definition.Category,
                TargetId = definition.TargetId,
                DisplayName = definition.DisplayName,
                DuringRun = duringRun,
            });
            return true;
        }

        /// <inheritdoc />
        public int ConsecutiveWins => State.ConsecutiveWins;

        /// <inheritdoc />
        public IReadOnlyCollection<string> ClassesPlayed => State.ClassesPlayed;

        /// <inheritdoc />
        public void RecordRunCompleted(bool won, string classId)
        {
            if (!string.IsNullOrEmpty(classId))
            {
                State.ClassesPlayed.Add(classId);
            }

            // Consistencia vs acumulación (#164): la racha resetea al morir,
            // el set de clases jugadas nunca.
            State.ConsecutiveWins = won ? State.ConsecutiveWins + 1 : 0;
            SaveNow();
        }

        /// <inheritdoc />
        public void SaveNow()
        {
            _store?.Save((MetaProgressionSnapshot)State.CaptureState());
        }

        /// <inheritdoc />
        public void ResetProgression()
        {
            State.RestoreState(null);
            _store?.Delete();
            Debug.Log(LogPrefix + "Meta-progresión reseteada a estado inicial (save borrado).");
        }

        // ====================================================================
        // Helpers
        // ====================================================================

        private bool IsGated(UnlockableCategory category, string targetId)
        {
            var defs = Definitions;
            for (int i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                if (def.Category == category &&
                    string.Equals(def.TargetId, targetId, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
