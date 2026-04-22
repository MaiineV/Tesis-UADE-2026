using System;
using System.Collections.Generic;
using Rollgeon.Dungeon.State;
using UnityEngine;

namespace Rollgeon.Dungeon
{
    /// <summary>
    /// Wrapper dict-like para <see cref="RoomObjectState"/> polimórficos (TECHNICAL.md §13.6.1).
    /// <para>
    /// Unity no serializa <c>Dictionary&lt;string, TBase&gt;</c> con
    /// <c>[SerializeReference]</c> nativo. Dos listas paralelas (keys + values)
    /// preservan el subtipo concreto en round-trip (JsonUtility, YAML, Odin).
    /// </para>
    /// </summary>
    /// <remarks>
    /// Invariante: <see cref="_keys"/>.Count == <see cref="_values"/>.Count y
    /// las entries están alineadas por índice.
    /// </remarks>
    [Serializable]
    public sealed class SerializableObjectStates
    {
        [SerializeField] private List<string> _keys = new List<string>();

        [SerializeReference] private List<RoomObjectState> _values = new List<RoomObjectState>();

        public int Count => _keys.Count;

        public IReadOnlyList<string> Keys => _keys;

        public IReadOnlyList<RoomObjectState> Values => _values;

        /// <summary>Inserta o reemplaza el valor asociado a <paramref name="key"/>.</summary>
        public void Set(string key, RoomObjectState value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("key must be non-empty", nameof(key));

            int idx = _keys.IndexOf(key);
            if (idx >= 0)
            {
                _values[idx] = value;
                return;
            }
            _keys.Add(key);
            _values.Add(value);
        }

        public bool ContainsKey(string key) => _keys.IndexOf(key) >= 0;

        public bool TryGet(string key, out RoomObjectState value)
        {
            int idx = _keys.IndexOf(key);
            if (idx >= 0)
            {
                value = _values[idx];
                return true;
            }
            value = null;
            return false;
        }

        public bool TryGet<T>(string key, out T value) where T : RoomObjectState
        {
            int idx = _keys.IndexOf(key);
            if (idx >= 0 && _values[idx] is T typed)
            {
                value = typed;
                return true;
            }
            value = null;
            return false;
        }

        public bool Remove(string key)
        {
            int idx = _keys.IndexOf(key);
            if (idx < 0) return false;
            _keys.RemoveAt(idx);
            _values.RemoveAt(idx);
            return true;
        }

        public void Clear()
        {
            _keys.Clear();
            _values.Clear();
        }

        public IEnumerable<KeyValuePair<string, RoomObjectState>> Enumerate()
        {
            for (int i = 0; i < _keys.Count; i++)
                yield return new KeyValuePair<string, RoomObjectState>(_keys[i], _values[i]);
        }
    }
}