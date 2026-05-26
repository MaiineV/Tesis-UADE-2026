using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combos;
using Rollgeon.Entities.Behaviors;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Entities
{
    /// <summary>
    /// Datos estaticos de un enemigo. Unifica (a) la identidad + weakness del stub T97b
    /// con (b) los stats base + AI behaviors del Support (Content#0099). TECHNICAL.md §7.1.
    /// <para>
    /// Los 4 campos originales del stub (EntityId, DisplayName, WeaknessComboId,
    /// WeaknessMultiplierOverride) se <b>preservan sin renombrar</b> — este worktree
    /// solo agrega campos nuevos. Ver plan §10 handshake T97b ↔ T99.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Identity.</b> <see cref="EntityId"/>, <see cref="DisplayName"/>,
    /// <see cref="Description"/> vienen de <see cref="BaseEntitySO"/>. <c>EntityId</c>
    /// es overridable — el stub T97b lo declaraba como campo propio; al subir a
    /// <c>BaseEntitySO</c> queda el mismo nombre publico, solo cambia su owner.
    /// </para>
    /// <para>
    /// <b>CreateRuntimeStats.</b> Instancia un <see cref="ModifiableAttributes"/> fresh
    /// con <see cref="Health"/>, <see cref="Attributes.Stats.Speed"/>,
    /// <see cref="Attributes.Stats.Energy"/> y <see cref="Behaviors.HealStrength"/>
    /// inicializados con los valores base del SO. No se clonan modifiers (hero/enemy
    /// "fresco" al spawn — §2.2).
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Entities/Enemy Data", fileName = "EnemyData")]
    public class EnemyDataSO : BaseEntitySO
    {
        // -----------------------------------------------------------------
        // Weakness (§5 — T97b). NO renombrar estos 2 campos.
        // -----------------------------------------------------------------

        [Title("Weakness (§5 — T97b)")]
        [ValueDropdown(nameof(GetComboIds))]
        [Tooltip("ComboId al que este enemigo es debil. Vacio = sin debilidad. " +
                 "Se alimenta del ComboCatalogSO registrado en ServiceLocator.")]
        public string WeaknessComboId;

        [MinValue(0f)]
        [Range(0f, 5f)]
        [Tooltip("Override del multiplier global de weakness. 0 = usar RulesetSO.Weakness.DefaultMultiplier. " +
                 ">0 pisa el default global solo para este enemigo.")]
        public float WeaknessMultiplierOverride = 0f;

        // -----------------------------------------------------------------
        // Base Stats — Content#0099.
        // -----------------------------------------------------------------

        [Title("Base Stats")]
        [MinValue(1)]
        [Range(1, 200)]
        [Tooltip("HP maximo. Usado como cap para heals y como valor inicial de Health runtime.")]
        public int BaseHP = 20;

        [MinValue(0)]
        [Range(0, 100)]
        [Tooltip("Ataque base. Support puro suele tenerlo en 0 (el Auditor no ataca).")]
        public int BaseAttack = 0;

        [MinValue(0)]
        [Range(0, 50)]
        [Tooltip("Potencia de curacion base. Se suma al BaseHealAmount del SupportHealBehavior.")]
        public int BaseHealStrength = 5;

        [MinValue(1)]
        [Range(1, 20)]
        [Tooltip("Speed base (iniciativa). Consumido por TurnManager/InitiativeProvider. Oculto en UI via Speed[HiddenFromUI].")]
        public int BaseSpeed = 4;

        [MinValue(0)]
        [Range(0, 10)]
        [Tooltip("Energia maxima por turno. El Support gasta energia cuando el action economy esta activo (T100b).")]
        public int MaxEnergy = 3;

        [Title("Visual")]
        [Tooltip("Prefab que se instancia como pawn visual de este enemigo. " +
                 "Debe tener un EntityPawn (se agrega en runtime si falta).")]
        public GameObject VisualPrefab;

        [Title("Rewards")]
        [MinValue(0)]
        [Tooltip("Cantidad minima de oro que dropea al morir. 0 = no dropea oro.")]
        public int MinGoldDrop = 3;

        [MinValue(0)]
        [Tooltip("Cantidad maxima de oro que dropea al morir. Inclusive. Si <= MinGoldDrop, dropea exactamente MinGoldDrop.")]
        public int MaxGoldDrop = 5;

        // -----------------------------------------------------------------
        // Behaviors — Content#0099.
        // -----------------------------------------------------------------

        [Title("Behaviors")]
        [InfoBox("Lista de behaviors polimorficos inline que el enemigo ejecuta (segun su Trigger + Phase). " +
                 "Para el Auditor: agregar un SupportHealBehavior con Trigger=OnTurnStart y AllowedPhases=Combat.")]
        [OdinSerialize]
        public List<BaseBehavior> Behaviors = new List<BaseBehavior>();

        // -----------------------------------------------------------------
        // AI Decision Tree (§7.5 — Sprint04).
        // -----------------------------------------------------------------

        [Title("AI Decision Tree (§7.5)")]
        [InfoBox("Árbol polimórfico que decide qué hace el enemigo cada turno. " +
                 "Null/vacío = fallback al BasicEnemyAI (siempre ataca). " +
                 "Clonado deep al spawn para evitar shared state entre instancias.")]
        [OdinSerialize]
        public AIDecisionNode AIRoot;

        // -----------------------------------------------------------------
        // Runtime builders.
        // -----------------------------------------------------------------

        /// <inheritdoc />
        public override ModifiableAttributes CreateRuntimeStats()
        {
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(BaseHP));
            attrs.SetAttribute<Attack>(new Attack(BaseAttack));
            attrs.SetAttribute<Speed>(new Speed(BaseSpeed));
            attrs.SetAttribute<Energy>(new Energy(MaxEnergy));
            attrs.SetAttribute<HealStrength>(new HealStrength(BaseHealStrength));
            attrs.SetAttribute<Shield>(new Shield(0));
            return attrs;
        }

        /// <summary>
        /// Devuelve copias deep de los <see cref="Behaviors"/> declarados en el SO.
        /// Usa <see cref="SerializationUtility.CreateCopy"/> para preservar polimorfismo
        /// y garantizar que cada spawn tiene su propio <c>StoredValues</c> bag.
        /// </summary>
        public List<BaseBehavior> CreateRuntimeBehaviors()
        {
            var result = new List<BaseBehavior>();
            if (Behaviors == null) return result;
            foreach (var template in Behaviors)
            {
                if (template == null) continue;
                var clone = SerializationUtility.CreateCopy(template) as BaseBehavior;
                if (clone != null) result.Add(clone);
            }
            return result;
        }

        /// <summary>
        /// Devuelve una copia deep del <see cref="AIRoot"/> (si hay uno autorado).
        /// <see cref="TreeDrivenEnemyAI"/> la registra por enemigo spawned.
        /// </summary>
        public AIDecisionNode CreateRuntimeAIRoot()
        {
            if (AIRoot == null) return null;
            return SerializationUtility.CreateCopy(AIRoot) as AIDecisionNode;
        }

        // ---- Odin dropdown source (same pattern as BaseComboSO) ---------

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
    }
}
