using NUnit.Framework;
using Patterns.Save;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Patterns.Save.Tests
{
    [TestFixture]
    public class SaveSettingsSOTests
    {
        private SaveSettingsSO _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<SaveSettingsSO>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_settings != null) Object.DestroyImmediate(_settings);
        }

        [Test]
        public void ShouldFlushOn_EnabledAndDisabledTriggers()
        {
            _settings.FlushOn = new[] { SaveTrigger.RunEnd, SaveTrigger.Exit };

            Assert.IsTrue(_settings.ShouldFlushOn(SaveTrigger.RunEnd));
            Assert.IsTrue(_settings.ShouldFlushOn(SaveTrigger.Exit));
            Assert.IsFalse(_settings.ShouldFlushOn(SaveTrigger.RoomEnd));
            Assert.IsFalse(_settings.ShouldFlushOn(SaveTrigger.Manual));
        }

        [Test]
        public void GetSavePath_SlotIndex_FormatsPrefixUnderscoreSlot()
        {
            _settings.SaveFilePrefix = "rollgeon";

            StringAssert.EndsWith("rollgeon_2.save", _settings.GetSavePath(2));

            _settings.ActiveSlot = 1;
            StringAssert.EndsWith("rollgeon_1.save", _settings.GetSavePath());
        }
    }
}
