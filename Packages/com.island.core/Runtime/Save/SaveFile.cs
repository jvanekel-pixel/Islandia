using System;

namespace Island.Core.Save
{
    /// <summary>How a load ended.</summary>
    public enum LoadOutcome
    {
        /// <summary>The save read cleanly.</summary>
        Loaded,
        /// <summary>The save was unreadable, and the backup was used instead.</summary>
        RecoveredFromBackup,
        /// <summary>There is no save here. A new island, not a problem.</summary>
        NotFound,
        /// <summary>Both the save and its backup were unreadable.</summary>
        Lost,
    }

    /// <summary>What a load produced, and what the player should be told.</summary>
    public sealed class LoadResult
    {
        private LoadResult(LoadOutcome outcome, SaveContainer container, string detail)
        {
            Outcome = outcome;
            Container = container;
            Detail = detail;
        }

        public LoadOutcome Outcome { get; }

        /// <summary>Null unless the outcome is <see cref="LoadOutcome.Loaded"/> or recovered.</summary>
        public SaveContainer Container { get; }

        /// <summary>Why, in words a person could be shown. Null when nothing went wrong.</summary>
        public string Detail { get; }

        public bool HasIsland => Container != null;

        internal static LoadResult Loaded(SaveContainer container) =>
            new LoadResult(LoadOutcome.Loaded, container, null);

        internal static LoadResult Recovered(SaveContainer container, string detail) =>
            new LoadResult(LoadOutcome.RecoveredFromBackup, container, detail);

        internal static LoadResult NotFound() => new LoadResult(LoadOutcome.NotFound, null, null);

        internal static LoadResult Lost(string detail) => new LoadResult(LoadOutcome.Lost, null, detail);
    }

    /// <summary>
    /// Reads and writes a save, in a way that survives being interrupted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An island is hours of someone's work and there is no copy anywhere else, so the one thing
    /// this must never do is destroy a good save while writing a new one. The sequence is:
    /// </para>
    /// <list type="number">
    /// <item>write the new bytes to a temporary file beside the target, and flush them to the device;</item>
    /// <item>verify by reading that temporary file back and parsing it;</item>
    /// <item>move the current save aside as the backup;</item>
    /// <item>rename the temporary file into place.</item>
    /// </list>
    /// <para>
    /// Interrupted at any step, what is left on disk is either the old save or the new one, never
    /// a half of either. Step 2 is the unusual one and it is there deliberately: without it a
    /// disk that silently writes nonsense is discovered at load time, by which point the good
    /// copy has already been overwritten.
    /// </para>
    /// </remarks>
    public sealed class SaveFile
    {
        private readonly IFileStore _store;

        public SaveFile(IFileStore store) => _store = store ?? throw new ArgumentNullException(nameof(store));

        public static string BackupPathFor(string path) => path + ".backup";
        public static string TempPathFor(string path) => path + ".writing";

        /// <summary>Writes a save, keeping the previous one as a backup.</summary>
        public void Save(string path, SaveContainer container)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("A save needs a path.", nameof(path));
            if (container == null) throw new ArgumentNullException(nameof(container));

            var temp = TempPathFor(path);
            var backup = BackupPathFor(path);

            _store.Write(temp, container.ToBytes());

            // Read it back before trusting it. A disk that wrote nothing useful should be found
            // out now, while the previous save is still intact.
            try
            {
                SaveContainer.FromBytes(_store.Read(temp));
            }
            catch (Exception error)
            {
                _store.Delete(temp);
                throw new SaveFormatException(
                    "The new save did not read back correctly, so nothing was overwritten: " + error.Message);
            }

            if (_store.Exists(path)) _store.Move(path, backup, overwrite: true);
            _store.Move(temp, path, overwrite: true);
        }

        /// <summary>
        /// Loads a save, falling back to the backup if the save itself is unreadable.
        /// </summary>
        public LoadResult Load(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("A save needs a path.", nameof(path));

            var backup = BackupPathFor(path);
            string trouble = null;

            if (_store.Exists(path))
            {
                try
                {
                    return LoadResult.Loaded(SaveContainer.FromBytes(_store.Read(path)));
                }
                catch (Exception error)
                {
                    trouble = error.Message;
                }
            }

            if (_store.Exists(backup))
            {
                try
                {
                    var container = SaveContainer.FromBytes(_store.Read(backup));
                    return trouble == null
                        ? LoadResult.Recovered(container, "The save itself was missing, so the backup was used.")
                        : LoadResult.Recovered(container, "The save could not be read (" + trouble + "), so the backup was used.");
                }
                catch (Exception error)
                {
                    return LoadResult.Lost(
                        trouble == null
                            ? "The backup could not be read either: " + error.Message
                            : "Neither copy could be read. The save: " + trouble + ". The backup: " + error.Message);
                }
            }

            return trouble == null ? LoadResult.NotFound() : LoadResult.Lost(trouble);
        }

        /// <summary>Whether anything is saved here, counting a usable backup.</summary>
        public bool Exists(string path) => _store.Exists(path) || _store.Exists(BackupPathFor(path));
    }
}
