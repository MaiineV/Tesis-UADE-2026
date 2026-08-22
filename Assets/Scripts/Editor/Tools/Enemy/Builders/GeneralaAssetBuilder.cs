using System;
using System.Collections.Generic;
using System.IO;
using Rollgeon.Combat.AI.Bosses.Generala;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Combat.AI.Targeting;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Rooms;
using Rollgeon.Combat.Threat;
using Rollgeon.Combos;
using Rollgeon.Dice.Throw;
using Rollgeon.Entities;
using Rollgeon.Feedback;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;
using Rollgeon.UI.HUD;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Builders
{
    /// <summary>
    /// Builder idempotente de La Generala (jefe del piso 3) y de los cinco dados de su mesa.
    /// <see cref="BuildAIRoot"/> y <see cref="PopulateEnemyData"/> son estáticos puros — se testean
    /// en memoria sin tocar el <see cref="AssetDatabase"/>; el <c>[MenuItem]</c> es el único que
    /// escribe assets.
    /// </summary>
    /// <remarks>
    /// Tira sus dados vivos por el mismo detector de combos que la mano del jugador, y el combo que
    /// sale <b>es</b> el ataque. Romperle un dado le borra una categoría y le abre un hueco a la
    /// sala.
    /// </remarks>
    public static class GeneralaAssetBuilder
    {
        private const string LogPrefix = "[GeneralaAssetBuilder] ";

        private const string EnemiesFolder = "Assets/Rollgeon/Enemies";
        public const string BossAssetPath = EnemiesFolder + "/ED_Boss_Generala.asset";

        /// <summary>La mesa, como <see cref="RoomObjectDefinitionSO"/>.</summary>
        public const string DiceDefinitionPath =
            "Assets/Rollgeon/Combat/RoomObjects/RO_Generala_Dado.asset";

        /// <summary>El dado como enemigo. El árbol del jefe no lo apunta.</summary>
        public const string DiceAssetPath = EnemiesFolder + "/ED_Obj_DadoCasa.asset";

        /// <summary>
        /// La escarcha. Asset propio y no el del Anotador: aquél dura más a propósito y retunearlo
        /// le cambiaría la pelea al jefe del piso 2.
        /// </summary>
        public const string FrostHazardAssetPath =
            "Assets/Rollgeon/Combat/Hazards/GeneralaFrostHazardDefinition.asset";

        public const string BossEntityId = "boss.la_generala";
        public const string DiceEntityId = "obj.dado_casa";

        /// <summary>Id de la definición de la mesa. Formato <c>roomobj.&lt;jefe&gt;.&lt;pieza&gt;</c>.</summary>
        public const string DiceRoomObjectId = "roomobj.generala.dado";

        /// <summary>
        /// SourceId fijo (no <c>Guid.NewGuid()</c>) para que reconstruir el asset no le cambie la
        /// identidad al hazard — mismo criterio que <c>AnotadorAssetBuilder.IceHazardSourceId</c>,
        /// y obviamente distinto del suyo: dos hazards con el mismo source id se pisarían.
        /// </summary>
        public const string FrostHazardSourceId = "3f1c9a52-84d7-4b60-9e13-2ac6f5d80b74";

        // ---- Números de la ficha ------------------------------------------------------

        /// <summary>Vida del jefe de piso 3.</summary>
        public const int BossHp = 240;
        public const int BossAttack = 40;

        /// <summary>Vida de cada dado de la mesa.</summary>
        public const int DiceHp = 45;

        public const int HandSize = 5;

        /// <summary>
        /// Turnos que tarda en reponer un dado roto. <b>Negativo = no se repone</b>
        /// (<c>RoomObjectDefinitionSO.Respawns</c> es <c>RespawnDelayTurns &gt;= 0</c>, así que el 0
        /// es "vuelve enseguida").
        /// </summary>
        public const int TableRefillTurns = -1;

        /// <summary>
        /// Reducción de daño con la mesa entera en pie. Baja <see cref="TableArmorPerDie"/> por cada
        /// dado roto y <b>no vuelve</b>. Ver <c>RoomObjectArmorService</c>.
        /// </summary>
        public const float TableArmorMax = 0.5f;

        /// <summary>
        /// Lo que descuenta cada dado en pie. Sale de la división y no de un literal: autorar 0.15
        /// con cinco dados daría 75% y nadie lo notaría.
        /// </summary>
        public const float TableArmorPerDie = TableArmorMax / HandSize;

        public const int BustDamage = 18;
        public const int PairDamage = 25;
        public const int LadderDamage = 45;
        public const int FullHouseDamage = 20;
        public const int PokerDamage = 45;

        /// <summary>
        /// Daño de la mano grande: ocho cuadrados de 3×3 y una ronda extra de aviso.
        /// </summary>
        public const int GeneralaDamage = 45;

        /// <summary>
        /// Daño del cubilete: golpe melee directo contra quien esté pegado cuando ella tira, sin
        /// aviso.
        /// </summary>
        public const int CupSlamDamage = 12;

        /// <summary>
        /// Alcance del cubilete, en Manhattan. Constante y no literal porque
        /// <see cref="RepositionRange"/> tiene que quedar estrictamente por encima.
        /// </summary>
        public const int CupSlamRange = 1;

        // ---- La escarcha ----------------------------------------------------------------

        /// <summary>
        /// Alcance Chebyshev de la escarcha. 1 = el 3×3 que la rodea, o sea el anillo donde vive el
        /// quinto dado.
        /// </summary>
        public const int FrostRingRadius = 1;

        /// <summary>
        /// Área maciza, no un anillo hueco. <c>OnEnter</c> no dispara sobre quien ya estaba adentro,
        /// así que entrar en la ronda franca te deja pegándole gratis y salir cuesta el turno.
        /// </summary>
        public const bool FrostIsSolid = true;

        public const int FrostStunTurns = 1;

        /// <summary>
        /// Vida del anillo en el SO del hazard. Ojo con el corrimiento de +1:
        /// <c>DurationRounds = D</c> vale <c>D - 1</c> rondas pisables, porque el descuento va por
        /// wrap de ronda y la escarcha nace con el turno del jugador ya jugado (CNF-006).
        /// </summary>
        /// <remarks>
        /// Atado a <see cref="FrostParityDivisor"/>: el hielo tiene que ocupar menos rondas que la
        /// cadencia.
        /// </remarks>
        public const int FrostDurationRounds = 3;

        /// <summary>
        /// Cadencia de la escarcha: cae en las rondas múltiplo de este número, y las otras son la
        /// ventana para romperle el dado caro.
        /// </summary>
        /// <remarks>
        /// Atado a <see cref="FrostDurationRounds"/>: el hielo ocupa <c>D - 1</c> rondas, así que la
        /// ventana existe sólo si la cadencia es estrictamente mayor. Con duración 3 y cadencia 2 no
        /// queda una sola ronda pisable en toda la pelea.
        /// </remarks>
        public const int FrostParityDivisor = 3;

        /// <summary>Celeste del hielo, el mismo de la estela del Anotador: el hielo se lee igual en todo el juego.</summary>
        public static readonly Color FrostOverlayTint = new Color(0.35f, 0.8f, 1f, 0.55f);

        /// <summary>
        /// Burst de la pisada, compartido con el Anotador. La ruta se repite en vez de leer su
        /// constante para no acoplar dos builders; si no existe el prefab el anillo se ve igual.
        /// </summary>
        public const string FrostVfxPrefabPath = "Assets/Prefabs/VFX/VFX_IceBurst.prefab";

        /// <summary>Vida del burst. 1.5s le sobra al glow, que emite 0.5s (mismo valor que la estela).</summary>
        public const float FrostBurstLifetime = 1.5f;

        // ---- El reposicionamiento -------------------------------------------------------

        /// <summary>Distancia Manhattan que intenta mantener: se acerca hasta acá y no más.</summary>
        /// <remarks>
        /// Tiene que quedar estrictamente por encima de <see cref="CupSlamRange"/> (si no, se pega
        /// sola y cobra cubilete todos los turnos) y de <see cref="FrostRingRadius"/> (si no, frena
        /// sobre su propio hielo sólido).
        /// </remarks>
        public const int RepositionRange = 3;

        /// <summary>Pasos por turno del reposicionamiento: los 4 de su <c>BaseSpeed</c>.</summary>
        public const int RepositionSteps = 4;

        // ---- La regla de la mano repetida ------------------------------------------------

        /// <summary>
        /// Combos prohibidos por turno. 1 = "el último que anotaste", que es exactamente la regla:
        /// no podés repetir la mano de la ronda pasada. La ventana es deslizante — al turno
        /// siguiente se descarta y se prohíbe la nueva última.
        /// </summary>
        public const int RepeatBanWindow = 1;

        public const float Phase2HpThreshold = 0.5f;
        public const float WeaknessMultiplier = 1.5f;

        private const int MinGold = 60;
        private const int MaxGold = 80;

        // ---- Arte -----------------------------------------------------------------------
        //
        // El rig lo decide el animator: AnimCon_DiceBoss expone Roll, que es el cubilete y el
        // reroll de fase 2.

        public const string BossName = "Generala";

        public const string BossArtPrefabPath = "Assets/Prefabs/Enemies/DiceBoss_Animated.prefab";
        public const string BossVisualPrefabPath = "Assets/Prefabs/Enemies/Bosses/PF_Boss_Generala.prefab";
        /// <summary>Retrato del rig que viste (<c>DiceBoss_Animated</c>). Ver <see cref="BossPortraitLibrary"/>.</summary>
        public const string BossPortraitTexturePath = BossPortraitLibrary.GeneralaPath;

        /// <summary>El dado de la casa se viste con un dado 3D real — un humanoide no se lee como dado.</summary>
        public const string DiceArtPrefabPath = "Assets/Prefabs/Dice/DiceThrow3D_Die.prefab";
        public const string DiceVisualPrefabPath = "Assets/Prefabs/Enemies/Bosses/PF_Obj_DadoCasa.prefab";
        public const string DicePortraitTexturePath = "Assets/Art/2D/Symbols/Sprites/Casino_0051.png";

        /// <summary>El cubilete: la caja de dados de la mesa, a un costado de la torreta.</summary>
        public const string CupPropPrefabPath = "Assets/Prefabs/Props/CajaDadosv01.prefab";

        /// <summary>Estandarte a la espalda. Se saltea solo si el prop no cierra de tamaño.</summary>
        public const string BannerPropPrefabPath = "Assets/Prefabs/Props/BanderaParedv01.prefab";

        public const string MaterialsFolder = BossVisualWrapperBuilder.DefaultMaterialsRoot + "/" + BossName;

        /// <summary>Nombre del hijo que envuelve el arte — el default de <see cref="BossWrapperSpec"/>.</summary>
        private const string ArtChildName = "Art";

        /// <summary>Nombre del hijo con la barra de vida world-space que arma el wrapper.</summary>
        private const string HealthBarChildName = "Canvas";

        // Medidas objetivo, en unidades de mundo (TileSize = 1). El alto sigue a los jefes que
        // ya están en el juego (SecurityGuardBoss mide 1.8, GeneralDirector 2); el ancho está
        // capado porque un jefe más ancho que ~1.1 se derrama sobre el centro de las casillas
        // vecinas y deja de leerse en qué tile está parado.
        public const float BossTargetHeight = 2f;
        public const float BossMaxWidth = 1.1f;

        // El dado real mide ~1.2 con los puntos: a escala 1 llena la casilla entera y se lee
        // como caja, no como dado. 0.8 deja aire alrededor y sigue siendo pickeable.
        public const float DiceTargetHeight = 0.8f;
        public const float DiceMaxWidth = 0.85f;

        /// <summary>Alto del cubilete apoyado al lado suyo.</summary>
        public const float CupHeight = 0.35f;

        /// <summary>Alto del estandarte. Se rechaza el prop si para llegar hay que deformarlo.</summary>
        public const float BannerHeight = 1.2f;

        private const float BossBarClearance = 0.6f;
        private const float DiceBarClearance = 0.3f;

        /// <summary>
        /// La barra está autorada en unidades de mundo para un jefe de 2 de alto: sobre un dado de
        /// 0.8 tapa la entidad entera, y cinco dados llenan la sala de barras. Se encoge sólo en el
        /// wrapper del dado.
        /// </summary>
        private const float DiceBarScale = 0.35f;

        /// <summary>Escalas de arte fuera de este rango son síntoma de prop equivocado, no de tuning.</summary>
        private const float MinArtScale = 0.3f;
        private const float MaxArtScale = 3f;

        // ---- Paleta ---------------------------------------------------------------------
        //
        // Los materiales del arte son compartidos (Mat_Brown y compañía los usa medio casino),
        // así que el retinte los clona: ver BossVisualWrapperBuilder.

        /// <summary>Casaca/chasis: azul navy militar (sale del <c>Mat_Brown</c> del cuerpo).</summary>
        public static readonly MaterialRetint NavyRetint = MaterialRetint.FromColors(
            new Color(0.30f, 0.40f, 0.66f),
            new Color(0.12f, 0.18f, 0.35f),
            new Color(0.05f, 0.07f, 0.16f));

        /// <summary>Charreteras: latón más cálido y claro que el oro de casino, para que lea ornamento.</summary>
        public static readonly MaterialRetint BrassRetint = MaterialRetint.FromColors(
            new Color(1.00f, 0.92f, 0.60f),
            new Color(0.83f, 0.66f, 0.20f),
            new Color(0.36f, 0.26f, 0.06f));

        /// <summary>Dorso del dado: gunmetal teñido de azul, para que no se funda con la casaca.</summary>
        public static readonly MaterialRetint GunmetalRetint = MaterialRetint.FromColors(
            new Color(0.34f, 0.38f, 0.46f),
            new Color(0.20f, 0.23f, 0.30f),
            new Color(0.08f, 0.09f, 0.13f));

        /// <summary>Cuerpo del dado: marfil de la casa. Compartido — un cubo no justifica un clon.</summary>
        public const string DiceBodyMaterialPath = "Assets/Art/3D/Materials/Mat_Bone.mat";

        /// <summary>Puntos del dado. En el prefab de la bandeja usan la Default-Material legacy.</summary>
        public const string DicePipMaterialPath = "Assets/Art/3D/Materials/Mat_Black.mat";

        // ======================================================================
        // MenuItem
        // ======================================================================

        [MenuItem("Tools/Rollgeon/Bosses/Build Generala")]
        public static void Run()
        {
            var bossVisual = BuildBossVisual();
            var diceVisual = BuildDiceVisual();

            var bossPortrait = BossPortraitLibrary.Generala();
            var dicePortrait = SpriteImportUtility.EnsureSpriteImport(DicePortraitTexturePath);

            var dice = LoadOrCreate<EnemyDataSO>(DiceAssetPath);
            PopulateDiceData(dice, diceVisual, dicePortrait);
            EditorUtility.SetDirty(dice);

            var table = LoadOrCreate<RoomObjectDefinitionSO>(DiceDefinitionPath);
            PopulateDiceDefinition(table, diceVisual);
            EditorUtility.SetDirty(table);

            var frost = LoadOrCreate<HazardDefinitionSO>(FrostHazardAssetPath);
            ConfigureFrostHazard(frost, AssetDatabase.LoadAssetAtPath<GameObject>(FrostVfxPrefabPath));
            EditorUtility.SetDirty(frost);

            var boss = LoadOrCreate<EnemyDataSO>(BossAssetPath);
            PopulateEnemyData(boss, table, bossVisual, bossPortrait, frost);
            EditorUtility.SetDirty(boss);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(LogPrefix + $"Listo: '{BossAssetPath}' ({BossHp} HP) + su mesa en " +
                      $"'{DiceDefinitionPath}' ({HandSize} × {DiceHp} HP repartidos por la sala, " +
                      "sin reposición) + " +
                      $"'{FrostHazardAssetPath}', con wrappers '{BossVisualPrefabPath}' y " +
                      $"'{DiceVisualPrefabPath}'. Re-ejecutable sin duplicar nada.");
        }

        // ======================================================================
        // Visuales
        // ======================================================================

        /// <summary>
        /// Construye (o reconstruye) el wrapper de gameplay del jefe y lo devuelve. <c>null</c> si el
        /// arte no está en el proyecto.
        /// </summary>
        public static GameObject BuildBossVisual()
        {
            var fit = MeasureFit(BossArtPrefabPath, BossTargetHeight, BossMaxWidth, BossBarClearance);

            var wrapper = BossVisualWrapperBuilder.BuildWrapper(BuildBossSpec(fit, BuildBossProps(fit)));
            if (wrapper == null) return null;

            ApplyArtFit(BossVisualPrefabPath, fit);
            return AssetDatabase.LoadAssetAtPath<GameObject>(BossVisualPrefabPath);
        }

        /// <summary>
        /// Construye (o reconstruye) el wrapper del dado de la casa. Además del fit, este pasa por
        /// <see cref="SanitizeDieArt"/>: el prefab de origen es el dado <b>físico</b> de la bandeja.
        /// </summary>
        public static GameObject BuildDiceVisual()
        {
            var fit = MeasureFit(DiceArtPrefabPath, DiceTargetHeight, DiceMaxWidth, DiceBarClearance);

            var wrapper = BossVisualWrapperBuilder.BuildWrapper(BuildDiceSpec(fit));
            if (wrapper == null) return null;

            ApplyArtFit(DiceVisualPrefabPath, fit, SanitizeDieArt, DiceBarScale);
            return AssetDatabase.LoadAssetAtPath<GameObject>(DiceVisualPrefabPath);
        }

        /// <summary>
        /// Ficha del wrapper del jefe. Separada del build para poder testear el spec sin escribir
        /// assets: el collider Box, el retinte navy y la carpeta de materiales son el contrato.
        /// </summary>
        public static BossWrapperSpec BuildBossSpec(ArtFit fit, List<BossPropSpec> props)
        {
            return new BossWrapperSpec
            {
                ArtPrefabPath = BossArtPrefabPath,
                OutputPrefabPath = BossVisualPrefabPath,
                EntityId = BossEntityId,
                BossName = BossName,
                MaterialsFolder = MaterialsFolder,

                // Box y no Capsule: con manos y dado saliéndose de la silueta, el capsule deja el
                // cursor picando aire en las esquinas.
                Collider = ColliderKind.Box,

                AddHealthBar = true,
                HealthBarOffset = fit.HealthBarOffset,

                // Las keys son los Mat_* compartidos a los que DiceBoss_Model.fbx remapea sus
                // materiales por externalObjects. Cuáles son cuál sale del nombre que el FBX le
                // da a cada slot (Base, Trim, Back), no de mirarlos a ojo.
                //
                // Mat_Red (los puntos del dado) y Mat_White (el galón) quedan sin retintar: son
                // los dos acentos que tienen que seguir leyendo como acento contra el navy.
                Retints = new Dictionary<string, MaterialRetint>
                {
                    { "Mat_Blue", NavyRetint },        // Enemy__Base + las cuatro caras
                    { "Mat_LightBlue", BrassRetint },  // Enemy__Trim — el filo ornamental
                    { "Mat_Black", GunmetalRetint },   // Enemy__Back
                },

                Props = props,
            };
        }

        /// <summary>Ficha del wrapper del dado: barra propia y collider Box, porque es un cubo.</summary>
        public static BossWrapperSpec BuildDiceSpec(ArtFit fit)
        {
            return new BossWrapperSpec
            {
                ArtPrefabPath = DiceArtPrefabPath,
                OutputPrefabPath = DiceVisualPrefabPath,
                BossName = "DadoCasa",
                MaterialsFolder = MaterialsFolder,
                Collider = ColliderKind.Box,

                // Sin barra no hay forma de saber cuánto le falta al dado.
                AddHealthBar = true,
                HealthBarOffset = fit.HealthBarOffset,

                // Sin retinte: los materiales del dado se asignan en SanitizeDieArt (el prefab de la
                // bandeja apunta a un material que ya no existe en el proyecto).
            };
        }

        /// <summary>
        /// Props del jefe: el cubilete siempre, el estandarte sólo si el prop cierra de tamaño. Las
        /// medidas salen de los bounds reales, así que un prop reexportado más grande no descoloca
        /// nada — se recalcula en el próximo build.
        /// </summary>
        public static List<BossPropSpec> BuildBossProps(ArtFit fit)
        {
            var props = new List<BossPropSpec>();

            if (TryMeasurePrefab(CupPropPrefabPath, out var cupBounds))
                props.Add(BuildCupProp(fit, cupBounds));

            if (TryMeasurePrefab(BannerPropPrefabPath, out var bannerBounds)
                && TryBuildBannerProp(fit, bannerBounds, out var banner))
            {
                props.Add(banner);
            }

            return props;
        }

        /// <summary>
        /// Cubilete apoyado en el piso y pegado al costado derecho del casco, sin meterse dentro.
        /// </summary>
        /// <remarks>
        /// Las cuentas van contra los bordes de los bounds y no contra los extents porque ni el jefe
        /// ni el prop tienen el pivot en el centro de su volumen: el de la caja de dados viene del
        /// transform de la sala donde se autoró. Restando <c>min</c> / sumando <c>max</c> el prop
        /// apoya y toca sin importar dónde caiga su pivot.
        /// </remarks>
        public static BossPropSpec BuildCupProp(ArtFit fit, Bounds cupBounds)
        {
            float scale = FitScale(cupBounds, CupHeight, maxWidth: CupHeight * 2f);
            var scaled = ScaleBounds(cupBounds, scale);

            return new BossPropSpec
            {
                PrefabPath = CupPropPrefabPath,
                Name = "Cubilete",
                LocalScale = Vector3.one * scale,
                LocalPosition = new Vector3(
                    fit.Bounds.max.x - scaled.min.x,
                    -scaled.min.y,
                    fit.Bounds.center.z - scaled.center.z),
            };
        }

        /// <summary>
        /// Estandarte a la espalda. Devuelve false — y no cuelga nada — si para llegar al alto
        /// pedido hay que escalar el prop fuera de rango.
        /// </summary>
        public static bool TryBuildBannerProp(ArtFit fit, Bounds bannerBounds, out BossPropSpec prop)
        {
            prop = null;
            if (bannerBounds.size.y <= Mathf.Epsilon) return false;

            float raw = BannerHeight / bannerBounds.size.y;
            if (raw < MinArtScale || raw > MaxArtScale) return false;

            var scaled = ScaleBounds(bannerBounds, raw);

            prop = new BossPropSpec
            {
                PrefabPath = BannerPropPrefabPath,
                Name = "Estandarte",
                LocalScale = Vector3.one * raw,

                // -Z es la espalda: el arte mira a +Z (los cañones están en z positivo).
                LocalPosition = new Vector3(
                    fit.Bounds.center.x - scaled.center.x,
                    -scaled.min.y,
                    fit.Bounds.min.z - scaled.max.z),
            };
            return true;
        }

        // ======================================================================
        // Fit del arte
        // ======================================================================

        /// <summary>
        /// Escala, levantada y bounds finales de un prefab de arte dentro de su wrapper.
        /// </summary>
        /// <remarks>
        /// <see cref="BossVisualWrapperBuilder"/> anida el arte a escala 1 en el origen del wrapper,
        /// que es lo correcto para un rig con el pivot en los pies y la altura de un jefe. El dado
        /// tiene el pivot en el centro del cubo, así que apoyado en el origen queda medio enterrado.
        /// Este struct calcula la corrección a partir de los bounds reales del arte, y
        /// <see cref="ApplyArtFit"/> la escribe en el prefab.
        /// </remarks>
        public readonly struct ArtFit
        {
            /// <summary>Escala uniforme del hijo <c>Art</c>.</summary>
            public readonly float Scale;

            /// <summary>Y local del hijo <c>Art</c> para que el arte apoye en el piso.</summary>
            public readonly float Lift;

            /// <summary>Bounds del arte ya escalado y apoyado — es lo que tiene que cubrir el collider.</summary>
            public readonly Bounds Bounds;

            public readonly Vector3 HealthBarOffset;

            public ArtFit(float scale, float lift, Bounds bounds, Vector3 healthBarOffset)
            {
                Scale = scale;
                Lift = lift;
                Bounds = bounds;
                HealthBarOffset = healthBarOffset;
            }

            public static ArtFit For(Bounds raw, float targetHeight, float maxWidth, float barClearance)
            {
                float scale = FitScale(raw, targetHeight, maxWidth);
                var scaled = ScaleBounds(raw, scale);

                float lift = -scaled.min.y;
                var grounded = new Bounds(scaled.center + new Vector3(0f, lift, 0f), scaled.size);

                return new ArtFit(scale, lift, grounded,
                    new Vector3(0f, grounded.max.y + barClearance, 0f));
            }

            /// <summary>Fallback cuando el arte no reporta bounds: se deja como lo dejó el wrapper.</summary>
            public static ArtFit Unmeasured(float barHeight) => new ArtFit(
                1f, 0f,
                new Bounds(new Vector3(0f, 1f, 0f), new Vector3(1f, 2f, 1f)),
                new Vector3(0f, barHeight, 0f));
        }

        private static ArtFit MeasureFit(string artPath, float targetHeight, float maxWidth, float barClearance)
        {
            if (TryMeasurePrefab(artPath, out var raw))
                return ArtFit.For(raw, targetHeight, maxWidth, barClearance);

            Debug.LogWarning(LogPrefix + $"No se pudieron medir los bounds de '{artPath}' — el wrapper " +
                             "sale a escala 1 y hay que revisar collider y barra a mano.");
            return ArtFit.Unmeasured(targetHeight + barClearance);
        }

        /// <summary>
        /// Escala para llegar a <paramref name="targetHeight"/> sin pasarse de
        /// <paramref name="maxWidth"/>: manda la restricción más chica, porque un jefe que llega al
        /// alto pedido derramándose sobre las casillas vecinas deja de leerse en su tile.
        /// </summary>
        private static float FitScale(Bounds raw, float targetHeight, float maxWidth)
        {
            float scale = targetHeight / Mathf.Max(raw.size.y, Mathf.Epsilon);

            float widest = Mathf.Max(raw.size.x, raw.size.z);
            if (widest > Mathf.Epsilon) scale = Mathf.Min(scale, maxWidth / widest);

            return Mathf.Clamp(scale, MinArtScale, MaxArtScale);
        }

        private static Bounds ScaleBounds(Bounds bounds, float scale) =>
            new Bounds(bounds.center * scale, bounds.size * scale);

        /// <summary>
        /// Bounds de los Mesh/SkinnedMesh renderers de un prefab, medidos con el prefab en el origen
        /// y a escala 1 — el mismo encuadre en el que el wrapper anida el arte.
        /// </summary>
        private static bool TryMeasurePrefab(string prefabPath, out Bounds bounds)
        {
            bounds = default;

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (asset == null)
            {
                Debug.LogWarning(LogPrefix + $"No hay prefab en '{prefabPath}' — no se puede medir.");
                return false;
            }

            var probe = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            if (probe == null) return false;

            try
            {
                // El prefab puede traer el transform de la sala donde se autoró (la caja de dados
                // viene en 1.5/0.783/-1.5): sin resetear, los bounds saldrían corridos.
                probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                probe.transform.localScale = Vector3.one;

                bool any = false;
                foreach (var renderer in probe.GetComponentsInChildren<Renderer>(true))
                {
                    if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;

                    if (any) bounds.Encapsulate(renderer.bounds);
                    else { bounds = renderer.bounds; any = true; }
                }

                if (!any || bounds.size.y <= Mathf.Epsilon)
                {
                    Debug.LogWarning(LogPrefix + $"'{prefabPath}' no reporta bounds usables.");
                    return false;
                }
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        /// <summary>
        /// Escribe el fit sobre el wrapper ya guardado: escala y levanta el hijo <c>Art</c>, re-dimensiona
        /// el collider del root y, opcionalmente, encoge la barra y corre un paso extra sobre el arte.
        /// </summary>
        /// <remarks>
        /// Es una segunda pasada y no un parámetro del spec porque <see cref="BossVisualWrapperBuilder"/>
        /// fija el arte en identidad a propósito (su collider asume eso). Reescribir sobre el mismo path
        /// mantiene el GUID, así que los <c>EnemyDataSO</c> que ya apuntan al wrapper sobreviven.
        /// </remarks>
        private static void ApplyArtFit(
            string prefabPath,
            ArtFit fit,
            Action<Transform> postProcess = null,
            float barScale = 1f)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            if (contents == null)
            {
                Debug.LogWarning(LogPrefix + $"No se pudo abrir '{prefabPath}' para ajustar el arte.");
                return;
            }

            try
            {
                var art = contents.transform.Find(ArtChildName);
                if (art == null)
                {
                    Debug.LogWarning(LogPrefix + $"'{prefabPath}' no tiene hijo '{ArtChildName}' — " +
                                     "no se ajusta ni la escala ni el collider.");
                    return;
                }

                art.localScale = Vector3.one * fit.Scale;
                art.localPosition = new Vector3(0f, fit.Lift, 0f);

                // El wrapper dimensionó el collider con el arte en identidad: escalado y levantado,
                // ese collider queda chico y corrido respecto de lo que se ve.
                var box = contents.GetComponent<BoxCollider>();
                if (box != null)
                {
                    box.center = fit.Bounds.center;
                    box.size = fit.Bounds.size;
                }

                if (!Mathf.Approximately(barScale, 1f))
                {
                    var bar = contents.transform.Find(HealthBarChildName);
                    if (bar != null) bar.localScale = Vector3.one * barScale;
                }

                postProcess?.Invoke(art);

                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// Deja el dado de la bandeja física en condiciones de ser un pawn de mesa.
        /// </summary>
        /// <remarks>
        /// <c>DiceThrow3D_Die.prefab</c> es el dado del minijuego de tirada: Rigidbody sin kinematic,
        /// el script del throw, el juice de la bandeja y la capa DiceTray. Anidado tal cual, el arte se
        /// cae del pawn en el primer frame de física.
        /// </remarks>
        private static void SanitizeDieArt(Transform art)
        {
            // El juice primero y el die después: DiceThrow3DDie tiene [RequireComponent(Rigidbody)],
            // así que el Rigidbody no se puede sacar mientras el script siga puesto.
            foreach (var juice in art.GetComponentsInChildren<DiceThrowDieJuice>(true))
                UnityEngine.Object.DestroyImmediate(juice);
            foreach (var die in art.GetComponentsInChildren<DiceThrow3DDie>(true))
                UnityEngine.Object.DestroyImmediate(die);
            foreach (var body in art.GetComponentsInChildren<Rigidbody>(true))
                UnityEngine.Object.DestroyImmediate(body);

            // El collider que resuelve el pick es el del root (lo pone el wrapper junto al EntityPawn);
            // el del arte sólo agrega superficie de colisión que nadie consulta.
            foreach (var collider in art.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);

            // DiceTray es la capa de la bandeja de tirada, con su física y su culling: un pawn de
            // combate va en Default como el resto de los enemigos.
            foreach (var child in art.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = 0;

            AssignDieMaterials(art);
        }

        /// <summary>
        /// Marfil en el cubo, negro en los puntos.
        /// </summary>
        /// <remarks>
        /// El dado de la bandeja apunta a un material cuyo asset ya no está en el proyecto y los puntos
        /// usan la <c>Default-Material</c> built-in: las dos cosas salen magenta bajo URP. Se asignan
        /// materiales compartidos y no clones retintados.
        /// </remarks>
        private static void AssignDieMaterials(Transform art)
        {
            var body = AssetDatabase.LoadAssetAtPath<Material>(DiceBodyMaterialPath);
            var pip = AssetDatabase.LoadAssetAtPath<Material>(DicePipMaterialPath);

            if (body == null || pip == null)
            {
                Debug.LogWarning(LogPrefix + $"Faltan materiales del dado ('{DiceBodyMaterialPath}' / " +
                                 $"'{DicePipMaterialPath}') — queda con los del prefab de la bandeja.");
                return;
            }

            foreach (var renderer in art.GetComponentsInChildren<MeshRenderer>(true))
            {
                // El cubo es el renderer del propio root del arte; los puntos son los hijos Face1..Face6.
                var material = renderer.transform == art ? body : pip;

                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = material;
                renderer.sharedMaterials = slots;
            }
        }

        // ======================================================================
        // Data (puro — testeable sin assets)
        // ======================================================================

        /// <summary>
        /// Escribe identidad, stats, recompensa y árbol de La Generala sobre
        /// <paramref name="boss"/>. <paramref name="diceTable"/> es la
        /// <see cref="RoomObjectDefinitionSO"/> de su mesa (puede ser null en tests que no miren el
        /// spawn).
        /// </summary>
        public static void PopulateEnemyData(
            EnemyDataSO boss,
            RoomObjectDefinitionSO diceTable,
            GameObject visualPrefab,
            Sprite portrait = null,
            HazardDefinitionSO frostHazard = null)
        {
            if (boss == null) return;

            boss.EntityId = BossEntityId;
            boss.DisplayName = "La Generala";
            boss.Description =
                "The house playing your own game. Five dice of her own on the table, the same combo " +
                "sheet you use, and one hand per round. Her roll is public before it detonates: you " +
                "see the five numbers and you know what is coming. Break a die and you erase a " +
                "category — with four she cannot roll Generala, with three she loses Poker — and you " +
                "open a hole in a room made of her own dice. Walking up to the table is not free: " +
                "every roll brings the cup down on whoever is standing next to her.";

            boss.BaseHP = BossHp;
            boss.BaseAttack = BossAttack;
            boss.BaseSpeed = 4;
            boss.MaxEnergy = 3;
            boss.BaseAttackRange = 1;
            boss.BaseHealStrength = 0;

            // Base: débil a la Generala. En Fase 2 AINode_AdoptWeakness la repunta al combo que el
            // jugador más viene usando, con el mismo multiplicador.
            boss.WeaknessComboId = Rollgeon.Combos.ComboId.Generala;
            boss.WeaknessMultiplierOverride = WeaknessMultiplier;

            boss.MinGoldDrop = MinGold;
            boss.MaxGoldDrop = MaxGold;

            if (visualPrefab != null) boss.VisualPrefab = visualPrefab;

            // El par de dados del atlas de símbolos: el retrato de la cola de turnos y de la BossBar
            // sale del mismo campo (BaseEntitySO.Portrait → IEntityPortraitResolver).
            if (portrait != null) boss.Portrait = portrait;

            boss.AIRoot = BuildAIRoot(diceTable, frostHazard);
        }

        /// <summary>
        /// Configura la definición de la escarcha. Área dinámica: <see cref="HazardDefinitionSO.Shape"/>
        /// se ignora (las casillas del anillo las pasa el nodo), el daño es 0 —el hielo cobra en
        /// turnos, no en HP— y la casilla pisada se derrite, que es lo que impide encadenar stuns.
        /// </summary>
        /// <param name="triggerVfx">
        /// Burst opcional. Con <c>null</c> el anillo queda con el quad celeste solo: el visual no es
        /// parte del contrato del hazard, así que un builder corrido sin el prefab no rompe la pelea.
        /// </param>
        public static void ConfigureFrostHazard(HazardDefinitionSO definition, GameObject triggerVfx = null)
        {
            if (definition == null) return;

            definition.Trigger = HazardTriggerMode.OnEnter;
            // El anillo nace alrededor de la Generala, así que ella queda rodeada de su propio hielo:
            // sin esto se congela al primer paso y le abre las casillas al jugador de regalo.
            definition.Affects = HazardAffects.PlayerOnly;
            definition.Damage = 0;
            definition.Kind = AttackKind.Environmental;
            definition.ConsumeOnTrigger = true;
            definition.DurationRounds = FrostDurationRounds;
            definition.OverlayTint = FrostOverlayTint;
            definition.SourceId = FrostHazardSourceId;
            definition.TriggerVfxLifetime = FrostBurstLifetime;

            if (triggerVfx != null) definition.TriggerVfxPrefab = triggerVfx;
        }

        /// <summary>
        /// Escribe los dados de la mesa: objetos de <see cref="DiceHp"/> HP que no atacan ni se
        /// mueven. Existen para ser rotos — cada uno que cae le borra una categoría a la mano.
        /// </summary>
        public static void PopulateDiceData(EnemyDataSO dice, GameObject visualPrefab, Sprite portrait = null)
        {
            if (dice == null) return;

            dice.EntityId = DiceEntityId;
            dice.DisplayName = "Dado de la Casa";
            dice.Description =
                "One of the five dice the house rolls. No attack of its own — it sits on the table " +
                "being part of her hand and blocking the way. Breaking one costs you a full swing " +
                "and erases a category from her sheet.";

            dice.BaseHP = DiceHp;
            dice.BaseAttack = 0;
            dice.BaseSpeed = 1;
            dice.MaxEnergy = 0;
            dice.BaseAttackRange = 0;
            dice.BaseHealStrength = 0;

            dice.WeaknessComboId = string.Empty;
            dice.WeaknessMultiplierOverride = 0f;

            dice.MinGoldDrop = 0;
            dice.MaxGoldDrop = 0;

            if (visualPrefab != null) dice.VisualPrefab = visualPrefab;

            // Un dado suelto, no el par: en la cola de turnos hay cinco de estos seguidos y el par
            // de dados (el retrato del jefe) los haría indistinguibles de ella.
            if (portrait != null) dice.Portrait = portrait;

            // AIRoot explícito: sin árbol el spawn cae al BasicEnemyAI, que ataca siempre — un dado
            // que le pega al jugador rompe la lectura de "todo el daño entra por la mano".
            dice.AIRoot = new AINode_Wait();
        }

        /// <summary>
        /// Escribe la definición de la mesa: cinco dados de <see cref="DiceHp"/> HP que bloquean su
        /// casilla, <b>no se reponen</b> (<see cref="TableRefillTurns"/> negativo) y no ocupan slot
        /// en la cola de turnos.
        /// </summary>
        public static void PopulateDiceDefinition(RoomObjectDefinitionSO table, GameObject visualPrefab)
        {
            if (table == null) return;

            table.Id = DiceRoomObjectId;
            table.DisplayName = "Dado de la Casa";
            table.Hp = DiceHp;
            table.Blocks = true;
            table.HideFromTurnQueue = true;
            table.RespawnDelayTurns = TableRefillTurns;
            table.OnDeathHazard = null;
            table.OwnerDamageReductionPerObject = TableArmorPerDie;

            if (visualPrefab != null) table.VisualPrefab = visualPrefab;
        }

        // ======================================================================
        // Árbol
        // ======================================================================

        /// <summary>
        /// Árbol de decisión del jefe. Orden del turno: cobra el aviso pendiente, corre el gate de
        /// fase, repone la mesa, tira la mano —bajando el cubilete sobre quien esté pegado—, marca
        /// el área del combo que le salió, congela el anillo de la mesa, tacha la mano que el
        /// jugador acaba de anotar, y recién ahí se reacomoda.
        /// </summary>
        /// <param name="diceTable">
        /// Definición de la mesa (<see cref="PopulateDiceDefinition"/>). Null en tests que no miren la
        /// mesa: el nodo devuelve Failed y su Selector de aislamiento lo absorbe.
        /// </param>
        /// <param name="frostHazard">
        /// Definición de la escarcha (<see cref="ConfigureFrostHazard"/>). Puede ser null en tests
        /// que no miren el hielo: el nodo devuelve Failed y su Selector de aislamiento lo absorbe.
        /// </param>
        public static AINode_Sequence BuildAIRoot(RoomObjectDefinitionSO diceTable,
                                                  HazardDefinitionSO frostHazard = null)
        {
            return new AINode_Sequence
            {
                Children = new List<AIDecisionNode>
                {
                    // 1. La mano de la ronda pasada explota con la forma del combo que le salió.
                    new AINode_ExecuteTelegraph { WindupFeedbackId = BossFeedbackIds.GeneralaRangeAnim },

                    // 2. Fase 2 ANTES del ataque, para que el reroll aplique en el mismo turno en
                    //    que cruza el umbral.
                    Isolate(BuildPhaseTwoGate()),

                    // 3. La mesa. Sin Once: el nodo se auto-gatea y necesita tickear para recoger
                    //    los rotos.
                    Isolate(new AINode_SpawnRoomObjects
                    {
                        Definition = diceTable,
                        Count = HandSize,
                        Pattern = AINode_SpawnRoomObjects.Placement.DoorFronts,
                        SpawnFeedbackId = BossFeedbackIds.GeneralaSummonAnim,
                    }),

                    // 4. Tira los dados vivos y canta el combo (público un turno antes de detonar).
                    new AINode_RollHand
                    {
                        SizeSource = AINode_RollHand.HandSizeSource.AliveAllies,
                        MaxDice = HandSize,
                        DieFaces = 6,
                        SlowCombos = new List<string> { Rollgeon.Combos.ComboId.Generala },
                    },

                    // 5. Y con la misma tirada baja el cubilete sobre quien esté pegado. Aislado
                    //    porque con el jugador lejos devuelve Failed, y un Failed acá le comería
                    //    la marca de la mano.
                    Isolate(BuildCupSlam()),

                    // 6. La tabla combo → telegraph.
                    BuildHandTelegraphTable(),

                    // 7. La escarcha. Cero daño: el hielo cobra en turnos, no en HP.
                    Isolate(BuildFrostGate(frostHazard)),

                    // 8. La mano que el jugador acaba de anotar queda prohibida para la ronda que
                    //    viene. Se computa al cerrar SU turno para que el jugador la vea tachada
                    //    antes de comprometer los dados.
                    Isolate(BuildRepeatBan()),

                    // 9. Y recién ahí se mueve. Último a propósito: el cubilete y la escarcha se
                    //    resuelven desde donde estaba parada cuando tiró, no desde donde terminó.
                    Isolate(BuildReposition()),
                },
            };
        }

        /// <summary>
        /// Selector con una rama por categoría, de la mano más alta a la más baja, y un
        /// <see cref="AINode_Wait"/> al final. Ese Wait es el que cubre el turno de la mano
        /// <i>cantada</i> (Generala recién tirada): ninguna rama matchea porque todas piden la mano
        /// armada, y el turno tiene que seguir igual.
        /// </summary>
        /// <remarks>
        /// Cuidado al tocar las ramas de <c>DirectionalBand</c>: ahí <c>Size</c> es el MEDIO ancho
        /// (1 ⇒ 3 casillas), mientras que en <c>Row</c> es el ancho total. Y la banda se centra en
        /// ELLA, no en el jugador, así que un jugador fuera del eje no queda marcado.
        /// </remarks>
        private static AINode_Selector BuildHandTelegraphTable()
        {
            return new AINode_Selector
            {
                Children = new List<AIDecisionNode>
                {
                    // Casi toda la sala salvo el anillo del borde: ocho cuadrados de 3×3 anclados
                    // en el 50% central, que es cómo ScatteredSquares reparte por construcción.
                    HandBranch(Rollgeon.Combos.ComboId.Generala, new AINode_TelegraphMark
                    {
                        Shape = ThreatShape.ScatteredSquares,
                        Count = 8,
                        Size = 3,
                        Damage = GeneralaDamage,
                        Kind = AttackKind.BasicAttack,
                    }),

                    HandBranch(Rollgeon.Combos.ComboId.Poker, new AINode_TelegraphMark
                    {
                        Shape = ThreatShape.SquareAroundPlayer,
                        Size = 2, // radio 2 ⇒ 5×5 sobre el jugador
                        Damage = PokerDamage,
                        Kind = AttackKind.BasicAttack,
                    }),

                    HandBranch(Rollgeon.Combos.ComboId.FullHouse, new AINode_TelegraphMark
                    {
                        Shape = ThreatShape.ScatteredSquares,
                        Count = 2,
                        Size = 3,
                        Damage = FullHouseDamage,
                        Kind = AttackKind.BasicAttack,
                    }),

                    HandBranch(Rollgeon.Combos.ComboId.Straight, new AINode_TelegraphMark
                    {
                        Shape = ThreatShape.DirectionalBand,
                        Size = 1,
                        Depth = 4,
                        Damage = LadderDamage,
                        Kind = AttackKind.BasicAttack,
                    }),

                    HandBranch(Rollgeon.Combos.ComboId.Par, new AINode_TelegraphMark
                    {
                        Shape = ThreatShape.DirectionalBand,
                        Size = 1,
                        Depth = 3,
                        Damage = PairDamage,
                        Kind = AttackKind.BasicAttack,
                    }),

                    // El bust: fallar del todo duele menos que un par.
                    BustBranch(new AINode_TelegraphMark
                    {
                        Shape = ThreatShape.DirectionalBand,
                        Size = 0,
                        Depth = 3,
                        Damage = BustDamage,
                        Kind = AttackKind.BasicAttack,
                    }),

                    new AINode_Wait(),
                },
            };
        }

        /// <summary>
        /// <c>If(mano == comboId) → mark</c>, sin <c>Else</c>: el <see cref="AINode_If"/> devuelve
        /// Failed cuando la rama elegida es null, que es justo lo que hace avanzar al Selector a la
        /// categoría siguiente.
        /// </summary>
        private static AINode_If HandBranch(string comboId, AIDecisionNode mark)
        {
            return new AINode_If
            {
                Conditions = new List<BasePreCondition>
                {
                    new PcBossHandCombo
                    {
                        Match = PcBossHandCombo.HandMatch.Combo,
                        ComboId = comboId,
                        RequireArmed = true,
                    },
                },
                Then = mark,
            };
        }

        private static AINode_If BustBranch(AIDecisionNode mark)
        {
            return new AINode_If
            {
                Conditions = new List<BasePreCondition>
                {
                    new PcBossHandCombo
                    {
                        Match = PcBossHandCombo.HandMatch.NoCombo,
                        RequireArmed = true,
                    },
                },
                Then = mark,
            };
        }

        /// <summary>
        /// El cubilete: melee directo, sin ronda de aviso y sin gate de paridad. Cae en cada tirada.
        /// </summary>
        /// <remarks>
        /// Manhattan 1: son las cuatro casillas desde las que él puede pegarle a ella o a un dado
        /// pegado a ella.
        /// </remarks>
        public static AINode_GeneralaCupSlam BuildCupSlam()
        {
            return new AINode_GeneralaCupSlam
            {
                Damage = CupSlamDamage,
                Range = CupSlamRange,
                Metric = DistanceMetric.Manhattan,
                Kind = AttackKind.BasicAttack,
            };
        }

        /// <summary>
        /// La escarcha, colgada de la cadencia de ronda (<see cref="FrostParityDivisor"/>): cae en
        /// las múltiplo de 3 y deja franca la tercera.
        /// </summary>
        /// <remarks>
        /// Sin ronda franca el hielo nuevo cae antes de que se derrita el anterior y no queda ventana
        /// para romperle la mesa. Ver <see cref="FrostParityDivisor"/>.
        /// </remarks>
        public static AINode_If BuildFrostGate(HazardDefinitionSO frostHazard)
        {
            return new AINode_If
            {
                Conditions = new List<BasePreCondition>
                {
                    new PcRoundNumber
                    {
                        Mode = PcRoundNumber.CompareMode.Multiple,
                        Value = FrostParityDivisor,
                    },
                },
                Then = BuildFrostRing(frostHazard),
                // Sin Else: en ronda impar el If devuelve Failed y lo absorbe el Selector de Isolate.
            };
        }

        public static AINode_GeneralaFrostRing BuildFrostRing(HazardDefinitionSO frostHazard)
        {
            return new AINode_GeneralaFrostRing
            {
                Hazard = frostHazard,
                Radius = FrostRingRadius,
                Solid = FrostIsSolid,
                StunTurns = FrostStunTurns,
                ReplacePreviousRing = true,
            };
        }

        /// <summary>
        /// "No repitas la mano." Prohíbe el último combo del <c>IComboLogService</c> vía
        /// <c>IContractModifierService</c>: armarlo otra vez paga 0 y la fila sale tachada en el
        /// Contrato.
        /// </summary>
        public static AINode_RotateBlock BuildRepeatBan()
        {
            return new AINode_RotateBlock
            {
                Target = AINode_RotateBlock.BlockTarget.Combo,
                Count = RepeatBanWindow,
            };
        }

        /// <summary>
        /// El reposicionamiento: persigue hasta <see cref="RepositionRange"/> y frena ahí.
        /// <c>Retreat = false</c> con <c>DesiredRange</c> devuelve <c>Failed</c> cuando ya está más
        /// cerca, que es la correa — nunca cierra a melee por su cuenta.
        /// </summary>
        /// <remarks>
        /// Aislado porque ese <c>Failed</c> es el caso benigno y pasa en la mayoría de sus turnos.
        /// </remarks>
        public static AINode_Move BuildReposition()
        {
            return new AINode_Move
            {
                MaxSteps = new AIConstantInt { Value = RepositionSteps },
                TargetSelector = new TargetSelector_AlwaysPlayer(),
                DesiredRange = new AIConstantInt { Value = RepositionRange },
                Retreat = false,
                StopAdjacent = false, // legacy, ignorado: manda DesiredRange.
            };
        }

        /// <summary>
        /// Fase 2 al 50%: le dan reroll, adopta como debilidad el combo que el jugador más usa, y
        /// emite el cambio de fase para el feedback. <see cref="AINode_Once"/> porque es setup, no
        /// un efecto por turno.
        /// </summary>
        private static AINode_If BuildPhaseTwoGate()
        {
            return new AINode_If
            {
                Conditions = new List<BasePreCondition>
                {
                    new PcOwnerHpBelow { Percent = Phase2HpThreshold },
                },
                Then = new AINode_Once
                {
                    Child = new AINode_Sequence
                    {
                        Children = new List<AIDecisionNode>
                        {
                            new AINode_SetHandReroll { RerollsPerRound = 1 },
                            new AINode_AdoptWeakness
                            {
                                LogWindow = 8,
                                MultiplierOverride = WeaknessMultiplier,
                            },
                            new AINode_ApplyStatModifier
                            {
                                AttackDelta = 0,
                                SpeedDelta = 0,
                                PhaseIndex = 2,
                                EmitPhaseChangedEvent = true,
                            },
                        },
                    },
                },
                // Sin Else: en Fase 1 el If devuelve Failed y lo absorbe el Selector de Isolate.
            };
        }

        /// <summary>
        /// Envuelve un nodo que puede devolver Failed en <c>Selector[node, Wait]</c>: sin esto un
        /// fallo suyo (sin tiles de borde libres, servicio ausente) le cancelaría al jefe el resto
        /// del turno — el ataque incluido.
        /// </summary>
        private static AINode_Selector Isolate(AIDecisionNode node)
        {
            return new AINode_Selector
            {
                Children = new List<AIDecisionNode> { node, new AINode_Wait() },
            };
        }

        // ======================================================================
        // AssetDatabase helpers
        // ======================================================================

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            var leaf = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
