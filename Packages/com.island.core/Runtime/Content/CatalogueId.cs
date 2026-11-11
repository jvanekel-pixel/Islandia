using System.Text;
using Island.Core.Save;

namespace Island.Core.Content
{
    /// <summary>
    /// Turns a catalogue id string into the number a saved island stores.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same derivation as <c>tools/content/generate_ids.py</c>: CRC-32 of the id's UTF-8 bytes.
    /// Having it here as well as in the generator is not duplication for its own sake — the
    /// generator runs at build time and this runs when a save file or a piece of configuration
    /// hands us an id string at runtime. A test asserts the two agree, because if they ever
    /// disagreed the symptom would be an island that loads with some of its contents missing and
    /// no error anywhere.
    /// </para>
    /// <para>
    /// CRC-32 is not a security property and nothing here pretends otherwise. It is a stable,
    /// cheap, well-defined mapping that both toolchains already have.
    /// </para>
    /// </remarks>
    public static class CatalogueId
    {
        /// <summary>No item. An empty slot in a saved island reads as this.</summary>
        public const uint None = 0u;

        /// <summary>The numeric id for a catalogue id string.</summary>
        public static uint Of(string id) =>
            string.IsNullOrEmpty(id) ? None : Crc32.Compute(Encoding.UTF8.GetBytes(id));

        /// <summary>
        /// Whether a numeric id is in the catalogue this build was generated from.
        /// </summary>
        /// <remarks>
        /// A linear scan. The catalogue settles at about 120 items (ADR-11) and this is called
        /// when a save is read, not per frame; an index would be faster and would also have to be
        /// kept in step with the generated array, which is a worse trade at this size.
        /// </remarks>
        public static bool IsKnown(uint numericId)
        {
            if (numericId == None) return false;
            var all = CatalogueIds.All;
            for (var i = 0; i < all.Length; i++) if (all[i] == numericId) return true;
            return false;
        }
    }
}
