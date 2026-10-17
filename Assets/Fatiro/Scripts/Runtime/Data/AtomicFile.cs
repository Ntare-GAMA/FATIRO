using System.IO;

namespace Fatiro.Data
{
    /// <summary>
    /// Writes to a temporary file first and then swaps it in, so a crash or forced close never
    /// leaves a half-written session file (DMP 6.3).
    /// </summary>
    public static class AtomicFile
    {
        public static void WriteAllText(string path, string contents)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var temp = path + ".tmp";
            File.WriteAllText(temp, contents);
            if (File.Exists(path))
            {
                File.Replace(temp, path, null);
            }
            else
            {
                File.Move(temp, path);
            }
        }

        public static bool TryReadAllText(string path, out string contents)
        {
            contents = null;
            if (!File.Exists(path)) return false;
            contents = File.ReadAllText(path);
            return true;
        }
    }
}
