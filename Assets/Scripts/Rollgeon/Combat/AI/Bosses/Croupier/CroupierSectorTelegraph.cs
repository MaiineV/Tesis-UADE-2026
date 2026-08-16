using System;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Croupier
{
    /// <summary>
    /// Marca / limpia el área telegráfica de <b>un</b> número cantado. Código puro compartido por el
    /// nodo que marca y por el servicio de la rueda (que re-marca cuando el jugador corre la rueda),
    /// para que las dos rutas no puedan divergir en cómo se llama el área ni en qué se pinta.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Un source guid por slot, no el del jefe.</b> <see cref="IThreatenedAreaService"/> guarda
    /// <i>una</i> área pendiente por fuente y sobrescribe al re-marcar: con los dos números de fase 2
    /// marcados bajo el guid del jefe, el segundo se comía al primero y la columna de costura pegaba
    /// 12 en vez de 24. Cada slot marca bajo un guid derivado del jefe, así que las dos áreas
    /// coexisten y se resuelven por separado — el jugador en la costura recibe los dos golpes.
    /// </para>
    /// <para>
    /// El guid derivado no se usa como <c>SourceId</c> del daño: el que detona resuelve siempre con el
    /// guid real del jefe, para que atribución, debilidad y feedback sigan apuntando al Croupier.
    /// </para>
    /// </remarks>
    public static class CroupierSectorTelegraph
    {
        /// <summary>Slots máximos simultáneos: fase 2 canta dos números.</summary>
        public const int MaxSlots = 2;

        /// <summary>
        /// Latón del sector cantado. Es el mismo matiz que el número de la ruleta (<c>BrassLight</c>
        /// del builder), y por eso no usa el naranja genérico de <c>ThreatOverlayState.Marked</c>:
        /// con el naranja de fábrica, el bloque que va a caer se veía igual que el telegraph de
        /// cualquier otro jefe y nada lo ataba a la rueda.
        /// </summary>
        /// <remarks>
        /// Latón para el aviso, rojo para el fuego (<c>CroupierAssetBuilder.FireOverlayTint</c>): el
        /// paño cuenta dos cosas distintas —"acá va a caer" y "acá está ardiendo"— y compartir matiz
        /// las volvía una sola.
        /// </remarks>
        public static readonly Color SectorTint = new Color(0.831f, 0.635f, 0.196f, 0.55f);

        // XOR sobre el último byte del guid del jefe: determinístico, distinto por slot y nunca igual
        // al guid original (el XOR es con un valor != 0), así que no puede pisar el área que marque
        // otro sistema bajo la fuente del propio jefe (ej. un hazard de ciclo).
        private const int SlotSalt = 0xC0;

        /// <summary>Fuente derivada del slot <paramref name="slot"/> del jefe <paramref name="bossGuid"/>.</summary>
        public static Guid SlotGuid(Guid bossGuid, int slot)
        {
            if (bossGuid == Guid.Empty) return Guid.Empty;

            var bytes = bossGuid.ToByteArray();
            bytes[15] = (byte)(bytes[15] ^ (byte)(SlotSalt + slot));
            return new Guid(bytes);
        }

        /// <summary>
        /// Marca el sector <paramref name="sector"/> como área pendiente del slot y pinta su overlay.
        /// Devuelve <c>false</c> si no hay servicios/sala o si el sector queda vacío.
        /// </summary>
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

        /// <summary>Apaga el overlay del slot. El área pendiente la consume el nodo que detona.</summary>
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
