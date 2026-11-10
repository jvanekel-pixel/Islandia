using System;

namespace Island.Core.StreamBox
{
    /// <summary>
    /// A response did not match <c>contract/streambox/v1.json</c> in a way we cannot work around.
    /// </summary>
    /// <remarks>
    /// Thrown only for a <em>missing or unusable required field</em>. An unknown field, or an
    /// unknown value for a known enum, is not an error: StreamBox is under active development and
    /// will add both, and an older Islandia build has to keep working rather than refuse to show
    /// a library. See <see cref="Json"/>.
    /// <para>
    /// The message carries the JSON path, because the alternative — "object reference not set" —
    /// tells whoever reads the log nothing about which field the server stopped sending.
    /// </para>
    /// </remarks>
    public sealed class StreamBoxContractException : Exception
    {
        public StreamBoxContractException(string path, string problem)
            : base($"StreamBox response did not match the contract at '{path}': {problem}")
        {
            Path = path;
            Problem = problem;
        }

        /// <summary>Where in the response the trouble was, e.g. <c>items[2].libraryId</c>.</summary>
        public string Path { get; }

        /// <summary>What was wrong, without the path prefix.</summary>
        public string Problem { get; }
    }
}
