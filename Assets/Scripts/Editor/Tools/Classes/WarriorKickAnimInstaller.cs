using System.Collections.Generic;
using System.Reflection;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Feedback;
using Rollgeon.Heroes;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools.Classes
{
    /// <summary>
    /// Cablea la animación de patada del Warrior a su Class Skill. El controller ya
    /// rutea el trigger <c>Ability</c> al estado Kick (clip 0.93s); lo que faltaba era
    /// el lado gameplay: el <c>EffPlaySequence</c> del Class Skill reusaba
    /// <c>anim.player.warrior.attack</c>. Esta tool crea la entry
    /// <c>anim.player.warrior.kick</c> (AnimTrigger = Ability) en el FeedbackDB y
    /// re-apunta el step del skill en CH_Warrior y CH_Warrior_Tutorial.
    /// </summary>
    /// <remarks>
    /// Por MenuItem y NO editando YAML: los hero SOs son Odin
    /// (<c>SerializedScriptableObject</c>) — tocarlos a mano renumera los
    /// SerializationNodes y deserializa null en silencio. Idempotente.
    /// </remarks>
    public static class WarriorKickAnimInstaller
    {
        private const string FeedbackDbPath = "Assets/Rollgeon/Feedback/FeedbackDB.asset";
        private const string KickFeedbackId = "anim.player.warrior.kick";
        private const string AttackFeedbackId = "anim.player.warrior.attack";
        private const string AbilityTrigger = "Ability";

        private static readonly string[] HeroPaths =
        {
            "Assets/Rollgeon/Classes/CH_Warrior.asset",
            "Assets/Rollgeon/Tutorial/CH_Warrior_Tutorial.asset",
        };

        [MenuItem("Rollgeon/Classes/Wire Warrior Kick Anim")]
        public static void Wire()
        {
            if (!UpsertKickFeedbackEntry()) return;

            foreach (var path in HeroPaths)
                RetargetClassSkillAnim(path);

            AssetDatabase.SaveAssets();
            Debug.Log("[WarriorKickAnim] Listo — Class Skill dispara 'Ability' (estado Kick).");
        }

        /// <summary>Entry de animación gemela de la del attack, con el trigger del Kick.</summary>
        private static bool UpsertKickFeedbackEntry()
        {
            var db = AssetDatabase.LoadAssetAtPath<FeedbackDBSO>(FeedbackDbPath);
            if (db == null)
            {
                Debug.LogError($"[WarriorKickAnim] No hay FeedbackDBSO en {FeedbackDbPath}.");
                return false;
            }

            var entriesField = typeof(FeedbackDBSO).GetField(
                "_entries", BindingFlags.NonPublic | BindingFlags.Instance);
            var entries = (List<FeedbackEntry>)entriesField.GetValue(db);

            foreach (var e in entries)
            {
                if (e.FeedbackId != KickFeedbackId) continue;
                // Re-correr re-afirma el trigger por si alguien lo pisó en el inspector.
                e.Type = FeedbackType.Animation;
                e.AnimTrigger = AbilityTrigger;
                MarkDbDirty(db);
                return true;
            }

            entries.Add(new FeedbackEntry
            {
                FeedbackId = KickFeedbackId,
                Type = FeedbackType.Animation,
                AnimTrigger = AbilityTrigger,
                TargetSourcePawn = true,
                // Misma ventana que anim.player.warrior.attack (0.95): el clip del Kick
                // dura 0.93 y el gate de turno espera este timer.
                Duration = 0.95f,
                CompletionMode = FeedbackCompletionMode.Timer,
            });
            MarkDbDirty(db);
            return true;
        }

        private static void MarkDbDirty(FeedbackDBSO db)
        {
            db.RebuildCache();
            EditorUtility.SetDirty(db);
        }

        private static void RetargetClassSkillAnim(string heroPath)
        {
            var hero = AssetDatabase.LoadAssetAtPath<ClassHeroSO>(heroPath);
            if (hero == null)
            {
                Debug.LogWarning($"[WarriorKickAnim] No se pudo cargar {heroPath} — salteado.");
                return;
            }

            int swapped = 0;
            foreach (var behavior in hero.PhaseBehaviors ?? new List<HeroActionBehavior>())
            {
                if (behavior == null || behavior.Slot != HeroBehaviorSlot.ClassSkill) continue;
                foreach (var group in behavior.Effects ?? new List<EffectData>())
                {
                    if (group?.Effects == null) continue;
                    foreach (var eff in group.Effects)
                        swapped += RetargetInEffect(eff);
                }
            }

            if (swapped > 0)
            {
                EditorUtility.SetDirty(hero);
                Debug.Log($"[WarriorKickAnim] {heroPath}: {swapped} step(s) → {KickFeedbackId}.");
            }
            else
            {
                Debug.Log($"[WarriorKickAnim] {heroPath}: sin steps que re-apuntar (¿ya corrió?).");
            }
        }

        // Recursivo vía EffectTree: el sequence del skill puede vivir anidado en un
        // EffChain (mismo walk que hace HeroActionTooltip para los textos).
        private static int RetargetInEffect(IEffect eff)
        {
            int swapped = 0;

            if (eff is EffPlaySequence sequence)
            {
                var stepsField = typeof(EffPlaySequence).GetField(
                    "_steps", BindingFlags.NonPublic | BindingFlags.Instance);
                var steps = (List<FeedbackSequenceStep>)stepsField.GetValue(sequence);
                foreach (var step in steps ?? new List<FeedbackSequenceStep>())
                {
                    if (step == null || step.FeedbackRefId != AttackFeedbackId) continue;
                    step.FeedbackRefId = KickFeedbackId;
                    swapped++;
                }
            }

            foreach (var child in EffectTree.DirectChildren(eff))
                swapped += RetargetInEffect(child);

            return swapped;
        }
    }
}
