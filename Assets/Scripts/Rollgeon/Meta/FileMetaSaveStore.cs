using System;
using System.IO;
using UnityEngine;

namespace Rollgeon.Meta
{
    /// <summary>
    /// <see cref="IMetaSaveStore"/> de producción (#164): JSON plano en
    /// <c>Application.persistentDataPath/meta_progression.json</c> vía
    /// <c>JsonUtility</c>. Defensivo: un archivo corrupto o un error de IO degradan
    /// a "sin save" (estado inicial) en vez de crashear el bootstrap.
    /// </summary>
    public sealed class FileMetaSaveStore : IMetaSaveStore
    {
        private const string LogPrefix = "[FileMetaSaveStore] ";
        public const string FileName = "meta_progression.json";

        private readonly string _path;

        public FileMetaSaveStore() : this(Path.Combine(Application.persistentDataPath, FileName)) { }

        /// <summary>Path explícito — usado por tests para escribir en un temp dir.</summary>
        public FileMetaSaveStore(string path)
        {
            _path = path;
        }

        /// <inheritdoc />
        public MetaProgressionSnapshot Load()
        {
            try
            {
                if (!File.Exists(_path)) return null;
                var json = File.ReadAllText(_path);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonUtility.FromJson<MetaProgressionSnapshot>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(LogPrefix + $"No se pudo leer '{_path}' — arrancando sin save. {ex.Message}");
                return null;
            }
        }

        /// <inheritdoc />
        public void Save(MetaProgressionSnapshot snapshot)
        {
            if (snapshot == null) return;
            try
            {
                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_path, JsonUtility.ToJson(snapshot, prettyPrint: true));
            }
            catch (Exception ex)
            {
                Debug.LogError(LogPrefix + $"No se pudo escribir '{_path}'. {ex.Message}");
            }
        }

        /// <inheritdoc />
        public void Delete()
        {
            try
            {
                if (File.Exists(_path)) File.Delete(_path);
            }
            catch (Exception ex)
            {
                Debug.LogError(LogPrefix + $"No se pudo borrar '{_path}'. {ex.Message}");
            }
        }
    }
}
