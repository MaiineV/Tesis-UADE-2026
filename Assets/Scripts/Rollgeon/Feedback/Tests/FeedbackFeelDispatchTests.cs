using System.Reflection;
using NUnit.Framework;
using Patterns;
using UnityEngine;

namespace Rollgeon.Feedback.Tests
{
    /// <summary>
    /// Cobertura del branch <see cref="FeedbackType.Feel"/> (MMF_Player) del dispatch §10.3.
    /// La reproducción real del MMF_Player vive en Play Mode — acá cubrimos el contrato
    /// que sí es verificable en EditMode: estabilidad del enum serializado y el no-op seguro.
    /// </summary>
    [TestFixture]
    public class FeedbackFeelDispatchTests
    {
        private GameObject _go;
        private FeedbackManager _manager;
        private MethodInfo _dispatchFeel;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("FeedbackManager");
            _manager = _go.AddComponent<FeedbackManager>();

            // Mismo patrón que FeedbackManagerFloatingDedupTests: el dispatch por tipo es
            // privado y llegar a él por RequestFeedbackBlocking exige un FeedbackDBSO armado.
            _dispatchFeel = typeof(FeedbackManager).GetMethod(
                "DispatchFeel", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(_dispatchFeel, "DispatchFeel no encontrado — revisar rename.");
        }

        [TearDown]
        public void Teardown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        // ── Estabilidad del enum ────────────────────────────────────────
        // El FeedbackDB serializa Type como int. Si alguien inserta un valor en el medio
        // del enum, TODAS las entries autoradas cambian de tipo en silencio.

        [Test]
        public void FeedbackType_ExistingValues_KeepTheirSerializedInts()
        {
            Assert.AreEqual(0, (int)FeedbackType.VFX);
            Assert.AreEqual(1, (int)FeedbackType.SFX);
            Assert.AreEqual(2, (int)FeedbackType.Animation);
            Assert.AreEqual(3, (int)FeedbackType.Wait);
            Assert.AreEqual(4, (int)FeedbackType.BehaviorValue);
            Assert.AreEqual(5, (int)FeedbackType.FloatingNumber);
        }

        [Test]
        public void FeedbackType_Feel_IsAppendedLast()
        {
            Assert.AreEqual(6, (int)FeedbackType.Feel);
        }

        // ── Dispatch ────────────────────────────────────────────────────

        [Test]
        public void DispatchFeel_NullPlayerPrefab_IsSafeNoOp()
        {
            // Arrange — entry Feel sin prefab autorado (el caso del autoral a medio hacer).
            // FeelPlayerPrefab queda en su default null: nombrar el tipo MMF_Player acá
            // obligaría a este asmdef a referenciar MoreMountains.Tools sólo para el test.
            var entry = new FeedbackEntry
            {
                FeedbackId = "feel.test",
                Type = FeedbackType.Feel,
                FeelIntensity = 1f,
            };
            int before = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length;

            // Act
            TestDelegate act = () => _dispatchFeel.Invoke(_manager, new object[] { entry, Vector3.zero });

            // Assert — ni tira, ni instancia nada.
            Assert.DoesNotThrow(act);
            int after = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length;
            Assert.AreEqual(before, after, "Un entry Feel sin prefab no debe instanciar nada.");
        }

        [Test]
        public void FeedbackEntry_FeelIntensity_DefaultsToAuthoredStrength()
        {
            var entry = new FeedbackEntry();

            // 1 = "reproducí el MMF_Player como está autorado". Un default en 0 mataría el juice.
            Assert.AreEqual(1f, entry.FeelIntensity);
        }

        // ── Regresión: fake-null en ResolveAnimator ─────────────────────
        // El pawn registra su transform RAÍZ, pero el Animator vive en el hijo del modelo
        // rigeado. ResolveAnimator hacía `GetComponent ?? GetComponentInChildren`, y como
        // GetComponent devuelve un fake-null, `??` se lo quedaba y nunca caía al hijo:
        // DispatchAnimation abortaba en silencio y ningún enemigo animaba.

        [Test]
        public void GetComponent_WhenMissing_ReturnsFakeNull_SoNullCoalescingIsUnsafe()
        {
            var root = new GameObject("root");
            try
            {
                var missing = root.transform.GetComponent<Animator>();

                // Este es el corazón del bug: para `??` NO es null, para Unity SÍ.
                Assert.IsFalse(ReferenceEquals(missing, null), "GetComponent devolvió un null real — el bug cambió de forma.");
                Assert.IsTrue(missing == null, "El operator== de Unity debe reportarlo como null.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ── Regresión: duración de un step que referencia el DB ─────────
        // GuessDuration ignoraba la Duration autorada en la entry y devolvía el estimate
        // genérico de 5s. Como el TurnManager espera a la secuencia, cada impacto
        // (VFX/SFX/Feel) congelaba el turno 5 segundos en vez de ~0.55s.

        [Test]
        public void GuessDuration_FeedbackRefStep_UsesEntryDurationNotTheGenericEstimate()
        {
            var db = ScriptableObject.CreateInstance<FeedbackDBSO>();
            try
            {
                var entries = (System.Collections.Generic.List<FeedbackEntry>)
                    typeof(FeedbackDBSO)
                        .GetField("_entries", BindingFlags.NonPublic | BindingFlags.Instance)
                        .GetValue(db);
                entries.Add(new FeedbackEntry
                {
                    FeedbackId = "vfx.test",
                    Type = FeedbackType.VFX,
                    Duration = 0.55f,
                });
                db.RebuildCache();
                _manager.Configure(db);

                var step = new FeedbackSequenceStep
                {
                    Source = StepSource.FeedbackRef,
                    FeedbackRefId = "vfx.test",
                    EndMode = StepEndMode.OnDuration,
                    // DurationOverride queda en 0 => tiene que leer la entry.
                };

                var guess = typeof(FeedbackManager).GetMethod(
                    "GuessDuration", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(guess, "GuessDuration no encontrado — revisar rename.");

                var actual = (float)guess.Invoke(_manager, new object[] { step });

                Assert.AreEqual(0.55f, actual, 0.001f, "Debe usar la Duration de la entry, no los 5s genéricos.");
            }
            finally { Object.DestroyImmediate(db); }
        }

        [Test]
        public void ResolveAnimator_WithAnimatorOnChild_FindsIt()
        {
            var root = new GameObject("pawn");
            var child = new GameObject("model");
            child.transform.SetParent(root.transform);
            var expected = child.AddComponent<Animator>();

            try
            {
                var resolve = typeof(FeedbackManager).GetMethod(
                    "ResolveAnimator", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.IsNotNull(resolve, "ResolveAnimator no encontrado — revisar rename.");

                var registry = new PawnRegistry();
                var guid = System.Guid.NewGuid();
                registry.Register(guid, root.transform);

                ServiceLocator.RemoveService<IPawnRegistry>();
                ServiceLocator.AddService<IPawnRegistry>(registry, ServiceScope.Global);

                var actual = (Animator)resolve.Invoke(null, new object[] { guid });

                Assert.AreSame(expected, actual, "El Animator del hijo tiene que resolverse desde el transform raíz del pawn.");
            }
            finally
            {
                ServiceLocator.RemoveService<IPawnRegistry>();
                Object.DestroyImmediate(root);
            }
        }
    }
}
