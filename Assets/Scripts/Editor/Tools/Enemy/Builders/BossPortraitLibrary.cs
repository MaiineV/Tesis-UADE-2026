using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Builders
{
    /// <summary>
    /// Los retratos de los seis jefes (<c>BaseEntitySO.Portrait</c>), que la barra de vida
    /// (<c>BossBarView</c>) y la cola de turnos (<c>TurnQueueView</c>) resuelven por
    /// <c>IEntityPortraitResolver</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El retrato sigue al rig: cada jefe hereda la cara del arte 3D que viste, así que si un jefe
    /// cambia de rig su retrato tiene que moverse con él o la cola de turnos pasa a mentir.
    /// </para>
    /// <para>
    /// Las texturas están en modo Multiple (la hoja compartida trae un sub-sprite por personaje,
    /// los PNGs sueltos uno solo <c>&lt;Nombre&gt;_0</c>), así que todo pasa por
    /// <see cref="SpriteImportUtility.FindSubSprite"/> y no por <c>EnsureSpriteImport</c>: forzar
    /// la hoja a Single borraría los sub-sprites y dejaría en null a los enemigos que ya la usan.
    /// </para>
    /// </remarks>
    public static class BossPortraitLibrary
    {
        /// <summary>Hoja compartida, en modo Multiple con un sub-sprite por personaje.</summary>
        public const string SheetPath = "Assets/Art/UI/CharactersSprites/RollGeonSprites.png";

        /// <summary>Croupier — la cara de <c>SunkedGrand_Animated</c>, el rig que viste.</summary>
        public const string CroupierSpriteName = "RollGeonSprites_4";

        /// <summary>Tahúr — el mismo rig y la misma cara que el Croupier.</summary>
        public const string TahurSpriteName = "RollGeonSprites_4";

        /// <summary>Generala — <c>DiceBoss_Animated</c>.</summary>
        public const string GeneralaPath = "Assets/Art/UI/CharactersSprites/DiceBoss.png";
        public const string GeneralaSpriteName = "DiceBoss_0";

        /// <summary>Bandida — <c>MechaBoss_Animated</c>.</summary>
        public const string BandidaPath = "Assets/Art/UI/CharactersSprites/MechaBoss.png";
        public const string BandidaSpriteName = "MechaBoss_0";

        /// <summary>
        /// Cajero — la cara de <c>MechaBoss_Animated</c>, el rig que viste (la misma que la Bandida).
        /// </summary>
        public const string CajeroPath = BandidaPath;
        public const string CajeroSpriteName = BandidaSpriteName;

        /// <summary>Anotador — el rig <c>ChestMimic</c>.</summary>
        public const string AnotadorPath = "Assets/Art/UI/CharactersSprites/Mimic.png";
        public const string AnotadorSpriteName = "Mimic_0";

        public static Sprite Croupier() => Resolve(SheetPath, CroupierSpriteName);
        public static Sprite Cajero() => Resolve(CajeroPath, CajeroSpriteName);
        public static Sprite Tahur() => Resolve(SheetPath, TahurSpriteName);
        public static Sprite Generala() => Resolve(GeneralaPath, GeneralaSpriteName);
        public static Sprite Bandida() => Resolve(BandidaPath, BandidaSpriteName);
        public static Sprite Anotador() => Resolve(AnotadorPath, AnotadorSpriteName);

        /// <summary>
        /// Sub-sprite por nombre. Un <c>Portrait</c> en null no rompe nada visible en el editor:
        /// sin el warning el jefe llega a Play sin cara y sin rastro de por qué.
        /// </summary>
        private static Sprite Resolve(string texturePath, string spriteName)
        {
            if (AssetImporter.GetAtPath(texturePath) as TextureImporter == null)
            {
                Debug.LogWarning($"[BossPortraitLibrary] No hay textura importable en " +
                                 $"'{texturePath}' — el jefe queda sin retrato en la cola de turnos.");
                return null;
            }

            return SpriteImportUtility.FindSubSprite(texturePath, spriteName);
        }
    }
}
