using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combos;
using Rollgeon.Combos.Play;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Readers;
using Rollgeon.Heroes;
using Rollgeon.Player;
using Rollgeon.Upgrades.Dice;
using UnityEngine;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// Tests del hook de item <see cref="PassiveHookKind.ComboPlayed"/>: dispara al
    /// jugar un combo (no en preview), filtra por combo y por owner, se desengancha
    /// con el item, y puede inyectar bono al daño del golpe en curso.
    /// </summary>
    [TestFixture]
    public class ComboPlayedItemHookTests
    {
        private InventoryService _service;
        private Guid _playerGuid;
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            TypedEvent<ComboPlayedPayload>.Clear();
            _playerGuid = Guid.NewGuid();
            ServiceLocator.AddService<IPlayerService>(new StubPlayerService(_playerGuid));
            _service = new InventoryService(null, 4);
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            _service = null;

            foreach (var o in _created)
            {
                if (o != null) UnityEngine.Object.DestroyImmediate(o);
            }
            _created.Clear();

            ServiceLocator.Clear();
            TypedEvent<ComboPlayedPayload>.Clear();
        }

        // ================================================================
        // Helpers
        // ================================================================

        private ItemSO NewComboPlayedPassive(string id, RecordingEffect effect, params string[] comboIds)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = id;
            item.DisplayName = id;
            item.Type = ItemType.Passive;

            var hook = new PassiveItemHook
            {
                Kind = PassiveHookKind.ComboPlayed,
                ComboFilter = comboIds.Length == 0
                    ? new ComboFilter { Mode = ComboFilterMode.AnyCombo }
                    : new ComboFilter { Mode = ComboFilterMode.ComboIds, ComboIds = new List<string>(comboIds) },
            };
            hook.Effect.Effects.Add(effect);
            item.PassiveHooks.Add(hook);

            _created.Add(item);
            return item;
        }

        private ComboPlayedPayload BuildPayload(string comboId, Guid? source = null)
        {
            return new ComboPlayedPayload
            {
                SourceGuid = source ?? _playerGuid,
                ComboId = comboId,
                ComboResult = ComboDetectionResult.Match(comboId, baseDamage: 10, countUsed: 2,
                    contributingIndices: new[] { 0, 1 }),
                DiceResult = new[] { 2, 2, 5 },
            };
        }

        private sealed class RecordingEffect : BaseEffect
        {
            public int ApplyCount;
            public EffectContext LastContext;

            protected override bool ShowSelection => false;

            public override bool ApplyEffect(EffectContext context)
            {
                ApplyCount++;
                LastContext = context;
                return true;
            }
        }

        private sealed class StubPlayerService : IPlayerService
        {
            public StubPlayerService(Guid guid) { PlayerGuid = guid; }
            public Guid PlayerGuid { get; }
            public Guid RunId => Guid.Empty;
            public ClassHeroSO CurrentHero => null;
            public DiceBagSO DiceBag => null;
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) { }
            public void ClearPlayer() { }
            public event Action<ClassHeroSO> OnPlayerSet { add { } remove { } }
            public event Action OnPlayerCleared { add { } remove { } }
        }

        // ================================================================
        // Dispatch + filtros
        // ================================================================

        [Test]
        public void ComboPlayedHook_ExecutesEffect_WhenFilteredComboIsPlayed()
        {
            // Arrange — pasiva que escucha SOLO combo.par (criterio del goal: filtro por combo).
            var effect = new RecordingEffect();
            _service.AddItem(NewComboPlayedPassive("item.par", effect, "combo.par"));

            // Act
            TypedEvent<ComboPlayedPayload>.Raise(BuildPayload("combo.par"));

            // Assert
            Assert.AreEqual(1, effect.ApplyCount);
            Assert.IsTrue(effect.LastContext.ComboResult.HasValue);
            Assert.AreEqual("combo.par", effect.LastContext.ComboResult.Value.ComboId);
        }

        [Test]
        public void ComboPlayedHook_DoesNotFire_ForOtherCombo()
        {
            // Arrange
            var effect = new RecordingEffect();
            _service.AddItem(NewComboPlayedPassive("item.par", effect, "combo.par"));

            // Act — se juega Trío, la pasiva escucha Par.
            TypedEvent<ComboPlayedPayload>.Raise(BuildPayload("combo.trio"));

            // Assert
            Assert.AreEqual(0, effect.ApplyCount);
        }

        [Test]
        public void ComboPlayedHook_AnyComboFilter_FiresForEveryCombo()
        {
            // Arrange
            var effect = new RecordingEffect();
            _service.AddItem(NewComboPlayedPassive("item.any", effect));

            // Act
            TypedEvent<ComboPlayedPayload>.Raise(BuildPayload("combo.par"));
            TypedEvent<ComboPlayedPayload>.Raise(BuildPayload("combo.trio"));

            // Assert
            Assert.AreEqual(2, effect.ApplyCount);
        }

        [Test]
        public void ComboPlayedHook_IgnoresForeignSourceGuid()
        {
            // Arrange
            var effect = new RecordingEffect();
            _service.AddItem(NewComboPlayedPassive("item.par", effect, "combo.par"));

            // Act — combo jugado por otra entidad (no el player).
            TypedEvent<ComboPlayedPayload>.Raise(BuildPayload("combo.par", Guid.NewGuid()));

            // Assert
            Assert.AreEqual(0, effect.ApplyCount);
        }

        [Test]
        public void RemoveItem_UnbindsComboPlayedHook()
        {
            // Arrange
            var effect = new RecordingEffect();
            _service.AddItem(NewComboPlayedPassive("item.par", effect, "combo.par"));

            // Act
            _service.RemoveItem("item.par");
            TypedEvent<ComboPlayedPayload>.Raise(BuildPayload("combo.par"));

            // Assert
            Assert.AreEqual(0, effect.ApplyCount);
        }

        [Test]
        public void LegacyHook_DefaultKind_IsEventBus()
        {
            // Los assets existentes deserializan Kind ausente como default (0) —
            // el path legacy no cambia.
            Assert.AreEqual(PassiveHookKind.EventBus, new PassiveItemHook().Kind);
        }

        // ================================================================
        // Bono al golpe en curso (integración con la ventana de combo jugado)
        // ================================================================

        [Test]
        public void ComboPlayedHook_EffAddComboBonus_WritesToPlayScratch()
        {
            // Arrange — item con EffAddComboBonus(9) escuchando combo.par.
            var play = new ComboPlayService();
            play.Register();

            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = "item.bonus";
            item.DisplayName = "item.bonus";
            item.Type = ItemType.Passive;
            var hook = new PassiveItemHook
            {
                Kind = PassiveHookKind.ComboPlayed,
                ComboFilter = new ComboFilter { Mode = ComboFilterMode.ComboIds, ComboIds = new List<string> { "combo.par" } },
            };
            hook.Effect.Effects.Add(new EffAddComboBonus { Amount = new ReadConstantInt { Value = 9 } });
            item.PassiveHooks.Add(hook);
            _created.Add(item);
            _service.AddItem(item);

            // Act — la ventana emite el payload; el hook corre adentro y escribe al play scratch.
            play.BeginPlay(new EffectContext
            {
                SourceGuid = _playerGuid,
                DiceResult = new[] { 2, 2, 5 },
                ComboResult = ComboDetectionResult.Match("combo.par", baseDamage: 10, countUsed: 2,
                    contributingIndices: new[] { 0, 1 }),
            });

            // Assert — el bono del item queda listo para PlayerComboDamage.Resolve.
            Assert.AreEqual(9, play.CurrentPlayScratch.BonusComboDamage);

            play.EndPlay();
            play.Dispose();
        }

        // ================================================================
        // Cura desde un item (Fix#0051 — las Lágrimas curaban al enemigo)
        // ================================================================

        /// <summary>
        /// El hook ComboPlayed viaja con <c>TargetGuid</c> = objetivo de la acción (el enemigo
        /// atacado). Un <c>EffHeal</c> con self-heal tiene que ignorarlo y curar al jugador:
        /// con <c>EffModifyIntAttribute</c> Health/Add la cura caía sobre ese target.
        /// </summary>
        [Test]
        public void ComboPlayedHook_EffHeal_HealsThePlayer_NotTheActionTarget()
        {
            // Arrange
            var pipeline = new SpyHealPipeline();
            ServiceLocator.AddService<Rollgeon.Combat.Pipelines.IHealPipeline>(pipeline);

            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = "item.lagrima";
            item.DisplayName = "item.lagrima";
            item.Type = ItemType.Passive;
            var hook = new PassiveItemHook
            {
                Kind = PassiveHookKind.ComboPlayed,
                ComboFilter = new ComboFilter { Mode = ComboFilterMode.ComboIds, ComboIds = new List<string> { "combo.par" } },
            };
            hook.Effect.Effects.Add(new EffHeal());
            item.PassiveHooks.Add(hook);
            _created.Add(item);
            _service.AddItem(item);

            var enemyGuid = Guid.NewGuid();
            var payload = BuildPayload("combo.par");
            payload.TargetGuid = enemyGuid;

            // Act — el par se jugó ATACANDO a un enemigo.
            TypedEvent<ComboPlayedPayload>.Raise(payload);

            // Assert
            Assert.AreEqual(1, pipeline.Resolved.Count);
            Assert.AreEqual(_playerGuid, pipeline.Resolved[0].TargetId, "la cura del item es para el jugador");
            Assert.AreNotEqual(enemyGuid, pipeline.Resolved[0].TargetId);
            Assert.Greater(pipeline.Resolved[0].BaseHeal, 0);
        }

        private sealed class SpyHealPipeline : Rollgeon.Combat.Pipelines.IHealPipeline
        {
            public readonly List<Rollgeon.Combat.Pipelines.HealContext> Resolved =
                new List<Rollgeon.Combat.Pipelines.HealContext>();

            public Rollgeon.Combat.Pipelines.HealContext Resolve(Rollgeon.Combat.Pipelines.HealContext ctx)
            {
                ctx.FinalHeal = ctx.BaseHeal;
                Resolved.Add(ctx);
                return ctx;
            }
        }
    }
}
