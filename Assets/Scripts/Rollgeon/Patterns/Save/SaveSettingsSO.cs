using System;
using System.IO;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Patterns.Save
{
    /// <summary>
    /// Configuración editor del SaveSystem (§15.4). El Game Designer decide en qué
    /// triggers se escribe el cache a disco sin tocar código; los triggers
    /// deshabilitados sólo capturan en memoria.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Save/Settings")]
    public class SaveSettingsSO : ScriptableObject
    {
        [Title("Flush Triggers")]
        [InfoBox("Activá los triggers en los que querés que se escriba el cache a JSON.\n" +
                 "Los triggers desactivados sólo capturan en memoria — útil para reducir I/O.")]
        [EnumToggleButtons]
        public SaveTrigger[] FlushOn = new[]
        {
            SaveTrigger.RunStart,
            SaveTrigger.FloorEnd,
            SaveTrigger.Manual,
            SaveTrigger.RunEnd,
            SaveTrigger.Exit,
        };

        [Title("File")]
        [InfoBox("Relativo a Application.persistentDataPath.")]
        public string SaveFilePrefix = "rollgeon";

        [InfoBox("Cantidad de slots de save. Estilo Isaac: 3 saves independientes.")]
        [MinValue(1)] public int MaxSaveSlots = 3;

        [Title("Flush strategy (§15.3.1)")]
        [InfoBox("Sobre este tamaño, Flush() corre async (Task.Run) en vez de sincrono. " +
                 "500 KB cubre el caso normal (payload mid-dungeon con 5 rooms + modifiers).")]
        [MinValue(1)] public int AsyncFlushThresholdBytes = 500_000;

        [Title("Debug")]
        // Odin SerializationUtility no expone toggle de pretty-print para JSON;
        // se mantiene por fidelidad a §15.4 hasta que el serializer lo soporte.
        public bool PrettyPrint = false;
        public bool LogFlushes = false;

        /// <summary>Slot activo — seteado al elegir save en el menú principal.</summary>
        [NonSerialized] public int ActiveSlot = 0;

        public bool ShouldFlushOn(SaveTrigger trigger) =>
            FlushOn != null && FlushOn.Contains(trigger);

        /// <summary>
        /// Devuelve el path del save para el slot activo.
        /// Slot 0 → "rollgeon_0.save", Slot 1 → "rollgeon_1.save", etc.
        /// </summary>
        public string GetSavePath(int slotIndex = -1)
        {
            var slot = slotIndex >= 0 ? slotIndex : ActiveSlot;
            return Path.Combine(Application.persistentDataPath, $"{SaveFilePrefix}_{slot}.save");
        }

        public bool SlotExists(int slotIndex) =>
            File.Exists(GetSavePath(slotIndex));

        public void DeleteSlot(int slotIndex)
        {
            var path = GetSavePath(slotIndex);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
