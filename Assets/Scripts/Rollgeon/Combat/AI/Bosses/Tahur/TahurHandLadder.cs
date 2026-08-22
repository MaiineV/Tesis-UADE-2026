using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combos;
using Rollgeon.Heroes;
using Rollgeon.Player;

namespace Rollgeon.Combat.AI.Bosses.Tahur
{
    /// <summary>
    /// La escalera de manos del contrato del jugador: los combos ordenados de peor a mejor,
    /// con escalones 1-based. Es lo que el Tahúr canta y contra lo que mide la mano jugada.
    /// </summary>
    /// <remarks>
    /// Ordena por <see cref="BaseComboSO.Priority"/> y no por daño base, porque Priority es el
    /// criterio con el que <c>ContractSheet.MatchBest</c> resuelve qué mano ganó. Desempata por
    /// <c>ComboId</c> ordinal para que el canto sea determinístico entre runs.
    /// </remarks>
    public readonly struct TahurHandLadder
    {
        private readonly List<string> _comboIds;

        private TahurHandLadder(List<string> comboIds)
        {
            _comboIds = comboIds;
        }

        /// <summary>Cantidad de escalones. 0 = sin contrato legible.</summary>
        public int Count => _comboIds?.Count ?? 0;

        /// <summary><c>true</c> si la escalera tiene al menos un escalón.</summary>
        public bool IsValid => Count > 0;

        /// <summary>Los comboIds del peor al mejor escalón.</summary>
        public IReadOnlyList<string> ComboIds => (IReadOnlyList<string>)_comboIds ?? Array.Empty<string>();

        /// <summary>
        /// Escalón (1-based) del combo, o 0 si no está en el contrato — el mismo valor que
        /// "no armó nada".
        /// </summary>
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

        /// <summary>Construye la escalera desde la hoja de contrato del jugador.</summary>
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

        /// <summary>
        /// Escalera del contrato del jugador activo, resolviendo el <see cref="IPlayerService"/>
        /// del contexto y, si no vino, del <c>ServiceLocator</c> (mismo degradado que
        /// <c>AINode_PromulgateRule</c>).
        /// </summary>
        public static TahurHandLadder FromContext(AIContext context)
        {
            var playerService = context?.PlayerService;
            if (playerService == null) ServiceLocator.TryGetService<IPlayerService>(out playerService);
            return FromSheet(playerService?.CurrentHero?.Sheet);
        }
    }
}
