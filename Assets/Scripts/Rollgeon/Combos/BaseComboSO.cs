using System;
using System.Collections.Generic;
using System.Linq;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Combos
{
    /// <summary>
    /// Base abstracta de todo combo detectable sobre una tirada de dados. Cubre la superficie
    /// del TECHNICAL.md §5.1 (<c>Matches</c>, <c>ComputeCount</c>, <c>Priority</c>) y agrega el
    /// metodo <see cref="Detect"/> requerido por Content#0097a (plan §4.1).
    /// <para>
    /// <b>Reconciliacion §5.1 / brief</b> (plan §4.1):
    /// <list type="bullet">
    /// <item><description><see cref="Matches"/> — abstract, lo implementa cada concreto.</description></item>
    /// <item><description><see cref="ComputeCount"/> — virtual, formula default del §5.1.1.</description></item>
    /// <item><description><see cref="Priority"/> — virtual con default <c>BaseDamage</c>. Generala override a <c>int.MaxValue</c>.</description></item>
    /// <item><description><see cref="Detect"/> — virtual, default orquesta <c>Matches</c> + <see cref="GetCountUsed"/>.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Hereda <see cref="SerializedScriptableObject"/></b> (Odin) para permitir round-trip
    /// polimorfico de <see cref="ExtraEffects"/> (TECHNICAL.md §13.6.1).
    /// </para>
    /// </summary>
    // [SETUP] Cada concrete expone su propio [CreateAssetMenu]. El usuario crea los .asset
    // en Assets/Rollgeon/Combos/Instances/ siguiendo docs/setup/Content#0097a_*.md.
    public abstract class BaseComboSO : SerializedScriptableObject
    {
        [Title("Identity")]
        [ValueDropdown(nameof(GetComboIds), AppendNextDrawer = true)]
        [Tooltip("ID canonico del combo. Formato 'combo.<snake_case>' per TECHNICAL.md §12.6. " +
                 "El dropdown muestra IDs ya autorados en el proyecto (asegurate de no duplicar); " +
                 "el field debajo te permite tipear un id nuevo.")]
        [SerializeField]
        protected string _comboId;

        [SerializeField]
        [Tooltip("Nombre legible para UI (pantalla de seleccion de clase, feedback de combate).")]
        protected string _displayName;

        [SerializeField, TextArea]
        [Tooltip("Descripcion para tooltips y UI de seleccion.")]
        protected string _description;

        [SerializeField]
        [Tooltip("Icono opcional para UI. Puede quedar null en esta tarea (pipeline de arte separado).")]
        protected Sprite _icon;

        [Title("Damage")]
        [SerializeField, Range(0, 500)]
        [Tooltip("Dano base plano del combo (editable por balance sin recompilar). " +
                 "Nunca incluye valores de dados: la formula v3 suma las caras contribuyentes " +
                 "por separado (Fix#0047). OJO: tambien es el Priority default del combo.")]
        protected int _baseDamage;

        [Title("Cuenta del combo (§5.1.1)")]
        [SerializeField]
        [Tooltip("Multiplicadores por valor del dado (index 0 = pip 1, ..., index 5 = pip 6). " +
                 "Usado por la formula del §5.1.1 en ComputeCount.")]
        [ValidateInput(nameof(ValidateValueMultipliersLength),
                       "ValueMultipliers debe tener exactamente 6 entradas (pip 1..6).")]
        protected float[] _valueMultipliers = new float[6];

        [SerializeField, MinValue(0)]
        [Tooltip("Multiplicador general de la formula §5.1.1.")]
        protected float _generalMultiplier = 1f;

        [Title("Extra effects (opcional)")]
        [OdinSerialize]
        [Tooltip("Efectos polimorficos extra disparados al resolver el combo (Foundation#0004). " +
                 "Consumidos downstream por T100b AttackResolver.")]
        protected List<EffectData> _extraEffects = new List<EffectData>();

        // ---- Public API (read-only para codigo consumer) -----------------

        /// <summary>ID canonico (ej. <c>combo.par</c>). Usado por <see cref="ComboCatalogSO"/>.</summary>
        public string ComboId => _comboId;

        /// <summary>Nombre legible para UI.</summary>
        public string DisplayName => _displayName;

        /// <summary>Descripcion para tooltips / UI.</summary>
        public string Description => _description;

        /// <summary>Icono opcional.</summary>
        public Sprite Icon => _icon;

        /// <summary>Dano base plano (editable en inspector).</summary>
        public int BaseDamage => _baseDamage;

        /// <summary>Multiplicadores por valor de dado (pip 1..6).</summary>
        public IReadOnlyList<float> ValueMultipliers => _valueMultipliers;

        /// <summary>Multiplicador general de la formula §5.1.1.</summary>
        public float GeneralMultiplier => _generalMultiplier;

        /// <summary>Efectos extra polimorficos (opcional).</summary>
        public IReadOnlyList<EffectData> ExtraEffects => _extraEffects;

        // ---- Abstract / virtual API (§5.1 + brief T97a) ------------------

        /// <summary>
        /// Predicado de matcheo sobre los dados. Cada concreto lo implementa. Orden-agnostico
        /// en general — los que necesiten orden (Escalera) normalizan internamente (plan §5.4).
        /// </summary>
        /// <param name="finalDice">Valores de los dados (post encantamientos). Puede venir null o vacio.</param>
        /// <returns><c>true</c> si el combo detecta match.</returns>
        public abstract bool Matches(int[] finalDice);

        /// <summary>
        /// Overload con los tipos de dado alineados 1:1 a <paramref name="finalDice"/>. Default:
        /// ignora los tipos y delega en <see cref="Matches(int[])"/> — solo los combos cuya regla
        /// depende del rango del dado (Fuerza Bruta) overridean.
        /// </summary>
        /// <param name="diceTypes">Tipo de cada dado, mismo orden que <paramref name="finalDice"/>.
        /// Puede venir null cuando el call site no tiene los tipos (paths legacy, tests).</param>
        public virtual bool Matches(int[] finalDice, IReadOnlyList<DiceType> diceTypes)
            => Matches(finalDice);

        /// <summary>
        /// Formula §5.1.1: <c>ComputeCount = (Σ dado × ValueMultipliers[dado-1]) × GeneralMultiplier</c>.
        /// Usado por <c>AttackResolver</c> (§12) downstream para la formula completa de dano:
        /// <c>damage = BaseDamage + ComputeCount</c>.
        /// </summary>
        public virtual float ComputeCount(int[] finalDice)
        {
            if (finalDice == null || finalDice.Length == 0) return 0f;
            float sum = 0f;
            for (int i = 0; i < finalDice.Length; i++)
            {
                int pip = finalDice[i];
                if (pip < 1 || pip > _valueMultipliers.Length) continue;
                sum += pip * _valueMultipliers[pip - 1];
            }
            return sum * _generalMultiplier;
        }

        /// <summary>
        /// Prioridad del combo al resolver conflictos (combo mas alto gana). Default: <see cref="BaseDamage"/>.
        /// Overrideado por Generala a <c>int.MaxValue</c> (plan §4 + §10.7).
        /// </summary>
        public virtual int Priority => _baseDamage;

        /// <summary>
        /// API tipada requerida por Content#0097a. Delega en la sobrecarga con override de base.
        /// </summary>
        /// <param name="diceValues">Valores de los dados. <c>null</c> o vacio devuelven <see cref="ComboDetectionResult.NoMatch"/>.</param>
        public ComboDetectionResult Detect(IReadOnlyList<int> diceValues)
            => Detect(diceValues, null);

        /// <summary>
        /// Default: orquesta <see cref="Matches"/> + <see cref="GetCountUsed"/> +
        /// <see cref="BaseDamage"/>. Combos con logica variable (SumaX, Higher Number,
        /// Fuerza Bruta) overridean para poblar <c>ContributingIndices</c> y
        /// <c>DynamicBonus</c> — el <c>BaseDamage</c> del resultado es SIEMPRE plano
        /// (Fix#0047: las caras entran al daño una sola vez, vía Σcaras).
        /// </summary>
        /// <param name="flatBaseOverride">Base plano de la tabla por clase (Spec Daño v2 —
        /// <c>ContractSheet.BaseDamageTable</c>). <c>null</c> = usar el base propio del SO.</param>
        public virtual ComboDetectionResult Detect(IReadOnlyList<int> diceValues, int? flatBaseOverride)
        {
            if (diceValues == null || diceValues.Count == 0) return ComboDetectionResult.NoMatch();
            var arr = diceValues as int[] ?? diceValues.ToArray();
            if (!Matches(arr)) return ComboDetectionResult.NoMatch();
            return ComboDetectionResult.Match(
                ComboId, flatBaseOverride ?? BaseDamage, GetCountUsed(arr), GetContributingIndices(arr));
        }

        /// <summary>
        /// Overload con los tipos de dado alineados 1:1 a <paramref name="diceValues"/>. Default:
        /// ignora los tipos y delega en <see cref="Detect(IReadOnlyList{int}, int?)"/> — solo los
        /// combos cuya regla depende del rango del dado (Fuerza Bruta) overridean.
        /// </summary>
        /// <param name="diceTypes">Tipo de cada dado, mismo orden que <paramref name="diceValues"/>.
        /// Null cuando el call site no tiene los tipos (paths legacy, tests).</param>
        /// <param name="flatBaseOverride">Ver <see cref="Detect(IReadOnlyList{int}, int?)"/>.</param>
        public virtual ComboDetectionResult Detect(IReadOnlyList<int> diceValues,
            IReadOnlyList<DiceType> diceTypes, int? flatBaseOverride)
            => Detect(diceValues, flatBaseOverride);

        /// <summary>
        /// Cantidad de dados consumidos cuando el combo matchea. Default: <c>finalDice.Length</c>.
        /// Cada concreto overridea con su constante (Par=2, Trio=3, etc.) o su calculo variable
        /// (SumaX).
        /// </summary>
        protected virtual int GetCountUsed(int[] finalDice)
            => finalDice?.Length ?? 0;

        /// <summary>
        /// Índices (en <paramref name="finalDice"/>) de los dados que formaron el combo ganador.
        /// Default: todos los índices. Combos de subconjunto menor (Par, Trio, Poker, Doble Par,
        /// Suma X) overridean.
        /// </summary>
        protected virtual int[] GetContributingIndices(int[] finalDice)
        {
            if (finalDice == null) return Array.Empty<int>();
            var indices = new int[finalDice.Length];
            for (int i = 0; i < finalDice.Length; i++) indices[i] = i;
            return indices;
        }

        // ---- Odin dropdown source ---------------------------------------

        /// <summary>
        /// Alimenta el <see cref="ValueDropdownAttribute"/> del <c>_comboId</c>.
        /// <para>
        /// <b>Runtime:</b> usa el <see cref="ComboCatalogSO"/> registrado en
        /// <c>ServiceLocator</c>.
        /// </para>
        /// <para>
        /// <b>Edit mode:</b> el <c>ServiceLocator</c> esta vacio (los bootstraps
        /// solo corren al Play). Escaneamos <c>BaseComboSO</c> assets del proyecto
        /// via <c>AssetDatabase</c> para que el Inspector muestre los IDs
        /// disponibles incluso sin un catalogo populado (plan §10.10).
        /// </para>
        /// </summary>
        /// <summary>
        /// Expone <see cref="GetComboIds"/> a otros drawers (ej. el <c>ValueDropdown</c> de
        /// <c>ContractSheet.BaseDamageTable</c>) sin duplicar el escaneo de assets.
        /// </summary>
        public static IEnumerable<string> GetKnownComboIds() => GetComboIds();

        private static IEnumerable<string> GetComboIds()
        {
            if (Application.isPlaying)
            {
                if (ServiceLocator.TryGetService<ComboCatalogSO>(out var cat) && cat != null)
                    return cat.AllIds;
                return Array.Empty<string>();
            }

#if UNITY_EDITOR
            var ids = new SortedSet<string>();
            var guids = UnityEditor.AssetDatabase.FindAssets("t:BaseComboSO");
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<BaseComboSO>(path);
                if (asset != null && !string.IsNullOrEmpty(asset.ComboId))
                    ids.Add(asset.ComboId);
            }
            return ids;
#else
            return Array.Empty<string>();
#endif
        }

        // ---- Odin validators --------------------------------------------

        private bool ValidateValueMultipliersLength(float[] arr)
            => arr != null && arr.Length == 6;
    }
}
