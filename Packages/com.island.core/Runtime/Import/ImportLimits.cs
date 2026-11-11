using System;
using System.Collections.Generic;
using Island.Core.Budget;
using Island.Core.StreamBox;   // Json: general helpers that happen to live there
using Newtonsoft.Json.Linq;

namespace Island.Core.Import
{
    /// <summary>
    /// What an imported model is allowed to be.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read from <c>contract/import/limits.v1.json</c>, which the browser converter reads too, so
    /// a file the converter produces is a file the headset accepts. Two copies of these numbers
    /// would drift, and the symptom would be a player watching the converter say "ready" and the
    /// headset say "no".
    /// </para>
    /// <para>
    /// The numbers are data, not code, for the same reason as ADR-10's budgets: recalibrating
    /// after a device gate should be an edit to a file, not a change to a program.
    /// </para>
    /// </remarks>
    public sealed class ImportLimits
    {
        private ImportLimits() { }

        // Shared by every tier: the shape of the container, not the cost of the content.
        public long MaxFileBytes { get; private set; }
        public int MaxJsonBytes { get; private set; }
        public int MaxBuffers { get; private set; }
        public int MaxMeshes { get; private set; }
        public int MaxNodes { get; private set; }
        public int MaxImages { get; private set; }
        public int MaxNodeDepth { get; private set; }
        public bool AllowExternalUris { get; private set; }
        public IReadOnlyList<string> AllowedImageTypes { get; private set; }

        // Per tier: what this headset can afford to draw and hold.
        public int MaxTriangles { get; private set; }
        public int MaxPrimitives { get; private set; }
        public int MaxTextureDimension { get; private set; }
        public long MaxEstimatedBytes { get; private set; }

        public DeviceTier Tier { get; private set; }

        /// <summary>
        /// Reads the rule file for one tier.
        /// </summary>
        /// <param name="json">The contents of <c>contract/import/limits.v1.json</c>.</param>
        /// <param name="tier">Which headset's limits to take.</param>
        public static ImportLimits For(string json, DeviceTier tier)
        {
            var body = Json.ParseObject(json, "import limits");
            var shared = Json.RequiredObject(body, "allTiers", "limits");
            var tiers = Json.RequiredObject(body, "tiers", "limits");

            var name = tier.ToString();
            var forTier = Json.OptionalObject(tiers, name);
            if (forTier == null)
            {
                throw new ImportRulesException(
                    $"The import limits file has no entry for {name}. Every tier in DeviceTier "
                    + "needs one, or a headset would import with no limits at all.");
            }

            var limits = new ImportLimits
            {
                Tier = tier,
                MaxFileBytes = Json.RequiredLong(shared, "maxFileBytes", "limits.allTiers"),
                MaxJsonBytes = Json.RequiredInt(shared, "maxJsonBytes", "limits.allTiers"),
                MaxBuffers = Json.RequiredInt(shared, "maxBuffers", "limits.allTiers"),
                MaxMeshes = Json.RequiredInt(shared, "maxMeshes", "limits.allTiers"),
                MaxNodes = Json.RequiredInt(shared, "maxNodes", "limits.allTiers"),
                MaxImages = Json.RequiredInt(shared, "maxImages", "limits.allTiers"),
                MaxNodeDepth = Json.RequiredInt(shared, "maxNodeDepth", "limits.allTiers"),
                AllowExternalUris = Json.RequiredBool(shared, "allowExternalUris", "limits.allTiers"),
                AllowedImageTypes = Json.OptionalStrings(shared, "allowedImageTypes"),
                MaxTriangles = Json.RequiredInt(forTier, "maxTriangles", $"limits.tiers.{name}"),
                MaxPrimitives = Json.RequiredInt(forTier, "maxPrimitives", $"limits.tiers.{name}"),
                MaxTextureDimension = Json.RequiredInt(forTier, "maxTextureDimension", $"limits.tiers.{name}"),
                MaxEstimatedBytes = Json.RequiredLong(forTier, "maxEstimatedBytes", $"limits.tiers.{name}"),
            };

            if (limits.AllowExternalUris)
            {
                // Not configurable by accident. A glTF may point its buffers and images at any
                // URI; honouring them would let a dropped file make the headset fetch whatever it
                // named, from inside the player's home network.
                throw new ImportRulesException(
                    "allowExternalUris must be false. The headset is the trust boundary (ADR-12) "
                    + "and an imported file does not get to choose what it fetches.");
            }

            return limits;
        }
    }
}
