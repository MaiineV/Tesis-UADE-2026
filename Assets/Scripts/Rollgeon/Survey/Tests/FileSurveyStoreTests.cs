using System;
using System.IO;
using NUnit.Framework;

namespace Rollgeon.Survey.Tests
{
    /// <summary>Store real en un directorio temporal (Feature#0074): write atómico, orden, mark sent.</summary>
    [TestFixture]
    public class FileSurveyStoreTests
    {
        private string _root;
        private FileSurveyStore _store;

        [SetUp]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "rollgeon-survey-tests", Guid.NewGuid().ToString("N"));
            _store = new FileSurveyStore(_root);
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        [Test]
        public void test_store_empty_root_lists_nothing()
        {
            Assert.AreEqual(0, _store.PendingCount);
            Assert.IsEmpty(_store.ListPending());
        }

        [Test]
        public void test_store_write_then_read_round_trips()
        {
            _store.WritePending("20260101-000001_a", "{\"x\":1}");

            Assert.AreEqual("{\"x\":1}", _store.ReadPending("20260101-000001_a"));
            Assert.IsFalse(File.Exists(Path.Combine(_store.PendingDirectory, "20260101-000001_a.json.tmp")), "No queda el .tmp.");
        }

        [Test]
        public void test_store_list_is_chronological_by_key()
        {
            _store.WritePending("20260101-000002_b", "b");
            _store.WritePending("20260101-000001_a", "a");

            CollectionAssert.AreEqual(new[] { "20260101-000001_a", "20260101-000002_b" }, _store.ListPending());
        }

        [Test]
        public void test_store_mark_sent_moves_file()
        {
            _store.WritePending("20260101-000001_a", "a");

            _store.MarkSent("20260101-000001_a");

            Assert.AreEqual(0, _store.PendingCount);
            Assert.IsNull(_store.ReadPending("20260101-000001_a"));
            Assert.IsTrue(File.Exists(Path.Combine(_store.SentDirectory, "20260101-000001_a.json")), "Se conserva como respaldo.");
        }

        [Test]
        public void test_store_mark_sent_missing_key_is_noop()
        {
            Assert.DoesNotThrow(() => _store.MarkSent("nope"));
        }

        [Test]
        public void test_store_overwrite_same_key_keeps_single_file()
        {
            _store.WritePending("k", "1");
            _store.WritePending("k", "2");

            Assert.AreEqual(1, _store.PendingCount);
            Assert.AreEqual("2", _store.ReadPending("k"));
        }

        [Test]
        public void test_store_invalid_key_throws()
        {
            Assert.Throws<ArgumentException>(() => _store.WritePending("a/b", "x"));
            Assert.Throws<ArgumentException>(() => _store.WritePending("", "x"));
        }
    }
}
