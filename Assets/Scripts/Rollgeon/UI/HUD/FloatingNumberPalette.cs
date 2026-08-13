using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Paleta única de tints para floating numbers. Antes vivían como
    /// <c>[SerializeField]</c> en <see cref="FloatingDamageSpawner"/> — moverlos a código
    /// evita que dos prefabs/escenas queden con paletas divergentes (era el caso: el daño
    /// saliente usaba un amarillo casi idéntico al del oro). Ver
    /// <see cref="FloatingNumberFormat"/> para el mapeo dato → estilo.
    /// </summary>
    public static class FloatingNumberPalette
    {
        public static readonly Color DamageDealt = new Color32(0xF5, 0xEF, 0xE0, 0xFF);
        public static readonly Color DamageWeakness = new Color32(0xFF, 0xD7, 0x5A, 0xFF);
        public static readonly Color DamageTaken = new Color32(0xFF, 0x4B, 0x4B, 0xFF);
        public static readonly Color Heal = new Color32(0x63, 0xE0, 0x63, 0xFF);
        public static readonly Color Shield = new Color32(0x6F, 0xD3, 0xFF, 0xFF);
        public static readonly Color Gold = new Color32(0xFF, 0xC5, 0x33, 0xFF);
        public static readonly Color Status = new Color32(0xC5, 0x8B, 0xFF, 0xFF);
    }
}
