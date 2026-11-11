using System;
using System.IO;
using System.Text;
using Island.Core.Budget;
using Island.Core.Import;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Island.Core.Tests.Import
{
    /// <summary>
    /// The gatekeeper against files no exporter would write.
    /// </summary>
    /// <remarks>
    /// ADR-12 makes the headset the trust boundary. The browser converter checks a file too, but
    /// it runs in the player's own browser and can be bypassed by dropping a file into the imports
    /// folder — so what matters is that this refuses everything, cheaply, before glTFast is handed
    /// anything, and that it never throws, because the caller is an import screen.
    /// </remarks>
    [TestFixture]
    public class GlbGatekeeperTests
    {
        private ImportLimits _quest2;

        [OneTimeSetUp]
        public void LoadTheRealRuleFile()
        {
            // The file that ships, not a fixture. If the shipped limits stop parsing, these fail.
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null &&
                   !File.Exists(Path.Combine(directory.FullName, "contract", "import", "limits.v1.json")))
                directory = directory.Parent;

            Assert.That(directory, Is.Not.Null, "could not find contract/import/limits.v1.json");
            var json = File.ReadAllText(Path.Combine(directory.FullName, "contract", "import", "limits.v1.json"));
            _quest2 = ImportLimits.For(json, DeviceTier.Quest2);
        }

        private static GlbBuilder Plausible(int triangles = 1000, int textureSize = 512)
        {
            var builder = new GlbBuilder();
            var mesh = builder.Mesh(triangles);
            builder.Node(mesh);
            if (textureSize > 0) builder.Image(GlbBuilder.Png(textureSize, textureSize), "image/png");
            return builder;
        }

        private ImportVerdict Inspect(GlbBuilder builder) =>
            GlbGatekeeper.Inspect(builder.Build(), _quest2);

        private void AssertRefused(GlbBuilder builder, string because)
        {
            var verdict = Inspect(builder);
            Assert.That(verdict.Accepted, Is.False, "this should have been refused");
            Assert.That(verdict.Reason, Does.Contain(because),
                $"the reason should mention {because}; it said: {verdict.Reason}");
        }

        // ---- the rule file ---------------------------------------------------------------------

        [Test]
        public void TheShippedRuleFileCoversEveryTier()
        {
            // A tier with no entry would import with no limits at all, so this is the check that
            // adding a DeviceTier cannot quietly open a hole.
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null &&
                   !File.Exists(Path.Combine(directory.FullName, "contract", "import", "limits.v1.json")))
                directory = directory.Parent;
            var json = File.ReadAllText(Path.Combine(directory.FullName, "contract", "import", "limits.v1.json"));

            foreach (DeviceTier tier in Enum.GetValues(typeof(DeviceTier)))
            {
                var limits = ImportLimits.For(json, tier);
                Assert.That(limits.MaxTriangles, Is.GreaterThan(0), tier.ToString());
                Assert.That(limits.MaxFileBytes, Is.GreaterThan(0), tier.ToString());
                Assert.That(limits.AllowExternalUris, Is.False, tier.ToString());
            }
        }

        [Test]
        public void TheQuest2IsTheTightestTier()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null &&
                   !File.Exists(Path.Combine(directory.FullName, "contract", "import", "limits.v1.json")))
                directory = directory.Parent;
            var json = File.ReadAllText(Path.Combine(directory.FullName, "contract", "import", "limits.v1.json"));

            var quest3 = ImportLimits.For(json, DeviceTier.Quest3);
            Assert.That(_quest2.MaxTriangles, Is.LessThan(quest3.MaxTriangles));
            Assert.That(_quest2.MaxTextureDimension, Is.LessThan(quest3.MaxTextureDimension));
        }

        [Test]
        public void ARuleFileAllowingExternalUrisIsRefusedOutright()
        {
            // Not configurable by accident: honouring a file's URIs would let a dropped model make
            // the headset fetch whatever it named, from inside the player's home network.
            var json = @"{
                ""allTiers"": { ""maxFileBytes"": 1, ""maxJsonBytes"": 1, ""maxBuffers"": 1,
                  ""maxMeshes"": 1, ""maxNodes"": 1, ""maxImages"": 1, ""maxNodeDepth"": 1,
                  ""allowExternalUris"": true, ""allowedImageTypes"": [] },
                ""tiers"": { ""Quest2"": { ""maxTriangles"": 1, ""maxPrimitives"": 1,
                  ""maxTextureDimension"": 1, ""maxEstimatedBytes"": 1 } } }";

            var error = Assert.Throws<ImportRulesException>(
                () => ImportLimits.For(json, DeviceTier.Quest2));
            Assert.That(error.Message, Does.Contain("trust boundary"));
        }

        [Test]
        public void ARuleFileMissingATierIsRefused()
        {
            var json = @"{
                ""allTiers"": { ""maxFileBytes"": 1, ""maxJsonBytes"": 1, ""maxBuffers"": 1,
                  ""maxMeshes"": 1, ""maxNodes"": 1, ""maxImages"": 1, ""maxNodeDepth"": 1,
                  ""allowExternalUris"": false, ""allowedImageTypes"": [] },
                ""tiers"": { ""Quest3"": { ""maxTriangles"": 1, ""maxPrimitives"": 1,
                  ""maxTextureDimension"": 1, ""maxEstimatedBytes"": 1 } } }";

            Assert.Throws<ImportRulesException>(() => ImportLimits.For(json, DeviceTier.Quest2));
        }

        // ---- a model that is fine --------------------------------------------------------------

        [Test]
        public void AnOrdinaryModelIsAccepted()
        {
            var verdict = Inspect(Plausible());

            Assert.That(verdict.Accepted, Is.True, verdict.Reason);
            Assert.That(verdict.Reason, Is.Null);
            Assert.That(verdict.Facts.Triangles, Is.EqualTo(1000));
            Assert.That(verdict.Facts.Primitives, Is.EqualTo(1));
            Assert.That(verdict.Facts.Images, Is.EqualTo(1));
            Assert.That(verdict.Facts.MaxTextureDimension, Is.EqualTo(512));
            Assert.That(verdict.Facts.EstimatedBytes, Is.GreaterThan(0));
        }

        [Test]
        public void AModelWithNoTextureIsFine()
        {
            var verdict = Inspect(Plausible(textureSize: 0));
            Assert.That(verdict.Accepted, Is.True, verdict.Reason);
            Assert.That(verdict.Facts.Images, Is.Zero);
        }

        [Test]
        public void OneMeshUsedByTwoNodesIsCountedTwice()
        {
            // It is drawn twice, so it costs twice. Counting unique meshes would under-report the
            // thing the budget is about.
            var builder = new GlbBuilder();
            var mesh = builder.Mesh(1000);
            builder.Node(mesh);
            builder.Node(mesh);

            var verdict = Inspect(builder);
            Assert.That(verdict.Accepted, Is.True, verdict.Reason);
            Assert.That(verdict.Facts.Triangles, Is.EqualTo(2000));
            Assert.That(verdict.Facts.Primitives, Is.EqualTo(2));
        }

        [Test]
        public void AMeshNoNodeUsesCostsNothingToDraw()
        {
            var builder = new GlbBuilder();
            builder.Mesh(50_000);   // present in the file, never placed in the scene

            var verdict = Inspect(builder);
            Assert.That(verdict.Accepted, Is.True, verdict.Reason);
            Assert.That(verdict.Facts.Triangles, Is.Zero);
        }

        [Test]
        public void JpegAndKtx2TexturesAreReadToo()
        {
            foreach (var (bytes, type, size) in new[]
            {
                (GlbBuilder.Jpeg(256, 128), "image/jpeg", 256),
                (GlbBuilder.Ktx2(512, 512), "image/ktx2", 512),
            })
            {
                var builder = new GlbBuilder();
                builder.Node(builder.Mesh(10));
                builder.Image(bytes, type);

                var verdict = Inspect(builder);
                Assert.That(verdict.Accepted, Is.True, $"{type}: {verdict.Reason}");
                Assert.That(verdict.Facts.MaxTextureDimension, Is.EqualTo(size), type);
            }
        }

        [Test]
        public void NonSquareTexturesAreMeasuredOnTheirLongerSide()
        {
            var builder = new GlbBuilder();
            builder.Node(builder.Mesh(10));
            builder.Image(GlbBuilder.Png(1024, 256), "image/png");

            Assert.That(Inspect(builder).Facts.MaxTextureDimension, Is.EqualTo(1024));
        }

        // ---- the container ---------------------------------------------------------------------

        [Test]
        public void NothingAtAllIsRefused()
        {
            foreach (var bytes in new[] { null, new byte[0], new byte[] { 1, 2, 3 } })
            {
                var verdict = GlbGatekeeper.Inspect(bytes, _quest2);
                Assert.That(verdict.Accepted, Is.False);
                Assert.That(verdict.Reason, Is.Not.Null.And.Not.Empty);
            }
        }

        [Test]
        public void SomethingThatIsNotAGlbIsRefusedWithAdvice()
        {
            var builder = Plausible();
            builder.MagicOverride = 0x12345678;
            AssertRefused(builder, "not a GLB");
        }

        [Test]
        public void AFutureGltfVersionIsRefused()
        {
            var builder = Plausible();
            builder.VersionOverride = 3;
            AssertRefused(builder, "version 3");
        }

        [Test]
        public void AFileThatLiesAboutItsLengthIsRefused()
        {
            // Truncated in transit, or lying. Both end the same way, and this is the check that
            // makes the chunk walk safe.
            var builder = Plausible();
            builder.DeclaredLengthOverride = 999_999;
            AssertRefused(builder, "incomplete");
        }

        [Test]
        public void ATruncatedFileIsRefused()
        {
            var whole = Plausible().Build();
            var half = new byte[whole.Length / 2];
            Array.Copy(whole, half, half.Length);

            var verdict = GlbGatekeeper.Inspect(half, _quest2);
            Assert.That(verdict.Accepted, Is.False);
        }

        [Test]
        public void AChunkClaimingMoreBytesThanTheFileHoldsIsRefused()
        {
            // The one that matters most: without this check, a declared length of four billion
            // becomes an allocation request.
            var builder = Plausible();
            builder.JsonChunkLengthOverride = 0xFFFFFF00;
            AssertRefused(builder, "larger than the file");
        }

        [Test]
        public void AFileWithNoDescriptionIsRefused()
        {
            var builder = Plausible();
            builder.OmitJsonChunk = true;
            AssertRefused(builder, "no model description");
        }

        [Test]
        public void AFileWhoseDescriptionIsNotFirstIsRefused()
        {
            var builder = Plausible();
            builder.PutBinaryChunkFirst = true;
            AssertRefused(builder, "laid out wrongly");
        }

        [Test]
        public void ADescriptionThatIsNotJsonIsRefused()
        {
            // Hand-built, because the builder always writes valid JSON.
            var good = Plausible().Build();
            var jsonAt = 20;
            good[jsonAt] = (byte)'{';
            good[jsonAt + 1] = (byte)'{';
            good[jsonAt + 2] = (byte)'{';

            var verdict = GlbGatekeeper.Inspect(good, _quest2);
            Assert.That(verdict.Accepted, Is.False);
        }

        [Test]
        public void AFileOverTheSizeLimitIsRefusedBeforeItIsParsed()
        {
            var huge = new byte[_quest2.MaxFileBytes + 1];
            var verdict = GlbGatekeeper.Inspect(huge, _quest2);

            Assert.That(verdict.Accepted, Is.False);
            Assert.That(verdict.Reason, Does.Contain("converter can shrink it"));
        }

        // ---- nothing may be fetched from outside -----------------------------------------------

        [Test]
        public void AModelKeepingItsDataElsewhereIsRefused()
        {
            var builder = Plausible();
            ((JArray)builder.Gltf["buffers"]).Add(new JObject
            {
                ["uri"] = "https://example.invalid/mesh.bin", ["byteLength"] = 1024,
            });
            AssertRefused(builder, "self-contained");
        }

        [Test]
        public void AModelPointingAtExternalTexturesIsRefused()
        {
            var builder = Plausible(textureSize: 0);
            ((JArray)builder.Gltf["images"]).Add(new JObject
            {
                ["uri"] = "file:///etc/passwd",
            });
            AssertRefused(builder, "outside the file");
        }

        [Test]
        public void ADataUriIsStillAUriAndStillRefused()
        {
            var builder = Plausible(textureSize: 0);
            ((JArray)builder.Gltf["images"]).Add(new JObject
            {
                ["uri"] = "data:image/png;base64,iVBORw0KGgo=",
            });
            AssertRefused(builder, "outside the file");
        }

        // ---- absurd counts ---------------------------------------------------------------------

        [Test]
        public void TooManyTrianglesIsRefusedWithTheNumbers()
        {
            var builder = new GlbBuilder();
            builder.Node(builder.Mesh(_quest2.MaxTriangles + 1000));

            var verdict = Inspect(builder);
            Assert.That(verdict.Accepted, Is.False);
            Assert.That(verdict.Reason, Does.Contain("triangles").And.Contain("limit"));
            Assert.That(verdict.Reason, Does.Contain("simplify"), "say what to do about it");
            Assert.That(verdict.Facts, Is.Not.Null, "the facts are still worth showing");
        }

        [Test]
        public void TooManyPiecesIsRefused()
        {
            var builder = new GlbBuilder();
            builder.Node(builder.Mesh(10, primitives: _quest2.MaxPrimitives + 1));
            AssertRefused(builder, "pieces");
        }

        [Test]
        public void TooManyMeshesIsRefusedBeforeTheyAreWalked()
        {
            var builder = new GlbBuilder();
            for (var i = 0; i <= _quest2.MaxMeshes; i++) builder.Mesh(1);
            AssertRefused(builder, "meshes");
        }

        [Test]
        public void TooManyNodesIsRefused()
        {
            var builder = new GlbBuilder();
            for (var i = 0; i <= _quest2.MaxNodes; i++) builder.Node();
            AssertRefused(builder, "parts");
        }

        [Test]
        public void TooManyTexturesIsRefused()
        {
            var builder = new GlbBuilder();
            builder.Node(builder.Mesh(10));
            for (var i = 0; i <= _quest2.MaxImages; i++)
                builder.Image(GlbBuilder.Png(64, 64), "image/png");
            AssertRefused(builder, "textures");
        }

        [Test]
        public void AnAccessorCountThatWouldOverflowDoesNotWrapAround()
        {
            // The failure this guards against is arithmetic, not size: a count near int.MaxValue
            // summed naively wraps negative and sails under the limit.
            var builder = new GlbBuilder();
            var meshes = (JArray)builder.Gltf["meshes"];
            var huge = builder.Accessor(int.MaxValue);
            meshes.Add(new JObject
            {
                ["primitives"] = new JArray { new JObject { ["mode"] = 4, ["indices"] = huge } },
            });
            builder.Node(0);
            builder.Node(0);
            builder.Node(0);

            var verdict = Inspect(builder);
            Assert.That(verdict.Accepted, Is.False);
            Assert.That(verdict.Facts.Triangles, Is.GreaterThan(0), "it must not have wrapped negative");
        }

        [Test]
        public void ANegativeCountIsRefused()
        {
            var builder = new GlbBuilder();
            var meshes = (JArray)builder.Gltf["meshes"];
            var accessors = (JArray)builder.Gltf["accessors"];
            accessors.Add(new JObject { ["count"] = -5 });
            meshes.Add(new JObject
            {
                ["primitives"] = new JArray { new JObject { ["mode"] = 4, ["indices"] = 0 } },
            });
            builder.Node(0);

            AssertRefused(builder, "negative");
        }

        [Test]
        public void AReferenceToSomethingThatIsNotThereIsRefused()
        {
            var builder = new GlbBuilder();
            builder.Node(mesh: 7);   // there are no meshes at all
            AssertRefused(builder, "not in it");
        }

        [Test]
        public void AnAccessorIndexOutOfRangeIsRefused()
        {
            var builder = new GlbBuilder();
            var meshes = (JArray)builder.Gltf["meshes"];
            meshes.Add(new JObject
            {
                ["primitives"] = new JArray { new JObject { ["mode"] = 4, ["indices"] = 99 } },
            });
            builder.Node(0);
            AssertRefused(builder, "not in it");
        }

        // ---- the node tree ---------------------------------------------------------------------

        [Test]
        public void NodesThatPointAtEachOtherAreRefused()
        {
            // A real hostile case and the cheapest crash to cause: two nodes each claiming the
            // other as a child. Unguarded that is a stack overflow, which cannot be caught.
            var builder = new GlbBuilder();
            builder.Node(children: new[] { 1 });
            builder.Node(children: new[] { 0 });

            AssertRefused(builder, "loop");
        }

        [Test]
        public void ANodeThatContainsItselfIsRefused()
        {
            var builder = new GlbBuilder();
            builder.Node(children: new[] { 0 });
            AssertRefused(builder, "contains itself");
        }

        [Test]
        public void APartSharedBetweenTwoParentsIsRefused()
        {
            // glTF says nodes form a disjoint union of trees, and this is why it matters: a shared
            // child is drawn once per path to it, so a file can double the number of paths at
            // every level. Thirty levels of that is a billion draws described in a few kilobytes.
            var builder = new GlbBuilder();
            var shared = builder.Node(builder.Mesh(10));
            builder.Node(children: new[] { shared });
            builder.Node(children: new[] { shared });

            AssertRefused(builder, "shares a part");
        }

        [Test]
        public void TheDoublingAttackIsRefusedRatherThanCounted()
        {
            // The shape the sharing rule exists to stop: each level points at the level below
            // twice. Forty levels is a trillion draw paths in a file of a few kilobytes.
            var builder = new GlbBuilder();
            builder.Node(builder.Mesh(10));
            for (var level = 1; level < 40; level++)
                builder.Node(children: new[] { level - 1, level - 1 });

            var verdict = Inspect(builder);
            Assert.That(verdict.Accepted, Is.False);
            Assert.That(verdict.Reason, Does.Contain("shares a part"));
        }

        [Test]
        public void ATreeNestedTooDeepIsRefused()
        {
            var builder = new GlbBuilder();
            var depth = _quest2.MaxNodeDepth + 10;
            for (var i = 0; i < depth; i++)
                builder.Node(children: i + 1 < depth ? new[] { i + 1 } : null);

            AssertRefused(builder, "nested");
        }

        [Test]
        public void AChildIndexOutOfRangeIsRefused()
        {
            var builder = new GlbBuilder();
            builder.Node(children: new[] { 500 });
            AssertRefused(builder, "not in it");
        }

        // ---- textures --------------------------------------------------------------------------

        [Test]
        public void AnEnormousTextureIsRefusedFromItsHeaderAlone()
        {
            // The whole reason the headers are read: this file is a few hundred bytes and would be
            // a gigabyte decoded. File size is no guide at all.
            var builder = new GlbBuilder();
            builder.Node(builder.Mesh(10));
            builder.Image(GlbBuilder.Png(16_384, 16_384), "image/png");

            var verdict = Inspect(builder);
            Assert.That(verdict.Accepted, Is.False);
            Assert.That(verdict.Reason, Does.Contain("16384px").Or.Contain("not real"));
        }

        [Test]
        public void ATextureOverTheTierLimitIsRefusedWithAdvice()
        {
            var builder = new GlbBuilder();
            builder.Node(builder.Mesh(10));
            builder.Image(GlbBuilder.Png(_quest2.MaxTextureDimension * 2, 64), "image/png");

            var verdict = Inspect(builder);
            Assert.That(verdict.Accepted, Is.False);
            Assert.That(verdict.Reason, Does.Contain("resize"));
        }

        [Test]
        public void ATextureLabelledAsSomethingItIsNotIsRefused()
        {
            // The decoder acts on the bytes, not the label.
            var builder = new GlbBuilder();
            builder.Node(builder.Mesh(10));
            builder.Image(GlbBuilder.Png(64, 64), "image/jpeg");

            AssertRefused(builder, "labelled");
        }

        [Test]
        public void ATextureInAFormatWeDoNotReadIsRefused()
        {
            var builder = new GlbBuilder();
            builder.Node(builder.Mesh(10));
            builder.Image(Encoding.ASCII.GetBytes("GIF89a................"), "image/gif");

            AssertRefused(builder, "does not read");
        }

        [Test]
        public void ATruncatedPngHeaderIsRefused()
        {
            var builder = new GlbBuilder();
            builder.Node(builder.Mesh(10));
            builder.Image(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0 },
                          "image/png");

            AssertRefused(builder, "truncated PNG");
        }

        [Test]
        public void AJpegWithNoFrameIsRefused()
        {
            var builder = new GlbBuilder();
            builder.Node(builder.Mesh(10));
            builder.Image(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00 }, "image/jpeg");

            AssertRefused(builder, "no picture");
        }

        [Test]
        public void ATextureClaimingToLiveBeyondTheEndOfTheFileIsRefused()
        {
            var builder = new GlbBuilder();
            builder.Node(builder.Mesh(10));
            builder.Image(GlbBuilder.Png(64, 64), "image/png");
            ((JObject)((JArray)builder.Gltf["bufferViews"])[0])["byteLength"] = 9_999_999;

            AssertRefused(builder, "past the end");
        }

        [Test]
        public void ATextureWithANegativeOffsetIsRefused()
        {
            var builder = new GlbBuilder();
            builder.Node(builder.Mesh(10));
            builder.Image(GlbBuilder.Png(64, 64), "image/png");
            ((JObject)((JArray)builder.Gltf["bufferViews"])[0])["byteOffset"] = -4;

            AssertRefused(builder, "impossible size");
        }

        [Test]
        public void ATextureWithNoDataAtAllIsRefused()
        {
            var builder = Plausible(textureSize: 0);
            ((JArray)builder.Gltf["images"]).Add(new JObject { ["mimeType"] = "image/png" });
            AssertRefused(builder, "no data attached");
        }

        [Test]
        public void ATexturePointingAtABufferViewThatIsNotThereIsRefused()
        {
            var builder = Plausible(textureSize: 0);
            ((JArray)builder.Gltf["images"]).Add(new JObject { ["bufferView"] = 42 });
            AssertRefused(builder, "not in it");
        }

        // ---- memory ----------------------------------------------------------------------------

        [Test]
        public void AModelThatWouldNotFitInMemoryIsRefused()
        {
            // Under the texture-dimension limit and the triangle limit, and still too much
            // together. The estimate is what catches it.
            var builder = new GlbBuilder();
            builder.Node(builder.Mesh(10));
            for (var i = 0; i < _quest2.MaxImages; i++)
                builder.Image(GlbBuilder.Png(_quest2.MaxTextureDimension, _quest2.MaxTextureDimension),
                              "image/png");

            var verdict = Inspect(builder);
            Assert.That(verdict.Accepted, Is.False);
            Assert.That(verdict.Reason, Does.Contain("memory"));
        }

        [Test]
        public void TheEstimateIncludesMipsBecauseTheDeviceWillMakeThem()
        {
            var builder = new GlbBuilder();
            builder.Node(builder.Mesh(10));
            builder.Image(GlbBuilder.Png(512, 512), "image/png");

            var verdict = Inspect(builder);
            var flat = 512L * 512 * 4;
            Assert.That(verdict.Facts.EstimatedBytes, Is.GreaterThan(flat),
                "a mip chain adds about a third again");
        }

        // ---- never throws ----------------------------------------------------------------------

        [Test]
        public void RandomRubbishIsRefusedAndNeverThrows()
        {
            // Not a substitute for a real fuzzer, but it covers the shapes a corrupted download
            // actually takes, and the contract is that none of them escapes as an exception: the
            // caller is an import screen, and a throw there is a player watching their world
            // vanish.
            var random = new Random(20261111);

            for (var attempt = 0; attempt < 2000; attempt++)
            {
                var bytes = new byte[random.Next(0, 512)];
                random.NextBytes(bytes);

                // Half of them look like a GLB at a glance, so the parser gets past the magic.
                if (attempt % 2 == 0 && bytes.Length >= 12)
                {
                    bytes[0] = 0x67; bytes[1] = 0x6C; bytes[2] = 0x54; bytes[3] = 0x46;
                    bytes[4] = 2; bytes[5] = 0; bytes[6] = 0; bytes[7] = 0;
                    var length = (uint)bytes.Length;
                    bytes[8] = (byte)length; bytes[9] = (byte)(length >> 8);
                    bytes[10] = (byte)(length >> 16); bytes[11] = (byte)(length >> 24);
                }

                ImportVerdict verdict = null;
                Assert.DoesNotThrow(() => verdict = GlbGatekeeper.Inspect(bytes, _quest2),
                    $"attempt {attempt} threw");
                Assert.That(verdict, Is.Not.Null);
                Assert.That(verdict.Accepted, Is.False, $"attempt {attempt} was accepted");
                Assert.That(verdict.Reason, Is.Not.Null.And.Not.Empty, $"attempt {attempt} gave no reason");
            }
        }

        [Test]
        public void MangledValidFilesAreRefusedAndNeverThrow()
        {
            // Closer to the real failure: a good file with bytes flipped, which is what a bad
            // copy or a half-finished download produces.
            var good = Plausible().Build();
            var random = new Random(11112026);

            for (var attempt = 0; attempt < 500; attempt++)
            {
                var copy = (byte[])good.Clone();
                for (var i = 0; i < 8; i++)
                    copy[random.Next(copy.Length)] = (byte)random.Next(256);

                Assert.DoesNotThrow(() => GlbGatekeeper.Inspect(copy, _quest2), $"attempt {attempt}");
            }
        }

        [Test]
        public void TruncationAtEveryLengthIsRefusedAndNeverThrows()
        {
            var good = Plausible().Build();

            for (var length = 0; length < good.Length; length++)
            {
                var cut = new byte[length];
                Array.Copy(good, cut, length);

                ImportVerdict verdict = null;
                Assert.DoesNotThrow(() => verdict = GlbGatekeeper.Inspect(cut, _quest2),
                    $"cut to {length} bytes threw");
                Assert.That(verdict.Accepted, Is.False, $"cut to {length} bytes was accepted");
            }
        }

        [Test]
        public void NoLimitsIsAProgrammingMistakeNotAVerdict()
        {
            Assert.Throws<ArgumentNullException>(() => GlbGatekeeper.Inspect(new byte[10], null));
        }
    }
}
