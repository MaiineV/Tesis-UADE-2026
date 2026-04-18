using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combos;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Heroes
{
    /// <summary>
    /// Hoja de contrato del heroe (TECHNICAL.md §5.3). Contiene la lista ordenada de combos
    /// elegibles de la clase + la API minima de resolucion:
    /// <see cref="MatchBest"/> / <see cref="EvaluateRoll"/> (aliases) y el sistema de tachado
    /// <see cref="CrossCombo"/> / <see cref="IsCrossed"/> (consumido por T97c + T103 boss).
    /// <para>
    /// <b>No es un <see cref="ScriptableObject"/></b> — se embebe dentro de
    /// <see cref="ClassHeroSO"/>. Todas las referencias a <see cref="BaseComboSO"/> son
    /// punteros al catalogo (<see cref="ComboCatalogSO"/>) — estos SOs no se clonan.
    /// </para>
    /// <para>
    /// <b>Validacion §5.4.</b> Para el contrato del Guerrero el <see cref="Validate"/> exige
    /// 8 entradas no-nulas, sin <c>ComboId</c> duplicado, y la ultima debe ser
    /// <c>Generala</c> (<see cref="BaseComboSO.Priority"/> == <see cref="int.MaxValue"/>).
    /// </para>
    /// </summary>
    [Serializable]
    [HideReferenceObjectPicker]
    public class ContractSheet
    {
        [Title("Contract")]
        [InfoBox("Lista de combos elegibles de la clase. Para Warrior: 8 entradas en el orden " +
                 "[Par, DoblePar, SumaX, Trio, Escalera, FullHouse, Poker, Generala]. " +
                 "Se evalua por Priority desc — el mayor que matchee gana.")]
        [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
        [OdinSerialize]
        [Required]
        public List<BaseComboSO> Combos = new List<BaseComboSO>();

        [SerializeField]
        [Tooltip("Etiqueta legible para UI de seleccion de clase (ej. 'Contrato del Guerrero').")]
        private string _displayLabel;

        /// <summary>Etiqueta legible para UI (screen #98).</summary>
        public string DisplayLabel => _displayLabel;

        [NonSerialized] private HashSet<string> _crossedComboIds;

        /// <summary>Set de <c>ComboId</c> tachados en runtime (no serializado — runtime-only).</summary>
        private HashSet<string> CrossedSet
            => _crossedComboIds ??= new HashSet<string>();

        // ---- Validation -------------------------------------------------

        /// <summary>
        /// Valida la hoja: 8 entradas no-nulas, sin duplicados por <see cref="BaseComboSO.ComboId"/>,
        /// y la ultima entrada debe ser Generala (<c>Priority == int.MaxValue</c>).
        /// </summary>
        /// <param name="error">Mensaje humano-legible si retorna <c>false</c>.</param>
        public bool Validate(out string error)
        {
            if (Combos == null || Combos.Count != 8)
            {
                error = $"ContractSheet must have exactly 8 combos (got {Combos?.Count ?? 0}).";
                return false;
            }

            var seen = new HashSet<string>();
            for (int i = 0; i < Combos.Count; i++)
            {
                var c = Combos[i];
                if (c == null)
                {
                    error = $"ContractSheet entry [{i}] is null.";
                    return false;
                }
                if (string.IsNullOrEmpty(c.ComboId))
                {
                    error = $"ContractSheet entry [{i}] has empty ComboId.";
                    return false;
                }
                if (!seen.Add(c.ComboId))
                {
                    error = $"ContractSheet has duplicate ComboId '{c.ComboId}' at index [{i}].";
                    return false;
                }
            }

            var last = Combos[Combos.Count - 1];
            if (last.Priority != int.MaxValue)
            {
                error = $"ContractSheet last entry must be Generala (Priority == int.MaxValue). " +
                        $"Got '{last.ComboId}' with priority {last.Priority}.";
                return false;
            }

            error = null;
            return true;
        }

        // ---- Evaluation -------------------------------------------------

        /// <summary>
        /// Alias literal del brief: itera los combos ordenados por <see cref="BaseComboSO.Priority"/>
        /// descendente, retorna el primero que <see cref="BaseComboSO.Matches"/>. Skips tachados.
        /// Retorna <c>null</c> si no hay match (incluye input vacio/null).
        /// </summary>
        public BaseComboSO MatchBest(IReadOnlyList<int> dice)
        {
            if (dice == null || dice.Count == 0) return null;
            if (Combos == null || Combos.Count == 0) return null;

            int[] arr = dice as int[];
            if (arr == null)
            {
                arr = new int[dice.Count];
                for (int i = 0; i < dice.Count; i++) arr[i] = dice[i];
            }

            BaseComboSO best = null;
            int bestPriority = int.MinValue;
            for (int i = 0; i < Combos.Count; i++)
            {
                var combo = Combos[i];
                if (combo == null) continue;
                if (IsCrossed(combo)) continue;
                if (!combo.Matches(arr)) continue;
                if (combo.Priority > bestPriority)
                {
                    best = combo;
                    bestPriority = combo.Priority;
                }
            }
            return best;
        }

        /// <summary>
        /// Alias §5.3 TECHNICAL.md. Delega en <see cref="MatchBest"/>.
        /// </summary>
        public BaseComboSO EvaluateRoll(int[] finalDice)
            => MatchBest(finalDice);

        // ---- Cross combo -------------------------------------------------

        /// <summary>
        /// Marca el combo como "tachado" para el resto de la run (o hasta que se resetee la hoja).
        /// Idempotente. Dispara <see cref="EventName.OnComboCrossed"/> via <see cref="EventManager"/>
        /// con <c>args = [Guid.Empty, combo.ComboId]</c> (el caller puede disparar su propio
        /// evento con sourceGuid real si lo tiene).
        /// </summary>
        public void CrossCombo(BaseComboSO combo)
        {
            if (combo == null || string.IsNullOrEmpty(combo.ComboId)) return;
            if (!CrossedSet.Add(combo.ComboId)) return;
            EventManager.Trigger(EventName.OnComboCrossed, Guid.Empty, combo.ComboId);
        }

        /// <summary><c>true</c> si el combo fue tachado en esta hoja.</summary>
        public bool IsCrossed(BaseComboSO combo)
        {
            if (combo == null || string.IsNullOrEmpty(combo.ComboId)) return false;
            if (_crossedComboIds == null) return false;
            return _crossedComboIds.Contains(combo.ComboId);
        }

        // ---- Runtime cloning --------------------------------------------

        /// <summary>
        /// Clona la hoja para aislar runs. Los <see cref="BaseComboSO"/> se copian por referencia
        /// (son SOs de catalogo inmutables); el set de tachados se inicializa vacio.
        /// </summary>
        public ContractSheet Instantiate()
        {
            var copy = new ContractSheet
            {
                Combos = Combos != null ? new List<BaseComboSO>(Combos) : new List<BaseComboSO>(),
                _displayLabel = _displayLabel,
            };
            return copy;
        }
    }
}
