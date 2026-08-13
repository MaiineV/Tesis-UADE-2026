using NUnit.Framework;
using Patterns;

namespace Rollgeon.Analytics.Tests
{
    /// <summary>
    /// Tests del <see cref="AnalyticsConsentService"/> (Feature#0029):
    /// persistencia en PlayerPrefs, aplicación lazy al gateway, y degradación
    /// sin throw cuando el SDK no está registrado o no inicializó.
    /// </summary>
    [TestFixture]
    public class AnalyticsConsentServiceTests
    {
        private AnalyticsConsentService _service;
        private FakeGateway _gateway;

        internal sealed class FakeGateway : IAnalyticsGateway
        {
            public bool Initialized { get; set; }
            public string PrivacyUrl { get; set; } = "https://fake.test/privacy";
            public bool DeletionResult { get; set; } = true;

            public readonly System.Collections.Generic.List<bool> ConsentCalls =
                new System.Collections.Generic.List<bool>();

            public int DeletionRequests { get; private set; }

            public void ApplyConsent(bool granted) => ConsentCalls.Add(granted);

            public bool TryRequestDataDeletion()
            {
                DeletionRequests++;
                return DeletionResult;
            }
        }

        [SetUp]
        public void Setup()
        {
            ServiceLocator.Clear();
            AnalyticsPrefs.ClearDecision();

            _gateway = new FakeGateway();
            _service = new AnalyticsConsentService();
        }

        [TearDown]
        public void Teardown()
        {
            AnalyticsPrefs.ClearDecision();
            ServiceLocator.Clear();
        }

        private void RegisterGateway() =>
            ServiceLocator.AddService<IAnalyticsGateway>(_gateway, ServiceScope.Global);

        // ====================================================================
        // Persistencia
        // ====================================================================

        [Test]
        public void FreshPrefs_HasDecisionFalse_AndIsGrantedFalse()
        {
            Assert.That(_service.HasDecision, Is.False);
            Assert.That(_service.IsGranted, Is.False);
        }

        [Test]
        public void SetConsent_True_PersistsDecisionAndGrant()
        {
            _service.SetConsent(true);

            Assert.That(_service.HasDecision, Is.True);
            Assert.That(_service.IsGranted, Is.True);
            // Otra instancia ve lo mismo — el estado vive en prefs, no en el objeto.
            Assert.That(new AnalyticsConsentService().IsGranted, Is.True);
        }

        [Test]
        public void SetConsent_False_PersistsDecisionWithoutGrant()
        {
            _service.SetConsent(false);

            Assert.That(_service.HasDecision, Is.True);
            Assert.That(_service.IsGranted, Is.False);
        }

        [Test]
        public void SetConsent_Revoke_OverwritesPreviousGrant()
        {
            _service.SetConsent(true);

            _service.SetConsent(false);

            Assert.That(_service.HasDecision, Is.True);
            Assert.That(_service.IsGranted, Is.False);
        }

        // ====================================================================
        // Gateway
        // ====================================================================

        [Test]
        public void SetConsent_AppliesToGateway_WhenInitialized()
        {
            RegisterGateway();
            _gateway.Initialized = true;

            _service.SetConsent(true);
            _service.SetConsent(false);

            Assert.That(_gateway.ConsentCalls, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public void SetConsent_SkipsGateway_WhenNotInitialized()
        {
            RegisterGateway();
            _gateway.Initialized = false;

            _service.SetConsent(true);

            // El bootstrap UGS relee las prefs al terminar su init — acá solo
            // importa que no explote ni llame al SDK dormido.
            Assert.That(_gateway.ConsentCalls, Is.Empty);
            Assert.That(_service.IsGranted, Is.True);
        }

        [Test]
        public void SetConsent_DoesNotThrow_WhenNoGatewayRegistered()
        {
            Assert.DoesNotThrow(() => _service.SetConsent(true));
            Assert.That(_service.IsGranted, Is.True);
        }

        // ====================================================================
        // PrivacyUrl / borrado de datos
        // ====================================================================

        [Test]
        public void PrivacyUrl_UsesGatewayUrl_WhenAvailable()
        {
            RegisterGateway();

            Assert.That(_service.PrivacyUrl, Is.EqualTo("https://fake.test/privacy"));
        }

        [Test]
        public void PrivacyUrl_FallsBackToUnityPolicy_WhenNoGateway()
        {
            Assert.That(_service.PrivacyUrl, Is.EqualTo("https://unity.com/legal/privacy-policy"));
        }

        [Test]
        public void TryRequestDataDeletion_DelegatesToGateway_OnlyWhenInitialized()
        {
            RegisterGateway();
            _gateway.Initialized = false;

            Assert.That(_service.TryRequestDataDeletion(), Is.False);
            Assert.That(_gateway.DeletionRequests, Is.EqualTo(0));

            _gateway.Initialized = true;

            Assert.That(_service.TryRequestDataDeletion(), Is.True);
            Assert.That(_gateway.DeletionRequests, Is.EqualTo(1));
        }
    }
}
