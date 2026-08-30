using System.Collections.Generic;
using Rollgeon.Localization;
using Rollgeon.UI.HUD.Status;

namespace Rollgeon.Tiles.Visuals
{
    /// <summary>
    /// Las tarjetas de números del panel de una casilla: qué cobra y cuándo, como dato y no como
    /// frase. La descripción del header dice CÓMO se comporta; acá van sólo los precios, para que
    /// rebalancear cambie un número del asset y ningún texto en ningún idioma.
    /// </summary>
    public static class SpecialTileCards
    {
        public const string EffectKey = "tile.panel.effect";
        public const string EnterKey = "tile.panel.enter";
        public const string TurnStartKey = "tile.panel.turn_start";
        public const string HealKey = "tile.panel.heal";
        public const string StatusKey = "tile.panel.applies";

        /// <summary>
        /// Una tarjeta por precio. Casillas sin números (portal, hielo, advertencia) no agregan
        /// nada: su descripción ya dice todo y una tarjeta vacía sería un recuadro sin contenido.
        /// <paramref name="eyebrowOverride"/> re-etiqueta el bloque cuando la casilla se describe
        /// desde otro panel — el fuego que deja una bomba dice "Deja", no "Efecto".
        /// </summary>
        public static void Append(SpecialTileDefinitionSO def, List<StatusIconState> into,
                                  string eyebrowOverride = null)
        {
            if (def == null || into == null) return;

            string eyebrow = eyebrowOverride ?? LocalizedContent.Ui(EffectKey, "Efecto");

            if (def.EnterDamage > 0)
            {
                Add(into, "tile.card.enter", LocalizedContent.Ui(EnterKey, "Al entrar"),
                    def.EnterDamage, ref eyebrow);
            }

            if (def.TurnStartDamage > 0)
            {
                Add(into, "tile.card.turn_start",
                    LocalizedContent.Ui(TurnStartKey, "Empezar el turno encima"),
                    def.TurnStartDamage, ref eyebrow);
            }

            if (def.HealAmount > 0)
            {
                Add(into, "tile.card.heal",
                    LocalizedContent.Ui(HealKey, "Cura al terminar el turno"),
                    def.HealAmount, ref eyebrow);
            }

            if (def.StatusKind != TileStatusKind.None)
            {
                string statusId = def.StatusKind == TileStatusKind.Poison
                    ? "status.poison" : "status.stun";
                Add(into, statusId,
                    LocalizedContent.Ui(StatusKey, "Aplica") + " " + LocalizedContent.Name(
                        statusId, def.StatusKind == TileStatusKind.Poison ? "Envenenado" : "Aturdido"),
                    def.StatusTickDamage > 0 ? def.StatusTickDamage : (int?)null, ref eyebrow);
            }
        }

        // La etiqueta EFECTO subraya el bloque entero, así que la lleva sólo la primera tarjeta:
        // repetirla en cada precio leería como tres bloques distintos.
        private static void Add(List<StatusIconState> into, string id, string title, int? damage,
                                ref string eyebrow)
        {
            into.Add(new StatusIconState(
                id, title, description: null, icon: null, active: true,
                style: StatusCardStyle.Terrain, damage: damage, eyebrow: eyebrow));
            eyebrow = null;
        }
    }
}
