using System.Collections.Generic;
using Rollgeon.EditorTools.Localization;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.Editor.Tools.Enchantment
{
    /// <summary>
    /// Los textos localizados de un encantamiento, por idioma. Wrapper fino sobre
    /// <see cref="ContentLocalizationBridge"/>, igual que <c>ItemLocalizationBridge</c>
    /// del lado de items. Las claves son <c>&lt;UpgradeId&gt;.name</c> / <c>.desc</c> en la
    /// tabla <c>Content</c> — el <c>UpgradeId</c> ya trae el prefijo <c>ench.</c>.
    /// </summary>
    public static class EnchantmentLocalizationBridge
    {
        /// <summary>Los textos de un encantamiento en un idioma.</summary>
        public readonly struct Entry
        {
            /// <summary><c>null</c> si la key no existe en la tabla — el juego cae al texto del asset.</summary>
            public string Name { get; }

            /// <summary><c>null</c> si la key no existe en la tabla.</summary>
            public string Description { get; }

            public Entry(string name, string description)
            {
                Name = name;
                Description = description;
            }
        }

        /// <summary>El idioma en el que se autora el proyecto — sincroniza el fallback del asset.</summary>
        public const string AuthoringLocale = ContentLocalizationBridge.AuthoringLocale;

        /// <summary>Los códigos de locale del proyecto (<c>es</c>, <c>en</c>), en orden estable.</summary>
        public static IReadOnlyList<string> Locales() => ContentLocalizationBridge.Locales();

        public static string DisplayNameOf(string localeCode) => ContentLocalizationBridge.DisplayNameOf(localeCode);

        /// <summary>Lo que hay hoy en la tabla para <paramref name="upgradeId"/> en ese idioma.</summary>
        public static Entry Read(string upgradeId, string localeCode)
        {
            var entry = ContentLocalizationBridge.Read(upgradeId, localeCode);
            return new Entry(entry.Name, entry.Description);
        }

        /// <summary>
        /// Escribe el nombre y la descripción de un idioma, con Undo. Crea la key en la
        /// <c>SharedTableData</c> si falta.
        /// </summary>
        public static void Write(string upgradeId, string localeCode, string name, string description)
            => ContentLocalizationBridge.Write(upgradeId, localeCode, name, description, "Edit Enchantment Text");

        /// <summary>Lo que el juego mostraría hoy: la tabla si tiene entrada, si no el campo del asset.</summary>
        public static string EffectiveName(EnchantmentSO enchantment, string localeCode) =>
            enchantment == null
                ? string.Empty
                : Read(enchantment.UpgradeId, localeCode).Name ?? enchantment.DisplayName;
    }
}
