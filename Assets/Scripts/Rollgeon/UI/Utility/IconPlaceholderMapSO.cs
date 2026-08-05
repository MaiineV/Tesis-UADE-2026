using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rollgeon.UI.Utility
{
    /// <summary>
    /// Traduce placeholders de texto (<c>{ENERGY}</c>) al rich text de TMP que renderiza
    /// el glifo del atlas (<c>&lt;sprite name="Energy"&gt;</c>), para poder meter iconos
    /// dentro de strings autorados o localizados sin que el autor escriba markup.
    /// <para>
    /// El asset vive en <c>Assets/Resources/IconPlaceholderMap.asset</c> y lo puebla el
    /// <c>TMPSpriteAtlasGeneratorWindow</c> (tab "Assign to Project") leyendo los nombres
    /// de glifo del sprite asset default + sus fallbacks. Los call-sites lo consumen vía
    /// <see cref="IconSpriteTags"/>, no directo.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "IconPlaceholderMap", menuName = "Rollgeon/UI/Icon Placeholder Map")]
    public class IconPlaceholderMapSO : ScriptableObject
    {
        [Serializable]
        public struct IconMapping
        {
            [Tooltip("Token en el texto, ej: ENERGY (se usará como {ENERGY})")]
            public string placeholder;

            [Tooltip("Nombre del sprite en el TMP_SpriteAsset, ej: Energy")]
            public string spriteName;
        }

        [SerializeField] private List<IconMapping> mappings = new();

        private Dictionary<string, string> _cache;
        private int _lastCount = -1;

        private Dictionary<string, string> Cache
        {
            get
            {
                if (_cache == null || _lastCount != mappings.Count)
                    RebuildCache();
                return _cache;
            }
        }

        private void RebuildCache()
        {
            _cache = new Dictionary<string, string>(mappings.Count);
            foreach (var m in mappings)
            {
                if (!string.IsNullOrEmpty(m.placeholder) && !string.IsNullOrEmpty(m.spriteName))
                    _cache[$"{{{m.placeholder}}}"] = $"<sprite name=\"{m.spriteName}\">";
            }
            _lastCount = mappings.Count;
        }

        /// <summary>
        /// Reemplaza todos los placeholders conocidos por su tag de sprite. Los tokens que
        /// no estén mapeados quedan tal cual — un <c>{FOO}</c> visible en pantalla es la
        /// señal de que falta el glifo en el atlas.
        /// </summary>
        public string ReplacePlaceholders(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            foreach (var kvp in Cache)
            {
                if (text.Contains(kvp.Key))
                    text = text.Replace(kvp.Key, kvp.Value);
            }
            return text;
        }

        /// <summary>Tag de sprite para <paramref name="placeholder"/> (sin llaves), o <c>null</c>.</summary>
        public string GetSpriteTag(string placeholder)
        {
            var key = $"{{{placeholder}}}";
            return Cache.TryGetValue(key, out var tag) ? tag : null;
        }

        private void OnValidate()
        {
            _cache = null;
        }
    }
}
