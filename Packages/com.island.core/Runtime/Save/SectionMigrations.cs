using System;
using System.Collections.Generic;

namespace Island.Core.Save
{
    /// <summary>
    /// Brings an old section's payload forward, one version at a time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Islands outlive the code that made them. Someone will build something they care about,
    /// not open the game for six months, and come back to a build that has changed the terrain
    /// format twice. Either that island still opens or it does not, and the difference is decided
    /// now, while there is exactly one version to support and the work is trivial.
    /// </para>
    /// <para>
    /// Steps go from one version to the next, never from any version to the current one. Writing
    /// 1→3 directly looks shorter until 4 arrives and there are suddenly six functions that each
    /// have to know about every format that ever existed.
    /// </para>
    /// </remarks>
    public sealed class SectionMigrations
    {
        private readonly Dictionary<int, Func<byte[], byte[]>> _steps = new Dictionary<int, Func<byte[], byte[]>>();

        public SectionMigrations(string sectionId, int currentVersion)
        {
            if (currentVersion < 1) throw new ArgumentOutOfRangeException(nameof(currentVersion));
            SectionId = sectionId;
            CurrentVersion = currentVersion;
        }

        public string SectionId { get; }
        public int CurrentVersion { get; }

        /// <summary>Adds the step that turns <paramref name="fromVersion"/> into the one after it.</summary>
        public SectionMigrations Step(int fromVersion, Func<byte[], byte[]> migrate)
        {
            if (migrate == null) throw new ArgumentNullException(nameof(migrate));
            if (fromVersion < 1 || fromVersion >= CurrentVersion)
            {
                throw new ArgumentOutOfRangeException(nameof(fromVersion),
                    $"A step must go from an older version towards {CurrentVersion}; got {fromVersion}.");
            }
            if (_steps.ContainsKey(fromVersion))
                throw new InvalidOperationException($"There is already a step from version {fromVersion}.");
            _steps[fromVersion] = migrate;
            return this;
        }

        /// <summary>Whether every version from <paramref name="oldest"/> up has a path forward.</summary>
        public bool CanMigrateFrom(int oldest)
        {
            for (var version = oldest; version < CurrentVersion; version++)
                if (!_steps.ContainsKey(version)) return false;
            return true;
        }

        /// <summary>Applies every step needed to bring a payload up to date.</summary>
        public byte[] Migrate(byte[] payload, int fromVersion)
        {
            if (fromVersion == CurrentVersion) return payload;
            if (fromVersion > CurrentVersion)
            {
                throw new SaveFormatException(
                    $"Section '{SectionId}' is version {fromVersion}; this build writes {CurrentVersion}. " +
                    "It was probably saved by a newer version of the game.");
            }

            var current = payload;
            for (var version = fromVersion; version < CurrentVersion; version++)
            {
                if (!_steps.TryGetValue(version, out var step))
                {
                    throw new SaveFormatException(
                        $"Section '{SectionId}' is version {version}, and this build has no way to bring it forward. " +
                        "That island cannot be opened by this version.");
                }
                current = step(current);
            }
            return current;
        }
    }
}
