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
    /// Hasta que llegó el arte de personaje los seis vestían un glifo del pack de casino
    /// (<c>Casino_00XX.png</c>): una carta, una mano con monedas, un set de dados. Eran
    /// marcadores de posición honestos pero no identificaban al jefe — dos jefes distintos se
    /// leían igual de "genérico" en la cola de turnos.
    /// </para>
    /// <para>
    /// <b>El retrato sigue al rig, no al nombre.</b> Cada jefe hereda el retrato del arte 3D que
    /// viste, así que lo que el jugador ve en la cola es la misma silueta que tiene enfrente. Si
    /// un jefe cambia de rig —como la Generala, que pasó a <c>DiceBoss_Animated</c>— su retrato
    /// tiene que moverse con él o la cola pasa a mentir.
    /// </para>
    /// <para>
    /// Tres salen de la hoja compartida <c>RollGeonSprites.png</c> (sliceada en Multiple, un
    /// sub-sprite por personaje) y tres de PNGs sueltos que igual están en modo Multiple con un
    /// único sub-sprite <c>&lt;Nombre&gt;_0</c>. Por eso todo pasa por
    /// <see cref="SpriteImportUtility.FindSubSprite"/> y no por <c>EnsureSpriteImport</c>: forzar
    /// la hoja a Single borraría los sub-sprites y dejaría en null a los enemigos que ya la usan.
    /// </para>
    /// </remarks>
    public static class BossPortraitLibrary
    {
        /// <summary>Hoja compartida con los personajes que ya tenían retrato en <c>develop</c>.</summary>
        public const string SheetPath = "Assets/Art/UI/CharactersSprites/RollGeonSprites.png";

        /// <summary>Croupier — hereda el retrato de <c>Healer_Animated</c>, el rig que viste.</summary>
        public const string CroupierSpriteName = "RollGeonSprites_2";

        /// <summary>Cajero — hereda el retrato de <c>GeneralDirector_Animated</c>.</summary>
        public const string CajeroSpriteName = "RollGeonSprites_0";

        /// <summary>Tahúr — hereda el retrato de <c>SunkedGrand_Animated</c>.</summary>
        public const string TahurSpriteName = "RollGeonSprites_4";

        /// <summary>Generala — <c>DiceBoss_Animated</c>.</summary>
        public const string GeneralaPath = "Assets/Art/UI/CharactersSprites/DiceBoss.png";
        public const string GeneralaSpriteName = "DiceBoss_0";

        /// <summary>Bandida — <c>MechaBoss_Animated</c>.</summary>
        public const string BandidaPath = "Assets/Art/UI/CharactersSprites/MechaBoss.png";
        public const string BandidaSpriteName = "MechaBoss_0";

        /// <summary>Anotador — el rig <c>ChestMimic</c>.</summary>
        public const string AnotadorPath = "Assets/Art/UI/CharactersSprites/Mimic.png";
        public const string AnotadorSpriteName = "Mimic_0";

        public static Sprite Croupier() => Resolve(SheetPath, CroupierSpriteName);
        public static Sprite Cajero() => Resolve(SheetPath, CajeroSpriteName);
        public static Sprite Tahur() => Resolve(SheetPath, TahurSpriteName);
        public static Sprite Generala() => Resolve(GeneralaPath, GeneralaSpriteName);
        public static Sprite Bandida() => Resolve(BandidaPath, BandidaSpriteName);
        public static Sprite Anotador() => Resolve(AnotadorPath, AnotadorSpriteName);

        /// <summary>
        /// Sub-sprite por nombre, con el aviso explícito de qué se pierde si falta: un
        /// <c>Portrait</c> en null no rompe nada visible en el editor, así que sin este warning el
        /// jefe llegaría a Play sin cara y sin rastro de por qué.
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
