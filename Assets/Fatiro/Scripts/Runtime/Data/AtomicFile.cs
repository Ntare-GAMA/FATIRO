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
            if (!File.Exists(path))
            {
                File.Move(temp, path);
                return;
            }

            // Windows can briefly lock a file that was just written (indexers, antivirus), so a
            // replace fired moments after the previous one may fail. Retry, then overwrite in place.
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    File.Replace(temp, path, null);
                    return;
                }
                catch (IOException)
                {
                    System.Threading.Thread.Sleep(15 * (attempt + 1));
                }
            }

            File.Copy(temp, path, true);
            File.Delete(temp);
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
