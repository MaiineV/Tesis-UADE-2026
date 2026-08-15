using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.ComboBlock;
using Rollgeon.Combat.ContractMod;
using Rollgeon.Combos;
using Rollgeon.Heroes;
using Rollgeon.Localization;
using Rollgeon.Player;

namespace Rollgeon.UI.HUD.Contract
{
    /// <summary>
    /// Traduce las capas que le pegan al contrato — modificadores del jefe y bloqueo de
    /// combos — a la marca visual de cada fila.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué se deduce y no se pregunta.</b> <see cref="IContractModifierService"/> expone
    /// el daño efectivo y si el combo está prohibido, pero no QUÉ modificador produjo el valor.
    /// El corrimiento se reconoce porque <c>SetComboToNeighbor</c> copia el base de otra fila tal
    /// cual: el valor efectivo cae exactamente sobre uno de la tabla. Un ×2 que caiga justo sobre
    /// otra fila se lee como corrimiento — el mensaje al jugador ("ahora paga como aquella") sigue
    /// siendo cierto, así que la ambigüedad no miente. Ver <c>docs/setup/reglas-visibles.md</c>
    /// para el cambio de servicio que la eliminaría.
    /// </para>
    /// <para>
    /// <b><see cref="Resolve"/> es puro</b> — no toca <see cref="ServiceLocator"/>. Los overloads
    /// que sí leen servicios (<see cref="ResolveAll"/>, <see cref="ResolveSingle"/>) sólo juntan
    /// los datos y delegan.
    /// </para>
    /// </remarks>
    public static class ContractRowStateResolver
    {
        /// <summary>
        /// Marca de la fila <paramref name="index"/> de <paramref name="table"/>. La tabla
        /// entera hace falta para saber a qué fila fue corrida ésta.
        /// </summary>
        public static ContractRowState Resolve(IReadOnlyList<ContractRowBase> table, int index,
            int effectiveDamage, bool forbidden, int blockedTurns)
        {
            if (table == null || index < 0 || index >= table.Count)
                return ContractRowState.Unmodified(effectiveDamage);

            var row = table[index];
            int baseDamage = row.BaseDamage;

            // Bloqueado le gana a prohibido: prohibido paga 0 pero se puede armar, bloqueado ni
            // siquiera entra a la detección, y es el único que trae cuenta regresiva. Entre dos
            // tachaduras idénticas gana la que dice cuándo se va.
            if (blockedTurns > 0)
                return new ContractRowState(ContractRowMark.Blocked, baseDamage, effectiveDamage,
                    null, null, blockedTurns);

            if (forbidden)
                return new ContractRowState(ContractRowMark.Forbidden, baseDamage, 0, null, null, 0);

            if (effectiveDamage == baseDamage) return ContractRowState.Unmodified(baseDamage);

            for (int i = 0; i < table.Count; i++)
            {
                if (i == index) continue;
                var other = table[i];
                if (other.BaseDamage != effectiveDamage) continue;
                if (string.Equals(other.ComboId, row.ComboId, StringComparison.Ordinal)) continue;

                return new ContractRowState(ContractRowMark.Shifted, baseDamage, effectiveDamage,
                    other.ComboId, other.DisplayName, 0);
            }

            var mark = effectiveDamage > baseDamage ? ContractRowMark.Buffed : ContractRowMark.Nerfed;
            return new ContractRowState(mark, baseDamage, effectiveDamage, null, null, 0);
        }

        /// <summary>
        /// Los valores base de <paramref name="combos"/> según <paramref name="sheet"/>, en el
        /// mismo orden en que vienen. Las entradas null quedan en <c>default</c> para no correr
        /// los índices contra la lista de filas de la vista.
        /// </summary>
        public static ContractRowBase[] BuildTable(IReadOnlyList<BaseComboSO> combos, ContractSheet sheet)
        {
            if (combos == null || combos.Count == 0) return Array.Empty<ContractRowBase>();

            var table = new ContractRowBase[combos.Count];
            for (int i = 0; i < combos.Count; i++)
            {
                var combo = combos[i];
                if (combo == null) continue;
                table[i] = new ContractRowBase(
                    combo.ComboId,
                    LocalizedContent.Name(combo.ComboId, combo.DisplayName ?? string.Empty),
                    ComboRowView.ResolveBaseDamage(combo, sheet));
            }
            return table;
        }

        /// <summary>
        /// Estado de cada fila leyendo los servicios vivos. Sin servicios registrados (escena
        /// suelta en el editor, selección de clase) devuelve todas intactas.
        /// </summary>
        public static ContractRowState[] ResolveAll(IReadOnlyList<BaseComboSO> combos, ContractSheet sheet)
        {
            var table = BuildTable(combos, sheet);
            var states = new ContractRowState[table.Length];

            ServiceLocator.TryGetService<IContractModifierService>(out var mods);
            ServiceLocator.TryGetService<IComboBlockService>(out var blocks);

            for (int i = 0; i < table.Length; i++)
            {
                string comboId = table[i].ComboId;
                int baseDamage = table[i].BaseDamage;

                int effective = mods != null ? mods.GetEffectiveBaseDamage(comboId, baseDamage) : baseDamage;
                bool forbidden = mods != null && mods.IsForbidden(comboId);
                int blockedTurns = blocks != null ? blocks.GetRemainingTurns(comboId) : 0;

                states[i] = Resolve(table, i, effective, forbidden, blockedTurns);
            }
            return states;
        }

        /// <summary>
        /// Estado de una fila suelta, para los callers que no tienen la tabla entera. Sin
        /// vecinos no puede reconocer un corrimiento: lo reporta como buff o nerf, que es
        /// verdadero pero dice menos.
        /// </summary>
        public static ContractRowState ResolveSingle(BaseComboSO combo, ContractSheet sheet)
            => combo == null
                ? ContractRowState.Unmodified(0)
                : ResolveAll(new[] { combo }, sheet)[0];

        /// <summary>
        /// Combos de menor a mayor daño base — es el orden en que el jugador los va a buscar,
        /// y deja la escalera de valor a la vista.
        /// </summary>
        /// <remarks>
        /// Ordena una COPIA: <c>sheet.Combos</c> es la lista viva del contrato del héroe y
        /// reordenarla desde la UI le cambiaría el orden a todo el que la recorra.
        /// </remarks>
        public static List<BaseComboSO> SortByBaseDamage(ContractSheet sheet)
        {
            var ordered = new List<BaseComboSO>();
            if (sheet?.Combos == null) return ordered;

            foreach (var combo in sheet.Combos)
                if (combo != null) ordered.Add(combo);

            ordered.Sort((a, b) =>
            {
                int byDamage = ComboRowView.ResolveBaseDamage(a, sheet)
                    .CompareTo(ComboRowView.ResolveBaseDamage(b, sheet));
                // Empate: por nombre, para que el orden no baile entre aperturas.
                return byDamage != 0 ? byDamage : string.CompareOrdinal(a.ComboId, b.ComboId);
            });
            return ordered;
        }

        /// <summary>Contrato del héroe actual, o <c>null</c> fuera de una run.</summary>
        public static ContractSheet ResolvePlayerSheet()
            => ServiceLocator.TryGetService<IPlayerService>(out var players)
                ? players?.CurrentHero?.Sheet
                : null;
    }
}
