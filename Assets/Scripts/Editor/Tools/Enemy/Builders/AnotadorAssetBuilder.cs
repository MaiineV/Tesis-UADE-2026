using System;
using System.Collections.Generic;
using System.IO;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Entities;
using Rollgeon.Feedback;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Builders
{
    /// <summary>
    /// Arma El Anotador (jefe de piso 2): su <see cref="EnemyDataSO"/> con el árbol AI inline, el
    /// <see cref="HazardDefinitionSO"/> de la estela helada y el vestuario visual.
    /// </summary>
    /// <remarks>
    /// Las <c>Build*</c>/<c>Populate*</c>/<c>Configure*</c> son puras y se testean sin tocar el
    /// <see cref="AssetDatabase"/>; el <see cref="MenuItem"/> es el único que escribe.
    /// </remarks>
    public static class AnotadorAssetBuilder
    {
        /// <summary>Menú que regenera estos assets. Lo lee el Editor de enemigos para avisar que el builder pisa el árbol.</summary>
        public const string MenuPath = "Tools/Rollgeon/Bosses/Build Anotador";

        // ======================================================================
        // Identidad y rutas
        // ======================================================================

        public const string EnemyAssetPath = "Assets/Rollgeon/Enemies/ED_Boss_Anotador.asset";
        public const string IceHazardAssetPath = "Assets/Rollgeon/Combat/Hazards/IceTrailHazardDefinition.asset";

        /// <summary>Wrapper de gameplay del jefe, armado por <see cref="BossVisualWrapperBuilder"/>.</summary>
        public const string VisualPrefabPath = "Assets/Prefabs/Enemies/Bosses/PF_Boss_Anotador.prefab";

        /// <summary>
        /// El <b>modelo</b> del mímico, no <c>ChestMimic_Prefab.prefab</c>: ese ya trae pawn, registro
        /// y barra, y quedarían duplicados con los que el wrapper pone en el root.
        /// </summary>
        public const string ArtModelPath = "Assets/Art/3D/Models/Enemies/ChestMimic_Model.fbx";

        /// <summary>Controller del mímico. El FBX no lo trae; se asigna tras armar el wrapper.</summary>
        public const string AnimatorControllerPath =
            "Assets/Art/3D/Animations/Enemies/ChestMimic/AnimCon_ChestMimic.controller";

        /// <summary>Parámetro del controller que separa "cofre inocente" de "planilla viva".</summary>
        public const string AwakenParameter = "Awaken";

        /// <summary>Retrato del rig que viste (<c>ChestMimic</c>). Ver <see cref="BossPortraitLibrary"/>.</summary>
        public const string PortraitTexturePath = BossPortraitLibrary.AnotadorPath;

        public const string BossName = "Anotador";
        public const string ArtChildName = "Art";

        /// <summary>FPS del stepping — el mismo del resto del roster animado.</summary>
        public const int SteppedAnimationFps = 8;

        /// <summary>
        /// El root del FBX mira -Z y el wrapper fuerza identidad en el hijo de arte: sin esto el
        /// mímico entra a la sala de espaldas.
        /// </summary>
        public static readonly Vector3 ArtLocalEuler = new Vector3(0f, 180f, 0f);

        public const string EntityId = "boss.scorekeeper";
        public const string DisplayName = "El Anotador";

        /// <summary>Debilidad: la única mano que no depende de la tabla.</summary>
        public const string WeaknessComboId = "combo.generala";
        public const float WeaknessMultiplier = 1.5f;

        /// <summary>
        /// Fijo y no <c>Guid.NewGuid()</c>: reconstruir el asset no puede cambiarle la identidad al
        /// hazard o se rompe cualquier estado keyed por source.
        /// </summary>
        public const string IceHazardSourceId = "b7d4f2a6-3c81-4e59-9a02-5f6d8c1e7b43";

        // ======================================================================
        // Números de la ficha
        // ======================================================================

        /// <summary>Piso 2: ~7 turnos con el golpe base del piso (mediana 24).</summary>
        public const int BaseHp = 170;
        public const int BaseAttack = 30;
        public const int MinGoldDrop = 30;
        public const int MaxGoldDrop = 60;

        public const int RowDamage = 30;
        public const int ColumnDamage = 32;
        public const int PencilDamage = 12;

        /// <summary>Alcance del lápiz, en Manhattan: el peaje de las casillas de melee.</summary>
        public const int PencilRange = 1;

        public const int MarkSize = 1;

        /// <summary>
        /// Ancho de la columna en fase 2. El <c>Size</c> de <see cref="ThreatShape.Column"/> es el
        /// ancho de la franja en casillas: 3 ⇒ la columna del jugador ±1.
        /// </summary>
        public const int Phase2ColumnSize = 3;

        /// <summary>Distancia que el repliegue intenta mantener. Solo se mueve si lo tienen a 3 o menos.</summary>
        public const int IdealDistance = 4;

        /// <summary>
        /// Pasos del repliegue ⇒ tope real de casillas de la estela: por debajo de
        /// <see cref="MaxTrailTiles"/> ese recorte sería letra muerta. No aleja más al jefe —
        /// <see cref="AINode_KeepDistance"/> topea el destino en <see cref="IdealDistance"/>.
        /// </summary>
        public const int RetreatSteps = 4;

        /// <summary>Casillas de la estela.</summary>
        public const int MaxTrailTiles = 4;

        public const int TrailStunTurns = 1;

        /// <summary>
        /// Vida de la estela. La ficha pide <b>3 rondas</b> y acá va <b>4</b>: la estela nace en el
        /// turno del jefe, con el del jugador de esa ronda ya jugado (CNF-006), así que
        /// <c>DurationRounds = D</c> vale <c>D - 1</c> rondas pisables. Mismo +1 que
        /// <c>CroupierAssetBuilder.FireDurationRounds</c>.
        /// </summary>
        public const int TrailDurationRounds = 4;

        public const float Phase2HpThreshold = 0.35f;
        public const int ShiftsPerTurnPhase1 = 1;
        public const int ShiftsPerTurnPhase2 = 2;

        /// <summary>Paridad de ronda del lápiz y de la columna: impares lápiz/fila, pares columna.</summary>
        public const int ParityDivisor = 2;

        /// <summary>Celeste: la estela no puede leerse como el naranja del telegraph.</summary>
        public static readonly Color IceOverlayTint = new Color(0.35f, 0.8f, 1f, 0.55f);

        /// <summary>
        /// Grafito del lápiz. <c>AnotadorVisualWiringTests</c> lo afirma legible contra los otros dos
        /// tintes.
        /// </summary>
        public static readonly Color PencilOverlayTint = new Color(0.42f, 0.45f, 0.58f, 0.6f);

        // ======================================================================
        // Paleta del mímico congelado
        // ======================================================================

        public const string PaletteShaderPath = "Assets/Shaders/PaletteCelLit.shader";
        public const string PaletteShaderName = "Rollgeon/PaletteCelLit";

        /// <summary>Carpeta de los materiales propios del jefe.</summary>
        public static string MaterialsFolder =>
            $"{BossVisualWrapperBuilder.DefaultMaterialsRoot}/{BossName}";

        /// <summary>Los materiales que este builder autorea para vestir al mímico de hielo.</summary>
        /// <remarks>
        /// No se usa <c>BossWrapperSpec.Retints</c>: ese camino clona el material del FBX, y estos
        /// traen el namespace de Maya (<c>Enemy_:Wood1</c>, con <c>:</c> ilegal en un path de Windows)
        /// y están en URP Lit, sin los canales del retinte ni <c>_HitFlashAmount</c>.
        /// </remarks>
        public static readonly IReadOnlyDictionary<string, MaterialRetint> IcePaints =
            new Dictionary<string, MaterialRetint>
            {
                // Cuerpo: la tapa de la planilla, hielo azul.
                ["Ice"] = MaterialRetint.FromColors(
                    new Color(0.78f, 0.90f, 0.96f),
                    new Color(0.25f, 0.65f, 0.75f),
                    new Color(0.09f, 0.22f, 0.38f)),

                // Herrajes: el grafito del lápiz, la única parte que no es hielo.
                ["Graphite"] = MaterialRetint.FromColors(
                    new Color(0.62f, 0.66f, 0.72f),
                    new Color(0.38f, 0.41f, 0.47f),
                    new Color(0.14f, 0.16f, 0.21f)),

                // Carne congelada: más pálida y menos saturada que el cuerpo.
                ["Frost"] = MaterialRetint.FromColors(
                    new Color(0.85f, 0.88f, 0.95f),
                    new Color(0.48f, 0.58f, 0.72f),
                    new Color(0.20f, 0.26f, 0.40f)),

                // Dientes: hueso casi blanco — el contraste que hace legible la boca.
                ["Bone"] = MaterialRetint.FromColors(
                    new Color(0.97f, 0.99f, 1.00f),
                    new Color(0.80f, 0.88f, 0.93f),
                    new Color(0.42f, 0.55f, 0.65f)),

                // Lengua/cavidad: teal oscuro, para que la boca lea como hueco y no como cuerpo.
                ["Maw"] = MaterialRetint.FromColors(
                    new Color(0.55f, 0.80f, 0.85f),
                    new Color(0.28f, 0.52f, 0.60f),
                    new Color(0.10f, 0.20f, 0.28f)),

                // Ojo: el celeste EXACTO del overlay, para atar el hielo del piso al bicho.
                ["Eye"] = MaterialRetint.FromColors(
                    new Color(0.75f, 0.95f, 1.00f),
                    new Color(0.35f, 0.80f, 1.00f),
                    new Color(0.10f, 0.35f, 0.55f)),
            };

        /// <summary>
        /// Material del FBX (canónico, ver <see cref="CanonicalMaterialName"/>) → entrada de
        /// <see cref="IcePaints"/>. <c>Material</c> es el slot sin nombre del export.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> ArtMaterialPaints =
            new Dictionary<string, string>
            {
                ["Wood1"] = "Ice",
                ["Material"] = "Ice",
                ["Frame1"] = "Graphite",
                ["Flesh"] = "Frost",
                ["Theet"] = "Bone",
                ["Tongue"] = "Maw",
                ["Eye"] = "Eye",
            };

        // ======================================================================
        // VFX de la estela
        // ======================================================================

        /// <summary>Plantilla del burst: el glow de curación, el único VFX one-shot del proyecto.</summary>
        public const string VfxTemplatePrefabPath = "Assets/Prefabs/VFX/VFX_HealGlow.prefab";
        public const string VfxTemplateMaterialPath = "Assets/Materials/VFX/VFXMat_Heal_Green.mat";

        public const string IceVfxPrefabPath = "Assets/Prefabs/VFX/VFX_IceBurst.prefab";
        public const string IceVfxMaterialPath = "Assets/Materials/VFX/VFXMat_Impact_Ice.mat";

        /// <summary>Mismo celeste que <see cref="IceOverlayTint"/>, sin alpha.</summary>
        public static readonly Color IceVfxColor = new Color(0.35f, 0.8f, 1f, 1f);

        /// <summary>
        /// Vida del burst, explícita: <c>IceTrailHazardDefinition.asset</c> es anterior a
        /// <c>TriggerVfxLifetime</c>, y dejarla al default filtra un ParticleSystem por pisada.
        /// </summary>
        public const float TrailBurstLifetime = 1.5f;

        // ======================================================================
        // Capa pura — testeable sin assets
        // ======================================================================

        /// <summary>
        /// Árbol del turno. Sequence raíz de 7 hijos, en el orden de la ficha:
        /// <c>detona → tacha → lápiz → se acomoda → estela → fila/columna → fase 2</c>.
        /// </summary>
        /// <remarks>
        /// Todo hijo que pueda devolver <c>Failed</c> va en <c>Selector[…, Wait]</c>: el Sequence
        /// aborta el turno al primer Failed. El <c>Selector</c> del hijo 6 garantiza <b>una sola</b>
        /// marca grande por turno (fila + columna rompen el techo de daño del piso), y el lápiz va
        /// antes del repliegue o el boss ya estaría lejos y no cobraría nunca.
        /// </remarks>
        public static AINode_Sequence BuildAIRoot(HazardDefinitionSO iceHazard)
        {
            return new AINode_Sequence
            {
                Children = new List<AIDecisionNode>
                {
                    // 1. Detona la marca del turno pasado. El rig sólo tiene 'Attack', así que melee
                    //    y lápiz caen en el mismo clip.
                    new AINode_ExecuteTelegraph { WindupFeedbackId = BossFeedbackIds.AnotadorMeleeAnim },

                    // 2. Tacha: corre el combo más jugado al vecino de la hoja. Envuelto porque
                    //    devuelve Failed si IContractModifierService no está registrado.
                    Fallback(BuildShiftNode()),

                    // 3. El lápiz, en rondas impares y antes de replegarse (ver remarks).
                    Fallback(new AINode_If
                    {
                        Conditions = new List<BasePreCondition> { OddRound() },
                        Then = new AINode_AnotadorPencil
                        {
                            Damage = PencilDamage,
                            Range = PencilRange,
                            Metric = DistanceMetric.Manhattan,
                            Kind = AttackKind.BasicAttack,
                        },
                    }),

                    // 4. Se acomoda: si lo tienen a 3 o menos, se repliega a 4.
                    Fallback(new AINode_KeepDistance
                    {
                        MaxSteps = new AIConstantInt { Value = RetreatSteps },
                        IdealDistance = new AIConstantInt { Value = IdealDistance },
                    }),

                    // 5. Congela lo que acaba de caminar.
                    Fallback(new AINode_IceTrail
                    {
                        Hazard = iceHazard,
                        MaxTiles = MaxTrailTiles,
                        StunTurns = TrailStunTurns,
                        ReplacePreviousTrail = true,
                    }),

                    // 6. Marca: columna en ronda par, fila en impar. Alternan desde el primer turno;
                    //    la fase 2 sólo elige qué tan ancha sale la columna.
                    new AINode_Selector
                    {
                        Children = new List<AIDecisionNode>
                        {
                            new AINode_If
                            {
                                Conditions = new List<BasePreCondition> { EvenRound() },
                                Then = BuildColumnMark(),
                            },
                            BuildMark(ThreatShape.Row, RowDamage),
                        },
                    },

                    // 7. Fase 2 ("muestra la manga"): feedback + diálogo, una sola vez.
                    Fallback(new AINode_If
                    {
                        Conditions = new List<BasePreCondition> { Phase2Hp() },
                        Then = new AINode_Once
                        {
                            Child = new AINode_ApplyStatModifier
                            {
                                AttackDelta = 0,
                                SpeedDelta = 0,
                                PhaseIndex = 2,
                                EmitPhaseChangedEvent = true,
                            },
                        },
                    }),
                },
            };
        }

        /// <summary>
        /// La "tacha". Los corrimientos de fase 2 son campos de este nodo y no acciones sueltas bajo
        /// el gate de HP: un solo lugar donde vive ese estado.
        /// </summary>
        public static AINode_ShiftComboToNeighbor BuildShiftNode()
        {
            return new AINode_ShiftComboToNeighbor
            {
                Direction = AINode_ShiftComboToNeighbor.ShiftDirection.RandomNeighbor,
                ComboLogWindow = 5,
                ShiftsPerTurnPhase1 = ShiftsPerTurnPhase1,
                ShiftsPerTurnPhase2 = ShiftsPerTurnPhase2,
                Phase2HpThreshold = Phase2HpThreshold,
                RevertPreviousShifts = true,
                Phase2ShiftsArePermanent = true,
                ImmuneComboIds = new List<string> { WeaknessComboId },
            };
        }

        /// <summary>Stats + identidad + árbol. No toca <see cref="AssetDatabase"/>.</summary>
        public static void PopulateEnemyData(
            EnemyDataSO data,
            HazardDefinitionSO iceHazard,
            GameObject visualPrefab,
            Sprite portrait = null)
        {
            if (data == null) return;

            data.EntityId = EntityId;
            data.DisplayName = DisplayName;
            data.Description = "No pelea: te corrige el puntaje mientras tirás, y nunca a tu favor.";

            data.WeaknessComboId = WeaknessComboId;
            data.WeaknessMultiplierOverride = WeaknessMultiplier;

            data.BaseHP = BaseHp;
            data.BaseAttack = BaseAttack;
            data.BaseSpeed = 4;
            data.MaxEnergy = 3;
            data.BaseHealStrength = 0;
            data.BaseAttackRange = 1;

            data.MinGoldDrop = MinGoldDrop;
            data.MaxGoldDrop = MaxGoldDrop;

            // Con guarda de null: un asset con arte asignado no lo pierde porque el prefab o la
            // textura falten en el disco de quien corre el builder.
            if (visualPrefab != null) data.VisualPrefab = visualPrefab;
            if (portrait != null) data.Portrait = portrait;

            data.AIRoot = BuildAIRoot(iceHazard);
            data.AIDetachedNodes.Clear(); // el builder es fuente de verdad: nada suelto sobrevive
            data.Design = new EnemyDesignSheet
            {
                Archetype = EnemyArchetype.Ranged,
                Pattern = AttackPatternKind.TelegraphRowColumn,
                Timing = AttackTiming.Telegraph,
                Notes = "Mantiene distancia; lápiz a distancia, estela de hielo, telegraphs de fila/columna; desplaza combos vecinos.",
            };
        }

        /// <summary>
        /// Configura el hielo. Área dinámica: <see cref="HazardDefinitionSO.Shape"/> se ignora (las
        /// casillas las pasa el nodo), el daño es 0 —cobra en turnos— y la casilla pisada se derrite,
        /// que es lo que impide encadenar stuns.
        /// </summary>
        /// <param name="triggerVfx">Burst opcional; no es parte del contrato del hazard.</param>
        public static void ConfigureIceHazard(HazardDefinitionSO definition, GameObject triggerVfx = null)
        {
            if (definition == null) return;

            definition.Trigger = HazardTriggerMode.OnEnter;
            // Deja la estela caminando, o sea que pisa su propio hielo por definición: sin esto se
            // congelaría solo y gastaría las casillas antes de que el jugador llegue.
            definition.Affects = HazardAffects.PlayerOnly;
            definition.Damage = 0;
            definition.Kind = AttackKind.Environmental;
            definition.ConsumeOnTrigger = true;
            definition.DurationRounds = TrailDurationRounds;
            definition.OverlayTint = IceOverlayTint;
            definition.SourceId = IceHazardSourceId;
            definition.TriggerVfxLifetime = TrailBurstLifetime;

            if (triggerVfx != null) definition.TriggerVfxPrefab = triggerVfx;
        }

        /// <summary>Ficha del wrapper visual. Pura: no toca <see cref="AssetDatabase"/>.</summary>
        /// <remarks>
        /// <see cref="BossWrapperSpec.Retints"/> queda en <c>null</c> <b>a propósito</b> (ver los
        /// remarks de <see cref="IcePaints"/>): el repintado lo hace <see cref="RepaintArt"/>.
        /// </remarks>
        public static BossWrapperSpec BuildWrapperSpec()
        {
            return new BossWrapperSpec
            {
                ArtPrefabPath = ArtModelPath,
                OutputPrefabPath = VisualPrefabPath,
                EntityId = EntityId,
                BossName = BossName,
                ArtChildName = ArtChildName,
                // El jefe muestra vida en la BossBarView del HUD; una barra world-space
                // encima del pawn la duplicaría.
                AddHealthBar = false,
                // Capsule y no Box: el cofre es alto y angosto cuando abre la tapa.
                Collider = ColliderKind.Capsule,
                Retints = null,
            };
        }

        /// <summary>
        /// Nombre "limpio" de un material: sin namespace de Maya (<c>Enemy_:Wood1</c> → <c>Wood1</c>),
        /// sin prefijo <c>Mat_</c> y sin los sufijos de duplicado del exportador.
        /// </summary>
        /// <remarks>
        /// El importer puede o no conservar el namespace en el material embebido; canonizar los dos
        /// lados de la comparación hace que el mapeo funcione igual en ambos casos.
        /// </remarks>
        public static string CanonicalMaterialName(string materialName)
        {
            if (string.IsNullOrEmpty(materialName)) return string.Empty;

            var name = materialName.Trim();

            int colon = name.LastIndexOf(':');
            if (colon >= 0 && colon < name.Length - 1) name = name.Substring(colon + 1);

            // El importer de Unity sanitiza el ':' del namespace de Maya a '_', así que
            // "Enemy_:Wood1" llega como "Enemy__Wood1" y el strip de arriba no dispara.
            int doubleUnderscore = name.LastIndexOf("__", StringComparison.Ordinal);
            if (doubleUnderscore >= 0 && doubleUnderscore < name.Length - 2)
                name = name.Substring(doubleUnderscore + 2);

            if (name.StartsWith("Mat_")) name = name.Substring("Mat_".Length);

            int dot = name.LastIndexOf('.');
            if (dot > 0 && IsAllDigits(name.Substring(dot + 1))) name = name.Substring(0, dot);

            int space = name.LastIndexOf(' ');
            if (space > 0 && IsAllDigits(name.Substring(space + 1))) name = name.Substring(0, space);

            return name.Trim();
        }

        /// <summary>Entrada de <see cref="IcePaints"/> del material, o <c>null</c> si no está mapeado.</summary>
        public static string PaintKeyFor(string materialName)
        {
            var canonical = CanonicalMaterialName(materialName);
            if (canonical.Length == 0) return null;
            return ArtMaterialPaints.TryGetValue(canonical, out var paint) ? paint : null;
        }

        /// <summary>Path del material autorado para una entrada de <see cref="IcePaints"/>.</summary>
        public static string MaterialPathFor(string paintKey) =>
            $"{MaterialsFolder}/Mat_{BossName}_{paintKey}.mat";

        private static bool IsAllDigits(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (var c in value)
                if (c < '0' || c > '9') return false;
            return true;
        }

        // ======================================================================
        // Menú — la única capa que escribe a disco
        // ======================================================================

        [MenuItem(MenuPath)]
        public static void BuildAnotador()
        {
            var iceBurst = BuildIceBurstVfx();

            var ice = LoadOrCreate<HazardDefinitionSO>(IceHazardAssetPath);
            ConfigureIceHazard(ice, iceBurst);
            EditorUtility.SetDirty(ice);

            var visualPrefab = BuildVisualPrefab();
            var portrait = BossPortraitLibrary.Anotador();

            var boss = LoadOrCreate<EnemyDataSO>(EnemyAssetPath);
            PopulateEnemyData(boss, ice, visualPrefab, portrait);
            EditorUtility.SetDirty(boss);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AnotadorAssetBuilder] Listo: '{EnemyAssetPath}' + '{IceHazardAssetPath}' + " +
                      $"'{VisualPrefabPath}' + '{IceVfxPrefabPath}'.");
            Selection.activeObject = boss;
        }

        // ======================================================================
        // Wrapper visual
        // ======================================================================

        /// <summary>Construye (o reconstruye) el prefab de gameplay y devuelve el asset guardado.</summary>
        /// <remarks>
        /// Dos pasadas: <see cref="BossVisualWrapperBuilder.BuildWrapper"/> arma la estructura común y
        /// después se abre el prefab resultante para lo específico de este arte (Animator, stepping,
        /// la vuelta de 180° y el repintado). Idempotente: el path se reescribe preservando el GUID.
        /// </remarks>
        public static GameObject BuildVisualPrefab()
        {
            var wrapper = BossVisualWrapperBuilder.BuildWrapper(BuildWrapperSpec());
            if (wrapper == null)
            {
                Debug.LogError($"[AnotadorAssetBuilder] No se pudo construir el wrapper en " +
                               $"'{VisualPrefabPath}' — el jefe queda sin VisualPrefab.");
                return null;
            }

            var paints = EnsurePaletteMaterials();

            var contents = PrefabUtility.LoadPrefabContents(VisualPrefabPath);
            try
            {
                var art = contents.transform.Find(ArtChildName);
                if (art == null)
                {
                    Debug.LogError($"[AnotadorAssetBuilder] El wrapper no tiene un hijo " +
                                   $"'{ArtChildName}' — ¿cambió BossVisualWrapperBuilder? " +
                                   "El jefe queda sin animator ni paleta.");
                    return wrapper;
                }

                art.localEulerAngles = ArtLocalEuler;

                var animator = EnsureAnimator(art.gameObject);
                if (animator != null) EnsureSteppedAnimation(contents, animator);

                RepaintArt(art.gameObject, paints);

                PrefabUtility.SaveAsPrefabAsset(contents, VisualPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.SaveAssets();

            // El puente se re-pide ACÁ porque el de BuildWrapper corrió cuando el FBX todavía no
            // tenía Animator — lo agrega EnsureAnimator, unas líneas arriba.
            return BossVisualWrapperBuilder.EnsureAnimationFeedbackBridge(VisualPrefabPath)
                   ?? AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath);
        }

        /// <summary>Animator en el hijo de arte; se agrega el componente si el FBX no lo trajo.</summary>
        private static Animator EnsureAnimator(GameObject art)
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorControllerPath);
            if (controller == null)
            {
                Debug.LogWarning($"[AnotadorAssetBuilder] No se encontró el controller en " +
                                 $"'{AnimatorControllerPath}' — el mímico queda estático.");
            }

            var animator = art.GetComponent<Animator>();
            if (animator == null) animator = art.AddComponent<Animator>();

            if (controller != null) animator.runtimeAnimatorController = controller;

            // EntityPawn mueve por tween: con root motion el clip de Movement pelearía contra el
            // tween y el jefe terminaría a media casilla de la grilla.
            animator.applyRootMotion = false;

            return animator;
        }

        /// <summary>
        /// <see cref="SteppedAnimation"/> en el root, apuntando al Animator del arte.
        /// </summary>
        private static void EnsureSteppedAnimation(GameObject root, Animator animator)
        {
            var stepped = root.GetComponent<SteppedAnimation>();
            if (stepped == null) stepped = root.AddComponent<SteppedAnimation>();

            // Explícito y no por el OnValidate del componente: ese hace GetComponent<Animator>() en
            // SU objeto y acá el Animator vive en el hijo. Sin la referencia, su Update NREa.
            stepped.AnimCon = animator;
            stepped.FPS = SteppedAnimationFps;
        }

        /// <summary>Crea/actualiza los materiales de <see cref="IcePaints"/> y los devuelve por clave.</summary>
        /// <remarks>
        /// Reusar el asset (en vez de borrar y recrear) preserva su GUID: el wrapper los referencia
        /// por GUID y recrearlos dejaría los renderers en null tras cada rebuild.
        /// </remarks>
        private static Dictionary<string, Material> EnsurePaletteMaterials()
        {
            var result = new Dictionary<string, Material>();

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(PaletteShaderPath)
                         ?? Shader.Find(PaletteShaderName);
            if (shader == null)
            {
                Debug.LogError($"[AnotadorAssetBuilder] No se encontró el shader " +
                               $"'{PaletteShaderName}' — no se pueden autorar los materiales del jefe.");
                return result;
            }

            BossVisualWrapperBuilder.EnsureFolder(MaterialsFolder);

            foreach (var pair in IcePaints)
            {
                var path = MaterialPathFor(pair.Key);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                    AssetDatabase.CreateAsset(material, path);
                }
                else
                {
                    material.shader = shader;
                }

                ApplyPaint(material, pair.Value);
                result[pair.Key] = material;
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        /// <summary>Escribe un <see cref="MaterialRetint"/> de colores directos en el material.</summary>
        /// <remarks>
        /// <c>_UsePalette = 0</c> es obligatorio: el shader ramea
        /// <c>_UsePalette &gt; 0.5 ? _PaletteXColors[slot] : _XColor</c>, así que con el toggle
        /// prendido los colores quedan escritos en el asset y no se ven.
        /// </remarks>
        private static void ApplyPaint(Material material, MaterialRetint paint)
        {
            material.SetFloat("_UsePalette", 0f);
            if (paint.LightColor.HasValue) material.SetColor("_LightColor", paint.LightColor.Value);
            if (paint.MidColor.HasValue) material.SetColor("_MidColor", paint.MidColor.Value);
            if (paint.ShadowColor.HasValue) material.SetColor("_ShadowColor", paint.ShadowColor.Value);
            EditorUtility.SetDirty(material);
        }

        /// <summary>
        /// Swapea los materiales del FBX por los de <paramref name="paints"/>, según
        /// <see cref="ArtMaterialPaints"/>; los no mapeados quedan como vinieron.
        /// </summary>
        private static void RepaintArt(GameObject art, Dictionary<string, Material> paints)
        {
            if (paints == null || paints.Count == 0) return;

            var unmatched = new SortedSet<string>();
            int swapped = 0;

            foreach (var renderer in art.GetComponentsInChildren<Renderer>(includeInactive: true))
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;

                var shared = renderer.sharedMaterials;
                if (shared == null || shared.Length == 0) continue;

                bool changed = false;
                for (int i = 0; i < shared.Length; i++)
                {
                    var src = shared[i];
                    if (src == null) continue;

                    // Ya repintado por una corrida anterior: el material del slot es uno nuestro.
                    if (IsOwnMaterial(src, paints)) continue;

                    var key = PaintKeyFor(src.name);
                    if (key == null || !paints.TryGetValue(key, out var replacement))
                    {
                        unmatched.Add(src.name);
                        continue;
                    }

                    shared[i] = replacement;
                    changed = true;
                    swapped++;
                }

                if (changed) renderer.sharedMaterials = shared;
            }

            if (unmatched.Count > 0)
            {
                // Un material sin mapear sale con el color de fábrica y no reacciona al hit flash:
                // el síntoma no grita nada en el editor, así que se grita acá.
                Debug.LogWarning($"[AnotadorAssetBuilder] Materiales del arte sin mapear en " +
                                 $"ArtMaterialPaints: {string.Join(", ", unmatched)}. " +
                                 "Salen con el material del FBX (sin paleta y sin hit flash).");
            }

            if (swapped == 0)
            {
                Debug.Log("[AnotadorAssetBuilder] No hubo materiales que swapear — " +
                          "el wrapper ya estaba repintado.");
            }
        }

        private static bool IsOwnMaterial(Material candidate, Dictionary<string, Material> paints)
        {
            foreach (var mine in paints.Values)
                if (mine == candidate) return true;
            return false;
        }

        // ======================================================================
        // VFX de la estela
        // ======================================================================

        /// <summary>Crea/actualiza <see cref="IceVfxPrefabPath"/> y su material, y devuelve el prefab.</summary>
        /// <remarks>
        /// Clona la plantilla con <see cref="AssetDatabase.CopyAsset"/> y sólo le cambia el color. El
        /// <c>startColor</c> del sistema se retinta <b>además</b> del material: el prefab plantilla
        /// lleva el verde en los dos lados y tocar sólo el material deja partículas verdes.
        /// </remarks>
        public static GameObject BuildIceBurstVfx()
        {
            var material = EnsureIceVfxMaterial();

            if (AssetDatabase.LoadAssetAtPath<GameObject>(IceVfxPrefabPath) == null)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(VfxTemplatePrefabPath) == null)
                {
                    Debug.LogWarning($"[AnotadorAssetBuilder] No está la plantilla de VFX en " +
                                     $"'{VfxTemplatePrefabPath}' — la estela queda sin burst.");
                    return null;
                }
                if (!AssetDatabase.CopyAsset(VfxTemplatePrefabPath, IceVfxPrefabPath))
                {
                    Debug.LogError($"[AnotadorAssetBuilder] Falló el clon de " +
                                   $"'{VfxTemplatePrefabPath}' a '{IceVfxPrefabPath}'.");
                    return null;
                }
                AssetDatabase.ImportAsset(IceVfxPrefabPath);
            }

            var contents = PrefabUtility.LoadPrefabContents(IceVfxPrefabPath);
            try
            {
                foreach (var particles in contents.GetComponentsInChildren<ParticleSystem>(includeInactive: true))
                {
                    var main = particles.main;
                    main.startColor = new ParticleSystem.MinMaxGradient(IceVfxColor);
                }

                if (material != null)
                {
                    foreach (var renderer in contents.GetComponentsInChildren<ParticleSystemRenderer>(includeInactive: true))
                    {
                        var shared = renderer.sharedMaterials;
                        if (shared == null) continue;

                        for (int i = 0; i < shared.Length; i++)
                            if (shared[i] != null) shared[i] = material;

                        renderer.sharedMaterials = shared;
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(contents, IceVfxPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(IceVfxPrefabPath);
        }

        /// <summary>Clon celeste de <see cref="VfxTemplateMaterialPath"/>.</summary>
        private static Material EnsureIceVfxMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(IceVfxMaterialPath);
            if (material == null)
            {
                if (AssetDatabase.LoadAssetAtPath<Material>(VfxTemplateMaterialPath) == null)
                {
                    Debug.LogWarning($"[AnotadorAssetBuilder] No está el material plantilla en " +
                                     $"'{VfxTemplateMaterialPath}' — el burst queda con el material verde.");
                    return null;
                }
                if (!AssetDatabase.CopyAsset(VfxTemplateMaterialPath, IceVfxMaterialPath))
                {
                    Debug.LogError($"[AnotadorAssetBuilder] Falló el clon de " +
                                   $"'{VfxTemplateMaterialPath}' a '{IceVfxMaterialPath}'.");
                    return null;
                }
                AssetDatabase.ImportAsset(IceVfxMaterialPath);
                material = AssetDatabase.LoadAssetAtPath<Material>(IceVfxMaterialPath);
                if (material == null) return null;
            }

            // Los dos nombres: URP lee _BaseColor, el built-in _Color.
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", IceVfxColor);
            if (material.HasProperty("_Color")) material.SetColor("_Color", IceVfxColor);
            EditorUtility.SetDirty(material);

            return material;
        }

        /// <summary>Carga el asset o lo crea vacío; reusar el existente preserva su GUID.</summary>
        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        // ======================================================================
        // Helpers de árbol
        // ======================================================================

        /// <summary><c>Selector[node, Wait]</c> — "intentá esto; si falla, el turno sigue".</summary>
        private static AINode_Selector Fallback(AIDecisionNode node)
        {
            return new AINode_Selector
            {
                Children = new List<AIDecisionNode> { node, new AINode_Wait() },
            };
        }

        private static AINode_TelegraphMark BuildMark(ThreatShape shape, int damage, int size = MarkSize)
        {
            return new AINode_TelegraphMark
            {
                Shape = shape,
                Size = size,
                Damage = damage,
                Kind = AttackKind.BasicAttack,
            };
        }

        /// <summary>La columna: <see cref="Phase2ColumnSize"/> de ancho en fase 2, una antes.</summary>
        /// <remarks>
        /// El gate de HP va <b>adentro</b> del de paridad y no envolviéndolo: colgar la columna entera
        /// de un <see cref="PcOwnerHpBelow"/> la volvería un ataque de fase 2 y el eje dejaría de
        /// alternar hasta el umbral. La fase no decide <i>si</i> se marca la columna, sólo <i>cuál</i>
        /// de las dos.
        /// </remarks>
        private static AINode_Selector BuildColumnMark()
        {
            return new AINode_Selector
            {
                Children = new List<AIDecisionNode>
                {
                    new AINode_If
                    {
                        Conditions = new List<BasePreCondition> { Phase2Hp() },
                        Then = BuildMark(ThreatShape.Column, ColumnDamage, Phase2ColumnSize),
                    },
                    BuildMark(ThreatShape.Column, ColumnDamage),
                },
            };
        }

        /// <summary>
        /// El umbral en un solo lugar: lo comparten el ancho de la columna y el gate del feedback, y
        /// desfasados el jefe ensancharía el eje sin anunciar la fase.
        /// </summary>
        private static PcOwnerHpBelow Phase2Hp() => new PcOwnerHpBelow { Percent = Phase2HpThreshold };

        private static PcRoundNumber EvenRound()
        {
            return new PcRoundNumber
            {
                Mode = PcRoundNumber.CompareMode.Multiple,
                Value = ParityDivisor,
            };
        }

        /// <summary>"Ronda impar": <see cref="PcRoundNumber"/> no tiene negación propia.</summary>
        private static PCComposite OddRound()
        {
            return new PCComposite
            {
                Mode = CompositeMode.Not,
                Children = new List<BasePreCondition> { EvenRound() },
            };
        }
    }
}
