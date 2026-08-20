using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rollgeon.UI.HUD.Status
{
    /// <summary>
    /// Mapa id de estado → sprite del ícono. Los providers de estados (veneno, stun) son
    /// POCOs sin acceso a assets: la vista les inyecta este catálogo. Agregar el arte de un
    /// estado nuevo = una entry acá, sin tocar código.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/UI/Status Icon Catalog", fileName = "StatusIconCatalog")]
    public class StatusIconCatalogSO : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("Id estable del estado (status.poison, status.stun).")]
            public string Id;
            public Sprite Icon;
        }

        [SerializeField] private List<Entry> _entries = new List<Entry>();

        /// <summary>Sprite del estado, o null si no hay arte todavía (el slot dibuja solo el marco).</summary>
        public Sprite Resolve(string id)
        {
            if (string.IsNullOrEmpty(id) || _entries == null) return null;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Id == id) return _entries[i].Icon;
            }
            return null;
        }
    }
}
