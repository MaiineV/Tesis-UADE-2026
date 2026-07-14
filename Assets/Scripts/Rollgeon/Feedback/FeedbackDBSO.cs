using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Feedback
{
    /// <summary>
    /// Base de datos autoral de <see cref="FeedbackEntry"/>. TECHNICAL.md §10.2.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Feedback/Feedback DB", fileName = "FeedbackDBSO")]
    public class FeedbackDBSO : ScriptableObject
    {
        [SerializeField]
        [ListDrawerSettings(DraggableItems = true, ShowFoldout = true)]
        private List<FeedbackEntry> _entries = new List<FeedbackEntry>();

        [SerializeField]
        [ListDrawerSettings(DraggableItems = true, ShowFoldout = true)]
        private List<FeedbackSequenceEntry> _sequences = new List<FeedbackSequenceEntry>();

        private readonly Dictionary<string, FeedbackEntry> _cache = new Dictionary<string, FeedbackEntry>();
        private readonly Dictionary<string, FeedbackSequenceEntry> _sequenceCache =
            new Dictionary<string, FeedbackSequenceEntry>();

        public IReadOnlyList<FeedbackEntry> Entries => _entries;
        public IReadOnlyList<FeedbackSequenceEntry> Sequences => _sequences;

        private void OnEnable() => RebuildCache();
        private void OnValidate() => RebuildCache();

        public void RebuildCache()
        {
            _cache.Clear();
            if (_entries != null)
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    var e = _entries[i];
                    if (e == null || string.IsNullOrEmpty(e.FeedbackId)) continue;
                    _cache[e.FeedbackId] = e;
                }
            }

            _sequenceCache.Clear();
            if (_sequences == null) return;
            for (int i = 0; i < _sequences.Count; i++)
            {
                var s = _sequences[i];
                if (s == null || string.IsNullOrEmpty(s.SequenceId)) continue;
                _sequenceCache[s.SequenceId] = s;
            }
        }

        /// <summary>
        /// Resuelve una secuencia autorada por id. Contraparte de <see cref="TryGetFeedback"/>
        /// para los callers que disparan secuencias desde código (ej. la muerte de un enemigo,
        /// que no nace de un <c>EffPlaySequence</c> autorado y por eso no trae steps inline).
        /// </summary>
        public bool TryGetSequence(string sequenceId, out List<FeedbackSequenceStep> steps)
        {
            steps = null;
            if (string.IsNullOrEmpty(sequenceId)) return false;
            if (_sequenceCache.Count == 0 && _sequences != null && _sequences.Count > 0) RebuildCache();
            if (!_sequenceCache.TryGetValue(sequenceId, out var entry) || entry == null) return false;
            steps = entry.Steps;
            return steps != null && steps.Count > 0;
        }

        public IEnumerable<string> GetAllSequenceIds()
        {
            if (_sequenceCache.Count == 0 && _sequences != null && _sequences.Count > 0) RebuildCache();
            return _sequenceCache.Keys;
        }

        public bool TryGetFeedback(string feedbackId, out FeedbackEntry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(feedbackId)) return false;
            if (_cache.Count == 0 && _entries != null && _entries.Count > 0) RebuildCache();
            return _cache.TryGetValue(feedbackId, out entry);
        }

        public FeedbackEntry GetFeedbackOrDefault(string feedbackId) =>
            TryGetFeedback(feedbackId, out var e) ? e : null;

        public bool HasFeedback(string feedbackId) => TryGetFeedback(feedbackId, out _);

        public IEnumerable<string> GetAllFeedbackIds()
        {
            if (_cache.Count == 0 && _entries != null && _entries.Count > 0) RebuildCache();
            return _cache.Keys;
        }

        public IEnumerable<string> GetFilteredFeedbackIds(FeedbackType type)
        {
            if (_cache.Count == 0 && _entries != null && _entries.Count > 0) RebuildCache();
            foreach (var kv in _cache)
                if (kv.Value.Type == type) yield return kv.Key;
        }

#if UNITY_EDITOR
        [Button("Find duplicates")]
        private void FindDuplicates()
        {
            var seen = new HashSet<string>();
            var dupes = new List<string>();
            foreach (var e in _entries)
            {
                if (e == null || string.IsNullOrEmpty(e.FeedbackId)) continue;
                if (!seen.Add(e.FeedbackId)) dupes.Add(e.FeedbackId);
            }
            if (dupes.Count == 0) Debug.Log("[FeedbackDBSO] No duplicates.");
            else Debug.LogWarning($"[FeedbackDBSO] Duplicate ids: {string.Join(", ", dupes)}");
        }

        [Button("Remove empty entries")]
        private void RemoveEmptyEntries()
        {
            _entries.RemoveAll(e => e == null || string.IsNullOrEmpty(e.FeedbackId));
            RebuildCache();
        }

        [Button("Sort by Id")]
        private void SortById()
        {
            _entries.Sort((a, b) =>
                string.Compare(a?.FeedbackId, b?.FeedbackId, System.StringComparison.Ordinal));
            RebuildCache();
        }

        [Button("Clear all")]
        private void ClearAll()
        {
            _entries.Clear();
            RebuildCache();
        }
#endif
    }
}
