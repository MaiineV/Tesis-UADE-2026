using System.Collections.Generic;
using Rollgeon.Combat.Skills.Push;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Selection;
using Rollgeon.Feedback;
using Rollgeon.Heroes;
using Rollgeon.Patterns.Bootstrap;
using Rollgeon.UI.HUD;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Heroes
{
    /// <summary>
    /// Instalador idempotente de la Habilidad de Clase del Guerrero — Empuje (Feature#0055):
    /// <c>Rollgeon → Heroes → Install Warrior Class Skill</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Los <c>ClassHeroSO</c> son Odin-serializados: editar el YAML a mano no round-tripea
    /// (doble representación). Por eso el behavior se re-autora por código, igual que
    /// <c>TutorialAssetInstaller.TuneForceDoor</c>.
    /// </para>
    /// <para>
    /// Qué hace: (1) crea/actualiza la tabla <c>ClassSkillPushTable_Warrior</c> con los
    /// valores del GDD; (2) crea el <c>ClassSkillPushResolverBootstrap</c> y lo registra en
    /// <c>ServiceBootstrap.ExtraServices</c>; (3) en <c>CH_Warrior</c> y su clon de tutorial,
    /// toma el behavior base del slot <see cref="HeroBehaviorSlot.ClassSkill"/> (ex Special
    /// Attack) y reemplaza cada <see cref="EffDealDamage"/> de su árbol por un
    /// <see cref="EffClassSkillPush"/> con selección adyacente a enemigos. Conserva el
    /// <c>EffChain</c> y el <c>EffPlaySequence</c> (animación de golpe) que ya tenía.
    /// </para>
    /// </remarks>
    public static class WarriorClassSkillInstaller
    {
        private const string LogPrefix = "[WarriorClassSkillInstaller] ";

        private const string WarriorPath = "Assets/Rollgeon/Classes/CH_Warrior.asset";
        private const string WarriorTutorialPath = "Assets/Rollgeon/Tutorial/CH_Warrior_Tutorial.asset";
        private const string TablePath = "Assets/Rollgeon/Classes/ClassSkillPushTable_Warrior.asset";
        private const string BootstrapPath = "Assets/Rollgeon/Combat/ClassSkillPushResolverBootstrap.asset";
        private const string ServiceBootstrapPath = "Assets/Rollgeon/ServiceBootstrap.asset";

        private const string ActionName = "Class Skill";
        private const string PhaseLabel = "Push";

        [MenuItem("Rollgeon/Heroes/Install Warrior Class Skill")]
        public static void Install()
        {
            var table = EnsureTable();
            var bootstrap = EnsureBootstrap();
            RegisterBootstrap(bootstrap);

            int patched = 0;
            patched += PatchHero(WarriorPath, table, required: true);
            patched += PatchHero(WarriorTutorialPath, table, required: false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(LogPrefix + $"Listo. Héroes parcheados: {patched}. Tabla: {TablePath}. Bootstrap: {BootstrapPath}.");
        }

        // ------------------------------------------------------------------

        private static ClassSkillPushTableSO EnsureTable()
        {
            var table = AssetDatabase.LoadAssetAtPath<ClassSkillPushTableSO>(TablePath);
            if (table == null)
            {
                table = ClassSkillPushTableSO.CreateDefault();
                AssetDatabase.CreateAsset(table, TablePath);
                Debug.Log(LogPrefix + "Tabla creada con los valores del GDD.");
            }
            else if (table.Entries == null || table.Entries.Count == 0)
            {
                // Tabla vacía = nunca autorada: cargar la spec. Una tabla con valores se respeta
                // (balance puede haberla tocado).
                table.ResetToSpec();
                Debug.Log(LogPrefix + "Tabla vacía — cargada con los valores del GDD.");
            }
            EditorUtility.SetDirty(table);
            return table;
        }

        private static ClassSkillPushResolverBootstrap EnsureBootstrap()
        {
            var bootstrap = AssetDatabase.LoadAssetAtPath<ClassSkillPushResolverBootstrap>(BootstrapPath);
            if (bootstrap != null) return bootstrap;

            bootstrap = ScriptableObject.CreateInstance<ClassSkillPushResolverBootstrap>();
            AssetDatabase.CreateAsset(bootstrap, BootstrapPath);
            Debug.Log(LogPrefix + "Bootstrap del resolver creado.");
            return bootstrap;
        }

        private static void RegisterBootstrap(ClassSkillPushResolverBootstrap bootstrap)
        {
            var serviceBootstrap = AssetDatabase.LoadAssetAtPath<ServiceBootstrapSO>(ServiceBootstrapPath);
            if (serviceBootstrap == null)
            {
                Debug.LogError(LogPrefix + $"No se encontró {ServiceBootstrapPath} — registrá el bootstrap a mano.");
                return;
            }

            serviceBootstrap.ExtraServices ??= new List<IPreloadableService>();
            if (!serviceBootstrap.ExtraServices.Contains(bootstrap))
            {
                serviceBootstrap.ExtraServices.Add(bootstrap);
                EditorUtility.SetDirty(serviceBootstrap);
                Debug.Log(LogPrefix + "Bootstrap agregado a ServiceBootstrap.ExtraServices.");
            }
        }

        private static int PatchHero(string path, ClassSkillPushTableSO table, bool required)
        {
            var hero = AssetDatabase.LoadAssetAtPath<ClassHeroSO>(path);
            if (hero == null)
            {
                if (required) Debug.LogError(LogPrefix + $"No se encontró {path}.");
                else Debug.LogWarning(LogPrefix + $"No se encontró {path} — se omite.");
                return 0;
            }

            HeroActionBehavior behavior = null;
            if (hero.PhaseBehaviors != null)
            {
                foreach (var b in hero.PhaseBehaviors)
                {
                    if (b != null && b.IsBaseBehavior && b.Slot == HeroBehaviorSlot.ClassSkill) { behavior = b; break; }
                }
            }

            if (behavior == null)
            {
                Debug.LogError(LogPrefix + $"{hero.name} no tiene behavior base en el slot ClassSkill (2).");
                return 0;
            }

            behavior.ActionName = ActionName;
            behavior.NeedsDiceRoll = true;
            behavior.AllowsReroll = true;
            behavior.BoardType = DiceBoardType.Attack;

            int replaced = 0;
            if (behavior.Effects == null || behavior.Effects.Count == 0)
            {
                behavior.Effects = new List<EffectData> { BuildFreshChain(table) };
                replaced = 1;
            }
            else
            {
                foreach (var group in behavior.Effects)
                {
                    if (group?.Effects == null) continue;
                    replaced += ReplaceDealDamage(group.Effects, table);
                    foreach (var eff in group.Effects)
                    {
                        if (eff is EffChain chain && chain.Phases != null)
                            foreach (var phase in chain.Phases)
                                if (phase != null && phase.Label == "Attack") phase.Label = PhaseLabel;
                    }
                }

                if (replaced == 0 && !ContainsPush(behavior))
                {
                    // Behavior sin daño ni empuje (autoría inesperada): se reemplaza entero.
                    behavior.Effects = new List<EffectData> { BuildFreshChain(table) };
                    replaced = 1;
                }
            }

            // Reapuntar la tabla en los efectos ya instalados (idempotencia: corridas
            // posteriores solo refrescan la referencia).
            foreach (var push in FindPushes(behavior))
                push.Table = table;

            // La selección que gatea el botón y apunta el target es la PRIMERA BeforeRoll de
            // la fase 0 (EffChain.FindPhaseSelectionAt) — en el Warrior es la del
            // EffPlaySequence que envuelve al efecto, no la del efecto. Todo nodo del árbol
            // que pida selección pre-roll pasa a exigir adyacencia con un enemigo.
            int selections = 0;
            foreach (var group in behavior.Effects)
            {
                if (group?.Effects == null) continue;
                foreach (var eff in group.Effects)
                {
                    foreach (var node in EffectTree.SelfAndDescendants(eff))
                    {
                        if (node is EffChain) continue; // selección fantasma, oculta e ignorada
                        if (node is BaseEffect baseEffect
                            && (baseEffect is EffClassSkillPush
                                || baseEffect.RequiresSelectionAt(SelectionTiming.BeforeRoll)))
                        {
                            ConfigureSelection(baseEffect.Selection);
                            selections++;
                        }
                    }
                }
            }
            Debug.Log(LogPrefix + $"{hero.name}: selecciones pre-roll reconfiguradas a adyacencia: {selections}.");

            EditorUtility.SetDirty(hero);
            Debug.Log(LogPrefix + $"{hero.name}: slot ClassSkill → '{ActionName}', efectos reemplazados: {replaced}.");
            return 1;
        }

        /// <summary>Reemplaza in-place cada EffDealDamage del árbol por un EffClassSkillPush.</summary>
        private static int ReplaceDealDamage(List<IEffect> effects, ClassSkillPushTableSO table)
        {
            int count = 0;
            for (int i = 0; i < effects.Count; i++)
            {
                var eff = effects[i];
                switch (eff)
                {
                    case EffDealDamage:
                        effects[i] = BuildPush(table);
                        count++;
                        break;
                    case EffChain chain when chain.Phases != null:
                        foreach (var phase in chain.Phases)
                            if (phase?.Effects?.Effects != null)
                                count += ReplaceDealDamage(phase.Effects.Effects, table);
                        break;
                    case EffPlaySequence sequence when sequence.Steps != null:
                        foreach (var step in sequence.Steps)
                        {
                            if (step == null || step.Source != StepSource.InlineEffect) continue;
                            if (step.InlineEffects?.Effects != null)
                                count += ReplaceDealDamage(step.InlineEffects.Effects, table);
                        }
                        break;
                }
            }
            return count;
        }

        private static bool ContainsPush(HeroActionBehavior behavior)
        {
            foreach (var _ in FindPushes(behavior)) return true;
            return false;
        }

        private static IEnumerable<EffClassSkillPush> FindPushes(HeroActionBehavior behavior)
        {
            if (behavior.Effects == null) yield break;
            foreach (var group in behavior.Effects)
            {
                if (group?.Effects == null) continue;
                foreach (var eff in group.Effects)
                    foreach (var node in EffectTree.SelfAndDescendants(eff))
                        if (node is EffClassSkillPush push) yield return push;
            }
        }

        private static EffectData BuildFreshChain(ClassSkillPushTableSO table)
        {
            var phase = new ChainPhase { Label = PhaseLabel };
            phase.Effects.Effects.Add(BuildPush(table));
            var chain = new EffChain();
            chain.Phases.Add(phase);
            var group = new EffectData { Label = "Class Skill" };
            group.Effects.Add(chain);
            return group;
        }

        private static EffClassSkillPush BuildPush(ClassSkillPushTableSO table)
        {
            var push = new EffClassSkillPush { Table = table };
            ConfigureSelection(push.Selection);
            return push;
        }

        /// <summary>Adyacencia con un enemigo, elegida antes de tirar (compromiso ciego).</summary>
        private static void ConfigureSelection(SelectionSettings selection)
        {
            if (selection == null) return;
            selection.SlotState = SlotState.Occupied;
            selection.Timing = SelectionTiming.BeforeRoll;
            selection.EntityFilter = EntityFilterMask.Enemies;
            selection.IsGlobal = false;
            selection.Range = 1;
            selection.RangeMode = RangeMode.Manhattan;
            selection.RangeFromMovementDie = false;
            selection.TargetMode = TargetMode.Single;
            selection.IsConstantSelectionCount = true;
            selection.SelectionCount = 1;
            selection.AutoResolve = false;
            selection.AutoAccept = true;
        }
    }
}
