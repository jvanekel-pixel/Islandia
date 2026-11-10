using System;
using System.Collections.Generic;
using System.Linq;
using Island.Core.Save;

namespace Island.Core.Tests.Save
{
    /// <summary>
    /// A file store in memory that can be made to fail on command.
    /// </summary>
    /// <remarks>
    /// The whole point of the save system is what happens when a write is interrupted, and that
    /// cannot be tested against a real disk without pulling a plug. Here a test can say "fail the
    /// next write" or "corrupt this file" and then ask whether the island survived.
    /// </remarks>
    internal sealed class FakeFileStore : IFileStore
    {
        private readonly Dictionary<string, byte[]> _files = new Dictionary<string, byte[]>();

        /// <summary>Every operation performed, in order, for asserting on the sequence.</summary>
        public List<string> Operations { get; } = new List<string>();

        /// <summary>Throw on the write whose path contains this. Cleared after it fires.</summary>
        public string FailWriteContaining { get; set; }

        /// <summary>Throw on the move whose destination contains this. Cleared after it fires.</summary>
        public string FailMoveToContaining { get; set; }

        public IEnumerable<string> Paths => _files.Keys.OrderBy(p => p);

        public bool Exists(string path) => _files.ContainsKey(path);

        public byte[] Read(string path)
        {
            Operations.Add("read " + path);
            if (!_files.TryGetValue(path, out var bytes))
                throw new System.IO.FileNotFoundException("No such file in the fake store.", path);
            return (byte[])bytes.Clone();
        }

        public void Write(string path, byte[] bytes)
        {
            Operations.Add("write " + path);
            if (FailWriteContaining != null && path.Contains(FailWriteContaining))
            {
                FailWriteContaining = null;
                // What a power cut looks like: some bytes landed, then nothing.
                _files[path] = bytes.Take(bytes.Length / 2).ToArray();
                throw new System.IO.IOException("The fake store was interrupted mid-write.");
            }
            _files[path] = (byte[])bytes.Clone();
        }

        public void Move(string from, string to, bool overwrite)
        {
            Operations.Add($"move {from} -> {to}");
            if (FailMoveToContaining != null && to.Contains(FailMoveToContaining))
            {
                FailMoveToContaining = null;
                throw new System.IO.IOException("The fake store was interrupted mid-move.");
            }
            if (!_files.ContainsKey(from)) throw new System.IO.FileNotFoundException("Nothing to move.", from);
            if (_files.ContainsKey(to) && !overwrite) throw new System.IO.IOException("Already exists: " + to);
            _files[to] = _files[from];
            _files.Remove(from);
        }

        public void Copy(string from, string to, bool overwrite)
        {
            Operations.Add($"copy {from} -> {to}");
            if (!_files.ContainsKey(from)) throw new System.IO.FileNotFoundException("Nothing to copy.", from);
            _files[to] = (byte[])_files[from].Clone();
        }

        public void Delete(string path)
        {
            Operations.Add("delete " + path);
            _files.Remove(path);
        }

        public IEnumerable<string> List(string directory, string pattern) =>
            _files.Keys.Where(p => p.StartsWith(directory, StringComparison.Ordinal));

        // ---- for tests to meddle with -------------------------------------------------------------

        public void Put(string path, byte[] bytes) => _files[path] = (byte[])bytes.Clone();

        /// <summary>Flips a byte somewhere in the middle, as a bad sector would.</summary>
        public void Corrupt(string path, int at = -1)
        {
            var bytes = _files[path];
            var index = at >= 0 ? at : bytes.Length / 2;
            bytes[index] ^= 0xFF;
        }

        /// <summary>Cuts a file short, as an interrupted copy would.</summary>
        public void Truncate(string path, int toLength) =>
            _files[path] = _files[path].Take(toLength).ToArray();
    }
}
