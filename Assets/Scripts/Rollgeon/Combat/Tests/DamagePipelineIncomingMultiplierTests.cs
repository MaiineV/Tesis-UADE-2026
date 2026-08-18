using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Pipelines;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Tests del stage 3 de <see cref="DamagePipeline"/>: el seam
    /// <see cref="IIncomingDamageMultiplierProvider"/>, que TECHNICAL.md §12.2 tenía reservado como
    /// placeholder y que llena la armadura de la mesa de La Generala.
    /// </summary>
    /// <remarks>
    /// Lo que protegen: que sin provider registrado el pipeline se comporte idéntico, que
    /// <c>Resolve</c> y <c>Preview</c> den el mismo número (si no, el desglose que el jugador lee
    /// miente sobre lo que va a pasar), y que un golpe reducido nunca muestre 0.
    /// </remarks>
    [TestFixture]
    public class DamagePipelineIncomingMultiplierTests
    {
        private AttributesManager _attrs;
        private DamagePipeline _pipeline;
        private Guid _target;
        private Guid _source;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();

            _attrs = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_attrs);

            _target = Guid.NewGuid();
            _source = Guid.NewGuid();

            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(240));
            attrs.SetAttribute<Shield>(new Shield(0));
            _attrs.Register(_target, attrs);

            _pipeline = new DamagePipeline(_attrs);
        }

        [TearDown]
        public void TearDown()
        {
            _attrs?.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // ---- Helpers -----------------------------------------------------

        private DamageContext NewHit(int damage) => new DamageContext
        {
            SourceId = _source,
            TargetId = _target,
            BaseDamage = damage,
        };

        private void RegisterProvider(float multiplier, Guid? onlyFor = null) =>
            ServiceLocator.AddService<IIncomingDamageMultiplierProvider>(
                new StubProvider { Multiplier = multiplier, OnlyFor = onlyFor ?? _target });

        // ---- Sin provider -------------------------------------------------

        [Test]
        public void WithoutProvider_TheHitIsUntouched()
        {
            var ctx = _pipeline.Resolve(NewHit(30));

            Assert.AreEqual(30, ctx.FinalDamage);
            Assert.AreEqual(1f, ctx.IncomingMultiplier, 0.001f,
                "1 y no 0: un consumidor que lea 0 leería '-100%' de un hit que nadie modificó.");
        }

        [Test]
        public void ProviderThatDeclinesTheTarget_LeavesItUntouched()
        {
            RegisterProvider(0.3f, onlyFor: Guid.NewGuid());

            var ctx = _pipeline.Resolve(NewHit(30));

            Assert.AreEqual(30, ctx.FinalDamage);
            Assert.AreEqual(1f, ctx.IncomingMultiplier, 0.001f);
        }

        // ---- Con reducción -------------------------------------------------

        [Test]
        public void Resolve_AppliesTheMultiplier_AndReportsIt()
        {
            // 70% de reducción: la mesa entera de La Generala en pie.
            RegisterProvider(0.3f);

            var ctx = _pipeline.Resolve(NewHit(30));

            Assert.AreEqual(9, ctx.FinalDamage, "30 × 0.3 = 9.");
            Assert.AreEqual(0.3f, ctx.IncomingMultiplier, 0.001f);
        }

        [Test]
        public void Resolve_CommitsTheReducedDamage_ToHealth()
        {
            RegisterProvider(0.3f);

            _pipeline.Resolve(NewHit(30));

            Assert.AreEqual(231, _attrs.GetAttribute<Health>(_target).Value,
                "Lo que le baja la vida es el número reducido, no el crudo.");
        }

        [Test]
        public void Preview_MatchesResolve_OrTheBreakdownLies()
        {
            RegisterProvider(0.3f);

            var preview = _pipeline.Preview(NewHit(30));
            var resolved = _pipeline.Resolve(NewHit(30));

            Assert.AreEqual(resolved.FinalDamage, preview.FinalDamage,
                "El jugador vería 30 en el desglose y la barra del jefe bajaría 9.");
            Assert.AreEqual(resolved.IncomingMultiplier, preview.IncomingMultiplier, 0.001f);
        }

        [Test]
        public void Preview_DoesNotTouchHealth()
        {
            RegisterProvider(0.3f);

            _pipeline.Preview(NewHit(30));

            Assert.AreEqual(240, _attrs.GetAttribute<Health>(_target).Value);
        }

        // ---- Orden con la debilidad ----------------------------------------

        [Test]
        public void TheMultiplier_AppliesAfterWeakness_SoBothStagesCount()
        {
            // Arrange — 30 × 1.5 (debilidad) × 0.3 (armadura) = 13.5 → 14.
            RegisterProvider(0.3f);
            var pipeline = new DamagePipeline(_attrs, new StubWeaknessChecker { Multiplier = 1.5f });

            // Act
            var ctx = pipeline.Resolve(new DamageContext
            {
                SourceId = _source,
                TargetId = _target,
                BaseDamage = 30,
                IsWeaknessHit = true,
                ComboId = "combo.generala",
            });

            // Assert — pegarle a la debilidad sigue valiendo con la mesa en pie: es el orden de stages
            // que documenta §12.2, y el que hace que romper dados Y acertar la debilidad se sumen.
            Assert.AreEqual(14, ctx.FinalDamage);
        }

        // ---- El piso de 1 ---------------------------------------------------

        [Test]
        public void AReducedHit_NeverShowsZero()
        {
            // Arrange — 1 × 0.3 = 0.3 → redondea a 0.
            RegisterProvider(0.3f);

            // Act
            var ctx = _pipeline.Resolve(NewHit(1));

            // Assert — un golpe que muestra 0 se lee como un bug, no como una armadura.
            Assert.AreEqual(1, ctx.FinalDamage);
        }

        [Test]
        public void AZeroDamageHit_StaysZero_TheFloorDoesNotInventDamage()
        {
            RegisterProvider(0.3f);

            var ctx = _pipeline.Resolve(NewHit(0));

            Assert.AreEqual(0, ctx.FinalDamage);
            Assert.AreEqual(1f, ctx.IncomingMultiplier, 0.001f);
        }

        [Test]
        public void ANegativeMultiplier_IsClampedToZero_AndStillLeavesTheFloor()
        {
            // Arrange — un provider mal escrito no puede curar al target.
            RegisterProvider(-2f);

            // Act
            var ctx = _pipeline.Resolve(NewHit(30));

            // Assert
            Assert.AreEqual(1, ctx.FinalDamage);
            Assert.AreEqual(240 - 1, _attrs.GetAttribute<Health>(_target).Value);
        }

        [Test]
        public void AMultiplierOfOne_IsTreatedAsNoChange()
        {
            RegisterProvider(1f);

            var ctx = _pipeline.Resolve(NewHit(30));

            Assert.AreEqual(30, ctx.FinalDamage);
            Assert.AreEqual(1f, ctx.IncomingMultiplier, 0.001f,
                "Sin cambio real no se reporta reducción: el HUD no debe pintar un '-0%'.");
        }

        // ---- El payload ------------------------------------------------------

        [Test]
        public void TheResolvedPayload_CarriesTheMultiplier_SoTheHudCanExplainTheNumber()
        {
            RegisterProvider(0.3f);
            float seen = -1f;
            Action<DamageResolvedPayload> handler = p => seen = p.IncomingMultiplier;
            TypedEvent<DamageResolvedPayload>.Subscribe(handler);

            try
            {
                _pipeline.Resolve(NewHit(30));

                // Sin esto un golpe de 30 que hace 9 no tiene explicación en pantalla y el jugador
                // aprende "mis golpes no sirven" en vez de "rompé los dados".
                Assert.AreEqual(0.3f, seen, 0.001f);
            }
            finally
            {
                TypedEvent<DamageResolvedPayload>.Unsubscribe(handler);
            }
        }

        // ---- Stubs -------------------------------------------------------------

        private sealed class StubProvider : IIncomingDamageMultiplierProvider
        {
            public float Multiplier;
            public Guid OnlyFor;

            public bool TryGetMultiplier(Guid targetId, out float multiplier)
            {
                multiplier = 1f;
                if (targetId != OnlyFor) return false;

                multiplier = Multiplier;
                return true;
            }
        }

        private sealed class StubWeaknessChecker : Weakness.IWeaknessChecker
        {
            public float Multiplier = 1.5f;

            public float GetMultiplier(Guid sourceId, Guid targetId, string comboId) => Multiplier;
            public float PeekMultiplier(Guid sourceId, Guid targetId, string comboId) => Multiplier;
        }
    }
}
