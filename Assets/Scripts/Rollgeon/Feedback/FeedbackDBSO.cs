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

        private readonly Dictionary<string, FeedbackEntry> _cache = new Dictionary<string, FeedbackEntry>();

        public IReadOnlyList<FeedbackEntry> Entries => _entries;

        private void OnEnable() => RebuildCache();
        private void OnValidate() => RebuildCache();

        public void RebuildCache()
        {
            _cache.Clear();
            if (_entries == null) return;
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e == null || string.IsNullOrEmpty(e.FeedbackId)) continue;
                _cache[e.FeedbackId] = e;
            }
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
