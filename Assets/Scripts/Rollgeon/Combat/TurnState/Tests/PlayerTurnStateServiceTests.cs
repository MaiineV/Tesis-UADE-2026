using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Rolls;
using Rollgeon.Dice;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Movement;
using Rollgeon.Player;

namespace Rollgeon.Combat.TurnState.Tests
{
    /// <summary>
    /// Estado por turno/combate del jugador: tiles recorridas (Corredor/Piedra) y racha
    /// de rondas limpias (Furia Contenida).
    /// </summary>
    public sealed class PlayerTurnStateServiceTests
    {
        FakeMovement _movement;
        PlayerTurnStateService _service;
        Guid _player;
        Guid _enemy;
        Guid _room;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            _player = Guid.NewGuid();
            _enemy = Guid.NewGuid();
            _room = Guid.NewGuid();
            ServiceLocator.AddService<IPlayerService>(new FakePlayerService(_player));

            _movement = new FakeMovement();
            _service = new PlayerTurnStateService(_movement);
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        void StartCombat() => EventManager.Trigger(EventName.OnCombatStart, _room);
        void PlayerTurn() => EventManager.Trigger(EventName.OnTurnStarted, _player);

        void MovePlayer(int tiles)
        {
            var path = new List<GridCoord>();
            for (int i = 0; i <= tiles; i++) path.Add(new GridCoord(i, 0));
            _movement.RaiseMoved(_player, path[0], path[tiles], path);
        }

        void PlayAttackCombo() => TypedEvent<ComboPlayedPayload>.Raise(new ComboPlayedPayload
        {
            SourceGuid = _player,
            TargetGuid = _enemy,
            ComboId = "combo.par",
            ActionKind = RollActionKind.Attack,
        });

        void DamagePlayer(int finalDamage, int shieldAbsorbed = 0)
            => TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = _enemy,
                TargetGuid = _player,
                FinalDamage = finalDamage,
                ShieldAbsorbed = shieldAbsorbed,
                BlockedByShield = finalDamage == 0 && shieldAbsorbed > 0,
            });

        // ---- tiles -------------------------------------------------------------

        [Test]
        public void Moving_AccumulatesEffectivePathTiles()
        {
            StartCombat();
            PlayerTurn();

            MovePlayer(3);
            MovePlayer(2);

            Assert.AreEqual(5, _service.TilesMovedThisTurn);
        }

        [Test]
        public void TilesReadInsideAttackDispatch_AreStillVisible()
        {
            // El reset post-ataque es DIFERIDO: un item que lee tiles dentro del mismo
            // dispatch de ComboPlayed (Corredor Incansable) tiene que ver el contador.
            StartCombat();
            PlayerTurn();
            MovePlayer(3);

            int seen = -1;
            Action<ComboPlayedPayload> reader = _ => seen = _service.TilesMovedThisTurn;
            TypedEvent<ComboPlayedPayload>.Subscribe(reader);
            try { PlayAttackCombo(); }
            finally { TypedEvent<ComboPlayedPayload>.Unsubscribe(reader); }

            Assert.AreEqual(3, seen, "el tracker no puede resetear dentro del dispatch");
        }

        [Test]
        public void MovingAfterAttack_StartsFreshCount()
        {
            // "Solo el ataque que sigue al movimiento": el próximo movimiento limpia.
            StartCombat();
            PlayerTurn();
            MovePlayer(3);
            PlayAttackCombo();

            MovePlayer(2);

            Assert.AreEqual(2, _service.TilesMovedThisTurn);
        }

        [Test]
        public void TurnStart_ResetsTiles()
        {
            StartCombat();
            PlayerTurn();
            MovePlayer(4);

            PlayerTurn();

            Assert.AreEqual(0, _service.TilesMovedThisTurn);
        }

        [Test]
        public void EnemyMovement_DoesNotCount()
        {
            StartCombat();
            PlayerTurn();

            var path = new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0) };
            _movement.RaiseMoved(_enemy, path[0], path[1], path);

            Assert.AreEqual(0, _service.TilesMovedThisTurn);
        }

        [Test]
        public void OutOfCombat_MovementDoesNotCount()
        {
            MovePlayer(3);
            Assert.AreEqual(0, _service.TilesMovedThisTurn);
        }

        // ---- racha limpia --------------------------------------------------------

        [Test]
        public void CleanRounds_IncrementStreakAtEachPlayerTurnStart()
        {
            StartCombat();
            PlayerTurn();          // ronda 1 abre (no suma: no había ronda previa)
            PlayerTurn();          // cierra ronda 1 limpia → streak 1
            PlayerTurn();          // cierra ronda 2 limpia → streak 2

            Assert.AreEqual(2, _service.CleanTurnStreak);
        }

        [Test]
        public void TakingRealDamage_ResetsStreak_AndMarksTheRound()
        {
            StartCombat();
            PlayerTurn();
            PlayerTurn(); // streak 1
            DamagePlayer(10);

            Assert.AreEqual(0, _service.CleanTurnStreak);

            PlayerTurn(); // la ronda del golpe NO suma
            Assert.AreEqual(0, _service.CleanTurnStreak);

            PlayerTurn(); // ronda limpia posterior sí
            Assert.AreEqual(1, _service.CleanTurnStreak);
        }

        [Test]
        public void FullyShieldedHit_DoesNotBreakTheStreak()
        {
            // "Recibir daño" = perder vida: jugar defensivo es lo que Furia premia.
            StartCombat();
            PlayerTurn();
            DamagePlayer(0, shieldAbsorbed: 12);
            PlayerTurn();

            Assert.AreEqual(1, _service.CleanTurnStreak);
        }

        [Test]
        public void CombatStart_ResetsEverything()
        {
            StartCombat();
            PlayerTurn();
            MovePlayer(3);
            PlayerTurn(); // streak 1

            StartCombat(); // combate nuevo

            Assert.AreEqual(0, _service.TilesMovedThisTurn);
            Assert.AreEqual(0, _service.CleanTurnStreak);
        }

        [Test]
        public void CombatEnd_ResetsEverything()
        {
            StartCombat();
            PlayerTurn();
            MovePlayer(3);
            PlayerTurn();

            EventManager.Trigger(EventName.OnCombatEnd, _room);

            Assert.AreEqual(0, _service.TilesMovedThisTurn);
            Assert.AreEqual(0, _service.CleanTurnStreak);
        }

        [Test]
        public void Dispose_StopsListening()
        {
            StartCombat();
            _service.Dispose();

            PlayerTurn();
            MovePlayer(3);

            Assert.AreEqual(0, _service.TilesMovedThisTurn);
        }

        // ---- fakes ---------------------------------------------------------------

        sealed class FakeMovement : IMovementService
        {
            public event Action<Guid, GridCoord, GridCoord, IReadOnlyList<GridCoord>> OnEntityMoved;

            public void RaiseMoved(Guid entity, GridCoord from, GridCoord to, IReadOnlyList<GridCoord> path)
                => OnEntityMoved?.Invoke(entity, from, to, path);

            public List<GridCoord> GetReachableTiles(GridCoord origin, int range, bool includeOrigin = false)
                => new List<GridCoord>();

            public List<GridCoord> FindPath(GridCoord from, GridCoord to) => new List<GridCoord>();

            public bool Move(Guid entity, GridCoord destination) => false;
        }

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

#pragma warning disable CS0067
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
#pragma warning restore CS0067
        }
    }
}
