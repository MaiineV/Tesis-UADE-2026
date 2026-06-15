using System.Collections.Generic;
using Rollgeon.Dungeon.Components;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.EditorTools
{
    /// <summary>
    /// Valida (no auto-corrige) que el "bake doors" de una sala sea consistente con el
    /// runtime: cada puerta debe tener su tile-frente caminable en el <see cref="NavGraph"/>
    /// horneado, porque el cruce en Exploración se resuelve por esa casilla
    /// (<see cref="DoorTileQuery"/>). Devuelve mensajes accionables; el caller decide cómo
    /// mostrarlos (warnings de consola, HelpBox, reporte de los menús).
    /// </summary>
    public static class RoomDoorBakeValidator
    {
        /// <summary>
        /// Revisa <paramref name="layout"/> contra su NavGraph actual y devuelve un finding por
        /// problema. Vacío = sala OK. No modifica nada.
        /// </summary>
        public static List<string> ValidateRoom(RoomLayout layout)
        {
            var findings = new List<string>();
            if (layout == null) return findings;

            string room = layout.name;
            var graph = layout.NavGraph;
            var origin = layout.GetOrigin();
            float tileSize = Mathf.Max(layout.TileSize, 0.01f);

            // NavGraph vacío => HasNode devuelve true para CUALQUIER celda (GridManager lo trata
            // como "sin restricciones"). El cruce "funciona" pero por accidente; al bakear un
            // grafo real puede romperse en silencio. No tiene sentido chequear tile-frentes acá.
            if (graph == null || graph.IsEmpty)
            {
                findings.Add(
                    $"[{room}] NavGraph vacío → todo caminable (sin restricciones). Sala sin bakear: " +
                    "correr 'Bake NavGraph' o 'Rollgeon/Tools/Repair Room Doors'.");
                return findings;
            }

            // Door-front walkability: misma cuenta que DoorTileQuery (usa door.transform.position).
            foreach (var door in layout.GetComponentsInChildren<DoorController>(includeInactive: true))
            {
                var front = WorldToGrid(door.transform.position, origin, tileSize) + door.Direction.InwardOffset();
                if (!graph.HasNode(front))
                {
                    findings.Add(
                        $"[{room}] puerta {door.Direction} ('{door.name}'): tile-frente {front} no es nodo " +
                        "caminable. Pintar un Floor ahí (o moverlo dentro de la sala) y rebakear el NavGraph.");
                }
            }

            // Divergencia Anchor ↔ DoorController: el spawn al entrar usa slot.Anchor
            // (PlayerRoomTransitioner) y el cruce usa door.transform (DoorTileQuery). Si no
            // resuelven la misma celda, el player aparece en un lado y cruza por otro.
            foreach (var slot in layout.DoorSlots)
            {
                if (slot?.Anchor == null || slot.DoorRoot == null) continue;
                var ctrl = slot.DoorRoot.GetComponentInChildren<DoorController>(includeInactive: true);
                if (ctrl == null) continue;

                var anchorFront = WorldToGrid(slot.Anchor.position, origin, tileSize) + slot.Direction.InwardOffset();
                var ctrlFront = WorldToGrid(ctrl.transform.position, origin, tileSize) + ctrl.Direction.InwardOffset();
                if (anchorFront != ctrlFront)
                {
                    findings.Add(
                        $"[{room}] slot {slot.Direction}: el Anchor resuelve tile-frente {anchorFront} pero el " +
                        $"DoorController resuelve {ctrlFront}. Re-correr Auto-Populate (el Anchor debe ser el " +
                        "transform del DoorController).");
                }
            }

            return findings;
        }

        // Inversa exacta de GridManager.WorldToGrid: FloorToInt((world - origin)/tileSize) en X/Z.
        private static GridCoord WorldToGrid(Vector3 world, Vector3 origin, float tileSize)
        {
            var local = world - origin;
            return new GridCoord(
                Mathf.FloorToInt(local.x / tileSize),
                Mathf.FloorToInt(local.z / tileSize));
        }
    }
}
