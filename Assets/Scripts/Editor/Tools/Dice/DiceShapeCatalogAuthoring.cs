using System;
using System.Collections.Generic;
using Rollgeon.Dice;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools
{
    /// <summary>
    /// Autora el <see cref="DiceShapeCatalogSO"/> de Resources desde el sheet de dados
    /// (<c>Assets/Art/UI/Dices/Dices.png</c>). Re-ejecutable: reescribe las entradas sin
    /// duplicar, así que re-sliceando el sheet y volviendo a correr esto alcanza.
    /// </summary>
    /// <remarks>
    /// Reemplazó al generador de placeholders. Aquel asumía un PNG por tipo y que el arte final
    /// se pisaría encima con el mismo GUID; el arte llegó como un solo sheet, así que ese plan
    /// no aplicaba.
    /// </remarks>
    public static class DiceShapeCatalogAuthoring
    {
        private const string LogPrefix = "[DiceShapeCatalog] ";
        private const string SheetPath = "Assets/Art/UI/Dices/Dices.png";
        private const string CatalogPath = "Assets/Resources/Dice/DiceShapeCatalog.asset";

        /// <summary>Columnas por fila del sheet — el orden es el de <see cref="DiceShapeRole"/>.</summary>
        private const int Columns = 5;

        /// <summary>
        /// Fila del sheet de cada tipo de dado, de arriba hacia abajo.
        /// </summary>
        /// <remarks>
        /// El D3 reusa la fila del D4: llegó con el pack de Encantamientos, después de que se
        /// pintara el sheet, y sin entrada el catálogo no validaría. El costo es que D3 y D4 se
        /// ven idénticos — registrado como deuda hasta que el artista mande su fila.
        /// </remarks>
        private static readonly (DiceType Type, int Row)[] TypeRows =
        {
            (DiceType.D4, 0),
            (DiceType.D6, 1),
            (DiceType.D8, 2),
            (DiceType.D10, 3),
            (DiceType.D12, 4),
            (DiceType.D20, 5),
            (DiceType.D3, 0),
        };

        [MenuItem("Tools/Rollgeon/Dice/Author Shape Catalog From Sheet")]
        public static void Run()
        {
            var sprites = LoadSheetSprites();
            if (sprites == null) return;

            var catalog = AssetDatabase.LoadAssetAtPath<DiceShapeCatalogSO>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<DiceShapeCatalogSO>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
                Debug.Log($"{LogPrefix}Catálogo creado en {CatalogPath}.");
            }

            var entries = new List<DiceShapeEntry>();
            foreach (var (type, row) in TypeRows)
            {
                if (!TryBuildEntry(type, row, sprites, out var entry)) return;
                entries.Add(entry);
            }
            catalog.Shapes = entries;

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!catalog.ValidateRoles(out var error))
            {
                Debug.LogError($"{LogPrefix}Catálogo inválido tras autorar: {error}", catalog);
                return;
            }
            Debug.Log($"{LogPrefix}{entries.Count} tipos × {Columns} roles autorados desde {SheetPath}.",
                catalog);
        }

        private static bool TryBuildEntry(
            DiceType type, int row, IReadOnlyDictionary<string, Sprite> sprites, out DiceShapeEntry entry)
        {
            entry = new DiceShapeEntry { Type = type };
            var set = new Sprite[Columns];

            for (int col = 0; col < Columns; col++)
            {
                string name = $"Dices_{row * Columns + col}";
                if (!sprites.TryGetValue(name, out var sprite))
                {
                    Debug.LogError($"{LogPrefix}Falta la sub-sprite '{name}' ({type}, columna {col}) " +
                                   $"en {SheetPath}. ¿Se re-sliceó el sheet con otros nombres?");
                    return false;
                }
                set[col] = sprite;
            }

            entry.Front = set[(int)DiceShapeRole.Front];
            entry.SideA = set[(int)DiceShapeRole.SideA];
            entry.SideB = set[(int)DiceShapeRole.SideB];
            entry.Hover = set[(int)DiceShapeRole.Hover];
            entry.Selected = set[(int)DiceShapeRole.Selected];
            return true;
        }

        /// <summary>Sub-sprites del sheet por nombre, o <c>null</c> si el sheet no está sliceado.</summary>
        private static Dictionary<string, Sprite> LoadSheetSprites()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(SheetPath);
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError($"{LogPrefix}No se encontró el sheet en {SheetPath}.");
                return null;
            }

            var byName = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var asset in assets)
                if (asset is Sprite sprite) byName[sprite.name] = sprite;

            if (byName.Count == 0)
            {
                Debug.LogError($"{LogPrefix}{SheetPath} no tiene sub-sprites — el importer debe " +
                               "estar en Sprite Mode: Multiple y sliceado.");
                return null;
            }
            return byName;
        }
    }
}
