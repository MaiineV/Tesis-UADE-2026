using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Combat.DiceBlock;
using Rollgeon.Dice;
using Rollgeon.Heroes;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Tests del modo dirigido de <see cref="AINode_RotateBlock"/>: con un
    /// <see cref="AINode_RotateBlock.DirectedIndex"/> seteado el dado bloqueado sale de un reader en vez
    /// del sorteo. El caso que lo pide es el Croupier (el número cantado es a la vez el sector que cae y
    /// el dado que se confisca), pero el hook es genérico.
    /// </summary>
    /// <remarks>
    /// El primer test es el importante para el resto del proyecto: hay assets viejos apuntando a este
    /// nodo, así que <c>DirectedIndex</c> vacío tiene que seguir sorteando exactamente como antes.
    /// </remarks>
    [TestFixture]
    public class AINode_RotateBlockDirectedTests
    {
        private const int BagSize = 5;

        private DiceBlockService _dice;
        private StubPlayerService _playerService;
        private DiceBagSO _bag;
        private Guid _playerGuid;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _playerGuid = Guid.NewGuid();

            _bag = ScriptableObject.CreateInstance<DiceBagSO>();
            _bag.hideFlags = HideFlags.HideAndDontSave;
            // Uno de cada tipo: la bolsa canónica son 5 dados
            // (una bolsa inválida sólo loguea warnings, pero ensucian el runner).
            _bag.Dice = new List<DiceType> { DiceType.D4, DiceType.D6, DiceType.D8, DiceType.D10, DiceType.D12 };

            Assert.AreEqual(BagSize, _bag.Dice.Count, "El módulo del índice dirigido se mide contra esta bolsa.");

            _playerService = new StubPlayerService { Guid = _playerGuid, Bag = _bag };
            ServiceLocator.AddService<IPlayerService>(_playerService);

            _dice = new DiceBlockService();
            _dice.Register();
        }

        [TearDown]
        public void TearDown()
        {
            _dice.Dispose();
            if (_bag != null) UnityEngine.Object.DestroyImmediate(_bag);
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void WithoutDirectedIndex_StillDrawsAtRandom()
        {
            // Arrange — el default histórico: los assets viejos no pueden cambiar de comportamiento.
            var node = new AINode_RotateBlock
            {
                Target = AINode_RotateBlock.BlockTarget.Dice,
                Count = 2,
            };

            // Act
            var result = node.Tick(Context(seed: 7));

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(2, _dice.BlockedIndices.Count, "Con Count=2 se sortean 2 dados distintos.");
        }

        [Test]
        public void WithDirectedIndex_BlocksExactlyThatDie_AndIgnoresCount()
        {
            // Arrange — "cuántos" ya lo dice la mecánica que dirige el índice.
            var node = new AINode_RotateBlock
            {
                Target = AINode_RotateBlock.BlockTarget.Dice,
                Count = 3,
                DirectedIndex = new AIConstantInt { Value = 2 },
            };

            // Act
            node.Tick(Context());

            // Assert
            Assert.AreEqual(1, _dice.BlockedIndices.Count);
            Assert.IsTrue(_dice.IsBlocked(2));
        }

        [Test]
        public void DirectedIndex_BeyondTheBag_WrapsAroundInsteadOfClamping()
        {
            // Arrange — el paño tiene 6 números y la build 5 dados: el 6 (índice 5) da la vuelta al
            // primero. Clampear le daría al último dado el doble de chance de ser confiscado.
            var node = new AINode_RotateBlock
            {
                Target = AINode_RotateBlock.BlockTarget.Dice,
                DirectedIndex = new AIConstantInt { Value = 5 },
            };

            // Act
            node.Tick(Context());

            // Assert
            Assert.IsTrue(_dice.IsBlocked(0), "Índice 5 en una bolsa de 5 vuelve al dado 0.");
            Assert.AreEqual(1, _dice.BlockedIndices.Count);
        }

        [Test]
        public void DirectedIndex_Negative_BlocksNothing()
        {
            // Arrange — el reader del Croupier devuelve -1 cuando no hay número en el aire. Bloquear un
            // dado al azar en ese caso sería un candado que el jugador no puede leer en pantalla.
            var node = new AINode_RotateBlock
            {
                Target = AINode_RotateBlock.BlockTarget.Dice,
                Count = 2,
                DirectedIndex = new AIConstantInt { Value = -1 },
            };

            // Act
            var result = node.Tick(Context());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result, "No confiscar no es un fallo del turno.");
            Assert.IsEmpty(_dice.BlockedIndices);
        }

        [Test]
        public void DirectedIndex_IsFreshEachTurn()
        {
            // Arrange — la confiscación dura un turno: el candado del turno anterior no se acumula.
            var node = new AINode_RotateBlock
            {
                Target = AINode_RotateBlock.BlockTarget.Dice,
                DirectedIndex = new AIConstantInt { Value = 1 },
            };
            node.Tick(Context());

            var second = new AINode_RotateBlock
            {
                Target = AINode_RotateBlock.BlockTarget.Dice,
                DirectedIndex = new AIConstantInt { Value = 3 },
            };

            // Act
            second.Tick(Context());

            // Assert
            Assert.AreEqual(1, _dice.BlockedIndices.Count);
            Assert.IsTrue(_dice.IsBlocked(3));
            Assert.IsFalse(_dice.IsBlocked(1), "El dado del turno anterior tiene que quedar libre.");
        }

        private AIContext Context(int seed = 1) => new AIContext
        {
            SelfGuid = Guid.NewGuid(),
            PlayerGuid = _playerGuid,
            PlayerService = _playerService,
            Rng = new System.Random(seed),
        };

        private sealed class StubPlayerService : IPlayerService
        {
            public Guid Guid;
            public DiceBagSO Bag;

            public Guid PlayerGuid => Guid;
            public Guid RunId => System.Guid.Empty;
            public ClassHeroSO CurrentHero => null;
            public DiceBagSO DiceBag => Bag;

            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) => Bag = bag;
            public void ClearPlayer() { }

#pragma warning disable CS0067
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
#pragma warning restore CS0067
        }
    }
}
