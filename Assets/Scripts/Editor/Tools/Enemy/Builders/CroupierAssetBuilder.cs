using System.Collections.Generic;
using System.IO;
using Rollgeon.Combat.AI.Bosses.Croupier;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Combat.AI.Targeting;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Entities;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Feedback;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;
using Rollgeon.Tiles;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Editor.Tools.Enemy.Builders
{
    /// <summary>
    /// Arma por código el jefe de piso 1 <b>El Croupier</b>: su <see cref="EnemyDataSO"/> con el árbol
    /// de AI inline, las dos definiciones de fuego de paño y su prefab visual (arte del Healer
    /// retintado a carmesí de crupier + la ruleta parenteada al costado).
    /// </summary>
    /// <remarks>
    /// <see cref="BuildAIRoot"/>, <see cref="BuildWrapperSpec"/> y <see cref="PopulateEnemyData"/>
    /// son estáticos puros — se testean en memoria sin tocar el <c>AssetDatabase</c>.
    /// <see cref="BuildCroupier"/> es la capa que persiste, y es idempotente. El arte es el del
    /// Healer retintado: su lectura la lleva el paño, no la silueta, y su
    /// <c>LocomotionStyle.Blink</c> le queda bien — el crupier no camina, reaparece.
    /// </remarks>
    public static class CroupierAssetBuilder
    {
        // ======================================================================
        // Rutas
        // ======================================================================

        public const string BossAssetPath = "Assets/Rollgeon/Enemies/ED_Boss_Croupier.asset";
        public const string FirePhase1Path = "Assets/Rollgeon/Combat/Hazards/HZ_Croupier_TableFire.asset";
        public const string FirePhase2Path = "Assets/Rollgeon/Combat/Hazards/HZ_Croupier_TableFire_Phase2.asset";

        /// <summary>
        /// La casilla de fuego del jefe. Propia y no <c>Tile_FireTemp</c>: sube el dano por turno a
        /// 18 para superar el escudo del jugador y la duracion a 6 rondas para que las bandas se
        /// acumulen. Tocar la generica se lo cambiaria al resto del juego.
        /// </summary>
        public const string CroupierFirePath = "Assets/Rollgeon/Tiles/Tile_Fire_Croupier.asset";

        /// <summary>
        /// Llama del paño. Vive acá y no en el builder de la Bandida porque el fuego es del Croupier
        /// y ella lo reusa — un solo asset, un solo lugar donde cambiarlo.
        /// </summary>
        public const string FireVfxPrefabPath = "Assets/Prefabs/VFX/VFX_Fire.prefab";

        /// <summary>Mesh de fuego que trajo el arte; es un MeshRenderer con luces, no un sistema de partículas.</summary>
        private const string FireMeshPrefabPath = "Assets/Art/3D/Models/Items/Fire.prefab";

        /// <summary>Segundos que dura el fogonazo de pisar una casilla encendida.</summary>
        private const float FireBurstLifetime = 0.9f;

        /// <summary>Arte a vestir: el Healer ya viene con copa, moño, capa y bastón.</summary>
        public const string ArtPrefabPath = "Assets/Prefabs/Enemies/Healer_Animated.prefab";

        /// <summary>Prefab de gameplay que sale del wrapper y va a <c>EnemyData.VisualPrefab</c>.</summary>
        public const string VisualPrefabPath = "Assets/Prefabs/Enemies/Bosses/PF_Boss_Croupier.prefab";

        /// <summary>Ruleta parenteada al wrapper — la mesa del jefe, y lo que gira al cantar.</summary>
        public const string WheelPropPrefabPath = "Assets/Prefabs/Props/Ruletav03.prefab";

        /// <summary>Nombre del hijo de la ruleta. Lo busca <c>CroupierWheelSpinVisual</c> por fallback.</summary>
        public const string WheelChildName = CroupierWheelSpinVisual.DefaultWheelChildName;

        /// <summary>Nombre del label del número cantado. Lo busca <c>CroupierWheelNumberView</c>.</summary>
        public const string WheelNumberChildName = CroupierWheelNumberView.DefaultLabelChildName;

        /// <summary>
        /// Fuente del número. La pixel font del HUD y no la decorativa <c>Casino.ttf</c>: el número
        /// tiene que leerse de un vistazo a la distancia de la cámara, y ahí una tipografía de
        /// fantasía cuesta legibilidad justo en el dato del que cuelga toda la pelea.
        /// </summary>
        public const string WheelNumberFontPath = "Assets/Fonts/m6x11plus SDF.asset";

        /// <summary>
        /// Cuánto del disco ocupa el número, en diámetros. El label se autoescala a esta caja, así
        /// que el tamaño sale del prop y no de un font size cableado — si arte cambia la ruleta por
        /// una más grande, el número la sigue.
        /// </summary>
        public const float WheelNumberFillRatio = 0.62f;

        /// <summary>Retrato del rig que viste (<c>Healer_Animated</c>). Ver <see cref="BossPortraitLibrary"/>.</summary>
        public const string PortraitTexturePath = BossPortraitLibrary.SheetPath;

        // ======================================================================
        // Ficha de diseño — todos los números del jefe, en un solo lugar
        // ======================================================================

        public const string EntityId = "boss.croupier";
        public const string DisplayName = "The Croupier";
        public const string WeaknessComboId = "combo.pair";
        public const float WeaknessMultiplier = 1.5f;

        /// <summary>
        /// Jefe de piso 1: ~6 turnos con el golpe base del piso (13-27, mediana 20).
        /// La simulación que había subido esto a 350 asumía un golpe mediano de 42, que es
        /// daño de run avanzada y no el kit con el que se llega al primer jefe. Los tres
        /// jefes que ya estaban en el juego tienen 200: 350 era casi el doble del techo real.
        /// </summary>
        public const int MaxHp = 120;
        public const int Attack = 20;
        public const int Speed = 5;
        public const int MinGoldDrop = 15;
        public const int MaxGoldDrop = 23;

        /// <summary>Daño del sector en fase 1: 20% de la vida del jugador.</summary>
        public const int SectorDamage = 20;

        /// <summary>Daño de cada sector en fase 2 — 24 para quien esté en la columna de costura.</summary>
        public const int SectorDamagePhase2 = 12;

        /// <summary>
        /// Represalia de mesa: el precio de la casilla de melee. Se cobra en todo golpe que le entre,
        /// sin mirar el número ni la fase — es el único daño directo del jefe.
        /// </summary>
        public const int RetaliationDamage = 8;

        /// <summary>Fuego de paño: lo que cuesta terminar el turno en el sector que acaba de caer.</summary>
        public const int FireDamage = 6;

        /// <summary>
        /// "Arde 5 rondas" = 6 rondas de casilla. El fuego nace en el turno del jefe y el jugador
        /// tiene el primer turno de cada ronda (CNF-006), asi que la ronda en la que se enciende ya
        /// no tiene cierres de turno del jugador por delante: con 1 expiraria sin tickear nunca.
        /// </summary>
        /// <remarks>
        /// <b>Tiene que superar el intervalo entre igniciones o el efecto no existe.</b> El jefe
        /// prende en T2, o sea cada 2 rondas. Con duracion 2 nunca conviven dos bandas y el pano
        /// vuelve a estar limpio cada vez; con 6 conviven tres y el piso util se achica ronda a
        /// ronda hasta que no queda donde plantarse a defender. Eso es todo el plan del jefe: no
        /// romper el escudo de una, sino sacarle el lugar donde usarlo.
        /// </remarks>
        public const int FireDurationRounds = 6;

        /// <summary>
        /// Duracion del hazard de paño que este builder deja autorado para La Bandida (sus reels lo
        /// consumen). El Croupier ya no lo usa: bajarlo o subirlo no le cambia nada a el, pero si a
        /// ella. Era el "arde 2 rondas" original.
        /// </summary>
        public const int HazardDurationForBandida = 3;

        // ======================================================================
        // Rediseno: kiter de dos tiempos
        // ======================================================================

        /// <summary>
        /// Disparo de T1. Chico a proposito: el jefe no gana por sus golpes directos sino por el
        /// piso que va quemando, y un tiro grande a rango infinito volveria la persecucion injusta.
        /// </summary>
        public const int ShotDamage = 10;

        /// <summary>
        /// Alcance del disparo, en Manhattan. 20 cubre la diagonal entera de la sala 11x11 (max 20),
        /// asi que en la practica es "a cualquier distancia" sin escribir un centinela.
        /// </summary>
        public const int ShotRange = 20;

        /// <summary>Casillas que retrocede por turno de fuga.</summary>
        public const int FleeSteps = 2;

        /// <summary>
        /// Mientras el jugador este a menos de esto, huye. Alto a proposito: KeepDistance no hace
        /// nada en cuanto ya esta a la distancia ideal, y un ideal chico lo dejaria plantado a media
        /// sala esperando. Con 8 huye practicamente siempre, que es lo que define al personaje.
        /// </summary>
        public const int FleeIdealDistance = 8;

        /// <summary>Semi-ancho de la banda de fuego: 1 = 3 casillas de ancho.</summary>
        public const int BandHalfWidth = 1;

        /// <summary>
        /// Profundidad de la banda. 11 = el largo de la sala, asi que la banda siempre llega a la
        /// pared y no deja un pedazo del pasillo sin quemar por donde rodearla de una.
        /// </summary>
        public const int BandDepth = 11;

        /// <summary>Umbral del candado: desde aca le queda un dado menos, y no vuelve.</summary>
        public const float LockHpThreshold = 0.7f;

        /// <summary>
        /// Cual dado se traba. Fijo y no sorteado: el candado se tiene que leer como "me saco ESE",
        /// y RotateBlock etiqueta el candado con el indice + 1, asi que 0 muestra "1".
        /// </summary>
        public const int LockedDieIndex = 0;

        /// <summary>Umbral de "Pleno y color".</summary>
        public const float PlenoHpThreshold = 0.5f;

        /// <summary>
        /// Radio del hueco que "Pleno y color" NO prende, centrado en el jefe. 1 = su 3x3. Ojo que
        /// en <c>AllExceptSquareAroundSelf</c> el Size es el hueco, no el area amenazada.
        /// </summary>
        public const int PlenoHoleRadius = 1;

        /// <summary>Umbral de "Pleno y color".</summary>
        public const float Phase2HpThreshold = 0.5f;

        public const int Phase2NumbersPerTurn = 2;

        /// <summary>
        /// Distancia Manhattan que el crupier trata de sostener con el jugador.
        /// </summary>
        /// <remarks>
        /// 2 y no 1: su único daño directo es la Represalia, que es <i>el precio de la casilla de
        /// melee</i>. Si se plantara pegado al jugador estaría regalando esa casilla, y si kiteara a
        /// 4 como el Cajero —que sí tiene un disparo con ese alcance— sería infinito, porque su daño
        /// no depende de dónde esté parado. A 2 el jugador siempre puede cerrar con un paso, y ese
        /// paso es lo que cuesta llegar a la mesa.
        /// </remarks>
        public const int DesiredRange = 2;

        /// <summary>
        /// Casillas que se corre por turno. Menos que el presupuesto de movimiento del jugador a
        /// propósito: el reposicionamiento tiene que ser un peaje, no una persecución que no termina.
        /// </summary>
        public const int MoveSteps = 2;

        /// <summary>Rojo de brasa — se tiene que leer distinto del naranja del telegraph.</summary>
        public static readonly Color FireOverlayTint = new Color(0.85f, 0.10f, 0.05f, 0.60f);

        // ======================================================================
        // Ficha visual — paleta y transform del prop, en un solo lugar
        // ======================================================================

        // Los tres materiales que dominan la silueta del Healer y qué visten:
        //   Mat_Red      → capa, moño, banda del sombrero y mango del bastón.
        //   Mat_DarkGray → copa del sombrero y traje.
        //   Mat_Gold     → vivos del traje y cabeza del bastón.
        // Mat_White (camisa, guantes, esclerótica), Mat_Bone (piel) y Mat_Black (pupila) quedan
        // sin clonar a propósito: los guantes blancos son la mitad de la lectura "crupier", y
        // retintar el blanco también le cambiaría el ojo.
        //
        // Todos los colores van explícitos y no por PaletteSlot: los labels guardados en
        // PA_MainPalette están desalineados respecto de la tabla de PaletteSlots (Mat_Red hoy
        // apunta al slot 7, que en la tabla es "Green"), así que un slot no dice qué color sale.

        /// <summary>Carmesí de terciopelo del paño — el color que el jefe le presta a la mesa.</summary>
        public static readonly Color WineLight = new Color(0.647f, 0.157f, 0.251f);
        public static readonly Color WineMid = new Color(0.404f, 0.055f, 0.129f);
        public static readonly Color WineShadow = new Color(0.212f, 0.024f, 0.075f);

        /// <summary>Smoking casi negro con tiro a borravino: mantiene la luminancia del gris original.</summary>
        public static readonly Color TuxLight = new Color(0.196f, 0.145f, 0.169f);
        public static readonly Color TuxMid = new Color(0.129f, 0.090f, 0.110f);
        public static readonly Color TuxShadow = new Color(0.063f, 0.043f, 0.055f);

        /// <summary>Latón más brillante que el Mat_Gold de fábrica: los vivos tienen que saltar del vino.</summary>
        public static readonly Color BrassLight = new Color(0.980f, 0.855f, 0.529f);
        public static readonly Color BrassMid = new Color(0.831f, 0.635f, 0.196f);
        public static readonly Color BrassShadow = new Color(0.439f, 0.310f, 0.078f);

        /// <summary>
        /// La ruleta a su derecha (el bastón lo lleva en +X, así que el prop va en -X para no
        /// atravesarlo) y a la altura del pecho. El prop mide ~0.9 de diámetro y su malla cuelga
        /// -0.5 de su root, así que este Y es casi el centro del disco.
        /// </summary>
        public static readonly Vector3 WheelLocalPosition = new Vector3(-1.15f, 1.15f, 0f);

        /// <summary>
        /// Euler cero <b>a propósito</b>: el disco del prop mira a ±Z y el jefe también encara -Z
        /// (ojos y moño están en -Z), así que sin rotarlo la cara de la rueda queda hacia la cámara y
        /// el giro se ve. Rotarlo la pondría de perfil.
        /// </summary>
        public static readonly Vector3 WheelLocalEuler = Vector3.zero;

        /// <summary>0.85 uniforme: acompaña al jefe (~1.95 de alto) sin taparlo.</summary>
        public static readonly Vector3 WheelLocalScale = new Vector3(0.85f, 0.85f, 0.85f);

        /// <summary>
        /// Barra de vida más baja que el default de 3: el arte del Healer mide ~1.95 con bastón, y a 3
        /// la barra quedaba flotando con un hueco de una unidad sobre el sombrero.
        /// </summary>
        public static readonly Vector3 HealthBarOffset = new Vector3(0f, 2.4f, 0f);

        // Ids fijos y escritos a mano (no Guid.NewGuid): el builder es idempotente, y un id nuevo por
        // corrida haría que el asset cambie en cada build y que un fuego ya activo quede huérfano.
        private const string FirePhase1SourceId = "c0a17e11-0001-4c00-b001-6f75706965f1";
        private const string FirePhase2SourceId = "c0a17e11-0002-4c00-b002-6f75706965f2";

        // ======================================================================
        // Menú
        // ======================================================================

        [MenuItem("Tools/Rollgeon/Bosses/Build Croupier")]
        public static void BuildCroupier()
        {
            var flame = BuildFireVfx();

            // El Croupier ya no usa este hazard --su fuego es una casilla especial-- pero se sigue
            // autorando: HZ_Croupier_TableFire lo consume La Bandida para sus reels, y dejar de
            // escribirlo la dejaria con la definicion vieja sin que nadie lo note. La de fase 2 se
            // va porque no la referenciaba nadie mas.
            BuildFireDefinition(FirePhase1Path, HazardDurationForBandida, FirePhase1SourceId, flame);

            var visual = BuildVisualPrefab();
            var portrait = BossPortraitLibrary.Croupier();

            var boss = LoadOrCreate<EnemyDataSO>(BossAssetPath);
            // El fuego del jefe pasa a ser una casilla especial: trae visual propio, VFX al
            // dispararse, tooltip localizado, y el planner de la IA la esquiva sola.
            var croupierFire = AssetDatabase.LoadAssetAtPath<SpecialTileDefinitionSO>(CroupierFirePath);
            if (croupierFire == null)
            {
                Debug.LogError($"[CroupierAssetBuilder] Falta {CroupierFirePath}. El jefe queda sin " +
                               "fuego: el nodo de ignicion falla y sus turnos de quema no hacen nada.");
            }

            PopulateEnemyData(boss, croupierFire, visual, portrait);

            EditorUtility.SetDirty(boss);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CroupierAssetBuilder] Listo: '{BossAssetPath}' + fuego de paño (fase 1 y 2) + " +
                      $"'{VisualPrefabPath}'.");
            Selection.activeObject = boss;
        }

        // ======================================================================
        // Prefab visual
        // ======================================================================

        /// <summary>
        /// Ficha de armado del wrapper. Pura (no toca <c>AssetDatabase</c>) para que los tests puedan
        /// afirmar arte, retintes y transform del prop sin construir el prefab.
        /// </summary>
        public static BossWrapperSpec BuildWrapperSpec()
        {
            return new BossWrapperSpec
            {
                ArtPrefabPath = ArtPrefabPath,
                OutputPrefabPath = VisualPrefabPath,
                EntityId = EntityId,
                BossName = "Croupier",
                HealthBarOffset = HealthBarOffset,
                Retints = new Dictionary<string, MaterialRetint>
                {
                    { "Mat_Red", MaterialRetint.FromColors(WineLight, WineMid, WineShadow) },
                    { "Mat_DarkGray", MaterialRetint.FromColors(TuxLight, TuxMid, TuxShadow) },
                    { "Mat_Gold", MaterialRetint.FromColors(BrassLight, BrassMid, BrassShadow) },
                },
                Props = new List<BossPropSpec>
                {
                    new BossPropSpec
                    {
                        PrefabPath = WheelPropPrefabPath,
                        Name = WheelChildName,
                        LocalPosition = WheelLocalPosition,
                        LocalEuler = WheelLocalEuler,
                        LocalScale = WheelLocalScale,
                    },
                },
            };
        }

        /// <summary>
        /// Construye el wrapper y le cuelga el giro de la rueda. Devuelve <c>null</c> (con warning ya
        /// logueado por el wrapper) si el arte no está: el jefe queda sin <c>VisualPrefab</c>, que es
        /// exactamente lo que hay que ver en consola en vez de un prefab a medias.
        /// </summary>
        private static GameObject BuildVisualPrefab()
        {
            var wrapper = BossVisualWrapperBuilder.BuildWrapper(BuildWrapperSpec());
            if (wrapper == null) return null;

            AttachWheelSpinVisual(VisualPrefabPath);

            // Re-load: AttachWheelSpinVisual reescribe el prefab, y la instancia devuelta por el
            // wrapper apunta al contenido anterior.
            return AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath);
        }

        /// <summary>
        /// Agrega (idempotente) el <see cref="CroupierWheelSpinVisual"/> al root del wrapper y le cablea
        /// la ruleta.
        /// </summary>
        /// <remarks>
        /// Va acá y no en <c>BossVisualWrapperBuilder</c> porque es específico de este jefe: la
        /// fundación compartida no tiene por qué saber que existe una rueda. Se edita por
        /// <c>LoadPrefabContents</c> + <c>SaveAsPrefabAsset</c>, que reescribe sobre el mismo path y
        /// preserva el GUID.
        /// </remarks>
        private static void AttachWheelSpinVisual(string prefabPath)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            if (contents == null)
            {
                Debug.LogWarning($"[CroupierAssetBuilder] No se pudo abrir '{prefabPath}' para " +
                                 $"colgarle el giro de la rueda.");
                return;
            }

            try
            {
                var wheel = contents.transform.Find(WheelChildName);
                if (wheel == null)
                {
                    Debug.LogWarning($"[CroupierAssetBuilder] '{prefabPath}' no tiene un hijo " +
                                     $"'{WheelChildName}' — ¿faltó el prop '{WheelPropPrefabPath}'? " +
                                     $"El jefe queda sin rueda que gire.");
                    return;
                }

                var spin = contents.GetComponent<CroupierWheelSpinVisual>();
                if (spin == null) spin = contents.AddComponent<CroupierWheelSpinVisual>();

                // Explícito y no por el fallback de Awake: así el prefab queda inspeccionable y el giro
                // no depende de que nadie renombre el hijo.
                var so = new SerializedObject(spin);
                var wheelProp = so.FindProperty("_wheel");
                if (wheelProp != null)
                {
                    wheelProp.objectReferenceValue = wheel;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                else
                {
                    Debug.LogWarning("[CroupierAssetBuilder] CroupierWheelSpinVisual no expone '_wheel' " +
                                     "— ¿se renombró el campo? Queda el fallback por nombre de hijo.");
                }

                EnsureWheelNumber(contents, wheel);

                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// Crea (idempotente) el label del número cantado en el centro de la ruleta y le cuelga el
        /// <see cref="CroupierWheelNumberView"/> al root del wrapper.
        /// </summary>
        /// <remarks>
        /// El label cuelga del root y no de <c>Wheel</c>: ahí giraría con el disco y sería ilegible
        /// justo en el canto. La posición sale de los bounds del prop y no de un número cableado, así
        /// sigue a la rueda si arte le cambia tamaño, escala u offset de la malla.
        /// </remarks>
        private static void EnsureWheelNumber(GameObject contents, Transform wheel)
        {
            if (!TryMeasure(wheel, out var hubWorld, out float diameter, out float depth))
            {
                Debug.LogWarning($"[CroupierAssetBuilder] '{WheelChildName}' no tiene renderers — no se " +
                                 "puede ubicar el número cantado. El jefe queda con la rueda muda.");
                return;
            }

            var label = FindOrCreateLabel(contents);
            if (label == null) return;

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(WheelNumberFontPath);
            if (font != null) label.font = font;
            else
                Debug.LogWarning($"[CroupierAssetBuilder] No está la fuente '{WheelNumberFontPath}' — " +
                                 "el número sale con la fuente default de TMP.");

            label.color = BrassLight;
            label.alignment = TextAlignmentOptions.Center;
            label.text = string.Empty;

            // Autosize contra una caja derivada del disco en vez de un fontSize fijo: el tamaño en
            // unidades de mundo de TMP depende de la fuente, así que cablearlo obliga a re-tunear con
            // cada cambio de tipografía.
            label.enableAutoSizing = true;
            label.fontSizeMin = 1f;
            label.fontSizeMax = 300f;

            float box = Mathf.Max(diameter * WheelNumberFillRatio, 0.01f);
            label.rectTransform.sizeDelta = new Vector2(box, box);

            // Al frente del disco (-Z local, que es hacia donde encara el jefe y por lo tanto hacia la
            // cámara) más un margen: apoyado en el plano del disco haría z-fighting con él.
            var hubLocal = contents.transform.InverseTransformPoint(hubWorld);
            hubLocal.z -= depth * 0.5f + 0.05f;
            label.rectTransform.localPosition = hubLocal;
            label.rectTransform.localRotation = Quaternion.identity;
            label.rectTransform.localScale = Vector3.one;

            var view = contents.GetComponent<CroupierWheelNumberView>();
            if (view == null) view = contents.AddComponent<CroupierWheelNumberView>();

            var so = new SerializedObject(view);
            var labelProp = so.FindProperty("_label");
            if (labelProp != null)
            {
                labelProp.objectReferenceValue = label;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[CroupierAssetBuilder] CroupierWheelNumberView no expone '_label' — " +
                                 "¿se renombró el campo? Queda el fallback por nombre de hijo.");
            }
        }

        /// <summary>Centro y tamaño del disco, de los bounds de sus renderers.</summary>
        private static bool TryMeasure(Transform wheel, out Vector3 center, out float diameter, out float depth)
        {
            center = Vector3.zero;
            diameter = 0f;
            depth = 0f;

            var renderers = wheel.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return false;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            center = bounds.center;
            // El diámetro es el lado ancho de la cara del disco (X/Y); Z es el canto.
            diameter = Mathf.Max(bounds.size.x, bounds.size.y);
            depth = bounds.size.z;
            return diameter > 0f;
        }

        private static TMP_Text FindOrCreateLabel(GameObject contents)
        {
            var existing = contents.transform.Find(WheelNumberChildName);
            if (existing != null)
            {
                var found = existing.GetComponent<TMP_Text>();
                if (found != null) return found;

                // Un hijo con ese nombre pero sin TMP es basura de un build viejo (o un rename a
                // mano): se reemplaza en vez de dejar dos objetos peleando por el mismo nombre.
                Object.DestroyImmediate(existing.gameObject);
            }

            var go = new GameObject(WheelNumberChildName);
            go.transform.SetParent(contents.transform, worldPositionStays: false);
            return go.AddComponent<TextMeshPro>();
        }

        // ======================================================================
        // Datos del jefe (puro — sin AssetDatabase)
        // ======================================================================

        /// <summary>
        /// Escribe la ficha completa del Croupier en <paramref name="data"/>, incluido su
        /// <see cref="EnemyDataSO.AIRoot"/>. No toca <c>AssetDatabase</c>: sirve igual para el asset
        /// real y para una instancia in-memory de test.
        /// </summary>
        public static void PopulateEnemyData(
            EnemyDataSO data,
            SpecialTileDefinitionSO fire,
            GameObject visualPrefab,
            Sprite portrait)
        {
            if (data == null) return;

            data.EntityId = EntityId;
            data.DisplayName = DisplayName;
            data.Description =
                "\"Place your bets.\" He calls one number per turn: the block of the table that falls " +
                "next turn. Ending your turn inside the called block spins the wheel one step further " +
                "— moving the axe means standing under it. Hitting him costs 8, always: the house " +
                "charges for the melee tile. The six blocks cover the whole table — no tile sits out " +
                "the fight — and the middle row is the seam, where two of them overlap. He drifts to " +
                "keep the table between you and him.";

            data.WeaknessComboId = WeaknessComboId;
            data.WeaknessMultiplierOverride = WeaknessMultiplier;

            data.BaseHP = MaxHp;
            data.BaseAttack = Attack;
            data.BaseSpeed = Speed;
            data.MaxEnergy = 3;
            data.BaseAttackRange = 1;

            // Sin esto su propio fuego lo quema: ShouldAffect exige
            // OwnerBossImmune && IsBoss && el dueño sea este guid, y ningún builder venía
            // escribiendo IsBoss, así que el jefe contaba como enemigo común.
            data.IsBoss = true;

            // No cura ni tiene behaviors de curación: dejar 0 evita autorar un número que miente.
            data.BaseHealStrength = 0;

            data.MinGoldDrop = MinGoldDrop;
            data.MaxGoldDrop = MaxGoldDrop;
            data.VisualPrefab = visualPrefab;

            // Sin retrato la cola de turnos y la barra de jefe caen a su visual default: no rompe, pero
            // el jefe se ve como cualquier otro enemigo del piso.
            data.Portrait = portrait;

            // Sin behaviors: el Croupier no tiene melee ni rango. Su único daño directo es la
            // Represalia, y esa entra por el hook de daño de la rueda, no por el árbol.
            data.Behaviors = new List<BaseBehavior>();
            data.ExtraTiers = new List<EnemyTier>();

            data.AIRoot = BuildAIRoot(fire);
        }

        /// <summary>
        /// Árbol del Croupier. Sequence raíz de siete pasos: seis de mesa y el reacomodo, último.
        /// </summary>
        /// <remarks>
        /// Detonar va primero, resolviendo lo cantado el turno pasado, y el gate de fase va antes del
        /// marcado: en el path no-coroutine un <c>Running</c> aborta el Sequence. El reacomodo va
        /// último por lo mismo — es el único paso que devuelve <c>Running</c> (espera el blink). Cada
        /// paso que puede fallar va en <c>Selector[paso, Wait]</c>.
        /// </remarks>
        public static AINode_Sequence BuildAIRoot(SpecialTileDefinitionSO fire)
        {
            return new AINode_Sequence
            {
                Children = new List<AIDecisionNode>
                {
                    // 1. Pleno y color: una sola vez al cruzar el 50%, prende TODO el pano menos su
                    //    propio 3x3. Marca y enciende en el mismo turno a proposito: el fuego es su
                    //    propia telegrafia --se ve en el piso y solo cobra al pisarlo o al arrancar
                    //    el turno adentro--, asi que no hace falta el turno de aviso que si necesita
                    //    un golpe que cobra de una.
                    Guarded(new AINode_If
                    {
                        Conditions = new List<BasePreCondition>
                        {
                            new PcOwnerHpBelow { Percent = PlenoHpThreshold },
                        },
                        Then = new AINode_Once
                        {
                            Child = new AINode_Sequence
                            {
                                Children = new List<AIDecisionNode>
                                {
                                    // No sube el dano: solo anuncia (feedback + dialogo de fase).
                                    new AINode_ApplyStatModifier
                                    {
                                        AttackDelta = 0,
                                        SpeedDelta = 0,
                                        PhaseIndex = 2,
                                        EmitPhaseChangedEvent = true,
                                    },
                                    new AINode_TelegraphMark
                                    {
                                        Shape = ThreatShape.AllExceptSquareAroundSelf,
                                        // Size es el radio del HUECO que se salva, no del area
                                        // amenazada: 1 = deja libre su 3x3 y prende el resto.
                                        Size = PlenoHoleRadius,
                                        Damage = 0,
                                        Kind = AttackKind.Environmental,
                                    },
                                    new AINode_IgniteArea
                                    {
                                        Definition = fire,
                                        DurationRounds = FireDurationRounds,
                                    },
                                },
                            },
                        },
                        Else = new AINode_Wait(),
                    }),

                    // 2. Desde el 70% le queda un dado con candado. SIN AINode_Once: RotateBlock
                    //    hace Clear() antes de bloquear en cada tick y DiceBlockService se limpia
                    //    solo al cerrar cada turno del jugador, asi que "permanente" se consigue
                    //    re-emitiendolo todos los turnos. Con Once el candado duraria un turno.
                    //    Y va FUERA del Alternate por lo mismo: adentro solo se emitiria uno de
                    //    cada dos turnos y el dado parpadearia.
                    Guarded(new AINode_If
                    {
                        Conditions = new List<BasePreCondition>
                        {
                            new PcOwnerHpBelow { Percent = LockHpThreshold },
                        },
                        Then = new AINode_RotateBlock
                        {
                            Target = AINode_RotateBlock.BlockTarget.Dice,
                            // Indice fijo y no la ruleta: el candado tiene que caer siempre en el
                            // mismo dado para que se lea como "me saco ese", no como un sorteo.
                            DirectedIndex = new AIConstantInt { Value = LockedDieIndex },
                            BlockVfxId = BossFeedbackIds.CroupierConfiscaVfx,
                            BlockFeelId = BossFeedbackIds.CroupierConfiscaFeel,
                        },
                        Else = new AINode_Wait(),
                    }),

                    // 3. Los dos tiempos. Alternate avanza el indice en cada tick pase lo que pase,
                    //    asi que un beat que falla igual gasta su turno -- que es lo que queremos:
                    //    el ciclo no se desincroniza nunca y el jugador puede contar los turnos.
                    new AINode_Alternate
                    {
                        Children = new List<AIDecisionNode>
                        {
                            // -- T1 "Reparte" --------------------------------------------------
                            new AINode_Sequence
                            {
                                Children = new List<AIDecisionNode>
                                {
                                    // Dispara primero: si huyera antes, el tiro saldria desde la
                                    // casilla nueva y el jugador veria el fogonazo salir de donde
                                    // el jefe ya no esta.
                                    Guarded(new AINode_RangedShot
                                    {
                                        Damage = ShotDamage,
                                        Range = ShotRange,
                                        Kind = AttackKind.BasicAttack,
                                    }),

                                    // Huye. KeepDistance y no Move{Retreat}: Move busca una banda
                                    // ABSOLUTA y devuelve Failed en cuanto ya esta a esa distancia,
                                    // asi que a distancia 2 se quedaba clavado. Esto se aleja
                                    // mientras el jugador este dentro de FleeIdealDistance, y contra
                                    // la pared se desliza al costado en vez de fallar.
                                    Guarded(new AINode_KeepDistance
                                    {
                                        MaxSteps = new AIConstantInt { Value = FleeSteps },
                                        IdealDistance = new AIConstantInt { Value = FleeIdealDistance },
                                    }),

                                    // Marca la banda: anclada en el, apuntando al jugador.
                                    // Size es el SEMI-ancho, asi que 1 = 3 casillas de ancho.
                                    Guarded(new AINode_TelegraphMark
                                    {
                                        Shape = ThreatShape.DirectionalBand,
                                        Size = BandHalfWidth,
                                        Depth = BandDepth,
                                        Damage = 0,
                                        Kind = AttackKind.Environmental,
                                    }),
                                },
                            },

                            // -- T2 "Quema" ----------------------------------------------------
                            // No se mueve ni dispara: es el unico turno en que se queda quieto, y
                            // eso es lo que lo hace matable. Enciende lo que marco el turno pasado.
                            Guarded(new AINode_IgniteArea
                            {
                                Definition = fire,
                                DurationRounds = FireDurationRounds,
                            }),
                        },
                    },
                },
            };
        }

        /// <summary>
        /// Envuelve un paso del turno en <c>Selector[paso, Wait]</c> — el idiom de aislamiento de
        /// fallos que ya usa el árbol del Sunken Grand.
        /// </summary>
        private static AINode_Selector Guarded(AIDecisionNode step)
        {
            return new AINode_Selector
            {
                Children = new List<AIDecisionNode> { step, new AINode_Wait() },
            };
        }

        // ======================================================================
        // Hazards
        // ======================================================================

        /// <summary>
        /// Crea/actualiza una definición de fuego de paño. Dos assets (uno por fase) y no un campo del
        /// nodo porque <see cref="IHazardService"/> toma la duración de la definición al activar:
        /// cambiarla desde el nodo pediría tocar el servicio, que es fundación compartida.
        /// </summary>
        /// <param name="flame">
        /// Llama persistente y burst de pisada. <c>null</c> deja el fuego como estaba —sólo el quad
        /// naranja—: el visual no es parte del contrato del hazard, así que un builder corrido sin el
        /// prefab construido no rompe la pelea.
        /// </param>
        public static HazardDefinitionSO BuildFireDefinition(
            string path, int durationRounds, string sourceId, GameObject flame = null)
        {
            var fire = LoadOrCreate<HazardDefinitionSO>(path);

            if (flame != null)
            {
                // Las dos mitades del fuego: la llama dice "esta casilla está ardiendo" mientras dura,
                // el burst dice "y te acaba de cobrar". Sin la primera, entre pisada y pisada el
                // sector encendido se ve igual que uno apagado.
                fire.PersistentVfxPrefab = flame;
                fire.TriggerVfxPrefab = flame;
                fire.TriggerVfxLifetime = FireBurstLifetime;
            }

            fire.Trigger = HazardTriggerMode.OnTurnEndInTile;
            // Explícito y no por default: el Croupier enciende sus propios sectores y su fila es
            // costura de dos bloques, así que arde bajo sus pies todos los turnos. Es el jefe al que
            // un rebuild silencioso le costaría la vida.
            fire.Affects = HazardAffects.PlayerOnly;
            fire.Damage = FireDamage;
            fire.Kind = AttackKind.Environmental;
            fire.DurationRounds = durationRounds;
            fire.ConsumeOnTrigger = false; // El fuego quema el bloque entero, no una casilla y se apaga.
            fire.OverlayTint = FireOverlayTint;
            fire.SourceId = sourceId;

            // El área real la pasa el nodo de ignición (el sector que detonó). Shape queda declarativa:
            // el overload con tiles la ignora, pero deja dicho en el asset de qué forma es el fuego.
            fire.Shape = ThreatShape.RoomSector;
            fire.Size = 1;
            fire.CycleRounds = 1;

            EditorUtility.SetDirty(fire);
            return fire;
        }

        /// <summary>
        /// Deja <see cref="FireVfxPrefabPath"/> listo clonando el mesh de fuego del arte.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Se clona en vez de referenciar <see cref="FireMeshPrefabPath"/> directo porque el hazard
        /// instancia y destruye el objeto por casilla: apuntar al prefab del arte lo ataría a un uso
        /// que no es el suyo, y cualquier ajuste de escala para la grilla se le volcaría encima.
        /// </para>
        /// <para>
        /// A diferencia del burst de hielo, esto <b>no</b> es un ParticleSystem: es un mesh con luces.
        /// Por eso no se retinta el <c>startColor</c> de nada — el color ya viene en <c>Mat_Fire</c>.
        /// </para>
        /// </remarks>
        public static GameObject BuildFireVfx()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FireVfxPrefabPath) == null)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(FireMeshPrefabPath) == null)
                {
                    Debug.LogWarning($"[CroupierAssetBuilder] No está el mesh de fuego en " +
                                     $"'{FireMeshPrefabPath}' — el paño queda ardiendo sin llama.");
                    return null;
                }

                EnsureFolder(Path.GetDirectoryName(FireVfxPrefabPath));
                if (!AssetDatabase.CopyAsset(FireMeshPrefabPath, FireVfxPrefabPath))
                {
                    Debug.LogError($"[CroupierAssetBuilder] Falló el clon de " +
                                   $"'{FireMeshPrefabPath}' a '{FireVfxPrefabPath}'.");
                    return null;
                }
                AssetDatabase.ImportAsset(FireVfxPrefabPath);
            }

            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(FireVfxPrefabPath);
        }

        // ======================================================================
        // Helpers de AssetDatabase
        // ======================================================================

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            EnsureFolder(Path.GetDirectoryName(path));
            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return;

            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder)) return;

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            var leaf = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
