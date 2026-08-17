using System.Collections.Generic;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Feedback;
using static Rollgeon.Feedback.BossFeedbackIds;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools
{
    /// <summary>
    /// Instalador de las entries de feedback de los seis jefes de casino
    /// (<c>Tools → Rollgeon → Bosses → Build Boss Feedback</c>). El <c>FeedbackDB</c> se
    /// autora a mano en el Inspector, así que el instalador hace upsert por
    /// <c>FeedbackId</c>: las 26 entries viejas quedan intactas y correrlo dos veces deja
    /// el asset igual.
    ///
    /// Los ids se exponen como <c>const</c> para que los nodos de IA los referencien sin
    /// escribir strings: un id mal tipeado no rompe nada, simplemente no suena.
    ///
    /// NO se autoran entries de SFX: el proyecto no tiene ni un clip de jefe, y una entry
    /// apuntando al clip genérico equivocado es peor que el silencio — se cuela en el mix
    /// y nadie la busca. Cuando lleguen los clips, agregar acá las <c>sfx.boss.*</c>.
    /// </summary>
    public static class BossFeedbackInstaller
    {
        private const string LogPrefix = "[BossFeedbackInstaller] ";
        private const string DbPath = "Assets/Rollgeon/Feedback/FeedbackDB.asset";

        private const string MeleeImpactVfxPath = "Assets/Prefabs/VFX/VFX_MeleeImpact.prefab";
        private const string RangedImpactVfxPath = "Assets/Prefabs/VFX/VFX_RangedImpact.prefab";
        private const string MeleeImpactFeelPath = "Assets/Prefabs/Feedbacks/MMF_EnemyMeleeImpact.prefab";
        private const string RangedImpactFeelPath = "Assets/Prefabs/Feedbacks/MMF_EnemyRangedImpact.prefab";

        // ================================================================
        // Ids — <channel>.boss.<jefe>.<acción>
        // ================================================================






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
                      $"{updated} re-escritas. Sin entries de SFX (no hay clips de jefe).");
        }

        private static IEnumerable<Spec> BuildSpecs()
        {
            // Los seis usan rigs prestados: cada AnimTrigger de acá existe en el Animator que
            // le tocó al jefe. Un trigger inventado no tira error, simplemente no pasa nada.

            // Croupier — Healer_Animated, sólo 'Attack'. Por eso el canto reusa el mismo golpe.
            yield return Spec.Anim(CroupierMeleeAnim, "Attack");
            yield return Spec.Anim(CroupierCantoAnim, "Attack");
            yield return Spec.Vfx(CroupierImpactVfx, MeleeImpactVfxPath);
            yield return Spec.Feel(CroupierImpactFeel, MeleeImpactFeelPath);

            // La confiscación reusa el impacto a distancia: el dado no lo agarra una mano, se lo
            // lleva el paño desde lejos, y ranged es el único de los dos que lee como "algo viajó".
            yield return Spec.Vfx(CroupierConfiscaVfx, RangedImpactVfxPath);
            yield return Spec.Feel(CroupierConfiscaFeel, RangedImpactFeelPath);

            // Bandida — MechaBoss_Animated. El brazo de la tragamonedas se lee como melee.
            yield return Spec.Anim(BandidaMeleeAnim, "Attack_Melee");
            yield return Spec.Anim(BandidaRangeAnim, "Attack_Range");
            yield return Spec.Anim(BandidaArmAnim, "Attack_Melee");
            yield return Spec.Vfx(BandidaImpactVfx, MeleeImpactVfxPath);
            yield return Spec.Vfx(BandidaRangeImpactVfx, RangedImpactVfxPath);
            yield return Spec.Feel(BandidaImpactFeel, MeleeImpactFeelPath);
            yield return Spec.Feel(BandidaRangeImpactFeel, RangedImpactFeelPath);

            // Cajero — GeneralDirector_Animated, sólo 'Attack'. El disparo es a distancia en
            // fiction pero comparte la animación; lo que lo distingue es el impacto ranged.
            yield return Spec.Anim(CajeroMeleeAnim, "Attack");
            yield return Spec.Anim(CajeroShotAnim, "Attack");
            yield return Spec.Vfx(CajeroImpactVfx, MeleeImpactVfxPath);
            yield return Spec.Vfx(CajeroShotImpactVfx, RangedImpactVfxPath);
            yield return Spec.Feel(CajeroImpactFeel, MeleeImpactFeelPath);
            yield return Spec.Feel(CajeroShotImpactFeel, RangedImpactFeelPath);

            // Anotador — ChestMimic, sólo 'Attack' y 'Awaken'. El lápiz reusa 'Attack'.
            yield return Spec.Anim(AnotadorMeleeAnim, "Attack");
            yield return Spec.Anim(AnotadorPencilAnim, "Attack");
            yield return Spec.Vfx(AnotadorImpactVfx, MeleeImpactVfxPath);
            yield return Spec.Feel(AnotadorImpactFeel, MeleeImpactFeelPath);

            // Generala — DiceBoss_Animated, el único rig con 'Roll' propio para la tirada.
            yield return Spec.Anim(GeneralaMeleeAnim, "Attack_Melee");
            yield return Spec.Anim(GeneralaRangeAnim, "Attack_Range");
            yield return Spec.Anim(GeneralaRollAnim, "Roll");
            yield return Spec.Anim(GeneralaCupSlamAnim, "Attack_Melee");
            // 'Heal' no cura a nadie acá: es el gesto de brazos en alto, y era el único clip del
            // DiceBoss que no usaba nadie. Reponer la mesa es lo más cerca de invocar que hace.
            yield return Spec.Anim(GeneralaSummonAnim, "Heal");
            // La escarcha comparte 'Attack_Range' con la mano: los cuatro triggers del DiceBoss ya
            // están tomados y el anillo de hielo cae lejos, que es lo que ese clip empuja.
            yield return Spec.Anim(GeneralaFrostAnim, "Attack_Range");
            yield return Spec.Vfx(GeneralaImpactVfx, MeleeImpactVfxPath);
            yield return Spec.Vfx(GeneralaRangeImpactVfx, RangedImpactVfxPath);
            yield return Spec.Feel(GeneralaImpactFeel, MeleeImpactFeelPath);
            yield return Spec.Feel(GeneralaRangeImpactFeel, RangedImpactFeelPath);

            // Tahúr — SunkedGrand_Animated. La banca tira a distancia, el pinche es de cerca.
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

            public static Spec Feel(string id, string prefabPath) =>
                new Spec(id, FeedbackType.Feel, SpawnPosition.AtTarget, Vector3.zero,
                         0.3f, null, prefabPath);
        }
    }
}
