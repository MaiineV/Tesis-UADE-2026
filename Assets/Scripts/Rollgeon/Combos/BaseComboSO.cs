using System;
using System.Collections.Generic;
using System.Linq;
using Patterns;
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
                 "Para combos variables (SumaX) se suma encima de la suma de los dados que matchean.")]
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
        /// API tipada requerida por Content#0097a. Default: orquesta <see cref="Matches"/> +
        /// <see cref="GetCountUsed"/> + <see cref="BaseDamage"/>. Combos con logica variable
        /// (SumaX) overridean para calcular <c>BaseDamage</c> dinamico.
        /// </summary>
        /// <param name="diceValues">Valores de los dados. <c>null</c> o vacio devuelven <see cref="ComboDetectionResult.NoMatch"/>.</param>
        public virtual ComboDetectionResult Detect(IReadOnlyList<int> diceValues)
        {
            if (diceValues == null || diceValues.Count == 0) return ComboDetectionResult.NoMatch();
            var arr = diceValues as int[] ?? diceValues.ToArray();
            if (!Matches(arr)) return ComboDetectionResult.NoMatch();
            return ComboDetectionResult.Match(BaseDamage, GetCountUsed(arr));
        }

        /// <summary>
        /// Cantidad de dados consumidos cuando el combo matchea. Default: <c>finalDice.Length</c>.
        /// Cada concreto overridea con su constante (Par=2, Trio=3, etc.) o su calculo variable
        /// (SumaX).
        /// </summary>
        protected virtual int GetCountUsed(int[] finalDice)
            => finalDice?.Length ?? 0;

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
