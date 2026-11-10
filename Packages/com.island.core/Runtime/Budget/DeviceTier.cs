using System;
using System.Collections.Generic;

namespace Island.Core.Budget
{
    /// <summary>Which headset this is, as far as the budgets are concerned.</summary>
    public enum DeviceTier
    {
        /// <summary>
        /// The target, and the floor. Anything unrecognised is treated as this: a device that
        /// turns out to be more capable loses a little headroom, where the opposite drops frames
        /// in someone's face.
        /// </summary>
        Quest2,
        QuestPro,
        Quest3S,
        Quest3,
        /// <summary>Running in the editor. Generous, and never a basis for a claim about speed.</summary>
        Editor,
    }

    /// <summary>
    /// Works out which headset this is from what the runtime reports.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A pure function over a table, deliberately. The blueprint's version was a chain of
    /// substring tests against internal codenames, in two different files that could disagree,
    /// and one of its rules — treat anything narrower than 1900 pixels as a Quest 3S — would
    /// have classed every Quest 3 as the cheaper device if the runtime reported the eye-buffer
    /// width rather than the panel width. Nobody would have noticed except as a vague sense that
    /// the Quest 3 looked worse than it should.
    /// </para>
    /// <para>
    /// So: a table, matched most specific first, with every rule testable without a headset and
    /// the strings themselves confirmed against real devices at the first device gate.
    /// </para>
    /// </remarks>
    public static class DeviceTiers
    {
        /// <summary>One rule: if the model or GPU contains this, it is that tier.</summary>
        private readonly struct Rule
        {
            public Rule(string fragment, DeviceTier tier, string note)
            {
                Fragment = fragment;
                Tier = tier;
                Note = note;
            }

            public string Fragment { get; }
            public DeviceTier Tier { get; }

            /// <summary>Why this rule exists, so a later reader can judge whether it still should.</summary>
            public string Note { get; }
        }

        // Ordered: the first match wins, so the more specific strings come first. "Quest 3S"
        // must be tested before "Quest 3", or every 3S would read as a 3.
        private static readonly Rule[] Rules =
        {
            new Rule("quest 3s",  DeviceTier.Quest3S,  "the cheaper Quest 3: same chip, fewer pixels"),
            new Rule("quest3s",   DeviceTier.Quest3S,  "spelled without the space"),
            new Rule("quest pro", DeviceTier.QuestPro, "Quest 2 chip, better optics"),
            new Rule("questpro",  DeviceTier.QuestPro, "spelled without the space"),
            new Rule("quest 3",   DeviceTier.Quest3,   "the full Quest 3"),
            new Rule("quest3",    DeviceTier.Quest3,   "spelled without the space"),
            new Rule("quest 2",   DeviceTier.Quest2,   "the target device"),
            new Rule("quest2",    DeviceTier.Quest2,   "spelled without the space"),
        };

        /// <summary>
        /// Classifies a device.
        /// </summary>
        /// <param name="deviceModel">What the runtime calls this hardware. May be null.</param>
        /// <param name="gpuName">The graphics device name. May be null.</param>
        /// <param name="isEditor">Whether this is the editor rather than a headset.</param>
        /// <remarks>
        /// Screen dimensions are deliberately <em>not</em> consulted. They were the trap in the
        /// previous version: on a headset the number available is usually the eye-buffer size,
        /// which the runtime scales for performance, so it describes the current render target
        /// rather than the hardware — and classifying hardware by it is circular.
        /// </remarks>
        public static DeviceTier Classify(string deviceModel, string gpuName, bool isEditor = false)
        {
            if (isEditor) return DeviceTier.Editor;

            var haystack = ((deviceModel ?? string.Empty) + " " + (gpuName ?? string.Empty))
                .ToLowerInvariant();

            foreach (var rule in Rules)
                if (haystack.Contains(rule.Fragment)) return rule.Tier;

            // Deliberately the floor, not a guess. An unknown Quest-like device given Quest 3
            // budgets would drop frames; given Quest 2 budgets it merely has headroom spare.
            return DeviceTier.Quest2;
        }

        /// <summary>Every rule, so a test can prove each one matches and none shadows another.</summary>
        public static IEnumerable<(string Fragment, DeviceTier Tier, string Note)> KnownRules()
        {
            foreach (var rule in Rules) yield return (rule.Fragment, rule.Tier, rule.Note);
        }

        /// <summary>Whether a tier has been measured on real hardware, or is still a guess.</summary>
        /// <remarks>
        /// Only the owner's Quest 2 will ever be measured. The rest are carried over from the
        /// blueprint and must be described as unverified wherever they are reported, rather than
        /// quietly presented as fact.
        /// </remarks>
        public static bool IsVerified(DeviceTier tier) => tier == DeviceTier.Quest2;
    }
}
