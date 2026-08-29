using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using Rollgeon.Rendering;

/// <summary>
/// MaterialPropertyDrawer para [PaletteSlot].
/// Muestra un cuadradito con el color Mid real del slot elegido + un botón que abre
/// un dropdown (AdvancedDropdown) con esa misma preview de color en cada opción.
///
/// Uso en shader:
///   [PaletteSlot] _PaletteSlot ("Palette Slot", Float) = 0
///
/// Nota técnica: EditorGUI.Popup con listas grandes delega al menú nativo del SO en
/// Windows, que no soporta íconos por ítem (por eso GUIContent.image no se veía).
/// AdvancedDropdown (lo mismo que usa "Add Component") se dibuja en IMGUI propio,
/// así que sí puede mostrar el swatch de color en cada fila.
///
/// La fuente de verdad del PaletteAsset es el GlobalPaletteManager activo en la
/// escena cargada — el mismo que se sube de verdad a la GPU. Si no hay ninguna
/// escena con manager cargada, cae a buscar por nombre "PA_MainPalette" y, si
/// tampoco, al primero que haya.
/// </summary>
public class PaletteSlotDrawer : MaterialPropertyDrawer
{
    // Cache de swatches por color exacto — se generan una vez y se reusan.
    static readonly Dictionary<Color, Texture2D> _swatchCache = new Dictionary<Color, Texture2D>();

    public override void OnGUI(Rect position, MaterialProperty prop,
                               string label, MaterialEditor editor)
    {
        PaletteAsset palette = FindPalette();

        if (palette == null || palette.slots == null || palette.slots.Length == 0)
        {
            EditorGUI.LabelField(position, label, "Sin PaletteAsset en el proyecto");
            return;
        }

        int current = Mathf.Clamp(Mathf.RoundToInt(prop.floatValue), 0, palette.slots.Length - 1);
        var slot = palette.slots[current];
        string slotLabel = string.IsNullOrEmpty(slot.label) ? $"Slot {current}" : slot.label;

        // Layout: [ label del shader | cuadradito de color | botón con el nombre ]
        Rect labelRect  = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
        Rect swatchRect = new Rect(labelRect.xMax + 2, position.y + 1, 16, position.height - 2);
        Rect buttonRect = new Rect(swatchRect.xMax + 4, position.y,
                                    position.xMax - swatchRect.xMax - 4, position.height);

        EditorGUI.LabelField(labelRect, label);
        GUI.DrawTexture(swatchRect, GetSwatch(slot.ComputedMid));

        if (EditorGUI.DropdownButton(buttonRect, new GUIContent($"{current}  —  {slotLabel}"), FocusType.Keyboard))
        {
            var dropdown = new PaletteSlotAdvancedDropdown(palette, selected => prop.floatValue = selected);
            dropdown.Show(buttonRect);
        }
    }

    // Altura estándar de una línea en el Inspector
    public override float GetPropertyHeight(MaterialProperty prop, string label,
                                             MaterialEditor editor)
        => EditorGUIUtility.singleLineHeight;

    // ── Búsqueda del asset ───────────────────────────────────────────────────
    static PaletteAsset FindPalette()
    {
        // 1. Fuente de verdad real: el GlobalPaletteManager activo en alguna escena
        // cargada ahora mismo — es literalmente el que sube los colores a la GPU.
        var manager = Object.FindFirstObjectByType<GlobalPaletteManager>(FindObjectsInactive.Include);
        if (manager != null && manager.Palette != null)
            return manager.Palette;

        // 2. Fallback: buscar por nombre preferido (sin manager en escena cargada)
        var guids = AssetDatabase.FindAssets("PA_MainPalette t:PaletteAsset");
        if (guids.Length > 0)
            return AssetDatabase.LoadAssetAtPath<PaletteAsset>(
                AssetDatabase.GUIDToAssetPath(guids[0]));

        // 3. Fallback final: cualquier PaletteAsset en el proyecto
        guids = AssetDatabase.FindAssets("t:PaletteAsset");
        if (guids.Length > 0)
            return AssetDatabase.LoadAssetAtPath<PaletteAsset>(
                AssetDatabase.GUIDToAssetPath(guids[0]));

        return null;
    }

    internal static Texture2D GetSwatch(Color c)
    {
        if (_swatchCache.TryGetValue(c, out var tex) && tex != null)
            return tex;

        tex = new Texture2D(16, 16, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode  = TextureWrapMode.Clamp,
        };
        var pixels = new Color[16 * 16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = c;
        tex.SetPixels(pixels);
        tex.Apply();

        _swatchCache[c] = tex;
        return tex;
    }
}

/// <summary>
/// Dropdown con swatch de color por fila para elegir un slot de PaletteAsset.
/// Se dibuja en IMGUI propio (no delega al menú nativo del SO), así que sí puede
/// mostrar el ícono de color de cada slot — a diferencia de EditorGUI.Popup.
/// </summary>
class PaletteSlotAdvancedDropdown : AdvancedDropdown
{
    class Item : AdvancedDropdownItem
    {
        public readonly int SlotIndex;
        public Item(string name, int slotIndex, Texture2D icon) : base(name)
        {
            SlotIndex = slotIndex;
            this.icon = icon;
        }
    }

    readonly PaletteAsset _palette;
    readonly System.Action<int> _onSelect;

    public PaletteSlotAdvancedDropdown(PaletteAsset palette, System.Action<int> onSelect)
        : base(new AdvancedDropdownState())
    {
        _palette = palette;
        _onSelect = onSelect;
        minimumSize = new Vector2(280, 350);
    }

    protected override AdvancedDropdownItem BuildRoot()
    {
        var root = new AdvancedDropdownItem("Palette Slot");
        for (int i = 0; i < _palette.slots.Length; i++)
        {
            var slot = _palette.slots[i];
            string name = string.IsNullOrEmpty(slot.label) ? $"Slot {i}" : slot.label;
            var item = new Item($"{i}  —  {name}", i, PaletteSlotDrawer.GetSwatch(slot.ComputedMid));
            root.AddChild(item);
        }
        return root;
    }

    protected override void ItemSelected(AdvancedDropdownItem item)
    {
        if (item is Item paletteItem)
            _onSelect?.Invoke(paletteItem.SlotIndex);
    }
}
