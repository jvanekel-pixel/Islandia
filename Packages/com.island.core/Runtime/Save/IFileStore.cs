using System;
using System.Collections.Generic;
using System.IO;

namespace Island.Core.Save
{
    /// <summary>
    /// The handful of file operations saving needs.
    /// </summary>
    /// <remarks>
    /// An interface rather than direct <c>System.IO</c> calls for one reason: the most important
    /// property of a save system is what happens when a write is interrupted, and that cannot be
    /// tested against a real disk without doing something barbaric. A fake store can simply refuse
    /// the third write, and then we find out whether the island survived.
    /// </remarks>
    public interface IFileStore
    {
        bool Exists(string path);
        byte[] Read(string path);
        void Write(string path, byte[] bytes);
        void Move(string from, string to, bool overwrite);
        void Copy(string from, string to, bool overwrite);
        void Delete(string path);
        IEnumerable<string> List(string directory, string pattern);
    }

    /// <summary>The real disk.</summary>
    public sealed class DiskFileStore : IFileStore
    {
        public bool Exists(string path) => File.Exists(path);

        public byte[] Read(string path) => File.ReadAllBytes(path);

        public void Write(string path, byte[] bytes)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                // Push it to the device before we rename over the previous save. Without this the
                // rename can land while the contents are still in a buffer, which is how a power
                // cut turns into a file of zeroes.
                stream.Flush(true);
            }
        }

        public void Move(string from, string to, bool overwrite)
        {
            if (overwrite && File.Exists(to)) File.Delete(to);
            File.Move(from, to);
        }

        public void Copy(string from, string to, bool overwrite) => File.Copy(from, to, overwrite);

        public void Delete(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        public IEnumerable<string> List(string directory, string pattern) =>
            Directory.Exists(directory) ? Directory.GetFiles(directory, pattern) : new string[0];
    }
}
