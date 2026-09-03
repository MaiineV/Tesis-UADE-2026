using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combos;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Entities.Traits;
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

        // -----------------------------------------------------------------
        // Ficha de diseño (GDD "Patrones de Ataque"). Solo metadata; ver EnemyDesignSheet.
        // -----------------------------------------------------------------

        [Title("Ficha de diseño (GDD)")]
        [Tooltip("Arquetipo, patrón geométrico y timing según el GDD. Ningún sistema de runtime lo lee; " +
                 "el Editor de enemigos lo usa para filtrar y para chequear coherencia con el árbol de IA.")]
        public EnemyDesignSheet Design = new EnemyDesignSheet();

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

        [Title("Player Curse")]
        [Tooltip("La pasiva de este jefe sobre el jugador, para el bloque PLAYER CURSE de su " +
                 "panel. Null = el bloque no existe (bestiario común y jefes sin maldición).")]
        public BossCurseSO Curse;

        // -----------------------------------------------------------------
        // Base Stats — Content#0099.
        // -----------------------------------------------------------------

        [Title("Base Stats")]
        [MinValue(1)]
        // 1000: la recalibración por simulación (3000 peleas) puso a los jefes de piso 3 arriba de
        // 500 — el Tahúr son 650. Con el tope viejo el valor se escribía bien desde el builder pero
        // el Inspector lo clampeaba en cuanto alguien rozaba el slider, y la ficha se perdía en
        // silencio.
        [Range(1, 1000)]
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

        [MinValue(0)]
        [Tooltip("Rango de ataque base (tiles). Se materializa como atributo AttackRange al " +
                 "spawnear (resuelto por tier, modificable por buffs): lo consumen el planner de " +
                 "movimiento (scoring de posiciones de ataque) y PcTargetInRange con " +
                 "UseOwnerAttackRange. 0 = sin rango declarado (los consumidores caen a 1/Range).")]
        public int BaseAttackRange = 1;

        // -----------------------------------------------------------------
        // Tiers — #158. Tier 1 = Base Stats de arriba.
        // -----------------------------------------------------------------

        [Title("Tiers (#158)")]
        [InfoBox("Tier 1 = los Base Stats de arriba. Agregá tiers para variantes mas fuertes. " +
                 "Cada stat por tier es Multiplicador (×base) o Manual (valor exacto), y se pueden " +
                 "mezclar dentro del mismo tier. Los tiers cambian solo stats, nunca la apariencia. " +
                 "En spawn se usa determinísticamente el tier más alto cuyo MinFloor <= piso actual " +
                 "(Tier 1 = desde piso 1). Sin tiers configurados, el enemigo siempre es Tier 1.")]
        public List<EnemyTier> ExtraTiers = new List<EnemyTier>();

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

        // -----------------------------------------------------------------
        // Unit Traits — Casillas Especiales + Pathing IA.
        // -----------------------------------------------------------------

        [Title("Unit Traits (Casillas Especiales)")]
        [Tooltip("Voladora: ignora las casillas especiales marcadas 'solo terrestres' (Pinchos, Hielo, Veneno). " +
                 "Fuego afecta igual a voladoras.")]
        public bool IsFlying;

        [Tooltip("Jefe: inmune a las casillas especiales de las que es owner, salvo override en la definición " +
                 "de la casilla. También cambia cómo el kill credit y la protección de SafeZone lo tratan.")]
        public bool IsBoss;

        [Tooltip("Familia de combate que muestra el panel al pasarle el mouse. Unset = no muestra " +
                 "la fila; un enemigo mal etiquetado miente sobre cómo se lo pelea. Es OTRO eje que " +
                 "Personality, que es riesgo de pathing: un support puede ser agresivo.")]
        public Rollgeon.Entities.Traits.EnemyArchetype Archetype = Rollgeon.Entities.Traits.EnemyArchetype.Unset;

        [Tooltip("Texto libre (ES) del renglón de tipo/familia de la tooltip. Vacío = usa el " +
                 "Archetype de arriba (localizado, con prefijo de Jefe). Si lo escribís, va " +
                 "TAL CUAL — incluido el 'Jefe · ' si corresponde. La tabla de localización, " +
                 "si tiene la key <EntityId>.type, le gana a este campo.")]
        public string TooltipType;

        [Tooltip("Perfil de riesgo del pathing IA frente a casillas peligrosas (MinSurvivalHP + Caution). " +
                 "Todo el catálogo arranca en Normal.")]
        public AIPersonality Personality = AIPersonality.Normal;

        [ShowIf("@Personality == Rollgeon.Entities.Traits.AIPersonality.Kamikaze")]
        [Tooltip("Flag narrativo: este kamikaze ignora por completo el filtro de supervivencia del pathing.")]
        public bool KamikazeIgnoresSurvival;

        [Title("Aura defensiva (0 = sin aura)")]
        [Tooltip("Radio Manhattan (rect-a-rect) del aura: los ALIADOS a esta distancia o menos " +
                 "reciben la reducción mientras el portador viva. 0 = sin aura.")]
        [MinValue(0)]
        public int AuraRadius;

        [Tooltip("Puntos de daño que el aura descuenta de cada golpe entrante a los aliados " +
                 "(piso de daño 1). Con varias auras aplica la mayor, no suman.")]
        [MinValue(0)]
        public int AuraFlatReduction;

        /// <summary>True si este enemigo proyecta aura (radio y reducción positivos).</summary>
        public bool HasAura => AuraRadius > 0 && AuraFlatReduction > 0;

        [Title("Tamaño en grilla")]
        [Tooltip("Celdas que ocupa (ancho × alto). (1,1) = enemigo común. El ancla es la celda inferior-izquierda " +
                 "(min X, min Y): el marcador de spawn del layout es esa celda y el pawn se centra en el rectángulo. " +
                 "Fase A: solo enemigos estáticos; el pathfinding y el targeting multi-celda son Fase B/C.")]
        public Vector2Int Footprint = Vector2Int.one;

        /// <summary>Footprint saneado (cada eje ≥ 1): lo único que lee el runtime.</summary>
        public Vector2Int EffectiveFootprint => Grid.GridFootprint.Normalize(Footprint);

        public bool HasMultiCellFootprint => !Grid.GridFootprint.IsUnit(EffectiveFootprint);

        [Title("AI Decision Tree (§7.5)")]
        [InfoBox("Árbol polimórfico que decide qué hace el enemigo cada turno. " +
                 "Null/vacío = fallback al BasicEnemyAI (siempre ataca). " +
                 "Clonado deep al spawn para evitar shared state entre instancias.")]
        [OdinSerialize]
        public AIDecisionNode AIRoot;

        /// <summary>
        /// Subárboles que el designer dejó sin conectar en el Editor de enemigos (raíces de cada
        /// subárbol suelto). Persisten para no perder trabajo a medio armar; el runtime los ignora
        /// (<see cref="CreateRuntimeAIRoot"/> solo copia <see cref="AIRoot"/>).
        /// </summary>
        [Title("Nodos sueltos (Editor de enemigos)")]
        [InfoBox("Subárboles sin conectar que quedaron en el canvas del Editor de enemigos. El runtime los ignora; " +
                 "editalos desde Tools → Editor de enemigos, no desde acá.")]
        [OdinSerialize]
        public List<AIDecisionNode> AIDetachedNodes = new List<AIDecisionNode>();

        // -----------------------------------------------------------------
        // Runtime builders.
        // -----------------------------------------------------------------

        /// <inheritdoc />
        public override ModifiableAttributes CreateRuntimeStats() => CreateRuntimeStats(1);

        /// <summary>
        /// Construye los stats runtime para el <paramref name="tier"/> pedido (1-based).
        /// Tier 1 = los <c>Base*</c>; tiers superiores resuelven cada stat via
        /// <see cref="TierStat.Resolve"/>. <c>virtual</c> para que
        /// <c>BossFloorManagerSO</c> herede el tiering sin cambios.
        /// </summary>
        public virtual ModifiableAttributes CreateRuntimeStats(int tier)
        {
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(ResolveStat(tier, t => t.HP, BaseHP)));
            attrs.SetAttribute<Attack>(new Attack(ResolveStat(tier, t => t.Attack, BaseAttack)));
            attrs.SetAttribute<Speed>(new Speed(ResolveStat(tier, t => t.Speed, BaseSpeed)));
            attrs.SetAttribute<Energy>(new Energy(ResolveStat(tier, t => t.Energy, MaxEnergy)));
            attrs.SetAttribute<HealStrength>(new HealStrength(ResolveStat(tier, t => t.HealStrength, BaseHealStrength)));
            attrs.SetAttribute<AttackRange>(new AttackRange(ResolveStat(tier, t => t.AttackRange, BaseAttackRange)));
            attrs.SetAttribute<Shield>(new Shield(0));
            return attrs;
        }

        // -----------------------------------------------------------------
        // Tier resolvers — #158.
        // -----------------------------------------------------------------

        /// <summary>Tiers totales disponibles (Tier 1 base + <see cref="ExtraTiers"/>).</summary>
        public int TierCount => 1 + (ExtraTiers?.Count ?? 0);

        /// <summary>Clampea un tier 1-based al rango disponible.</summary>
        public int ClampTier(int tier) => Mathf.Clamp(tier, 1, TierCount);

        /// <summary>
        /// Config del tier pedido, o <c>null</c> para Tier 1 / fuera de rango (⇒ usar <c>Base*</c>).
        /// </summary>
        public EnemyTier GetTier(int tier)
        {
            if (tier <= 1 || ExtraTiers == null) return null;
            int idx = tier - 2;
            return (idx >= 0 && idx < ExtraTiers.Count) ? ExtraTiers[idx] : null;
        }

        /// <summary>
        /// HP maximo resuelto para el tier — fuente unica de verdad para healthbar / AI / state.
        /// </summary>
        public int ResolveMaxHP(int tier) => ResolveStat(tier, t => t.HP, BaseHP);

        /// <summary>
        /// MinFloor efectivo de un tier (1-based). Único punto de normalización legacy:
        /// tiers autorados antes de MinFloor deserializan 0 ⇒ efectivo = número de tier
        /// (T2 ⇒ piso 2). Tier 1 (base) es siempre piso 1.
        /// </summary>
        public int EffectiveMinFloor(int tierNumber)
        {
            if (tierNumber <= 1) return 1;
            var t = GetTier(tierNumber);
            if (t == null) return 1;
            return t.MinFloor > 0 ? t.MinFloor : tierNumber;
        }

        /// <summary>
        /// Tier (1-based) a usar en el piso dado: el más alto cuyo MinFloor efectivo
        /// &lt;= <paramref name="floorNumber"/> (1-based, clampeado a ≥1). Determinístico —
        /// la aleatoriedad de spawn decide QUÉ enemigo aparece, no su tier.
        /// </summary>
        public int ResolveTierForFloor(int floorNumber)
        {
            int floor = Mathf.Max(1, floorNumber);
            for (int tier = TierCount; tier >= 2; tier--)
            {
                if (EffectiveMinFloor(tier) <= floor) return tier;
            }
            return 1;
        }

        /// <summary>
        /// Template para el próximo tier: copia memberwise del último extra tier (los
        /// <see cref="TierStat"/> son structs ⇒ copia por valor) con
        /// <c>MinFloor = efectivo(último) + 1</c> y Label auto. Sin extras ⇒ copia de
        /// Tier 1 (todo ×1 base) desde piso 2. El caller lo agrega a <see cref="ExtraTiers"/>.
        /// </summary>
        public EnemyTier CreateNextTierTemplate()
        {
            int nextNumber = TierCount + 1;
            var last = GetTier(TierCount);
            if (last == null)
                return new EnemyTier { Label = "T2", MinFloor = 2 };

            return new EnemyTier
            {
                Label = $"T{nextNumber}",
                MinFloor = EffectiveMinFloor(TierCount) + 1,
                HP = last.HP,
                Attack = last.Attack,
                Speed = last.Speed,
                Energy = last.Energy,
                HealStrength = last.HealStrength,
                AttackRange = last.AttackRange
            };
        }

        private int ResolveStat(int tier, Func<EnemyTier, TierStat> pick, int baseValue)
        {
            var t = GetTier(tier);
            return t == null ? baseValue : pick(t).Resolve(baseValue);
        }

        /// <summary>
        /// Traits runtime de este enemigo, listos para <c>IUnitTraitService.Register</c>.
        /// </summary>
        public UnitTraits CreateTraits()
            => new UnitTraits(IsFlying, IsBoss, Personality, KamikazeIgnoresSurvival);

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
