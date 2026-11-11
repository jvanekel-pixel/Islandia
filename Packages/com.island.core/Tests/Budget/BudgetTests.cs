using System;
using System.Linq;
using Island.Core.Budget;
using NUnit.Framework;

namespace Island.Core.Tests.Budget
{
    [TestFixture]
    public class DeviceTierTests
    {
        [Test]
        public void TheOwnersHeadsetIsRecognised()
        {
            Assert.That(DeviceTiers.Classify("Oculus Quest 2", "Adreno (TM) 650"), Is.EqualTo(DeviceTier.Quest2));
            Assert.That(DeviceTiers.Classify("Meta Quest 2", "Adreno 650"), Is.EqualTo(DeviceTier.Quest2));
        }

        [Test]
        public void AQuest3SIsNotMistakenForAQuest3()
        {
            // "Quest 3" is a substring of "Quest 3S", so rule order decides this. Getting it
            // backwards would give the cheaper headset the bigger device's budgets and drop
            // frames, with nothing in the logs to say why.
            Assert.That(DeviceTiers.Classify("Meta Quest 3S", "Adreno 740"), Is.EqualTo(DeviceTier.Quest3S));
            Assert.That(DeviceTiers.Classify("Meta Quest 3", "Adreno 740"), Is.EqualTo(DeviceTier.Quest3));
        }

        [Test]
        public void SpellingAndCapitalisationDoNotMatter()
        {
            foreach (var model in new[] { "Meta Quest 3", "META QUEST 3", "meta quest 3", "MetaQuest3" })
                Assert.That(DeviceTiers.Classify(model, null), Is.EqualTo(DeviceTier.Quest3), model);
        }

        [Test]
        public void AnythingUnrecognisedGetsTheTightestBudget()
        {
            // The safe direction. A device that is really more capable merely has headroom
            // spare; the opposite drops frames in someone's face.
            foreach (var model in new[] { "Some Future Headset", "", null, "Pico 4", "???" })
                Assert.That(DeviceTiers.Classify(model, null), Is.EqualTo(DeviceTier.Quest2), model ?? "null");
        }

        [Test]
        public void TheEditorIsNotAHeadset()
        {
            Assert.That(DeviceTiers.Classify("Meta Quest 3", "Adreno 740", isEditor: true),
                Is.EqualTo(DeviceTier.Editor));
        }

        [Test]
        public void TheGpuNameIsEnoughOnItsOwn()
        {
            Assert.That(DeviceTiers.Classify(null, "Meta Quest 3S GPU"), Is.EqualTo(DeviceTier.Quest3S));
        }

        [Test]
        public void EveryRuleMatchesTheStringItClaimsTo()
        {
            foreach (var (fragment, tier, note) in DeviceTiers.KnownRules())
            {
                Assert.That(DeviceTiers.Classify(fragment, null), Is.EqualTo(tier),
                    $"the rule for '{fragment}' does not classify its own fragment");
                Assert.That(note, Is.Not.Null.And.Not.Empty,
                    $"the rule for '{fragment}' should say why it exists");
            }
        }

        [Test]
        public void NoRuleIsShadowedByAnEarlierOne()
        {
            // A rule that can never fire is worse than no rule: it reads as handled.
            var rules = DeviceTiers.KnownRules().ToList();
            for (var i = 0; i < rules.Count; i++)
            {
                Assert.That(DeviceTiers.Classify(rules[i].Fragment, null), Is.EqualTo(rules[i].Tier),
                    $"'{rules[i].Fragment}' is shadowed by an earlier rule and can never match");
            }
        }

        [Test]
        public void OnlyTheDeviceWeOwnIsClaimedAsVerified()
        {
            Assert.That(DeviceTiers.IsVerified(DeviceTier.Quest2), Is.True);
            foreach (var tier in new[] { DeviceTier.Quest3, DeviceTier.Quest3S, DeviceTier.QuestPro })
            {
                Assert.That(DeviceTiers.IsVerified(tier), Is.False,
                    $"{tier} has never been measured and must not be reported as if it had");
            }
        }

        [Test]
        public void ScreenSizeIsNotConsulted()
        {
            // The trap in the version this replaces: it treated anything narrower than 1900
            // pixels as the cheaper headset. On a headset the number available is usually the
            // eye-buffer size, which the runtime scales for performance — so it describes the
            // current render target, not the hardware, and classifying hardware by it is
            // circular. Same strings must give the same answer whatever the resolution.
            Assert.That(DeviceTiers.Classify("Meta Quest 3", "Adreno 740"), Is.EqualTo(DeviceTier.Quest3));
        }
    }

    [TestFixture]
    public class BudgetLedgerTests
    {
        private static BudgetLedger Quest2() => new BudgetLedger(DeviceBudget.For(DeviceTier.Quest2));

        [Test]
        public void TheQuest2BudgetMatchesTheArchitectureDocument()
        {
            var budget = DeviceBudget.For(DeviceTier.Quest2);
            Assert.That(budget.FrameMilliseconds, Is.EqualTo(13.8f).Within(0.01f));
            Assert.That(budget.RefreshHz, Is.EqualTo(72));
            Assert.That(budget.MaxActivePets, Is.EqualTo(4));
            Assert.That(budget.MaxActiveScreens, Is.EqualTo(1));
            Assert.That(budget.For(Subsystem.Terrain).DrawCalls, Is.EqualTo(17));
            Assert.That(budget.For(Subsystem.Terrain).Triangles, Is.EqualTo(IslandConstants.IslandTriangleCount));
            Assert.That(budget.Total.DrawCalls, Is.EqualTo(140));
            Assert.That(budget.Total.Triangles, Is.EqualTo(711_072));
        }

        [Test]
        public void TheAllowancesNeverAddUpToMoreThanTheDeviceAllows()
        {
            // The real invariant. A budget whose parts sum past the cap is not a budget.
            foreach (DeviceTier tier in Enum.GetValues(typeof(DeviceTier)))
            {
                var budget = DeviceBudget.For(tier);
                Assert.That(budget.Total.DrawCalls, Is.LessThanOrEqualTo(budget.HardDrawCalls), $"{tier} draws");
                Assert.That(budget.Total.Triangles, Is.LessThanOrEqualTo(budget.HardTriangles), $"{tier} triangles");
                Assert.That(budget.Total.Bytes, Is.LessThanOrEqualTo(budget.HardBytes), $"{tier} memory");
            }
        }

        [Test]
        public void TheQuest2AllowancesLeaveNoMeaningfulHeadroom()
        {
            // Pinned deliberately, because it is the uncomfortable fact about ADR-10 and it
            // should break loudly if someone "tidies" the numbers without measuring anything.
            // Draw calls have room; triangles have 5%; memory has none at all. The first device
            // gate exists to replace these with readings.
            var budget = DeviceBudget.For(DeviceTier.Quest2);
            Assert.That((float)budget.Total.DrawCalls / budget.HardDrawCalls, Is.EqualTo(0.70f).Within(0.01f));
            Assert.That((float)budget.Total.Triangles / budget.HardTriangles, Is.EqualTo(0.95f).Within(0.01f));
            Assert.That(budget.Total.Bytes, Is.EqualTo(budget.HardBytes),
                "ADR-10 gives one memory figure, so the allowances spend all of it");
        }

        [Test]
        public void TheTargetDeviceIsTheTightestOfThem()
        {
            var quest2 = DeviceBudget.For(DeviceTier.Quest2);
            var quest3 = DeviceBudget.For(DeviceTier.Quest3);
            Assert.That(quest2.HardDrawCalls, Is.LessThan(quest3.HardDrawCalls));
            Assert.That(quest2.HardTriangles, Is.LessThan(quest3.HardTriangles));
            Assert.That(quest2.MaxActivePets, Is.LessThan(quest3.MaxActivePets));
        }

        [Test]
        public void AnEmptyIslandIsGreen()
        {
            var ledger = Quest2();
            Assert.That(ledger.OverallZone(), Is.EqualTo(BudgetZone.Green));
            Assert.That(ledger.Pressures(), Is.Empty);
        }

        [Test]
        public void TheZonesAreWhereTheGaugeSaysTheyAre()
        {
            Assert.That(BudgetLedger.ZoneOf(0.00f), Is.EqualTo(BudgetZone.Green));
            Assert.That(BudgetLedger.ZoneOf(0.79f), Is.EqualTo(BudgetZone.Green));
            Assert.That(BudgetLedger.ZoneOf(0.80f), Is.EqualTo(BudgetZone.Yellow));
            Assert.That(BudgetLedger.ZoneOf(0.94f), Is.EqualTo(BudgetZone.Yellow));
            Assert.That(BudgetLedger.ZoneOf(0.95f), Is.EqualTo(BudgetZone.Red));
            Assert.That(BudgetLedger.ZoneOf(2.00f), Is.EqualTo(BudgetZone.Red));
        }

        [Test]
        public void TheWorstOfTheThreeMeasuresDecidesTheZone()
        {
            // Being comfortably inside the triangle budget is no comfort at all if the draw
            // calls are at the limit. The tightest one is the one that bites.
            var ledger = Quest2();
            var allowed = ledger.Budget.For(Subsystem.Foliage);
            ledger.Report(Subsystem.Foliage, new Allocation(allowed.DrawCalls, 10, 1024));
            Assert.That(ledger.ZoneFor(Subsystem.Foliage), Is.EqualTo(BudgetZone.Red));
        }

        [Test]
        public void ASubsystemOverItsAllowanceIsNamed()
        {
            var ledger = Quest2();
            var foliage = ledger.Budget.For(Subsystem.Foliage);
            ledger.Report(Subsystem.Foliage, new Allocation(foliage.DrawCalls + 10, 0, 0));
            ledger.Report(Subsystem.Terrain, new Allocation(1, 0, 0));

            var pressures = ledger.Pressures().ToList();
            Assert.That(pressures, Has.Count.EqualTo(1));
            Assert.That(pressures[0].Subsystem, Is.EqualTo(Subsystem.Foliage));
            Assert.That(pressures[0].Zone, Is.EqualTo(BudgetZone.Red));
        }

        [Test]
        public void TheWorstOffenderComesFirst()
        {
            var ledger = Quest2();
            foreach (var subsystem in new[] { Subsystem.Foliage, Subsystem.Architecture })
            {
                var allowed = ledger.Budget.For(subsystem);
                var fraction = subsystem == Subsystem.Foliage ? 1.5f : 0.85f;
                ledger.Report(subsystem, new Allocation((int)(allowed.DrawCalls * fraction), 0, 0));
            }

            var pressures = ledger.Pressures().ToList();
            Assert.That(pressures[0].Subsystem, Is.EqualTo(Subsystem.Foliage),
                "the thing to look at first should be listed first");
            Assert.That(pressures[0].Fraction, Is.GreaterThan(pressures[1].Fraction));
        }

        [Test]
        public void AReportReplacesThePreviousOneRatherThanAddingToIt()
        {
            var ledger = Quest2();
            ledger.Report(Subsystem.Terrain, new Allocation(17, 0, 0));
            ledger.Report(Subsystem.Terrain, new Allocation(5, 0, 0));
            Assert.That(ledger.UsedBy(Subsystem.Terrain).DrawCalls, Is.EqualTo(5),
                "subsystems report what they currently cost, not what they have just added");
        }

        [Test]
        public void EverySubsystemAtItsAllowanceIsAnIslandAtTheLimit()
        {
            // Not a comfortable island. Spending every allowance spends the whole memory figure,
            // so the honest reading is red. Each subsystem is individually fine, which is exactly
            // why the overall figure has to be computed separately from the per-subsystem ones.
            var ledger = Quest2();
            foreach (Subsystem subsystem in Enum.GetValues(typeof(Subsystem)))
                ledger.Report(subsystem, ledger.Budget.For(subsystem));

            Assert.That(ledger.TotalUsed.DrawCalls, Is.EqualTo(ledger.Budget.Total.DrawCalls));
            Assert.That(ledger.FractionUsedOverall(), Is.EqualTo(1.0f).Within(0.001f));
            Assert.That(ledger.OverallZone(), Is.EqualTo(BudgetZone.Red));
            foreach (Subsystem subsystem in Enum.GetValues(typeof(Subsystem)))
            {
                Assert.That(ledger.ZoneFor(subsystem), Is.Not.EqualTo(BudgetZone.Green),
                    $"{subsystem} is at its own ceiling and should not read green either");
            }
        }

        [Test]
        public void AllThreeMeasuresAreJudgedAgainstTheDeviceCap()
        {
            // The bug this replaces: memory alone was measured against the sum of the allowances
            // while draws and triangles were measured against the hard caps, so one number meant
            // two things. Half the memory cap must read as half, whatever the allowances say.
            var ledger = Quest2();
            ledger.Report(Subsystem.ImportedModels,
                new Allocation(0, 0, ledger.Budget.HardBytes / 2));
            Assert.That(ledger.FractionUsedOverall(), Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void TheEditorIsNeverTakenAsEvidenceAboutADevice()
        {
            // Editor readings run high — extra cameras, gizmos, no stripping — so its budget is
            // deliberately loose. Nothing here should ever be quoted as a device result.
            var editor = DeviceBudget.For(DeviceTier.Editor);
            var quest2 = DeviceBudget.For(DeviceTier.Quest2);
            Assert.That(editor.HardDrawCalls, Is.GreaterThan(quest2.HardDrawCalls * 5));
            Assert.That(DeviceTiers.IsVerified(DeviceTier.Editor), Is.False);
        }

        [Test]
        public void AZeroAllowanceIsNotADivisionByZero()
        {
            var ledger = new BudgetLedger(DeviceBudget.For(DeviceTier.Quest2));
            ledger.Report(Subsystem.Terrain, new Allocation(1, 0, 0));
            Assert.DoesNotThrow(() => ledger.FractionUsedBy(Subsystem.Terrain));
            Assert.That(ledger.FractionUsedBy(Subsystem.Terrain), Is.GreaterThan(0f));
        }
    }
}
