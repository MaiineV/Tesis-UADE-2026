using Rollgeon.Combat.Pipelines;
using Rollgeon.Localization;

namespace Rollgeon.UI.HUD.Status
{
    /// <summary>
    /// El tipo de un ataque, localizado, y el título compuesto "Nombre · Tipo" de la tarjeta
    /// de próximo turno. El dato viaja en <c>AIIntent.Kind</c> — esto sólo lo escribe.
    /// </summary>
    public static class AttackKindText
    {
        public static string Describe(AttackKind kind)
        {
            var key = AttackKindTextKeys.Key(kind);
            if (string.IsNullOrEmpty(key)) return string.Empty;
            return LocalizedContent.Ui(key, AttackKindTextKeys.Fallback(key));
        }

        /// <summary>
        /// "{Nombre} · {Tipo}", o el nombre solo cuando el tipo no tiene texto — la entry vacía
        /// es el opt-out por tipo.
        /// </summary>
        public static string ComposeTitle(string name, AttackKind kind)
        {
            var kindText = Describe(kind);
            if (string.IsNullOrEmpty(kindText)) return name;

            return string.Format(
                LocalizedContent.Ui(AttackKindTextKeys.TitleFormat,
                                    AttackKindTextKeys.Fallback(AttackKindTextKeys.TitleFormat)),
                name, kindText);
        }
    }
}
