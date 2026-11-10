namespace Island.Core.Save
{
    /// <summary>
    /// CRC-32, used to tell a corrupt save from a good one.
    /// </summary>
    /// <remarks>
    /// Written out rather than taken from a package because <c>Island.Core</c> is compiled by two
    /// toolchains and <c>System.IO.Hashing</c> is in neither by default. This is not a security
    /// check — nobody is attacking a local save file — it is a check against a half-written file,
    /// a bad sector or a sync client that copied a file mid-write, and CRC-32 catches all of
    /// those.
    /// </remarks>
    public static class Crc32
    {
        private const uint Polynomial = 0xEDB88320u;
        private static readonly uint[] Table = BuildTable();

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (var i = 0u; i < 256u; i++)
            {
                var value = i;
                for (var bit = 0; bit < 8; bit++)
                    value = (value & 1u) != 0u ? (value >> 1) ^ Polynomial : value >> 1;
                table[i] = value;
            }
            return table;
        }

        public static uint Compute(byte[] data) => Compute(data, 0, data?.Length ?? 0);

        public static uint Compute(byte[] data, int offset, int count)
        {
            if (data == null) return 0u;
            var crc = 0xFFFFFFFFu;
            for (var i = offset; i < offset + count; i++)
                crc = (crc >> 8) ^ Table[(crc ^ data[i]) & 0xFFu];
            return crc ^ 0xFFFFFFFFu;
        }
    }
}
