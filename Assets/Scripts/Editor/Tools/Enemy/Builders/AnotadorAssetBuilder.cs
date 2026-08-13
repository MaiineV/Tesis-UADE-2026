using System.Collections.Generic;
using System.IO;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Entities;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Builders
{
    /// <summary>
    /// Arma El Anotador (jefe de piso 2, el del hielo): su <see cref="EnemyDataSO"/> con el árbol AI
    /// inline, el <see cref="HazardDefinitionSO"/> de la estela helada, y el vestuario — wrapper
    /// visual sobre el arte del mímico, retrato y el burst de hielo de la estela.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Dos capas a propósito.</b> <see cref="BuildAIRoot"/> / <see cref="PopulateEnemyData"/> /
    /// <see cref="ConfigureIceHazard"/> / <see cref="BuildWrapperSpec"/> son estáticas y puras: no
    /// tocan <see cref="AssetDatabase"/>, así que los tests EditMode validan el wiring, los números y
    /// las rutas de arte en memoria sin depender de que el <c>.asset</c> exista ni de un import. El
    /// <see cref="MenuItem"/> es la única parte que escribe a disco, y es idempotente: si los assets
    /// ya están, los repopula en vez de duplicarlos (los GUID de los <c>.asset</c> se preservan, así
    /// que las referencias desde catálogos/escenas no se rompen).
    /// </para>
    /// <para>
    /// <b>Vestuario.</b> El jefe usaba <c>SecurityGuardBoss.prefab</c> como placeholder: estático y sin
    /// Animator. Ahora <see cref="BuildVisualPrefab"/> lo viste con el mímico
    /// (<see cref="ArtModelPath"/>) — un cofre que se abre y muerde, o sea un libro de contabilidad
    /// viviente — congelado con la paleta de <see cref="IcePaints"/>. Los tres colores del piso quedan
    /// separados: fila naranja (default del overlay), estela celeste
    /// (<see cref="IceOverlayTint"/>) y lápiz grafito (<see cref="PencilOverlayTint"/>).
    /// </para>
    /// <para>
    /// <b>Ficha de diseño.</b> HP 190 · Attack 30 · fila Row 1 = 30 · lápiz SquareAroundSelf 1 = 12
    /// en rondas impares · KeepDistance ideal 4 · estela de 1-3 casillas con stun 1 · fase 2 al 35%
    /// (2 corrimientos por turno, permanentes, y columna 32 alternada con la fila). Techo de daño de
    /// piso 2 = 35 por golpe: el 32 de la columna es el máximo que sale de acá.
    /// </para>
    /// <para>
    /// <b>El lápiz corre por canal secundario.</b> <see cref="IThreatenedAreaService"/> guarda
    /// <b>un</b> área pendiente por source guid: si fila y lápiz marcaran los dos con
    /// <c>context.SelfGuid</c>, en rondas impares la segunda marca pisaría a la primera y el camino
    /// derecho pagaría 12 en vez de 42 (30 + 12). El lápiz marca y cobra vía
    /// <see cref="AINode_AuxTelegraph"/> (canal <see cref="PencilChannelId"/>) — la misma solución
    /// del cubilete de La Generala — así ambas marcas conviven y sus daños stackean si el jugador
    /// queda dentro de las dos.
    /// </para>
    /// </remarks>
    public static class AnotadorAssetBuilder
    {
        // ======================================================================
        // Identidad y rutas
        // ======================================================================

        public const string EnemyAssetPath = "Assets/Rollgeon/Enemies/ED_Boss_Anotador.asset";
        public const string IceHazardAssetPath = "Assets/Rollgeon/Combat/Hazards/IceTrailHazardDefinition.asset";

        /// <summary>
        /// Wrapper de gameplay del jefe, armado por <see cref="BossVisualWrapperBuilder"/>. Reemplazó
        /// al placeholder <c>SecurityGuardBoss.prefab</c>, que era un pawn estático sin Animator: este
        /// jefe zigzaguea dejando estela, así que quedarse quieto era mentirle al jugador sobre de
        /// dónde salió el hielo.
        /// </summary>
        public const string VisualPrefabPath = "Assets/Prefabs/Enemies/Bosses/PF_Boss_Anotador.prefab";

        /// <summary>
        /// Arte: el mímico. Se anida el <b>modelo</b> y no <c>ChestMimic_Prefab.prefab</c> a propósito —
        /// ese prefab ya trae <c>EntityPawn</c> + <c>PawnRegistryBinding</c> + su propia barra de vida,
        /// y el wrapper agrega los suyos en el root: quedarían duplicados y el pawn de adentro pelearía
        /// con el de afuera por el registro.
        /// </summary>
        public const string ArtModelPath = "Assets/Art/3D/Models/Enemies/ChestMimic_Model.fbx";

        /// <summary>
        /// Clips Attack/Awaken/IdleAwaken/IdleMysterious/Movement + el bool <c>Awaken</c>: un libro de
        /// contabilidad que se despierta. El FBX no trae controller, así que se asigna después de
        /// construir el wrapper.
        /// </summary>
        public const string AnimatorControllerPath =
            "Assets/Art/3D/Animations/Enemies/ChestMimic/AnimCon_ChestMimic.controller";

        /// <summary>Parámetro del controller que separa "cofre inocente" de "planilla viva".</summary>
        public const string AwakenParameter = "Awaken";

        /// <summary>Set de 6 dados = la planilla de generala que este jefe lleva.</summary>
        public const string PortraitTexturePath = "Assets/Art/2D/Symbols/Sprites/Casino_0044.png";

        public const string BossName = "Anotador";
        public const string ArtChildName = "Art";

        /// <summary>
        /// FPS del stepping de animación. 8 = el mismo que <c>ChestMimic_Prefab</c> y el resto del
        /// roster animado: sin <see cref="SteppedAnimation"/> el mímico se movería suave y se leería
        /// de otro juego que los demás enemigos.
        /// </summary>
        public const int SteppedAnimationFps = 8;

        /// <summary>Altura del canvas de vida, copiada del canvas de <c>ChestMimic_Prefab</c>.</summary>
        public static readonly Vector3 HealthBarOffset = new Vector3(0f, 2.5f, 0f);

        /// <summary>
        /// El root del FBX mira -Z; <c>ChestMimic_Prefab</c> lo compensa con 180° en Y y
        /// <see cref="BossVisualWrapperBuilder"/> fuerza identidad en el hijo de arte. Sin esto el
        /// mímico entra a la sala de espaldas.
        /// </summary>
        public static readonly Vector3 ArtLocalEuler = new Vector3(0f, 180f, 0f);

        public const string EntityId = "boss.scorekeeper";
        public const string DisplayName = "El Anotador";

        /// <summary>Debilidad: la única mano que no depende de la tabla.</summary>
        public const string WeaknessComboId = "combo.generala";
        public const float WeaknessMultiplier = 1.5f;

        /// <summary>
        /// SourceId fijo (no <c>Guid.NewGuid()</c>) para que reconstruir el asset no le cambie la
        /// identidad al hazard. Las instancias de área dinámica no lo usan, pero un source id
        /// inestable rompería cualquier estado keyed por source si algún día se activa por ciclo.
        /// </summary>
        public const string IceHazardSourceId = "b7d4f2a6-3c81-4e59-9a02-5f6d8c1e7b43";

        // ======================================================================
        // Números de la ficha
        // ======================================================================

        public const int BaseHp = 190;
        public const int BaseAttack = 30;
        public const int MinGoldDrop = 30;
        public const int MaxGoldDrop = 60;

        public const int RowDamage = 30;
        public const int ColumnDamage = 32;
        public const int PencilDamage = 12;
        public const int MarkSize = 1;

        /// <summary>Canal del lápiz — su marca no puede pisar (ni ser pisada por) la de la fila.</summary>
        public const string PencilChannelId = "anotador.lapiz";

        /// <summary>Distancia que el repliegue intenta mantener. Solo se mueve si lo tienen a 3 o menos.</summary>
        public const int IdealDistance = 4;

        /// <summary>Pasos del repliegue ⇒ tope natural de casillas de la estela (1-3).</summary>
        public const int RetreatSteps = 3;

        public const int MaxTrailTiles = 3;
        public const int TrailStunTurns = 1;

        /// <summary>
        /// Rondas de vida de la estela. <b>2, no 1</b>: la duración se descuenta en el wrap de ronda
        /// (<c>OnTurnQueueBuilt</c>) y el jugador tiene forzado el primer turno de cada ronda
        /// (CNF-006). Con 1, la estela que el boss deja en la ronda N muere en el arranque de la N+1,
        /// <i>antes</i> de que el jugador vuelva a moverse: nunca podría pisarla. Con 2 vive
        /// exactamente un turno del jugador, que es lo que la ficha llama "dura 1 turno".
        /// </summary>
        public const int TrailDurationRounds = 2;

        public const float Phase2HpThreshold = 0.35f;
        public const int ShiftsPerTurnPhase1 = 1;
        public const int ShiftsPerTurnPhase2 = 2;

        /// <summary>Paridad de ronda del lápiz y de la columna: impares lápiz/fila, pares columna.</summary>
        public const int ParityDivisor = 2;

        /// <summary>Celeste: la estela no puede leerse como el naranja del telegraph.</summary>
        public static readonly Color IceOverlayTint = new Color(0.35f, 0.8f, 1f, 0.55f);

        /// <summary>
        /// Grafito del lápiz. Tres marcas conviven en el piso de este jefe y cada una cobra distinto:
        /// la fila va en el naranja default de <c>ThreatTelegraphOverlay</c> (30), la estela en el
        /// celeste de <see cref="IceOverlayTint"/> (stun) y el anillo del lápiz acá (12). El default
        /// del nodo aux es un violeta que no dice nada; el grafito lo ata al lápiz que lo dibuja y no
        /// se confunde con ninguno de los otros dos.
        /// </summary>
        /// <remarks>
        /// Azulado y no gris neutro para que no se pierda contra el piso, y con el alpha en la banda
        /// del resto — el pulso del overlay lo sobreescribe igual, pero un 0 dejaría quads invisibles.
        /// </remarks>
        public static readonly Color PencilOverlayTint = new Color(0.42f, 0.45f, 0.58f, 0.6f);

        // ======================================================================
        // Paleta del mímico congelado
        // ======================================================================

        public const string PaletteShaderPath = "Assets/Shaders/PaletteCelLit.shader";
        public const string PaletteShaderName = "Rollgeon/PaletteCelLit";

        /// <summary>Carpeta de los materiales propios del jefe.</summary>
        public static string MaterialsFolder =>
            $"{BossVisualWrapperBuilder.DefaultMaterialsRoot}/{BossName}";

        /// <summary>
        /// Los materiales que este builder autorea para vestir al mímico de hielo, por nombre corto.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Por qué no se usa <c>BossWrapperSpec.Retints</c>.</b> El retinte del wrapper clona el
        /// material del arte y le escribe los colores; con este FBX no se puede:
        /// </para>
        /// <list type="number">
        /// <item>Los materiales vienen <b>embebidos en el FBX</b> con el namespace de Maya en el
        /// nombre (<c>Enemy_:Wood1</c>). El clon se guarda como
        /// <c>Mat_Anotador_&lt;nombre&gt;.mat</c>, y <c>:</c> es un carácter ilegal en un path de
        /// Windows: el <c>CreateAsset</c> fallaría.</item>
        /// <item>Están en URP Lit, no en <see cref="PaletteShaderName"/>. Los colores del retinte
        /// (<c>_LightColor</c>/<c>_MidColor</c>/<c>_ShadowColor</c>) no existen en ese shader, así que
        /// el retinte sería un no-op — y peor: <c>_HitFlashAmount</c> tampoco existe, o sea que el
        /// jefe no parpadearía al recibir daño aunque el <c>PawnMaterialFeedback</c> esté cableado.</item>
        /// </list>
        /// <para>
        /// Así que el builder autorea los materiales él mismo (mismo shader que el resto del roster,
        /// misma carpeta y mismo naming que usaría el retinte) y los swapea en el wrapper. Sigue sin
        /// tocar los materiales del FBX, que los comparte el enemigo Chest Mimic.
        /// </para>
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

                // El ojo va en el celeste EXACTO del overlay de la estela: el jugador tiene que poder
                // atar el hielo del piso al bicho que lo dejó.
                ["Eye"] = MaterialRetint.FromColors(
                    new Color(0.75f, 0.95f, 1.00f),
                    new Color(0.35f, 0.80f, 1.00f),
                    new Color(0.10f, 0.35f, 0.55f)),
            };

        /// <summary>
        /// Material del FBX (nombre canónico, ver <see cref="CanonicalMaterialName"/>) → entrada de
        /// <see cref="IcePaints"/>. <c>Material</c> es el slot sin nombre que dejó el export y va con
        /// el cuerpo: sin mapearlo quedaría un parche gris de URP en medio del hielo.
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

        /// <summary>
        /// Mismo celeste que <see cref="IceOverlayTint"/> sin alpha: el burst es la misma amenaza que
        /// el quad, cobrando.
        /// </summary>
        public static readonly Color IceVfxColor = new Color(0.35f, 0.8f, 1f, 1f);

        // ======================================================================
        // Capa pura — testeable sin assets
        // ======================================================================

        /// <summary>
        /// Árbol del turno. Sequence raíz de 8 hijos, en el orden de la ficha:
        /// <c>detona → cobra el lápiz → tacha → se acomoda → estela → fila/columna → lápiz → fase 2</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Todo hijo que pueda devolver <c>Failed</c> va dentro de un <c>Selector[…, Wait]</c>: el
        /// Sequence aborta el turno al primer Failed y este boss tiene UN ataque (la marca de fila).
        /// El caso más peligroso es <see cref="AINode_KeepDistance"/>, que devuelve <c>Failed</c> en
        /// el caso benigno "ya estoy a distancia ideal" — la mayoría de los turnos de esta pelea,
        /// porque solo se mueve si lo tienen a 3 casillas o menos. Es el mismo Failed que dejó quieto
        /// al Sunken Grand.
        /// </para>
        /// <para>
        /// El <c>Selector</c> del hijo 5 es lo que garantiza <b>una sola</b> marca grande por turno:
        /// fila (30) + columna (32) el mismo turno son 62 sobre 100 de vida y rompen el techo del
        /// piso. Si la columna entra, el Selector corta y la fila no se marca.
        /// </para>
        /// <para>
        /// El lápiz va <b>después</b> del repliegue para que el anillo quede alrededor de la casilla
        /// final del boss; marcado antes, telegrafía dónde ya no está. No lleva gate de rango: el
        /// anillo ES la adyacencia.
        /// </para>
        /// </remarks>
        public static AINode_Sequence BuildAIRoot(HazardDefinitionSO iceHazard)
        {
            return new AINode_Sequence
            {
                Children = new List<AIDecisionNode>
                {
                    // 1. Detona la marca del turno pasado.
                    new AINode_ExecuteTelegraph(),

                    // 1b. Cobra el lápiz pendiente. Fuera de todo gate: el aviso marcado en la
                    // ronda impar N se paga en la N+1 aunque esa ronda no marque lápiz nuevo.
                    new AINode_AuxTelegraph
                    {
                        Step = AINode_AuxTelegraph.TelegraphStep.Execute,
                        ChannelId = PencilChannelId,
                    },

                    // 2. Tacha: corre el combo más jugado al vecino de la hoja. Envuelto igual que
                    // el resto: devuelve Failed si IContractModifierService no está registrado, y
                    // ese Failed dejaría al boss sin marcar la fila por un bootstrap incompleto.
                    Fallback(BuildShiftNode()),

                    // 3. Se acomoda: si lo tienen a 3 o menos, se repliega a 4.
                    Fallback(new AINode_KeepDistance
                    {
                        MaxSteps = new AIConstantInt { Value = RetreatSteps },
                        IdealDistance = new AIConstantInt { Value = IdealDistance },
                    }),

                    // 4. Congela lo que acaba de caminar.
                    Fallback(new AINode_IceTrail
                    {
                        Hazard = iceHazard,
                        MaxTiles = MaxTrailTiles,
                        StunTurns = TrailStunTurns,
                        ReplacePreviousTrail = true,
                    }),

                    // 5. Marca: columna solo en fase 2 y ronda par; si no, fila.
                    new AINode_Selector
                    {
                        Children = new List<AIDecisionNode>
                        {
                            new AINode_If
                            {
                                Conditions = new List<BasePreCondition>
                                {
                                    new PcOwnerHpBelow { Percent = Phase2HpThreshold },
                                    EvenRound(),
                                },
                                Then = BuildMark(ThreatShape.Column, ColumnDamage),
                            },
                            BuildMark(ThreatShape.Row, RowDamage),
                        },
                    },

                    // 6. El lápiz, solo en rondas impares — por su canal, para no pisar la fila.
                    Fallback(new AINode_If
                    {
                        Conditions = new List<BasePreCondition> { OddRound() },
                        Then = new AINode_AuxTelegraph
                        {
                            Step = AINode_AuxTelegraph.TelegraphStep.Mark,
                            ChannelId = PencilChannelId,
                            Shape = ThreatShape.SquareAroundSelf,
                            Size = MarkSize,
                            Damage = PencilDamage,
                            Kind = AttackKind.BasicAttack,
                            // Sin esto el anillo sale en el violeta default del nodo, que no significa
                            // nada en este juego. Ver PencilOverlayTint.
                            OverlayTint = PencilOverlayTint,
                        },
                    }),

                    // 7. Fase 2 ("muestra la manga"): feedback + diálogo, una sola vez.
                    Fallback(new AINode_If
                    {
                        Conditions = new List<BasePreCondition>
                        {
                            new PcOwnerHpBelow { Percent = Phase2HpThreshold },
                        },
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
        /// La "tacha". Los corrimientos de fase 2 (cantidad + permanencia) son campos de este mismo
        /// nodo en vez de acciones sueltas bajo el gate de HP: un único nodo es un único lugar donde
        /// vive ese estado, igual que <see cref="AINode_PromulgateRule"/> resuelve su intervalo de
        /// fase leyendo su propia vida.
        /// </summary>
        public static AINode_ShiftComboToNeighbor BuildShiftNode()
        {
            return new AINode_ShiftComboToNeighbor
            {
                // La ficha deja la dirección como pregunta abierta ("¿arriba, abajo, o al azar?").
                // RandomNeighbor es lo único consistente con sus dos mitades: "nunca a tu favor" y
                // "hay corrimientos que te mejoran — es el único jefe que se puede aprovechar".
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
            data.Description = "El que lleva la planilla. No juega contra vos: te corrige el puntaje " +
                               "mientras tirás, y nunca a tu favor.";

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

            // Los dos con guarda de null: un asset que ya tiene arte asignado no debería perderlo
            // porque el prefab/textura falte en el disco de quien corre el builder.
            if (visualPrefab != null) data.VisualPrefab = visualPrefab;
            if (portrait != null) data.Portrait = portrait;

            data.AIRoot = BuildAIRoot(iceHazard);
        }

        /// <summary>
        /// Configura la definición del hielo. Área dinámica: <see cref="HazardDefinitionSO.Shape"/>
        /// se ignora (las casillas las pasa el nodo), el daño es 0 —la estela cobra en turnos, no en
        /// HP— y la casilla pisada se derrite, que es lo que impide encadenar stuns.
        /// </summary>
        /// <param name="triggerVfx">
        /// Burst opcional de <see cref="HazardDefinitionSO.TriggerVfxPrefab"/>. Con <c>null</c> la
        /// estela queda como antes (solo el quad celeste): el visual no es parte del contrato del
        /// hazard, así que un builder corrido sin el prefab construido no rompe la pelea.
        /// </param>
        public static void ConfigureIceHazard(HazardDefinitionSO definition, GameObject triggerVfx = null)
        {
            if (definition == null) return;

            definition.Trigger = HazardTriggerMode.OnEnter;
            definition.Damage = 0;
            definition.Kind = AttackKind.Environmental;
            definition.ConsumeOnTrigger = true;
            definition.DurationRounds = TrailDurationRounds;
            definition.OverlayTint = IceOverlayTint;
            definition.SourceId = IceHazardSourceId;

            if (triggerVfx != null) definition.TriggerVfxPrefab = triggerVfx;
        }

        /// <summary>
        /// Ficha del wrapper visual. Pura: no toca <see cref="AssetDatabase"/>, así que los tests
        /// pueden afirmar rutas y medidas sin que el prefab exista.
        /// </summary>
        /// <remarks>
        /// <see cref="BossWrapperSpec.Retints"/> queda en <c>null</c> <b>a propósito</b> — ver los
        /// remarks de <see cref="IcePaints"/>: los materiales de este FBX no se pueden clonar por el
        /// camino del retinte (nombre con <c>:</c>, shader sin las properties de paleta), así que el
        /// repintado lo hace <see cref="RepaintArt"/> con materiales que este builder autorea.
        /// </remarks>
        public static BossWrapperSpec BuildWrapperSpec()
        {
            return new BossWrapperSpec
            {
                ArtPrefabPath = ArtModelPath,
                OutputPrefabPath = VisualPrefabPath,
                BossName = BossName,
                ArtChildName = ArtChildName,
                AddHealthBar = true,
                HealthBarOffset = HealthBarOffset,
                // Capsule y no Box: el cofre es alto y angosto cuando abre la tapa, y el pick del
                // cursor resuelve por el collider del root.
                Collider = ColliderKind.Capsule,
                Retints = null,
            };
        }

        /// <summary>
        /// Nombre "limpio" de un material del arte: sin el namespace de Maya (<c>Enemy_:Wood1</c> →
        /// <c>Wood1</c>), sin el prefijo <c>Mat_</c> y sin los sufijos de duplicado que mete el
        /// exportador (<c>.002</c>, <c> 1</c>).
        /// </summary>
        /// <remarks>
        /// Existe porque no hay forma de saber sin abrir Unity si el importer conserva el namespace en
        /// el nombre del material embebido. Canonizar los dos lados de la comparación hace que el mapeo
        /// funcione igual en ambos casos, en vez de depender de una corazonada.
        /// </remarks>
        public static string CanonicalMaterialName(string materialName)
        {
            if (string.IsNullOrEmpty(materialName)) return string.Empty;

            var name = materialName.Trim();

            int colon = name.LastIndexOf(':');
            if (colon >= 0 && colon < name.Length - 1) name = name.Substring(colon + 1);

            if (name.StartsWith("Mat_")) name = name.Substring("Mat_".Length);

            int dot = name.LastIndexOf('.');
            if (dot > 0 && IsAllDigits(name.Substring(dot + 1))) name = name.Substring(0, dot);

            int space = name.LastIndexOf(' ');
            if (space > 0 && IsAllDigits(name.Substring(space + 1))) name = name.Substring(0, space);

            return name.Trim();
        }

        /// <summary>
        /// Entrada de <see cref="IcePaints"/> que le toca a un material del arte, o <c>null</c> si ese
        /// material no está mapeado (se deja como vino).
        /// </summary>
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

        [MenuItem("Tools/Rollgeon/Bosses/Build Anotador")]
        public static void BuildAnotador()
        {
            var iceBurst = BuildIceBurstVfx();

            var ice = LoadOrCreate<HazardDefinitionSO>(IceHazardAssetPath);
            ConfigureIceHazard(ice, iceBurst);
            EditorUtility.SetDirty(ice);

            var visualPrefab = BuildVisualPrefab();
            var portrait = SpriteImportUtility.EnsureSpriteImport(PortraitTexturePath);

            var boss = LoadOrCreate<EnemyDataSO>(EnemyAssetPath);
            PopulateEnemyData(boss, ice, visualPrefab, portrait);
            EditorUtility.SetDirty(boss);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AnotadorAssetBuilder] Listo: '{EnemyAssetPath}' + '{IceHazardAssetPath}' + " +
                      $"'{VisualPrefabPath}' + '{IceVfxPrefabPath}'. " +
                      "Falta a mano: sumarlo al EnemyCatalog / BossFloorManager del piso 2.");
            Selection.activeObject = boss;
        }

        // ======================================================================
        // Wrapper visual
        // ======================================================================

        /// <summary>
        /// Construye (o reconstruye) el prefab de gameplay del jefe y devuelve el asset guardado.
        /// </summary>
        /// <remarks>
        /// Dos pasadas: <see cref="BossVisualWrapperBuilder.BuildWrapper"/> arma la estructura
        /// (<c>EntityPawn</c>, registro, hit impulse, feedback, collider, barra) y después se abre el
        /// prefab resultante para lo que es específico de este arte: el Animator que el FBX no trae, el
        /// stepping a 8 FPS, la vuelta de 180° y el repintado a la paleta.
        /// <para>
        /// Idempotente: el path del wrapper se reescribe preservando el GUID (lo garantiza
        /// <c>SaveAsPrefabAsset</c>), así que la referencia del <c>EnemyDataSO</c> sobrevive al rebuild.
        /// </para>
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
            return AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath);
        }

        /// <summary>
        /// Animator en el hijo de arte con el controller del mímico. Se agrega el componente si el FBX
        /// no lo trajo (el import de un modelo sin animación no lo genera).
        /// </summary>
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

            // El pawn lo mueve EntityPawn por tween: con root motion el clip de Movement pelearía
            // contra el tween y el jefe terminaría a media casilla de la grilla.
            animator.applyRootMotion = false;

            return animator;
        }

        /// <summary>
        /// <see cref="SteppedAnimation"/> en el root, apuntando al Animator del arte — el look stepped
        /// a <see cref="SteppedAnimationFps"/> FPS que usa todo el roster animado.
        /// </summary>
        private static void EnsureSteppedAnimation(GameObject root, Animator animator)
        {
            var stepped = root.GetComponent<SteppedAnimation>();
            if (stepped == null) stepped = root.AddComponent<SteppedAnimation>();

            // Se cablea explícito y no por el OnValidate del componente: ese hace
            // GetComponent<Animator>() en SU objeto, y acá el Animator vive en el hijo de arte.
            // Sin la referencia, su Update NREa todos los frames.
            stepped.AnimCon = animator;
            stepped.FPS = SteppedAnimationFps;
        }

        /// <summary>
        /// Crea/actualiza los materiales de <see cref="IcePaints"/> y los devuelve por clave.
        /// </summary>
        /// <remarks>
        /// Reusar el asset existente (en vez de borrar y recrear) preserva su GUID: el wrapper los
        /// referencia por GUID y recrearlos dejaría los renderers en null tras cada rebuild.
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

        /// <summary>
        /// Escribe un <see cref="MaterialRetint"/> de colores directos en un material de paleta.
        /// </summary>
        /// <remarks>
        /// <c>_UsePalette = 0</c> es obligatorio: el shader ramea
        /// <c>_UsePalette &gt; 0.5 ? _PaletteXColors[slot] : _XColor</c>, así que con el toggle prendido
        /// los colores quedan escritos en el asset y no se ven. Se usan colores directos y no un slot de
        /// <c>PA_MainPalette</c> porque los labels de ese asset están desalineados respecto de la tabla
        /// de <see cref="PaletteSlots"/> (ver sus remarks) y no hay un slot de hielo.
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
        /// Swapea los materiales embebidos del FBX por los de <paramref name="paints"/>, según
        /// <see cref="ArtMaterialPaints"/>. Los que no estén mapeados quedan como vinieron.
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
                // Un material del arte sin mapear sale con el color de fábrica y no reacciona al hit
                // flash: el síntoma no grita nada en el editor, así que se grita acá.
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

        /// <summary>
        /// Crea/actualiza <see cref="IceVfxPrefabPath"/> y su material, y devuelve el prefab.
        /// </summary>
        /// <remarks>
        /// <b>Se clona con <see cref="AssetDatabase.CopyAsset"/> en vez de armar el ParticleSystem a
        /// mano</b>: el glow de curación ya tiene autoradas las curvas de emisión, tamaño y vida, y
        /// reescribirlas por código sería reinventar (peor) un asset que ya existe y ya se ve bien. Del
        /// clon solo se cambia el color, que es lo único que separa "te curaste" de "te congelaste".
        /// <para>
        /// El <c>startColor</c> del sistema se retinta además del material: el prefab plantilla lleva el
        /// verde en los dos lados, y tocar solo el material dejaría partículas verdes.
        /// </para>
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

            // Los dos nombres: el shader de partículas de URP lee _BaseColor, y _Color queda para el
            // built-in y para cualquier variante que el proyecto migre después.
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", IceVfxColor);
            if (material.HasProperty("_Color")) material.SetColor("_Color", IceVfxColor);
            EditorUtility.SetDirty(material);

            return material;
        }

        /// <summary>
        /// Carga el asset o lo crea vacío. Reusar el existente (en vez de borrar y recrear) preserva
        /// su GUID, así que las referencias desde catálogos, prefabs y escenas sobreviven al rebuild.
        /// </summary>
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

        /// <summary>
        /// <c>Selector[node, Wait]</c> — el idiom de "intentá esto; si falla, el turno sigue".
        /// </summary>
        private static AINode_Selector Fallback(AIDecisionNode node)
        {
            return new AINode_Selector
            {
                Children = new List<AIDecisionNode> { node, new AINode_Wait() },
            };
        }

        private static AINode_TelegraphMark BuildMark(ThreatShape shape, int damage)
        {
            return new AINode_TelegraphMark
            {
                Shape = shape,
                Size = MarkSize,
                Damage = damage,
                Kind = AttackKind.BasicAttack,
            };
        }

        private static PcRoundNumber EvenRound()
        {
            return new PcRoundNumber
            {
                Mode = PcRoundNumber.CompareMode.Multiple,
                Value = ParityDivisor,
            };
        }

        /// <summary>
        /// "Ronda impar" = NOT(múltiplo de 2). <see cref="PcRoundNumber"/> no tiene negación propia,
        /// así que se envuelve en un <see cref="PCComposite"/> en modo <c>Not</c> — el concrete que
        /// existe justo para esto.
        /// </summary>
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
