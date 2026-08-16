using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Bosses.Bandida;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.EnergyLib;
using Rollgeon.Grid;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// El peaje de la fila de La Bandida: cuánta energía cobra según cuántos rodillos rompibles
    /// quedan en pie, y qué hace cuando no hay de dónde cobrar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La energía va por un espía y no por el <c>EnergyService</c> real: lo que se está fijando es
    /// la <b>política</b> del peaje —cuánto pide y cómo reacciona a un jugador seco— y montar
    /// <c>AttributesManager</c> + <c>RulesetSO</c> para eso ataría estos tests al balance del
    /// jugador, que es justo lo que el peaje no toca.
    /// </para>
    /// <para>
    /// El espía cobra de a uno igual que el servicio real: <c>SpendEnergy</c> es todo-o-nada y
    /// devuelve <c>false</c> sin mutar si no alcanza. Ese detalle es el que hace que el peaje cobre
    /// "lo que haya" en vez de nada.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class BandidaReelTollTests
    {
        private BandidaJackpotService _jackpot;
        private SpyEnergyService _energy;
        private Guid _boss;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _jackpot = new BandidaJackpotService();
            _jackpot.Register();

            _energy = new SpyEnergyService { Current = 4 };
            ServiceLocator.AddService<IEnergyService>(_energy);

            _boss = Guid.NewGuid();
            _player = Guid.NewGuid();
            _jackpot.BindBoss(_boss);

            _jackpot.SetSlots(new List<GridCoord>
            {
                new GridCoord(3, 3), new GridCoord(4, 3), new GridCoord(5, 3),
            });
        }

        [TearDown]
        public void TearDown()
        {
            _jackpot.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // =====================================================================
        // Cuánto cobra
        // =====================================================================

        [Test]
        public void FullRow_ChargesTheCap_NotOnePerReel()
        {
            AttachAll();

            Toll(cap: 1).Tick(Context());

            Assert.AreEqual(1, _energy.TotalSpent,
                "Tres rodillos vivos con techo 1 cobran 1. Sin techo serían 3 contra un regen de 2, " +
                "o sea energía neta negativa para siempre.");
        }

        [Test]
        public void PhaseTwoCap_MatchesThePlayersRegen_SoItNeverSpirals()
        {
            AttachAll();

            Toll(cap: 2).Tick(Context());

            Assert.AreEqual(2, _energy.TotalSpent,
                "El techo de fase 2 empata el regen (EnergyRegenBase = 2): le saca el margen al " +
                "jugador sin dejarlo nunca en neto negativo.");
        }

        [Test]
        public void ChargesOnlyForWhatIsStillStanding()
        {
            _jackpot.AttachReel(0, Guid.NewGuid()); // Uno solo vivo, los otros dos rotos.

            Toll(cap: 2).Tick(Context());

            Assert.AreEqual(1, _energy.TotalSpent,
                "Romper rodillos tiene que pagar en el acto — es lo que el peaje le agrega a la pelea.");
        }

        [Test]
        public void EmptyRow_ChargesNothing()
        {
            var result = Toll(cap: 2).Tick(Context());

            Assert.AreEqual(AIResult.Succeeded, result,
                "Una fila rota es una resolución válida del peaje, no un fallo que corte el turno.");
            Assert.AreEqual(0, _energy.TotalSpent);
        }

        // =====================================================================
        // El rodillo trabado
        // =====================================================================

        [Test]
        public void LockedReel_DoesNotCount_BecauseThePlayerCannotAnswerIt()
        {
            AttachAll();
            _jackpot.LockSlot(ReelSide.Middle, lockedHp: 9999);

            Toll(cap: 2).Tick(Context());

            Assert.AreEqual(2, _energy.TotalSpent,
                "Quedan dos rompibles: el trabado no entra al conteo.");
        }

        [Test]
        public void OnlyTheLockedReelStanding_ChargesNothing()
        {
            _jackpot.AttachReel(1, Guid.NewGuid());
            _jackpot.LockSlot(ReelSide.Middle, lockedHp: 9999);

            Toll(cap: 2).Tick(Context());

            Assert.AreEqual(0, _energy.TotalSpent,
                "Cobrar por el único rodillo inrompible convierte el peaje en un impuesto que el " +
                "jugador no tiene forma de contestar.");
        }

        // =====================================================================
        // Jugador seco
        // =====================================================================

        [Test]
        public void DryPlayer_PaysWhatIsLeft_InsteadOfPayingNothing()
        {
            AttachAll();
            _energy.Current = 1;

            Toll(cap: 2).Tick(Context());

            // SpendEnergy(2) habría devuelto false sin mutar: el jugador con 1 de energía habría
            // pagado cero. De a uno el peaje cobra lo que hay.
            Assert.AreEqual(1, _energy.TotalSpent);
            Assert.AreEqual(0, _energy.Current);
        }

        [Test]
        public void PlayerAtZero_IsNotDrivenNegative()
        {
            AttachAll();
            _energy.Current = 0;

            var result = Toll(cap: 2).Tick(Context());

            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(0, _energy.TotalSpent);
            Assert.AreEqual(0, _energy.Current);
        }

        // =====================================================================
        // Degradado
        // =====================================================================

        [Test]
        public void MissingEnergyService_DoesNotCutTheBossTurn()
        {
            AttachAll();
            ServiceLocator.Clear();
            ServiceLocator.AddService<IBandidaJackpotService>(_jackpot);

            // El error es parte del contrato: un peaje que no cobra en silencio deja la pelea sin
            // su presión principal y nadie se entera hasta el playtest.
            LogAssert.Expect(LogType.Error, new Regex("IEnergyService no registrado"));

            var result = Toll(cap: 2).Tick(Context());

            Assert.AreEqual(AIResult.Succeeded, result,
                "Sin el servicio el peaje no cobra, pero el jefe tiene que seguir reponiendo la fila " +
                "y atacando — un Failed acá le cancelaría el resto del turno.");
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private void AttachAll()
        {
            for (int i = 0; i < 3; i++) _jackpot.AttachReel(i, Guid.NewGuid());
        }

        private static AINode_BandidaReelToll Toll(int cap) =>
            new AINode_BandidaReelToll { Cap = cap };

        private AIContext Context() => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = _player,
        };

        /// <summary>Balance en memoria con la misma semántica todo-o-nada del servicio real.</summary>
        private sealed class SpyEnergyService : IEnergyService
        {
            public int Current;
            public int TotalSpent;

            public void InitializeForEntity(Guid entityId) { }

            public bool SpendEnergy(Guid entityId, int cost)
            {
                if (cost > Current) return false;
                Current -= cost;
                TotalSpent += cost;
                return true;
            }

            public void RegenerateAtTurnEnd(Guid entityId) { }
            public int GetCurrent(Guid entityId) => Current;
            public int GetMax(Guid entityId) => 4;
        }
    }
}
