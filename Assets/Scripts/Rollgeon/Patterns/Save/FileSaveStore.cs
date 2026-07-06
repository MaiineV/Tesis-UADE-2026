using System.IO;

namespace Patterns.Save
{
    /// <summary>
    /// <see cref="ISaveFileStore"/> de producción. Write-to-temp + rename atómico
    /// (§15.3.1): si el proceso muere mid-write, el save anterior queda intacto en vez
    /// de un archivo truncado que rompe <c>LoadFromDisk</c>.
    /// </summary>
    public sealed class FileSaveStore : ISaveFileStore
    {
        public bool Exists(string path) => File.Exists(path);

        public byte[] Read(string path) => File.ReadAllBytes(path);

        public void Write(string path, byte[] bytes)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var tmp = path + ".tmp";
            File.WriteAllBytes(tmp, bytes);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
    }
}
