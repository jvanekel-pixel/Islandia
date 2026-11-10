using System.IO;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;

namespace Island.Core.Tests.StreamBox
{
    /// <summary>
    /// Loads the captured responses in <c>payloads/</c>.
    /// </summary>
    /// <remarks>
    /// Those files were produced by actually calling the mock gateway
    /// (<c>tools/capture-streambox-payloads.mjs</c>), not written by hand. A hand-written fixture
    /// records what its author believed the server sends — which is the very belief these tests
    /// exist to check — so it would agree with a wrong mapping and prove nothing.
    /// <para>
    /// Found relative to this source file rather than the working directory, because that works
    /// the same under <c>dotnet test</c> and Unity's editor test runner.
    /// </para>
    /// </remarks>
    internal static class Payloads
    {
        public static JObject Load(string name) =>
            JObject.Parse(File.ReadAllText(Path.Combine(Directory, "payloads", name + ".json")));

        /// <summary>The repository root, for reading the contract file.</summary>
        public static string RepositoryRoot =>
            Path.GetFullPath(Path.Combine(Directory, "..", "..", "..", ".."));

        private static string Directory => Path.GetDirectoryName(ThisFile());

        private static string ThisFile([CallerFilePath] string path = null) => path;
    }
}
