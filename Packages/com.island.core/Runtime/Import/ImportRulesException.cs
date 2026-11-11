using System;

namespace Island.Core.Import
{
    /// <summary>
    /// The import rule file itself is wrong.
    /// </summary>
    /// <remarks>
    /// Not something a player's model can cause — a bad model is an
    /// <see cref="ImportVerdict"/>, never an exception. This means
    /// <c>contract/import/limits.v1.json</c> is malformed or missing a tier, which is a packaging
    /// fault in the build and should stop it loudly rather than leave a headset importing with no
    /// limits at all.
    /// </remarks>
    public sealed class ImportRulesException : Exception
    {
        public ImportRulesException(string message) : base(message) { }
    }
}
