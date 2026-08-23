using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combos;
using Rollgeon.Heroes;
using Rollgeon.Player;

namespace Rollgeon.Combat.AI.Bosses.Tahur
{
    /// <summary>
    /// Los combos del contrato ordenados de peor a mejor, con escalones 1-based. Ordena por
    /// <see cref="BaseComboSO.Priority"/> y no por daño base, porque es el criterio con el que
    /// <c>ContractSheet.MatchBest</c> resuelve qué mano ganó; desempata por <c>ComboId</c> ordinal
    /// para que el canto sea determinístico entre runs.
    /// </summary>
    public readonly struct TahurHandLadder
    {
        private readonly List<string> _comboIds;

        private TahurHandLadder(List<string> comboIds)
        {
            _comboIds = comboIds;
        }

        /// <summary>Cantidad de escalones. 0 = sin contrato legible.</summary>
        public int Count => _comboIds?.Count ?? 0;

        public bool IsValid => Count > 0;

        public IReadOnlyList<string> ComboIds => (IReadOnlyList<string>)_comboIds ?? Array.Empty<string>();

        /// <summary>Escalón (1-based) del combo, o 0 si no está en el contrato — el mismo valor que "no armó nada".</summary>
        public int RankOf(string comboId)
        {
            if (_comboIds == null || string.IsNullOrEmpty(comboId)) return 0;
            for (int i = 0; i < _comboIds.Count; i++)
            {
                if (string.Equals(_comboIds[i], comboId, StringComparison.Ordinal)) return i + 1;
            }
            return 0;
        }

        /// <summary>ComboId del escalón pedido (1-based), o <c>null</c> fuera de rango.</summary>
        public string ComboIdAt(int rank)
        {
            if (_comboIds == null || rank < 1 || rank > _comboIds.Count) return null;
            return _comboIds[rank - 1];
        }

        public static TahurHandLadder FromSheet(ContractSheet sheet)
        {
            if (sheet?.Combos == null || sheet.Combos.Count == 0) return default;

            var ranked = new List<BaseComboSO>(sheet.Combos.Count);
            foreach (var combo in sheet.Combos)
            {
                if (combo == null || string.IsNullOrEmpty(combo.ComboId)) continue;
                ranked.Add(combo);
            }
            if (ranked.Count == 0) return default;

            ranked.Sort((a, b) =>
            {
                int byPriority = a.Priority.CompareTo(b.Priority);
                return byPriority != 0
                    ? byPriority
                    : string.Compare(a.ComboId, b.ComboId, StringComparison.Ordinal);
            });

            var ids = new List<string>(ranked.Count);
            foreach (var combo in ranked) ids.Add(combo.ComboId);
            return new TahurHandLadder(ids);
        }

        /// <summary>Resuelve el <see cref="IPlayerService"/> del contexto y, si no vino, del <c>ServiceLocator</c>.</summary>
        public static TahurHandLadder FromContext(AIContext context)
        {
            var playerService = context?.PlayerService;
            if (playerService == null) ServiceLocator.TryGetService<IPlayerService>(out playerService);
            return FromSheet(playerService?.CurrentHero?.Sheet);
        }
    }
}
