using System;
using System.Collections.Generic;
using Rollgeon.Combos;

namespace Rollgeon.Heroes
{
    /// <summary>
    /// Helper estatico que construye un <see cref="ContractSheet"/> con los 8 combos canonicos del
    /// Guerrero (TECHNICAL.md §5.4) resueltos desde un <see cref="ComboCatalogSO"/>.
    /// <para>
    /// <b>Orden canonico</b> (low priority first, Generala ultimo):
    /// <c>[Par, DoblePar, SumaX, Trio, Escalera, FullHouse, Poker, Generala]</c>.
    /// </para>
    /// <para>
    /// <b>Uso.</b> El designer puede correr este factory al crear
    /// <c>ClassHero_Warrior.asset</c>, o poblar <see cref="ContractSheet.Combos"/> manualmente en el
    /// inspector. La factory es sobre todo una red de seguridad para tests y doc — no es un SO.
    /// </para>
    /// </summary>
    public static class ContractWarriorFactory
    {
        /// <summary>
        /// Orden canonico de <see cref="ComboId"/> del Guerrero (§5.4).
        /// </summary>
        public static readonly IReadOnlyList<string> CanonicalOrder = new[]
        {
            ComboId.Par,
            ComboId.DoublePair,
            ComboId.SumX,
            ComboId.Triple,
            ComboId.Straight,
            ComboId.FullHouse,
            ComboId.Poker,
            ComboId.Generala,
        };

        /// <summary>
        /// Arma un <see cref="ContractSheet"/> buscando cada <see cref="ComboId"/> canonico en el
        /// catalogo. Lanza <see cref="InvalidOperationException"/> si falta alguno — el designer
        /// debe registrar los 8 assets de T97a antes de invocar esto.
        /// </summary>
        public static ContractSheet Build(ComboCatalogSO catalog, string label = "Contrato del Guerrero")
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            var sheet = new ContractSheet
            {
                Combos = new List<BaseComboSO>(CanonicalOrder.Count),
            };

            for (int i = 0; i < CanonicalOrder.Count; i++)
            {
                string id = CanonicalOrder[i];
                var combo = catalog.GetById(id);
                if (combo == null)
                {
                    throw new InvalidOperationException(
                        $"[ContractWarriorFactory] ComboCatalogSO missing id '{id}'. " +
                        "Registrá los 8 assets de T97a en el catalogo antes de invocar Build().");
                }
                sheet.Combos.Add(combo);
            }

            // Seteamos el display label via reflection porque es un campo privado del sheet.
            // La alternativa seria exponer un setter publico, pero el sheet es [Serializable]
            // y preferimos no ensuciar la API publica con un setter solo-factory.
            var field = typeof(ContractSheet).GetField("_displayLabel",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(sheet, label);
            }

            return sheet;
        }
    }
}
