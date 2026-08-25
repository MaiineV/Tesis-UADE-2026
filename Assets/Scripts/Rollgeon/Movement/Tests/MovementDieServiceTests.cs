using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Heroes;
using Rollgeon.Movement.Die;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Movement.Tests
{
    /// <summary>
    /// Dado de Movimiento (§6.6): entidad separada de la build de 5. Cubre la DoD
    /// "modificar la build no lo afecta y viceversa" y el ciclo de vida del rango activo.
    /// </summary>
    [TestFixture]
    public sealed class MovementDieServiceTests
    {
        private PlayerService _player;
        private MovementDieService _service;
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _player = new PlayerService();
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            _service = null;
            foreach (var so in _created) if (so != null) UnityEngine.Object.DestroyImmediate(so);
            _created.Clear();
            ServiceLocator.Clear();
        }

        private ClassHeroSO HeroWith(MovementDieSO die)
        {
            var hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            hero.StartingMovementDie = die;
            _created.Add(hero);
            return hero;
        }

        private MovementDieSO Die(DiceType type)
        {
            var die = ScriptableObject.CreateInstance<MovementDieSO>();
            die.Type = type;
            _created.Add(die);
            return die;
        }

        private static DiceBagSO Bag(params DiceType[] dice)
        {
            var bag = ScriptableObject.CreateInstance<DiceBagSO>();
            bag.Dice = new List<DiceType>(dice);
            return bag;
        }

        // ---- Entidad separada ------------------------------------------------

        [Test]
        public void MovementDieSO_DefaultsToD4()
        {
            var die = Die(MovementDieSO.DefaultType);
            Assert.AreEqual(DiceType.D4, die.Type);
            Assert.AreEqual(4, die.MaxFace);
        }

        [Test]
        public void CurrentType_WithoutHeroDie_FallsBackToD4()
        {
            _player.SetPlayer(HeroWith(null), Guid.NewGuid());
            _service = new MovementDieService(_player);

            Assert.AreEqual(DiceType.D4, _service.CurrentType);
        }

        [Test]
        public void CurrentType_ReadsHeroMovementDie()
        {
            _player.SetPlayer(HeroWith(Die(DiceType.D8)), Guid.NewGuid());
            _service = new MovementDieService(_player);

            Assert.AreEqual(DiceType.D8, _service.CurrentType);
        }

        [Test]
        public void CurrentType_NotInPlayerDiceBag_AndBagChangesDoNotAffectIt()
        {
            var hero = HeroWith(Die(DiceType.D6));
            hero.StartingDiceBagRef = Bag(DiceType.D4, DiceType.D4, DiceType.D4, DiceType.D4, DiceType.D4);
            _created.Add((ScriptableObject)hero.StartingDiceBagRef);
            _player.SetPlayer(hero, Guid.NewGuid());
            _service = new MovementDieService(_player);

            // El bag de combate sigue teniendo exactamente sus 5 dados: el de Movimiento no es un 6.º.
            Assert.AreEqual(DiceBagSO.RequiredSize, _player.DiceBag.Dice.Count);
            CollectionAssert.DoesNotContain(_player.DiceBag.Dice, DiceType.D6);

            // Reemplazar la build entera no toca el dado de Movimiento.
            var newBag = Bag(DiceType.D20, DiceType.D20, DiceType.D20, DiceType.D20, DiceType.D20);
            _created.Add(newBag);
            _player.SetDiceBag(newBag);
            Assert.AreEqual(DiceType.D6, _service.CurrentType);

            // Y al revés: un override del dado de Movimiento no toca la build.
            _service.SetTypeOverride(DiceType.D12);
            Assert.AreEqual(DiceType.D12, _service.CurrentType);
            Assert.IsTrue(_player.DiceBag.Dice.TrueForAll(d => d == DiceType.D20));
        }

        [Test]
        public void Roll_UsesOwnRng_NotTheRegisteredDiceRoller()
        {
            // Un IDiceRoller registrado que devuelve siempre 1 (simula un EnchantedDiceRoller
            // con encantamiento en el slot 0). El dado de Movimiento no lo consulta.
            ServiceLocator.AddService<IDiceRoller>(new AlwaysOneRoller(), ServiceScope.Global);
            _player.SetPlayer(HeroWith(Die(DiceType.D4)), Guid.NewGuid());
            _service = new MovementDieService(_player, seed: 1234);

            var expected = new System.Random(1234);
            var guid = Guid.NewGuid();
            for (int i = 0; i < 20; i++)
            {
                int face = -1;
                _service.Roll(guid, f => face = f);
                Assert.AreEqual(expected.Next(1, 5), face, $"roll #{i}");
                Assert.That(face, Is.InRange(1, 4));
                _service.ClearActiveRange();
            }
        }

        // ---- Rango activo ----------------------------------------------------

        [Test]
        public void Roll_WithoutPresenter_RevealsSynchronouslyAndPublishesRange()
        {
            _player.SetPlayer(HeroWith(Die(DiceType.D4)), Guid.NewGuid());
            _service = new MovementDieService(_player, seed: 7);
            var guid = Guid.NewGuid();
            Guid evtGuid = Guid.Empty; int evtFace = 0;
            _service.OnRolled += (g, f) => { evtGuid = g; evtFace = f; };

            int revealed = 0;
            _service.Roll(guid, f => revealed = f);

            Assert.That(revealed, Is.InRange(1, 4));
            Assert.IsTrue(_service.TryGetActiveRange(guid, out var range));
            Assert.AreEqual(revealed, range);
            Assert.AreEqual(revealed, _service.LastFace);
            Assert.AreEqual(guid, evtGuid);
            Assert.AreEqual(revealed, evtFace);
            Assert.IsFalse(_service.TryGetActiveRange(Guid.NewGuid(), out _), "otro guid no ve el rango");
        }

        [Test]
        public void Roll_WithPresenter_DefersRevealUntilPresenterFinishes()
        {
            _player.SetPlayer(HeroWith(Die(DiceType.D4)), Guid.NewGuid());
            _service = new MovementDieService(_player, seed: 7);
            var presenter = new SpyPresenter();
            _service.SetPresenter(presenter);
            var guid = Guid.NewGuid();

            int revealed = 0;
            _service.Roll(guid, f => revealed = f);

            Assert.AreEqual(0, revealed, "sin reveal hasta que el presenter termine");
            Assert.IsFalse(_service.TryGetActiveRange(guid, out _), "no spoilear el rango durante la animación");
            Assert.AreEqual(DiceType.D4, presenter.LastType);

            presenter.Finish();

            Assert.AreEqual(presenter.LastFace, revealed);
            Assert.IsTrue(_service.TryGetActiveRange(guid, out var range));
            Assert.AreEqual(presenter.LastFace, range);
        }

        [Test]
        public void ClearActiveRange_InvalidatesPendingReveal()
        {
            _player.SetPlayer(HeroWith(Die(DiceType.D4)), Guid.NewGuid());
            _service = new MovementDieService(_player, seed: 7);
            var presenter = new SpyPresenter();
            _service.SetPresenter(presenter);
            var guid = Guid.NewGuid();
            int cleared = 0;
            _service.OnCleared += () => cleared++;

            int revealed = 0;
            _service.Roll(guid, f => revealed = f);
            _service.ClearActiveRange();
            presenter.Finish(); // llega tarde

            Assert.AreEqual(0, revealed);
            Assert.IsFalse(_service.TryGetActiveRange(guid, out _));
            Assert.AreEqual(1, presenter.AbortCount);
            Assert.AreEqual(1, cleared);
        }

        [Test]
        public void ClearActiveRange_WithoutAnything_IsSilent()
        {
            _player.SetPlayer(HeroWith(null), Guid.NewGuid());
            _service = new MovementDieService(_player);
            int cleared = 0;
            _service.OnCleared += () => cleared++;

            _service.ClearActiveRange();

            Assert.AreEqual(0, cleared);
        }

        [Test]
        public void CombatEnd_ClearsActiveRange()
        {
            _player.SetPlayer(HeroWith(null), Guid.NewGuid());
            _service = new MovementDieService(_player, seed: 3);
            var guid = Guid.NewGuid();
            _service.Roll(guid, _ => { });
            Assert.IsTrue(_service.TryGetActiveRange(guid, out _), "pre-condition");

            EventManager.Trigger(EventName.OnCombatEnd);

            Assert.IsFalse(_service.TryGetActiveRange(guid, out _));
            Assert.AreEqual(0, _service.LastFace);
        }

        [Test]
        public void Roll_WhileRevealPending_IsIgnored()
        {
            _player.SetPlayer(HeroWith(null), Guid.NewGuid());
            _service = new MovementDieService(_player, seed: 3);
            var presenter = new SpyPresenter();
            _service.SetPresenter(presenter);
            var guid = Guid.NewGuid();

            _service.Roll(guid, _ => { });
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("reveal pendiente"));
            _service.Roll(guid, _ => { });

            Assert.AreEqual(1, presenter.PresentCount);
        }

        [Test]
        public void Roll_WithPresenter_EmitsRollStartedBeforeRolled_AndNothingWithoutPresenter()
        {
            // Arrange — la mesa abre con RollStarted y cierra con Rolled: el orden importa.
            _player.SetPlayer(HeroWith(Die(DiceType.D4)), Guid.NewGuid());
            _service = new MovementDieService(_player, seed: 7);
            var order = new List<string>();
            EventManager.EventReceiver onStarted = _ => order.Add("started");
            EventManager.EventReceiver onRolled = _ => order.Add("rolled");
            EventManager.Subscribe(EventName.OnMovementDieRollStarted, onStarted);
            EventManager.Subscribe(EventName.OnMovementDieRolled, onRolled);
            var guid = Guid.NewGuid();

            // Act 1 — sin presenter: reveal sincrónico, sin mesa que abrir.
            _service.Roll(guid, _ => { });
            _service.ClearActiveRange();
            CollectionAssert.AreEqual(new[] { "rolled" }, order);

            // Act 2 — con presenter: started → (spin) → rolled.
            order.Clear();
            var presenter = new SpyPresenter();
            _service.SetPresenter(presenter);
            _service.Roll(guid, _ => { });
            CollectionAssert.AreEqual(new[] { "started" }, order, "la mesa abre antes del reveal");
            presenter.Finish();

            // Assert
            CollectionAssert.AreEqual(new[] { "started", "rolled" }, order);
            EventManager.UnSubscribe(EventName.OnMovementDieRollStarted, onStarted);
            EventManager.UnSubscribe(EventName.OnMovementDieRolled, onRolled);
        }

        // ---- Fakes -----------------------------------------------------------

        private sealed class AlwaysOneRoller : IDiceRoller
        {
            public int[] RollAll(DiceBagSO bag) => Ones(bag.Dice.Count);
            public int[] Reroll(DiceBagSO bag, int[] previousResult, bool[] keep) => Ones(bag.Dice.Count);
            private static int[] Ones(int n) { var r = new int[n]; for (int i = 0; i < n; i++) r[i] = 1; return r; }
        }

        private sealed class SpyPresenter : IMovementDiePresenter
        {
            private Action _pending;
            public int PresentCount;
            public int AbortCount;
            public DiceType LastType;
            public int LastFace;

            public bool TryPresent(DiceType type, int face, Action onRevealed)
            {
                PresentCount++;
                LastType = type;
                LastFace = face;
                _pending = onRevealed;
                return true;
            }

            public void Abort() => AbortCount++;

            public void Finish()
            {
                var p = _pending;
                _pending = null;
                p?.Invoke();
            }
        }
    }
}
