using System;

namespace Island.Core.Rides
{
    public static partial class CoasterPieces
    {
        /// <summary>
        /// How big the pack is built in the world: two metres to the pack's unit. A planning
        /// figure, from the pack's own proportions — its cars are 0.7 × 1.3 units, which at 2 m
        /// is a two-seat car 1.4 m wide, the tight turn a 4 m radius and the loop 8 m tall.
        /// RC-03 confirms it against the avatar; the track itself is stored in units, so changing
        /// it rescales coasters rather than breaking them. A power of two, so sub-units convert
        /// to metres exactly.
        /// </summary>
        public const float MetresPerUnit = 2f;

        /// <summary>One sub-unit in metres: 1/32 m.</summary>
        public const float MetresPerSubUnit = MetresPerUnit / CoasterShapes.SubUnitsPerUnit;

        /// <summary>The piece a saved coaster names by number, or null if this build has none.</summary>
        public static TrackPiece Find(uint numericId)
        {
            foreach (var piece in All)
                if (piece.NumericId == numericId) return piece;
            return null;
        }

        /// <summary>The piece with this id, compared ordinally, or null.</summary>
        public static TrackPiece Find(string id)
        {
            if (id == null) return null;
            foreach (var piece in All)
                if (string.Equals(piece.Id, id, StringComparison.Ordinal)) return piece;
            return null;
        }
    }
}
