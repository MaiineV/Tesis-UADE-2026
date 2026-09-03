using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Rollgeon.Survey
{
    /// <summary>
    /// <see cref="ISurveyStore"/> de producción: <c>persistentDataPath/survey/pending</c>
    /// y <c>.../sent</c>. Escritura tmp+rename como <c>FileSaveStore</c> para que un
    /// corte a mitad de escritura no deje un JSON truncado. Las enviadas se conservan
    /// como respaldo: si el wifi del stand nunca vuelve, los archivos siguen ahí.
    /// </summary>
    public sealed class FileSurveyStore : ISurveyStore
    {
        public const string RootFolderName = "survey";
        public const string PendingFolderName = "pending";
        public const string SentFolderName = "sent";
        private const string Extension = ".json";

        private readonly string _pendingDir;
        private readonly string _sentDir;

        public static string DefaultRoot => Path.Combine(Application.persistentDataPath, RootFolderName);

        public FileSurveyStore(string rootDir)
        {
            if (string.IsNullOrWhiteSpace(rootDir)) throw new ArgumentException("rootDir vacío", nameof(rootDir));
            _pendingDir = Path.Combine(rootDir, PendingFolderName);
            _sentDir = Path.Combine(rootDir, SentFolderName);
        }

        public string PendingDirectory => _pendingDir;
        public string SentDirectory => _sentDir;

        public int PendingCount => ListPending().Count;

        public IReadOnlyList<string> ListPending()
        {
            if (!Directory.Exists(_pendingDir)) return Array.Empty<string>();

            var files = Directory.GetFiles(_pendingDir, "*" + Extension);
            var keys = new List<string>(files.Length);
            foreach (var file in files)
            {
                keys.Add(Path.GetFileNameWithoutExtension(file));
            }

            // Las claves llevan prefijo de timestamp: orden lexicográfico = cronológico.
            keys.Sort(StringComparer.Ordinal);
            return keys;
        }

        public void WritePending(string key, string json)
        {
            ValidateKey(key);
            Directory.CreateDirectory(_pendingDir);

            var path = PendingPath(key);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, json ?? string.Empty);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        public string ReadPending(string key)
        {
            ValidateKey(key);
            var path = PendingPath(key);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        public void MarkSent(string key)
        {
            ValidateKey(key);
            var from = PendingPath(key);
            if (!File.Exists(from)) return;

            Directory.CreateDirectory(_sentDir);
            var to = Path.Combine(_sentDir, key + Extension);
            if (File.Exists(to)) File.Delete(to);
            File.Move(from, to);
        }

        private string PendingPath(string key) => Path.Combine(_pendingDir, key + Extension);

        private static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("key vacía", nameof(key));
            if (key.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException($"key '{key}' contiene caracteres inválidos para un nombre de archivo", nameof(key));
            }
        }
    }
}
