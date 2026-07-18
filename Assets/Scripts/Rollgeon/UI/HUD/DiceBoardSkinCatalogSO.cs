using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Config de sprites del tablero de dados por <see cref="DiceBoardType"/>. El artista
    /// edita acá qué skin (sprite + tint + modo de Image) usa cada tipo de tirada; el
    /// <see cref="DiceBoardSkinView"/> lo consume y lo aplica sobre el Image existente del
    /// <c>DiceZoneView</c>. Modelado sobre <c>DiceShapeCatalogSO</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/UI/Dice Board Skin Catalog", fileName = "DiceBoardSkinCatalog")]
    public class DiceBoardSkinCatalogSO : ScriptableObject
    {
        [ListDrawerSettings(ShowFoldout = false)]
        [Tooltip("Skin de cada tipo de tablero. Ideal cubrir Default/Attack/Defense; " +
                 "un tipo sin entrada degrada a Default.")]
        public List<DiceBoardSkinEntry> Skins = new();

        /// <summary>
        /// Skin de <paramref name="type"/>, o la de <see cref="DiceBoardType.Default"/> si ese
        /// tipo no está autorado. Degradación en cadena <c>type → Default</c> para que un
        /// catálogo a medio autorar igual dibuje algo en vez de dejar el tablero sin skin.
        /// Devuelve <c>false</c> solo si tampoco hay Default: el caller deja el look actual.
        /// </summary>
        public bool TryGet(DiceBoardType type, out DiceBoardSkinEntry skin)
        {
            if (TryGetExact(type, out skin)) return true;
            // Degradación a Default: nunca dejamos el tablero sin skin por un tipo faltante.
            if (type != DiceBoardType.Default && TryGetExact(DiceBoardType.Default, out skin)) return true;
            skin = default;
            return false;
        }

        private bool TryGetExact(DiceBoardType type, out DiceBoardSkinEntry skin)
        {
            if (Skins != null)
            {
                for (int i = 0; i < Skins.Count; i++)
                {
                    if (Skins[i].Type != type) continue;
                    if (Skins[i].Sprite == null) break; // entrada sin arte: no es usable
                    skin = Skins[i];
                    return true;
                }
            }
            skin = default;
            return false;
        }

        private void OnValidate()
        {
            if (Skins == null) return;

            var seen = new HashSet<DiceBoardType>();
            for (int i = 0; i < Skins.Count; i++)
            {
                var entry = Skins[i];

                // Una entry recién agregada arranca con Tint (0,0,0,0): el sprite quedaría
                // invisible. Nadie quiere un tablero transparente, así que la snapeamos a
                // blanco (sprite tal cual) — el usuario igual puede tintar después.
                if (entry.Tint == default)
                {
                    entry.Tint = Color.white;
                    Skins[i] = entry;
                }

                if (!seen.Add(entry.Type))
                    Debug.LogWarning($"{name}: {entry.Type} duplicado — TryGet usa la primera entrada.", this);
            }
        }
    }

    /// <summary>Skin de un tipo de tablero: qué sprite dibujar y cómo tintarlo/estirarlo.</summary>
    [Serializable]
    public struct DiceBoardSkinEntry
    {
        public DiceBoardType Type;

        [Tooltip("Sprite que reemplaza al rectángulo negro para este tipo de tirada.")]
        public Sprite Sprite;

        [Tooltip("Tint del Image al aplicar el sprite. Blanco = sprite tal cual (saca el " +
                 "negro semitransparente del quad original).")]
        public Color Tint;

        [Tooltip("Modo de dibujo del Image. Sliced respeta los bordes 9-slice del sprite.")]
        public Image.Type ImageType;

        /// <summary>Entry con defaults sanos (tint blanco, Sliced) para autorado nuevo desde código.</summary>
        public static DiceBoardSkinEntry ForType(DiceBoardType type, Sprite sprite) => new()
        {
            Type = type,
            Sprite = sprite,
            Tint = Color.white,
            ImageType = Image.Type.Sliced,
        };
    }
}
