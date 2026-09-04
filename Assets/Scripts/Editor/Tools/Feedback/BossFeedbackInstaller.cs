using System.Collections.Generic;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Feedback;
using static Rollgeon.Feedback.BossFeedbackIds;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools
{
    /// <summary>
    /// Instalador de las entries de feedback de los jefes de casino
    /// (<c>Tools → Rollgeon → Bosses → Build Boss Feedback</c>). El <c>FeedbackDB</c> se
    /// autora a mano en el Inspector, así que el instalador hace upsert por
    /// <c>FeedbackId</c>: las entries que no nombra quedan intactas y correrlo dos veces
    /// deja el asset igual.
    ///
    /// Los ids se exponen como <c>const</c> para que los nodos de IA los referencien sin
    /// escribir strings: un id mal tipeado no rompe nada, simplemente no suena.
    /// </summary>
    public static class BossFeedbackInstaller
    {
        private const string LogPrefix = "[BossFeedbackInstaller] ";
        private const string DbPath = "Assets/Rollgeon/Feedback/FeedbackDB.asset";

        private const string MeleeImpactVfxPath = "Assets/Prefabs/VFX/VFX_MeleeImpact.prefab";
        private const string RangedImpactVfxPath = "Assets/Prefabs/VFX/VFX_RangedImpact.prefab";
        private const string MeleeImpactFeelPath = "Assets/Prefabs/Feedbacks/MMF_EnemyMeleeImpact.prefab";
        private const string RangedImpactFeelPath = "Assets/Prefabs/Feedbacks/MMF_EnemyRangedImpact.prefab";

        [MenuItem("Tools/Rollgeon/Bosses/Build Boss Feedback")]
        public static void Install()
        {
            var db = AssetDatabase.LoadAssetAtPath<FeedbackDBSO>(DbPath);
            if (db == null)
            {
                Debug.LogError(LogPrefix + $"No se encontró '{DbPath}' — nada instalado.");
                return;
            }

            // El asset es YAML plano de Unity con la lista privada: SerializedObject es la
            // única vía de escritura que respeta el layout que autora el Inspector.
            var so = new SerializedObject(db);
            var entries = so.FindProperty("_entries");
            if (entries == null)
            {
                Debug.LogError(LogPrefix + "El FeedbackDB no expone '_entries' — ¿cambió el campo?");
                return;
            }

            int created = 0, updated = 0;
            foreach (var spec in BuildSpecs())
            {
                if (Upsert(entries, spec)) created++;
                else updated++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(db);
            // El cache de ids se arma en OnEnable/OnValidate; sin esto el DB sigue
            // resolviendo la lista vieja hasta el próximo domain reload.
            db.RebuildCache();
            AssetDatabase.SaveAssets();

            Debug.Log(LogPrefix + $"Feedback de jefes instalado: {created} entries nuevas, " +
                      $"{updated} re-escritas. Sin entries de SFX.");
        }

        private static IEnumerable<Spec> BuildSpecs()
        {
            // Los jefes usan rigs prestados, y cada AnimTrigger de acá tiene que existir en el
            // Animator que le tocó: un trigger que el Animator no declara no tira error — el jefe
            // cobra el daño y no se mueve.

            // Croupier — SunkedGrand_Animated, que declara 'Attack_Melee', 'Attack_Range' y
            // 'Teleport'.
            yield return Spec.Anim(CroupierRangeAnim, "Attack_Range");
            yield return Spec.Vfx(CroupierRangeImpactVfx, RangedImpactVfxPath);
            yield return Spec.Feel(CroupierRangeImpactFeel, RangedImpactFeelPath);

            // Un solo trigger para los dos tramos del rig: 'Teleport' encadena a 'Teleport_2', que
            // es la mitad de reaparecer.
            yield return Spec.Anim(CroupierTeleportAnim, "Teleport");

            // AINode_SpinWheel y AINode_DetonateSungSectors nombran estos ids por default: sacarlos
            // del DB los deja pidiendo entries inexistentes.
            yield return Spec.Anim(CroupierMeleeAnim, "Attack_Melee");
            yield return Spec.Anim(CroupierCantoAnim, "Attack_Range");
            // El estallido de las bombas se ancla en la casilla que revienta, no en un pawn: es la
            // unica ignicion del Croupier que no sale de el ni cae sobre el jugador.
            yield return Spec.VfxAtWorld(CroupierImpactVfx, MeleeImpactVfxPath);
            yield return Spec.Feel(CroupierImpactFeel, MeleeImpactFeelPath);

            yield return Spec.Vfx(CroupierConfiscaVfx, RangedImpactVfxPath);
            yield return Spec.Feel(CroupierConfiscaFeel, RangedImpactFeelPath);

            // Bandida — MechaBoss_Animated.
            yield return Spec.Anim(BandidaMeleeAnim, "Attack_Melee");
            yield return Spec.Anim(BandidaRangeAnim, "Attack_Range");
            yield return Spec.Anim(BandidaArmAnim, "Attack_Melee");
            yield return Spec.Vfx(BandidaImpactVfx, MeleeImpactVfxPath);
            yield return Spec.Vfx(BandidaRangeImpactVfx, RangedImpactVfxPath);
            yield return Spec.Feel(BandidaImpactFeel, MeleeImpactFeelPath);
            yield return Spec.Feel(BandidaRangeImpactFeel, RangedImpactFeelPath);

            // Cajero — MechaBoss_Animated, el mismo rig que la Bandida.
            yield return Spec.Anim(CajeroMeleeAnim, "Attack_Melee");
            yield return Spec.Anim(CajeroShotAnim, "Attack_Range");
            yield return Spec.Anim(CajeroShoveAnim, "Attack_Push");
            // 'Idle_Var' es el gesto de recarga: sólo el telegraph lo usa.
            yield return Spec.Anim(CajeroAimAnim, "Idle_Var");
            yield return Spec.Vfx(CajeroImpactVfx, MeleeImpactVfxPath);
            yield return Spec.Vfx(CajeroShotImpactVfx, RangedImpactVfxPath);
            yield return Spec.Feel(CajeroImpactFeel, MeleeImpactFeelPath);
            yield return Spec.Feel(CajeroShotImpactFeel, RangedImpactFeelPath);

            // La Comisión viste GeneralDirector_Animated, que declara un solo 'Attack': por eso no
            // puede reusar los ids del Cajero, que piden 'Attack_Range'.
            yield return Spec.Anim(ComisionBiteAnim, "Attack");

            // Anotador — ChestMimic, que sólo declara 'Attack' y 'Awaken'.
            yield return Spec.Anim(AnotadorMeleeAnim, "Attack");
            yield return Spec.Anim(AnotadorPencilAnim, "Attack");
            yield return Spec.Vfx(AnotadorImpactVfx, MeleeImpactVfxPath);
            yield return Spec.Feel(AnotadorImpactFeel, MeleeImpactFeelPath);

            // Generala — DiceBoss_Animated, que declara 'Roll' además de los dos ataques.
            yield return Spec.Anim(GeneralaMeleeAnim, "Attack_Melee");
            yield return Spec.Anim(GeneralaRangeAnim, "Attack_Range");
            yield return Spec.Anim(GeneralaRollAnim, "Roll");
            yield return Spec.Anim(GeneralaCupSlamAnim, "Attack_Melee");
            // 'Heal' no cura a nadie acá: en el DiceBoss es el gesto de brazos en alto.
            yield return Spec.Anim(GeneralaSummonAnim, "Heal");
            // La escarcha comparte 'Attack_Range' con la mano: el DiceBoss no declara más triggers.
            yield return Spec.Anim(GeneralaFrostAnim, "Attack_Range");
            yield return Spec.Vfx(GeneralaImpactVfx, MeleeImpactVfxPath);
            yield return Spec.Vfx(GeneralaRangeImpactVfx, RangedImpactVfxPath);
            yield return Spec.Feel(GeneralaImpactFeel, MeleeImpactFeelPath);
            yield return Spec.Feel(GeneralaRangeImpactFeel, RangedImpactFeelPath);

            // Tahúr — SunkedGrand_Animated.
            yield return Spec.Anim(TahurMeleeAnim, "Attack_Melee");
            yield return Spec.Anim(TahurRangeAnim, "Attack_Range");
            yield return Spec.Anim(TahurPokeAnim, "Attack_Melee");
            yield return Spec.Anim(TahurBancaAnim, "Attack_Range");
            yield return Spec.Vfx(TahurImpactVfx, MeleeImpactVfxPath);
            yield return Spec.Vfx(TahurRangeImpactVfx, RangedImpactVfxPath);
            yield return Spec.Feel(TahurImpactFeel, MeleeImpactFeelPath);
            yield return Spec.Feel(TahurRangeImpactFeel, RangedImpactFeelPath);
        }

        /// <returns><c>true</c> si la entry se creó; <c>false</c> si ya existía y se re-escribió.</returns>
        private static bool Upsert(SerializedProperty entries, Spec spec)
        {
            var element = Find(entries, spec.Id);
            bool created = element == null;
            if (created)
            {
                entries.InsertArrayElementAtIndex(entries.arraySize);
                element = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            }
            Write(element, spec);
            return created;
        }

        private static SerializedProperty Find(SerializedProperty entries, string id)
        {
            for (int i = 0; i < entries.arraySize; i++)
            {
                var element = entries.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative("FeedbackId").stringValue == id) return element;
            }
            return null;
        }

        /// <summary>
        /// Escribe TODOS los campos, incluidos los que el <see cref="FeedbackType"/> ignora:
        /// <c>InsertArrayElementAtIndex</c> clona el elemento anterior, así que lo que no se
        /// pisa acá queda con el valor del vecino.
        /// </summary>
        private static void Write(SerializedProperty element, Spec spec)
        {
            element.FindPropertyRelative("FeedbackId").stringValue = spec.Id;
            element.FindPropertyRelative("Type").intValue = (int)spec.Type;
            element.FindPropertyRelative("Position").intValue = (int)spec.Position;
            element.FindPropertyRelative("PositionReaderSO").objectReferenceValue = null;
            element.FindPropertyRelative("PlayerTarget").intValue = (int)FeedbackPlayer.Player;
            element.FindPropertyRelative("PositionOffset").vector3Value = spec.Offset;
            element.FindPropertyRelative("Duration").floatValue = spec.Duration;
            element.FindPropertyRelative("CompletionMode").intValue = (int)FeedbackCompletionMode.Timer;

            element.FindPropertyRelative("VfxPrefab").objectReferenceValue =
                spec.Type == FeedbackType.VFX ? LoadPrefab(spec.PrefabPath) : null;
            element.FindPropertyRelative("ShouldDestroyOnParticleEnd").boolValue = false;

            element.FindPropertyRelative("AudioClip").objectReferenceValue = null;
            element.FindPropertyRelative("Volume").floatValue = 1f;

            element.FindPropertyRelative("AnimTrigger").stringValue = spec.AnimTrigger ?? string.Empty;
            element.FindPropertyRelative("TargetSourcePawn").boolValue = true;

            element.FindPropertyRelative("FeelPlayerPrefab").objectReferenceValue =
                spec.Type == FeedbackType.Feel ? LoadFeelPlayer(spec.PrefabPath) : null;
            element.FindPropertyRelative("FeelIntensity").floatValue = 1f;

            element.FindPropertyRelative("DeathSpinDegrees").floatValue = 720f;
            element.FindPropertyRelative("DeathEndScale").floatValue = 0f;
            element.FindPropertyRelative("DeathRiseHeight").floatValue = 0.35f;
            element.FindPropertyRelative("DeathHideHealthBar").boolValue = true;

            element.FindPropertyRelative("BehaviorValueKey").intValue = (int)BehaviorValueKey.None;
            element.FindPropertyRelative("ValueTarget").intValue = (int)BehaviorValueTarget.Target;
            element.FindPropertyRelative("FloatingNumberSourceKey").intValue =
                (int)BehaviorValueKey.FloatingDamage;
        }

        private static GameObject LoadPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                Debug.LogWarning(LogPrefix + $"Falta el prefab '{path}' — la entry queda sin referencia.");
            return prefab;
        }

        /// <summary>
        /// El campo apunta al componente <c>MMF_Player</c>, no al GameObject, pero el asmdef del
        /// Editor no referencia <c>MoreMountains.Feedbacks</c>: se resuelve por nombre de tipo
        /// para no tener que tocar el asmdef.
        /// </summary>
        private static UnityEngine.Object LoadFeelPlayer(string path)
        {
            var prefab = LoadPrefab(path);
            if (prefab == null) return null;

            foreach (var component in prefab.GetComponents<Component>())
            {
                if (component != null && component.GetType().Name == "MMF_Player") return component;
            }

            Debug.LogWarning(LogPrefix + $"'{path}' no tiene MMF_Player — la entry queda sin referencia.");
            return null;
        }

        private readonly struct Spec
        {
            public readonly string Id;
            public readonly FeedbackType Type;
            public readonly SpawnPosition Position;
            public readonly Vector3 Offset;
            public readonly float Duration;
            public readonly string AnimTrigger;
            public readonly string PrefabPath;

            private Spec(string id, FeedbackType type, SpawnPosition position, Vector3 offset,
                         float duration, string animTrigger, string prefabPath)
            {
                Id = id;
                Type = type;
                Position = position;
                Offset = offset;
                Duration = duration;
                AnimTrigger = animTrigger;
                PrefabPath = prefabPath;
            }

            public static Spec Anim(string id, string trigger) =>
                new Spec(id, FeedbackType.Animation, SpawnPosition.AtSource, Vector3.zero,
                         0.9f, trigger, null);

            // El offset en Y sube el impacto del piso al torso, como las entries de enemigo.
            public static Spec Vfx(string id, string prefabPath) =>
                new Spec(id, FeedbackType.VFX, SpawnPosition.AtTarget, new Vector3(0f, 1f, 0f),
                         0.55f, null, prefabPath);

            /// <summary>
            /// Chispazo anclado en una casilla y no en un pawn: la posicion la trae el caller en
            /// <c>FeedbackRequest.WorldPosition</c>. El offset en Y es la mitad del de los pawns —
            /// esto nace del piso, no de un torso.
            /// </summary>
            public static Spec VfxAtWorld(string id, string prefabPath) =>
                new Spec(id, FeedbackType.VFX, SpawnPosition.WorldPosition, new Vector3(0f, 0.5f, 0f),
                         0.55f, null, prefabPath);

            public static Spec Feel(string id, string prefabPath) =>
                new Spec(id, FeedbackType.Feel, SpawnPosition.AtTarget, Vector3.zero,
                         0.3f, null, prefabPath);
        }
    }
}
