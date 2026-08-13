using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Entities.Visuals;
using UnityEngine;

namespace Rollgeon.Feedback.Tests
{
    /// <summary>
    /// Cobertura del branch <see cref="FeedbackType.PawnDeath"/> (tween de muerte hecho
    /// desde código) y de las secuencias con nombre del <see cref="FeedbackDBSO"/>.
    /// El tween en sí es visual y se valida en Play Mode; acá cubrimos el contrato
    /// determinístico: estabilidad del enum, wiring del listener, y resolución por id.
    /// </summary>
    [TestFixture]
    public class FeedbackPawnDeathTests
    {
        private GameObject _managerGo;
        private FeedbackManager _manager;
        private MethodInfo _dispatchPawnDeath;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void Setup()
        {
            ServiceLocator.Clear();

            _managerGo = new GameObject("FeedbackManager");
            _manager = _managerGo.AddComponent<FeedbackManager>();

            _dispatchPawnDeath = typeof(FeedbackManager).GetMethod(
                "DispatchPawnDeath", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(_dispatchPawnDeath, "DispatchPawnDeath no encontrado — revisar rename.");
        }

        [TearDown]
        public void Teardown()
        {
            foreach (var go in _spawned)
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _spawned.Clear();

            if (_managerGo != null) UnityEngine.Object.DestroyImmediate(_managerGo);
            ServiceLocator.Clear();
        }

        // ── Estabilidad del enum ────────────────────────────────────────
        // El FeedbackDB serializa Type como int: un valor insertado en el medio recabla
        // TODAS las entries autoradas en silencio.

        [Test]
        public void FeedbackType_PawnDeath_IsAppendedLast()
        {
            Assert.AreEqual(7, (int)FeedbackType.PawnDeath);
            Assert.AreEqual(6, (int)FeedbackType.Feel, "Feel no se debe haber corrido de lugar.");
        }

        // ── Secuencias con nombre en el DB ──────────────────────────────

        [Test]
        public void TryGetSequence_AuthoredId_ReturnsItsSteps()
        {
            // Arrange
            var db = ScriptableObject.CreateInstance<FeedbackDBSO>();
            try
            {
                AddSequence(db, "death.enemy", new FeedbackSequenceStep
                {
                    Source = StepSource.FeedbackRef,
                    FeedbackRefId = "death.tween",
                });

                // Act
                bool found = db.TryGetSequence("death.enemy", out var steps);

                // Assert
                Assert.IsTrue(found);
                Assert.AreEqual(1, steps.Count);
                Assert.AreEqual("death.tween", steps[0].FeedbackRefId);
            }
            finally { UnityEngine.Object.DestroyImmediate(db); }
        }

        [Test]
        public void TryGetSequence_UnknownId_ReturnsFalse()
        {
            var db = ScriptableObject.CreateInstance<FeedbackDBSO>();
            try
            {
                AddSequence(db, "death.enemy", new FeedbackSequenceStep());

                Assert.IsFalse(db.TryGetSequence("no.existe", out var steps));
                Assert.IsNull(steps);
            }
            finally { UnityEngine.Object.DestroyImmediate(db); }
        }

        [Test]
        public void TryGetSequence_DoesNotCollideWithEntryIds()
        {
            // Entries y secuencias viven en diccionarios separados: un id de entry no debe
            // resolver como secuencia (si no, un EffPlaySequence sin steps se colgaría con
            // una entry suelta creyendo que es una secuencia).
            var db = ScriptableObject.CreateInstance<FeedbackDBSO>();
            try
            {
                AddEntry(db, new FeedbackEntry { FeedbackId = "vfx.solo", Type = FeedbackType.VFX });

                Assert.IsTrue(db.TryGetFeedback("vfx.solo", out _));
                Assert.IsFalse(db.TryGetSequence("vfx.solo", out _),
                    "Una entry suelta no debe resolver como secuencia.");
            }
            finally { UnityEngine.Object.DestroyImmediate(db); }
        }

        // ── DispatchPawnDeath ───────────────────────────────────────────

        [Test]
        public void DispatchPawnDeath_AttachesListenerToPawnRoot()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var pawn = RegisterPawn(guid, withHealthBar: false);
            var entry = new FeedbackEntry { Type = FeedbackType.PawnDeath, Duration = 0.6f };

            // Act
            var handle = InvokeDispatch(entry, guid);

            // Assert — el listener es lo que le avisa al manager que el tween terminó.
            Assert.IsNotNull(pawn.GetComponent<FeedbackCallbackListener>(),
                "Sin listener el step nunca completa y el turno se destraba recién por watchdog.");
            Assert.IsNotNull(GetListener(handle));
        }

        [Test]
        public void DispatchPawnDeath_HidesHealthBar()
        {
            // La barra es hija del pawn: sin apagarla se encoge junto con él y queda
            // un sprite de HP flotando sobre el cadáver.
            var guid = Guid.NewGuid();
            var pawn = RegisterPawn(guid, withHealthBar: true);
            var bar = pawn.GetComponent<EntityPawn>().HealthBar;
            Assert.IsTrue(bar.gameObject.activeSelf, "Precondición: la barra arranca visible.");

            InvokeDispatch(new FeedbackEntry
            {
                Type = FeedbackType.PawnDeath,
                Duration = 0.6f,
                DeathHideHealthBar = true,
            }, guid);

            Assert.IsFalse(bar.gameObject.activeSelf);
        }

        [Test]
        public void DispatchPawnDeath_UnregisteredPawn_IsSafeNoOp()
        {
            // El pawn puede no estar en el registry (tests, sala despawneada). No debe tirar.
            var entry = new FeedbackEntry { Type = FeedbackType.PawnDeath, Duration = 0.6f };

            TestDelegate act = () => InvokeDispatch(entry, Guid.NewGuid());

            Assert.DoesNotThrow(act);
        }

        [Test]
        public void FeedbackEntry_PawnDeathDefaults_CollapseAndSpin()
        {
            var entry = new FeedbackEntry();

            // Un default en 0 grados / 1 de escala dejaría la entry autorada sin efecto.
            Assert.AreEqual(720f, entry.DeathSpinDegrees);
            Assert.AreEqual(0f, entry.DeathEndScale);
            Assert.IsTrue(entry.DeathHideHealthBar);
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private object InvokeDispatch(FeedbackEntry entry, Guid targetGuid)
        {
            var handleType = typeof(FeedbackManager).GetNestedType(
                "PlaybackHandle", BindingFlags.NonPublic);
            var handle = Activator.CreateInstance(handleType, nonPublic: true);
            var request = new FeedbackRequest { TargetGuid = targetGuid };

            _dispatchPawnDeath.Invoke(_manager, new object[] { entry, request, handle });
            return handle;
        }

        private static FeedbackCallbackListener GetListener(object handle) =>
            (FeedbackCallbackListener)handle.GetType()
                .GetField("Listener", BindingFlags.Public | BindingFlags.Instance)
                .GetValue(handle);

        private GameObject RegisterPawn(Guid guid, bool withHealthBar)
        {
            var root = new GameObject("pawn");
            _spawned.Add(root);

            var pawn = root.AddComponent<EntityPawn>();
            if (withHealthBar)
            {
                var barGo = new GameObject("HealthBar");
                barGo.transform.SetParent(root.transform);
                var bar = barGo.AddComponent<WorldSpaceHealthBar>();

                typeof(EntityPawn)
                    .GetField("_healthBar", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(pawn, bar);
            }

            var registry = new PawnRegistry();
            registry.Register(guid, root.transform);
            ServiceLocator.RemoveService<IPawnRegistry>();
            ServiceLocator.AddService<IPawnRegistry>(registry, ServiceScope.Global);

            return root;
        }

        private static void AddEntry(FeedbackDBSO db, FeedbackEntry entry)
        {
            var list = (List<FeedbackEntry>)typeof(FeedbackDBSO)
                .GetField("_entries", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(db);
            list.Add(entry);
            db.RebuildCache();
        }

        private static void AddSequence(FeedbackDBSO db, string id, params FeedbackSequenceStep[] steps)
        {
            var list = (List<FeedbackSequenceEntry>)typeof(FeedbackDBSO)
                .GetField("_sequences", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(db);
            list.Add(new FeedbackSequenceEntry
            {
                SequenceId = id,
                Steps = new List<FeedbackSequenceStep>(steps),
            });
            db.RebuildCache();
        }
    }
}
