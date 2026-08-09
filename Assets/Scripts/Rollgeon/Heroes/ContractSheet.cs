using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.ComboBlock; // [T103]
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
    /// <b>Validacion.</b> <see cref="Validate"/> exige al menos una entrada, sin nulls y sin
    /// <c>ComboId</c> duplicado. La cantidad y el orden son libres — la resolucion usa
    /// <see cref="BaseComboSO.Priority"/>, no la posicion en la lista.
    /// </para>
    /// </summary>
    [Serializable]
    [HideReferenceObjectPicker]
    public class ContractSheet
    {
        [Title("Contract")]
        [InfoBox("Lista de combos elegibles de la clase. Cantidad y orden libres — " +
                 "se evalua por Priority desc, el mayor que matchee gana.")]
        [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
        [OdinSerialize]
        [Required]
        public List<BaseComboSO> Combos = new List<BaseComboSO>();

        [SerializeField]
        [Tooltip("Etiqueta legible para UI de seleccion de clase (ej. 'Contrato del Guerrero').")]
        private string _displayLabel;

        [Title("Base Damage por clase (Spec Daño v2)")]
        [InfoBox("daño_combo_base por clase: cada entrada overridea el base plano del combo SO " +
                 "para ESTA clase. Sin entrada = se usa el base del SO (tabla global). " +
                 "No cambia la selección de combo (Priority sigue leyendo el base global), solo el daño. " +
                 "Para SumaX reemplaza solo el piso plano — X × hits suma encima.")]
        public List<ComboBaseDamageEntry> BaseDamageTable = new List<ComboBaseDamageEntry>();

        [Title("Escudo por clase (Spec Escudo v3)")]
        [InfoBox("escudo_combo_base por clase: tabla FIJA, independiente de BaseDamageTable — " +
                 "el escudo NUNCA deriva del daño (causa raíz del bug de escudo trivial). " +
                 "Sin entrada = 0: esta clase no genera escudo con ese combo. " +
                 "Fórmula completa en PlayerComboShield: misma que el daño (ATQ + base × " +
                 "multi × scratch), sin cap — el freno es el reset de escudo por turno.")]
        public List<ComboShieldBaseEntry> ShieldBaseTable = new List<ComboShieldBaseEntry>();

        /// <summary>Etiqueta legible para UI (screen #98).</summary>
        public string DisplayLabel => _displayLabel;

        // ---- Base damage por clase (Spec Daño v2) ------------------------

        /// <summary>
        /// Base plano de la tabla por clase para <paramref name="comboId"/>, o <c>null</c> si
        /// esta clase no define entrada (fallback al base del SO). Scan lineal — la tabla es
        /// chica (una entrada por combo del contrato).
        /// </summary>
        public int? GetBaseDamageOverride(string comboId)
        {
            if (string.IsNullOrEmpty(comboId) || BaseDamageTable == null) return null;
            for (int i = 0; i < BaseDamageTable.Count; i++)
            {
                if (BaseDamageTable[i].ComboId == comboId) return BaseDamageTable[i].BaseDamage;
            }
            return null;
        }

        /// <summary>
        /// Base plano efectivo del combo para esta clase: entrada de la tabla, o
        /// <see cref="BaseComboSO.BaseDamage"/> si no hay. Para lectores "planos" (UI de contrato,
        /// comparaciones de boss); el path de detección usa
        /// <see cref="BaseComboSO.Detect(IReadOnlyList{int}, int?)"/> con
        /// <see cref="GetBaseDamageOverride"/> para respetar combos dinámicos (SumaX).
        /// </summary>
        public int GetBaseDamage(BaseComboSO combo)
            => combo == null ? 0 : GetBaseDamageOverride(combo.ComboId) ?? combo.BaseDamage;

        // ---- Escudo por clase (Spec Escudo v2) ----------------------------

        /// <summary>
        /// <c>escudo_combo_base</c> de esta clase para <paramref name="comboId"/>. Sin entrada
        /// ⇒ 0 — la clase no genera escudo con ese combo (fallback explícito, sin default
        /// global). Scan lineal — la tabla es chica (una entrada por combo del contrato).
        /// </summary>
        public int GetShieldBase(string comboId)
        {
            if (string.IsNullOrEmpty(comboId) || ShieldBaseTable == null) return 0;
            for (int i = 0; i < ShieldBaseTable.Count; i++)
            {
                if (ShieldBaseTable[i].ComboId == comboId) return ShieldBaseTable[i].ShieldBase;
            }
            return 0;
        }

        [NonSerialized] private HashSet<string> _crossedComboIds;

        /// <summary>Set de <c>ComboId</c> tachados en runtime (no serializado — runtime-only).</summary>
        private HashSet<string> CrossedSet
            => _crossedComboIds ??= new HashSet<string>();

        // ---- Validation -------------------------------------------------

        /// <summary>
        /// Valida la hoja: al menos una entrada, sin nulls y sin duplicados por
        /// <see cref="BaseComboSO.ComboId"/>. Cantidad y orden libres — la resolucion
        /// (<see cref="MatchBest"/>) usa <see cref="BaseComboSO.Priority"/>, no la posicion.
        /// </summary>
        /// <param name="error">Mensaje humano-legible si retorna <c>false</c>.</param>
        public bool Validate(out string error)
        {
            if (Combos == null || Combos.Count == 0)
            {
                error = "ContractSheet must have at least one combo.";
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
            => MatchBest(dice, null);

        /// <summary>
        /// Overload con los tipos de dado alineados 1:1 a <paramref name="dice"/>. Null ⇒ los
        /// combos que dependen del rango del dado (Fuerza Bruta) caen a su fallback d6.
        /// </summary>
        public BaseComboSO MatchBest(IReadOnlyList<int> dice, IReadOnlyList<Rollgeon.Dice.DiceType> diceTypes)
        {
            if (dice == null || dice.Count == 0) return null;
            if (Combos == null || Combos.Count == 0) return null;

            int[] arr = dice as int[];
            if (arr == null)
            {
                arr = new int[dice.Count];
                for (int i = 0; i < dice.Count; i++) arr[i] = dice[i];
            }

            IComboBlockService blockService = null; // [T103]
            ServiceLocator.TryGetService<IComboBlockService>(out blockService); // [T103]

            BaseComboSO best = null;
            int bestPriority = int.MinValue;
            for (int i = 0; i < Combos.Count; i++)
            {
                var combo = Combos[i];
                if (combo == null) continue;
                if (IsCrossed(combo)) continue;
                if (blockService != null && blockService.IsBlocked(combo.ComboId)) continue;
                if (!combo.Matches(arr, diceTypes)) continue;
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
                // Entradas struct — copia por valor: mutar la tabla de la run no toca el asset.
                BaseDamageTable = BaseDamageTable != null
                    ? new List<ComboBaseDamageEntry>(BaseDamageTable)
                    : new List<ComboBaseDamageEntry>(),
                ShieldBaseTable = ShieldBaseTable != null
                    ? new List<ComboShieldBaseEntry>(ShieldBaseTable)
                    : new List<ComboShieldBaseEntry>(),
            };
            return copy;
        }
    }

    /// <summary>
    /// Entrada de la tabla <c>daño_combo_base</c> por clase (Spec Daño v2). Struct plano
    /// (string + int) para serializar idéntico por Unity y Odin — no agregar polimorfismo.
    /// </summary>
    [Serializable]
    public struct ComboBaseDamageEntry
    {
        [ValueDropdown("@Rollgeon.Combos.BaseComboSO.GetKnownComboIds()", AppendNextDrawer = true)]
        [Tooltip("ComboId del combo a overridear (ej. 'combo.par').")]
        public string ComboId;

        [Range(0, 500)]
        [Tooltip("daño_combo_base de esta clase para el combo. No cambia la selección " +
                 "(Priority usa el base global), solo el daño. En SumaX reemplaza el piso plano.")]
        public int BaseDamage;
    }

    /// <summary>
    /// Entrada de la tabla <c>escudo_combo_base</c> por clase (Spec Escudo v3). Struct plano
    /// — mismo criterio de serialización que <see cref="ComboBaseDamageEntry"/>. Tabla
    /// independiente de la de daño: la entrada decide si el combo genera escudo y con qué
    /// base; la fórmula (compartida con el daño) decide cuánto.
    /// </summary>
    [Serializable]
    public struct ComboShieldBaseEntry
    {
        [ValueDropdown("@Rollgeon.Combos.BaseComboSO.GetKnownComboIds()", AppendNextDrawer = true)]
        [Tooltip("ComboId del combo (ej. 'combo.par').")]
        public string ComboId;

        [Range(0, 200)]
        [Tooltip("escudo_combo_base de esta clase para el combo. Entra a la fórmula " +
                 "compartida daño/escudo como término base del combo, sin cap. " +
                 "Escala 100: calibrado a la par de las bases de daño (Par 8 … Generala 90).")]
        public int ShieldBase;
    }
}
