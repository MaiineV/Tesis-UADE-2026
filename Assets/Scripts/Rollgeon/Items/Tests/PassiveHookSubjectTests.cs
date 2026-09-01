using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Heroes;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Items.Tests
{
    /// <summary>
    /// Cubre <see cref="PassiveHookSubject"/>: contra qué argumento del evento se compara al
    /// jugador.
    /// </summary>
    /// <remarks>
    /// El bug que motiva el campo: <c>OnDamageIncoming</c> se dispara como
    /// <c>[sourceGuid, targetGuid, damage]</c> (DamagePipeline) y el hook comparaba siempre contra
    /// <c>args[0]</c>. Un ítem autorado como "cuando te pegan" disparaba al PEGAR — lo contrario
    /// de lo que dice el nombre del evento, sin ningún aviso. Estos tests fijan las dos
    /// direcciones para que no se vuelva a invertir en silencio.
    /// </remarks>
    public sealed class PassiveHookSubjectTests
    {
        readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();
        InventoryService _service;
        Guid _playerGuid;
        Guid _enemyGuid;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _playerGuid = Guid.NewGuid();
            _enemyGuid = Guid.NewGuid();
            ServiceLocator.AddService<IPlayerService>(new FakePlayerService(_playerGuid));

            Eff_Record.Log.Clear();
            Eff_CaptureContext.Captured.Clear();
            _service = new InventoryService(null, 4);
        }

        [TearDown]
        public void TearDown()
        {
            // Los handlers viven en el EventManager estático: una suscripción filtrada dispararía
            // en el test siguiente.
            _service?.Dispose();
            _service = null;

            foreach (var o in _spawned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _spawned.Clear();
            ServiceLocator.Clear();
            Eff_Record.Log.Clear();
            Eff_CaptureContext.Captured.Clear();
        }

        ItemSO NewPassive(PassiveHookSubject subject)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = "item.subject";
            item.DisplayName = "Subject";
            item.Type = ItemType.Passive;

            var hook = new PassiveItemHook
            {
                TriggerEvent = EventName.OnDamageIncoming,
                Subject = subject,
            };
            hook.Effect.Effects.Add(new Eff_Record { Tag = subject.ToString() });
            item.PassiveHooks.Add(hook);

            _spawned.Add(item);
            return item;
        }

        /// <summary>Payload real de <c>DamagePipeline</c>: [quien pega, quien recibe, daño].</summary>
        static void FireDamage(Guid source, Guid target) =>
            EventManager.Trigger(EventName.OnDamageIncoming, source, target, 10);

        // ---- Target: "cuando te pegan" --------------------------------------

        [Test]
        public void SubjectTarget_PlayerIsTheOneHit_Fires()
        {
            _service.AddItem(NewPassive(PassiveHookSubject.Target));

            FireDamage(_enemyGuid, _playerGuid);

            CollectionAssert.AreEqual(new[] { "Target" }, Eff_Record.Log,
                "el jugador es el target del daño — 'cuando te pegan' tiene que disparar");
        }

        [Test]
        public void SubjectTarget_PlayerIsTheOneHitting_DoesNotFire()
        {
            _service.AddItem(NewPassive(PassiveHookSubject.Target));

            FireDamage(_playerGuid, _enemyGuid);

            CollectionAssert.IsEmpty(Eff_Record.Log,
                "el jugador está pegando, no recibiendo — 'cuando te pegan' no puede disparar");
        }

        // ---- Source: el default, comportamiento histórico ---------------------

        [Test]
        public void SubjectSource_PlayerIsTheOneHitting_Fires()
        {
            _service.AddItem(NewPassive(PassiveHookSubject.Source));

            FireDamage(_playerGuid, _enemyGuid);

            CollectionAssert.AreEqual(new[] { "Source" }, Eff_Record.Log);
        }

        [Test]
        public void SubjectSource_PlayerIsTheOneHit_DoesNotFire()
        {
            _service.AddItem(NewPassive(PassiveHookSubject.Source));

            FireDamage(_enemyGuid, _playerGuid);

            CollectionAssert.IsEmpty(Eff_Record.Log);
        }

        /// <summary>
        /// El default tiene que seguir siendo <see cref="PassiveHookSubject.Source"/>: se serializa
        /// como int y los assets ya autorados no lo tienen escrito, así que cambiarlo invertiría
        /// en silencio los 24 ítems del catálogo.
        /// </summary>
        [Test]
        public void NewHook_DefaultsToSource()
        {
            Assert.AreEqual(PassiveHookSubject.Source, new PassiveItemHook().Subject);
            Assert.AreEqual(0, (int)PassiveHookSubject.Source);
        }

        /// <summary>
        /// Un evento de un solo argumento con <c>Subject = Target</c> no puede matchear a nadie.
        /// Tiene que no disparar, no tirar <c>IndexOutOfRange</c> desde el bus.
        /// </summary>
        [Test]
        public void SubjectTarget_EventWithASingleArg_DoesNotThrow()
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = "item.short";
            item.Type = ItemType.Passive;
            var hook = new PassiveItemHook
            {
                TriggerEvent = EventName.OnTurnStarted,
                Subject = PassiveHookSubject.Target,
            };
            hook.Effect.Effects.Add(new Eff_Record { Tag = "short" });
            item.PassiveHooks.Add(hook);
            _spawned.Add(item);

            _service.AddItem(item);

            Assert.DoesNotThrow(() => EventManager.Trigger(EventName.OnTurnStarted, _playerGuid));
            CollectionAssert.IsEmpty(Eff_Record.Log);
        }

        // ---- None: sin filtro de entidad --------------------------------------

        /// <summary>
        /// <c>OnCombatStart</c> lleva el roomId en args[0]: con Source/Target el hook no
        /// dispara nunca (el bug del Talismán Vital). <c>None</c> desactiva el filtro.
        /// </summary>
        [Test]
        public void SubjectNone_EventCarriesRoomGuid_Fires()
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = "item.none";
            item.Type = ItemType.Passive;
            var hook = new PassiveItemHook
            {
                TriggerEvent = EventName.OnCombatStart,
                Subject = PassiveHookSubject.None,
            };
            hook.Effect.Effects.Add(new Eff_Record { Tag = "None" });
            item.PassiveHooks.Add(hook);
            _spawned.Add(item);
            _service.AddItem(item);

            EventManager.Trigger(EventName.OnCombatStart, Guid.NewGuid());

            CollectionAssert.AreEqual(new[] { "None" }, Eff_Record.Log,
                "Subject=None no compara contra el jugador — dispara con el guid de la sala");
        }

        [Test]
        public void SubjectSource_RoomGuidEvent_StillDoesNotFire()
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = "item.room.target";
            item.Type = ItemType.Passive;
            var hook = new PassiveItemHook
            {
                TriggerEvent = EventName.OnCombatStart,
                Subject = PassiveHookSubject.Source,
            };
            hook.Effect.Effects.Add(new Eff_Record { Tag = "room" });
            item.PassiveHooks.Add(hook);
            _spawned.Add(item);
            _service.AddItem(item);

            EventManager.Trigger(EventName.OnCombatStart, Guid.NewGuid());

            CollectionAssert.IsEmpty(Eff_Record.Log,
                "con filtro de entidad activo, el guid de la sala no matchea al jugador");
        }

        // ---- El "otro" guid viaja como TargetGuid ------------------------------

        /// <summary>
        /// Con <c>Subject=Target</c> en <c>OnDamageIncoming</c>, el TargetGuid del contexto
        /// tiene que ser el ATACANTE: sin eso "reflejar daño" apuntaba al propio jugador
        /// (bug del Amuleto de Reflejo).
        /// </summary>
        [Test]
        public void SubjectTarget_AttackerTravelsAsContextTargetGuid()
        {
            _service.AddItem(NewCapturePassive(PassiveHookSubject.Target));

            FireDamage(_enemyGuid, _playerGuid);

            Assert.AreEqual(1, Eff_CaptureContext.Captured.Count);
            Assert.AreEqual(_enemyGuid, Eff_CaptureContext.Captured[0].TargetGuid,
                "el que pegó viaja como target del efecto — es a quién se refleja");
            Assert.AreEqual(_playerGuid, Eff_CaptureContext.Captured[0].SourceGuid);
        }

        [Test]
        public void SubjectSource_VictimTravelsAsContextTargetGuid()
        {
            _service.AddItem(NewCapturePassive(PassiveHookSubject.Source));

            FireDamage(_playerGuid, _enemyGuid);

            Assert.AreEqual(1, Eff_CaptureContext.Captured.Count);
            Assert.AreEqual(_enemyGuid, Eff_CaptureContext.Captured[0].TargetGuid,
                "al pegar, el enemigo golpeado es el target natural del efecto");
        }

        [Test]
        public void EventBusHook_ExposesSourceItemId()
        {
            _service.AddItem(NewCapturePassive(PassiveHookSubject.Target));

            FireDamage(_enemyGuid, _playerGuid);

            Assert.AreEqual("item.capture", Eff_CaptureContext.Captured[0].SourceItemId,
                "los efectos derivan identidad estable por item de acá (ItemPassiveSourceId)");
        }

        ItemSO NewCapturePassive(PassiveHookSubject subject)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = "item.capture";
            item.Type = ItemType.Passive;
            var hook = new PassiveItemHook
            {
                TriggerEvent = EventName.OnDamageIncoming,
                Subject = subject,
            };
            hook.Effect.Effects.Add(new Eff_CaptureContext());
            item.PassiveHooks.Add(hook);
            _spawned.Add(item);
            return item;
        }

        // ---- helpers ---------------------------------------------------------

        /// <summary>Captura el EffectContext con el que corrió, para asertar el plumbing.</summary>
        [Serializable]
        sealed class Eff_CaptureContext : BaseEffect
        {
            public static readonly List<EffectContext> Captured = new List<EffectContext>();

            public override string GetEffectName() => "CaptureContext";
            public override bool ApplyEffect(EffectContext context)
            {
                Captured.Add(context);
                return true;
            }
        }

        /// <summary>Anota que corrió, para saber qué hook disparó.</summary>
        [Serializable]
        sealed class Eff_Record : BaseEffect
        {
            public static readonly List<string> Log = new List<string>();
            public string Tag;

            public override string GetEffectName() => "Record";
            public override bool ApplyEffect(EffectContext context)
            {
                Log.Add(Tag);
                return true;
            }
        }

        /// <summary>Solo importa <see cref="PlayerGuid"/> — es lo único que los hooks filtran.</summary>
        sealed class FakePlayerService : IPlayerService
        {
            public FakePlayerService(Guid guid) { PlayerGuid = guid; }

            public Guid PlayerGuid { get; }
            public Guid RunId => Guid.Empty;
            public ClassHeroSO CurrentHero => null;
            public DiceBagSO DiceBag => null;

            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) { }
            public void ClearPlayer() { }

#pragma warning disable CS0067 // nunca se levantan: nada bajo test los escucha
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
#pragma warning restore CS0067
        }
    }
}
