using UnityEngine;
using UnityEditor;
using Rollgeon.Rendering;

/// <summary>
/// MaterialPropertyDrawer para [PaletteSlot].
/// Muestra un desplegable con los nombres de los slots del PaletteAsset activo
/// en vez de un slider numérico.
///
/// Uso en shader:
///   [PaletteSlot] _PaletteSlot ("Palette Slot", Float) = 0
///
/// El drawer busca automáticamente el asset llamado "PA_MainPalette".
/// Si no lo encuentra, usa el primer PaletteAsset disponible en el proyecto.
/// </summary>
public class PaletteSlotDrawer : MaterialPropertyDrawer
{
    public override void OnGUI(Rect position, MaterialProperty prop,
                               string label, MaterialEditor editor)
    {
        PaletteAsset palette = FindPalette();

        if (palette == null || palette.slots == null || palette.slots.Length == 0)
        {
            EditorGUI.LabelField(position, label, "Sin PaletteAsset en el proyecto");
            return;
        }

        // Construir array de nombres "0: Black", "1: Bone", ...
        var names = new string[palette.slots.Length];
        for (int i = 0; i < palette.slots.Length; i++)
        {
            string slotLabel = palette.slots[i].label;
            names[i] = string.IsNullOrEmpty(slotLabel)
                ? $"Slot {i}"
                : $"{i}  —  {slotLabel}";
        }

        int current  = Mathf.RoundToInt(prop.floatValue);
        current      = Mathf.Clamp(current, 0, names.Length - 1);

        EditorGUI.BeginChangeCheck();
        int selected = EditorGUI.Popup(position, label, current, names);
        if (EditorGUI.EndChangeCheck())
            prop.floatValue = selected;
    }

    // Altura estándar de una línea en el Inspector
    public override float GetPropertyHeight(MaterialProperty prop, string label,
                                             MaterialEditor editor)
        => EditorGUIUtility.singleLineHeight;

    // ── Búsqueda del asset ───────────────────────────────────────────────────
    static PaletteAsset FindPalette()
    {
        // 1. Buscar por nombre preferido
        var guids = AssetDatabase.FindAssets("PA_MainPalette t:PaletteAsset");
        if (guids.Length > 0)
            return AssetDatabase.LoadAssetAtPath<PaletteAsset>(
                AssetDatabase.GUIDToAssetPath(guids[0]));

        // 2. Fallback: cualquier PaletteAsset en el proyecto
        guids = AssetDatabase.FindAssets("t:PaletteAsset");
        if (guids.Length > 0)
            return AssetDatabase.LoadAssetAtPath<PaletteAsset>(
                AssetDatabase.GUIDToAssetPath(guids[0]));

        return null;
    }
}
