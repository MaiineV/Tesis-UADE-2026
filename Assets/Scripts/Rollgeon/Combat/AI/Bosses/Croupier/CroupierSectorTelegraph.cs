using System;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Croupier
{
    /// <summary>
    /// Cada slot marca bajo un guid derivado del jefe, no bajo el del jefe:
    /// <see cref="IThreatenedAreaService"/> guarda <i>una</i> área pendiente por fuente, así que con
    /// un solo guid el segundo número de fase 2 se come al primero. El guid derivado no se usa como
    /// <c>SourceId</c>: el que detona resuelve con el guid real para no perder la atribución.
    /// </summary>
    public static class CroupierSectorTelegraph
    {
        public const int MaxSlots = 2;

        /// <summary>El mismo matiz que el número de la ruleta (<c>BrassLight</c> del builder), distinto del rojo del fuego.</summary>
        public static readonly Color SectorTint = new Color(0.831f, 0.635f, 0.196f, 0.55f);

        // XOR sobre el último byte del guid del jefe: determinístico, distinto por slot y nunca igual
        // al original, así que no pisa el área que marque otro sistema bajo la fuente del jefe.
        private const int SlotSalt = 0xC0;

        public static Guid SlotGuid(Guid bossGuid, int slot)
        {
            if (bossGuid == Guid.Empty) return Guid.Empty;

            var bytes = bossGuid.ToByteArray();
            bytes[15] = (byte)(bytes[15] ^ (byte)(SlotSalt + slot));
            return new Guid(bytes);
        }

        /// <summary>Devuelve <c>false</c> si no hay servicios/sala o si el sector queda vacío.</summary>
        public static bool Mark(Guid bossGuid, int slot, int sector, int damage, AttackKind kind)
        {
            var slotGuid = SlotGuid(bossGuid, slot);
            if (slotGuid == Guid.Empty) return false;

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return false;
            if (!ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat) || threat == null) return false;

            var tiles = ThreatAreaShape.ComputeRoomSector(grid, sector);
            if (tiles.Count == 0) return false;

            threat.Mark(slotGuid, tiles, damage, kind);
            ThreatTelegraphOverlay.ResolveOrCreate().Show(slotGuid, tiles, SectorTint);
            return true;
        }

        public static void ClearOverlay(Guid bossGuid, int slot)
        {
            var slotGuid = SlotGuid(bossGuid, slot);
            if (slotGuid == Guid.Empty) return;

            // TryGet y no ResolveOrCreate: limpiar no debe ser la razón por la que nace un overlay.
            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay) && overlay != null)
                overlay.Clear(slotGuid);
        }
    }
}
