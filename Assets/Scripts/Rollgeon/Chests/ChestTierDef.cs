using System;
using Rollgeon.Items;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Chests
{
    /// <summary>
    /// Config por tier de cofre. El tier ES un <see cref="ItemRarity"/> y se muestra
    /// con los nombres estándar de rareza del juego (los nombres custom del GDD se
    /// descartaron). Valores placeholder sujetos a Balance (TBD-15/16 del GDD).
    /// </summary>
    [Serializable]
    public class ChestTierDef
    {
        public ItemRarity Tier;

        [MinValue(1)]
        [Tooltip("HP total del cofre de este tier.")]
        public int MaxHP = 20;

        [Tooltip("Color del cuerpo (madera). Comunica el tier al jugador. Ignorado si " +
                 "BodyMaterialOverride está asignado.")]
        public Color BodyColor = Color.white;

        [Tooltip("Prefab propio de este tier. Null = usa el ChestPrefab global de " +
                 "ChestConfig. Debe tener EntityPawn con WorldSpaceHealthBar wireada " +
                 "(correr 'Rollgeon/Chests/4 - Setup Art Prefabs' si viene pelado del artista).")]
        public GameObject ChestPrefabOverride;

        [Tooltip("Material para los slots de madera ('Wood'/'Body') de este tier. Null = " +
                 "tint con BodyColor como siempre. El slot overrideado no se tinta; el " +
                 "shader debe exponer _BaseColor/_Color para que el hit-flash siga funcionando.")]
        public Material BodyMaterialOverride;

        [Tooltip("Material para los slots de herrajes ('Frame'/'Fittings') de este tier. " +
                 "Null = tint con el FittingsColor global. Mismas consideraciones de shader.")]
        public Material FittingsMaterialOverride;

        [Range(0f, 1f)]
        [Tooltip("Placeholder TBD-15: % de las stats del Melee base que usa el Mimic de este tier.")]
        public float MimicStatScale = 0.4f;

        [MinValue(0)]
        [Tooltip("Placeholder TBD-16: oro mínimo que dropea el Mimic derrotado.")]
        public int MimicGoldMin = 10;

        [MinValue(0)]
        [Tooltip("Placeholder TBD-16: oro máximo (inclusive) que dropea el Mimic derrotado.")]
        public int MimicGoldMax = 15;

        [MinValue(0)]
        [Tooltip("Oro otorgado si el roll fue un ítem pero el inventario no pudo recibirlo.")]
        public int FallbackGold = 10;
    }
}
