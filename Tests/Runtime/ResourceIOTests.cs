using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace UniRes.Tests
{
    public class ResourceIOTests
    {
        private string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "unires-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            ResourcePaths.UserDataRoot = root;
            ResourceLoader.ClearCache();
        }

        [TearDown]
        public void TearDown()
        {
            ResourcePaths.UserDataRoot = null;
            ResourceLoader.ClearCache();
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        private string OnDisk(string relative)
        {
            return Path.Combine(root, relative);
        }

        // ---- save and load -------------------------------------------------------------------

        [Test]
        public void SavedFileLoadsBack()
        {
            var resource = new SampleResource { Health = 12, Speed = 3.5f, ResourceName = "hero" };

            ResourceSaver.Save(resource, "user://hero.res");
            var loaded = ResourceLoader.Load<SampleResource>("user://hero.res");

            Assert.AreEqual(12, loaded.Health);
            Assert.AreEqual(3.5f, loaded.Speed);
            Assert.AreEqual("hero", loaded.ResourceName);
            Assert.AreEqual("user://hero.res", loaded.ResourcePath);
            Assert.AreNotSame(resource, loaded);
        }

        [Test]
        public void UserPathsResolveInsideTheUserDataRoot()
        {
            ResourceSaver.Save(new SampleResource(), "user://films/take.chtk");

            Assert.IsTrue(File.Exists(OnDisk("films/take.chtk")));
            Assert.IsTrue(ResourceLoader.Exists("user://films/take.chtk"));
            Assert.IsFalse(ResourceLoader.Exists("user://films/nope.chtk"));
        }

        [Test]
        public void PlainFileSystemPathsWork()
        {
            string path = OnDisk("plain/a.res");

            ResourceSaver.Save(new SampleResource { Health = 2 }, path);

            Assert.AreEqual(2, ResourceLoader.Load<SampleResource>(path).Health);
        }

        [Test]
        public void SavingOverwritesAnExistingFile()
        {
            ResourceSaver.Save(new SampleResource { Health = 1 }, "user://a.res");
            ResourceSaver.Save(new SampleResource { Health = 2 }, "user://a.res");

            ResourceLoader.ClearCache();
            Assert.AreEqual(2, ResourceLoader.Load<SampleResource>("user://a.res").Health);
        }

        [Test]
        public void FileIsUtf8WithoutABomAndKeepsUnicode()
        {
            var resource = new SampleResource { ResourceName = "h\u00e9llo \u65e5\u672c \ud83c\udfac" };

            ResourceSaver.Save(resource, "user://u.res");

            byte[] bytes = File.ReadAllBytes(OnDisk("u.res"));
            Assert.AreEqual((byte)'[', bytes[0]);

            ResourceLoader.ClearCache();
            Assert.AreEqual(resource.ResourceName, ResourceLoader.Load<SampleResource>("user://u.res").ResourceName);
        }

        [Test]
        public void FileContentMatchesSaveToString()
        {
            var resource = new SampleResource { Health = 4 };

            ResourceSaver.Save(resource, "user://a.res");

            Assert.AreEqual(ResourceSaver.SaveToString(resource), File.ReadAllText(OnDisk("a.res"), Encoding.UTF8));
        }

        [Test]
        public void ASaveThatFailsLeavesAnExistingFileUntouched()
        {
            ResourceSaver.Save(new TestNode { Depth = 7 }, "user://a.res");
            string before = File.ReadAllText(OnDisk("a.res"));

            var loop = new TestNode();
            loop.Child = loop;

            Assert.Throws<UniResException>(() => ResourceSaver.Save(loop, "user://a.res"));
            Assert.AreEqual(before, File.ReadAllText(OnDisk("a.res")));
        }

        [Test]
        public void ASaveThatFailsCreatesNoFile()
        {
            var loop = new TestNode();
            loop.Child = loop;

            Assert.Throws<UniResException>(() => ResourceSaver.Save(loop, "user://new/a.res"));
            Assert.IsFalse(File.Exists(OnDisk("new/a.res")));
        }

        // ---- paths --------------------------------------------------------------------------

        [Test]
        public void UnsupportedSchemeAndEmptyPathAreRejected()
        {
            Assert.Throws<UniResException>(() => ResourceLoader.Load("res://thing.res"));
            Assert.Throws<UniResException>(() => ResourceSaver.Save(new SampleResource(), "http://example.com/a.res"));
            Assert.Throws<UniResException>(() => ResourceLoader.Load(""));
            Assert.Throws<UniResException>(() => ResourceSaver.Save(new SampleResource(), "  "));
        }

        [Test]
        public void LoadingAMissingFileThrowsAnIOException()
        {
            Assert.Catch<IOException>(() => ResourceLoader.Load("user://missing.res"));
            Assert.Catch<IOException>(() => ResourceLoader.Load("user://missing-folder/missing.res"));
        }

        [Test]
        public void NullArgumentsAreRejected()
        {
            Assert.Throws<System.ArgumentNullException>(() => ResourceSaver.Save(null, "user://a.res"));
            Assert.Throws<System.ArgumentNullException>(() => ResourceSaver.SaveToString(null));
            Assert.Throws<System.ArgumentNullException>(() => ResourceLoader.LoadFromString(null));
            Assert.Throws<System.ArgumentNullException>(() => ResourceLoader.LoadFromStream(null));
        }

        // ---- cache --------------------------------------------------------------------------

        [Test]
        public void LoadingTheSamePathTwiceReturnsTheSameInstance()
        {
            ResourceSaver.Save(new SampleResource(), "user://a.res");

            var first = ResourceLoader.Load<SampleResource>("user://a.res");
            var second = ResourceLoader.Load<SampleResource>("user://a.res");

            Assert.AreSame(first, second);
        }

        [Test]
        public void CacheModeIgnoreReadsAgainWithoutTouchingTheCache()
        {
            ResourceSaver.Save(new SampleResource { Health = 1 }, "user://a.res");
            var cached = ResourceLoader.Load<SampleResource>("user://a.res");

            ResourceSaver.Save(new SampleResource { Health = 2 }, "user://a.res");
            var fresh = ResourceLoader.Load<SampleResource>("user://a.res", CacheMode.Ignore);

            Assert.AreNotSame(cached, fresh);
            Assert.AreEqual(2, fresh.Health);
            Assert.AreSame(cached, ResourceLoader.Load<SampleResource>("user://a.res"));
        }

        [Test]
        public void CacheModeReplaceReadsAgainAndUpdatesTheCache()
        {
            ResourceSaver.Save(new SampleResource { Health = 1 }, "user://a.res");
            var old = ResourceLoader.Load<SampleResource>("user://a.res");

            ResourceSaver.Save(new SampleResource { Health = 2, ResourceName = "updated" }, "user://a.res");
            var replaced = ResourceLoader.Load<SampleResource>("user://a.res", CacheMode.Replace);

            Assert.AreSame(old, replaced);
            Assert.AreEqual(2, old.Health);
            Assert.AreEqual("updated", old.ResourceName);
            Assert.AreSame(old, ResourceLoader.Load<SampleResource>("user://a.res"));
        }

        [Test]
        public void CacheModeReplaceRecreatesTheResourceWhenItsClassChanges()
        {
            ResourceSaver.Save(new SampleResource { Health = 1 }, "user://a.res");
            var old = ResourceLoader.Load<SampleResource>("user://a.res");
            ResourceSaver.Save(new TestAct { Count = 2 }, "user://a.res");

            var replaced = ResourceLoader.Load<TestAct>("user://a.res", CacheMode.Replace);

            Assert.AreNotSame(old, replaced);
            Assert.IsTrue(old.IsBuiltIn);
            Assert.AreSame(replaced, ResourceLoader.Load<TestAct>("user://a.res"));
        }

        [Test]
        public void CachedResourcesCountAsExistingEvenWithoutAFile()
        {
            var resource = new SampleResource();
            resource.TakeOverPath("user://not-written.res");

            Assert.IsTrue(ResourceLoader.Exists("user://not-written.res"));
        }

        [Test]
        public void UserPathsNormalizeDotSegmentsForTheCache()
        {
            ResourceSaver.Save(new SampleResource(), "user://folder/a.res");

            var first = ResourceLoader.Load<SampleResource>("user://folder/a.res");
            var second = ResourceLoader.Load<SampleResource>("user://folder/./child/../a.res");

            Assert.AreSame(first, second);
        }

        [Test]
        public void ClearCacheMakesTheNextLoadReadTheFileAgain()
        {
            ResourceSaver.Save(new SampleResource(), "user://a.res");
            var first = ResourceLoader.Load<SampleResource>("user://a.res");

            ResourceLoader.ClearCache();

            Assert.AreNotSame(first, ResourceLoader.Load<SampleResource>("user://a.res"));
        }

        [Test]
        public void BackslashAndSlashPathsShareACacheEntry()
        {
            string path = OnDisk("dir/a.res");
            ResourceSaver.Save(new SampleResource(), path);

            var first = ResourceLoader.Load<SampleResource>(path);
            var second = ResourceLoader.Load<SampleResource>(path.Replace('/', '\\'));

            Assert.AreSame(first, second);
        }

        [Test]
        public void LoadingAsTheWrongClassGivesAClearError()
        {
            ResourceSaver.Save(new SampleResource(), "user://a.res");

            var ex = Assert.Throws<UniResException>(() => ResourceLoader.Load<TestAct>("user://a.res"));
            StringAssert.Contains("TestAct", ex.Message);
            StringAssert.Contains("SampleResource", ex.Message);
        }

        // ---- paths of resources ---------------------------------------------------------------

        [Test]
        public void ChangePathMakesTheResourceTheCachedInstanceForThatPath()
        {
            var resource = new SampleResource { Health = 6 };

            ResourceSaver.Save(resource, "user://a.res", SaveFlags.ChangePath);

            Assert.AreEqual("user://a.res", resource.ResourcePath);
            Assert.IsFalse(resource.IsBuiltIn);
            Assert.AreSame(resource, ResourceLoader.Load<SampleResource>("user://a.res"));
        }

        [Test]
        public void SavingWithoutChangePathLeavesTheResourceBuiltIn()
        {
            var resource = new SampleResource();

            ResourceSaver.Save(resource, "user://a.res");

            Assert.IsTrue(resource.IsBuiltIn);
        }

        [Test]
        public void TakeOverPathMovesTheCacheEntry()
        {
            var resource = new SampleResource();
            resource.TakeOverPath("user://one.res");
            resource.TakeOverPath("user://two.res");

            ResourceSaver.Save(resource, "user://two.res");
            ResourceSaver.Save(new SampleResource { Health = 99 }, "user://one.res");

            Assert.AreSame(resource, ResourceLoader.Load<SampleResource>("user://two.res"));
            Assert.AreEqual(99, ResourceLoader.Load<SampleResource>("user://one.res").Health);
        }

        [Test]
        public void TakingOverAnOccupiedPathDisplacesThePreviousCachedResource()
        {
            var previous = new SampleResource();
            previous.TakeOverPath("user://occupied.res");
            var replacement = new SampleResource();
            replacement.TakeOverPath("user://occupied.res");

            Assert.IsTrue(previous.IsBuiltIn);
            Assert.AreSame(replacement, ResourceLoader.Load<SampleResource>("user://occupied.res"));
        }

        [Test]
        public void TakeOverPathRejectsBadPathsWithoutChangingAnything()
        {
            var resource = new SampleResource();

            Assert.Throws<UniResException>(() => resource.TakeOverPath(""));
            Assert.Throws<UniResException>(() => resource.TakeOverPath("res://x"));
            Assert.IsTrue(resource.IsBuiltIn);
        }

        // ---- nesting through files -------------------------------------------------------------

        [Test]
        public void ATakeWithActsInTheirOwnFilesRoundTrips()
        {
            var take = new TestTake { Title = "Take 1" };
            for (int i = 1; i <= 3; i++)
            {
                var act = new TestAct { Count = i, Samples = new[] { i * 0.1f, i * 0.2f } };
                ResourceSaver.Save(act, $"user://acts/{i}.chact", SaveFlags.ChangePath);
                take.Acts.Add(act);
                take.ByName["act" + i] = act;
            }

            ResourceSaver.Save(take, "user://films/take.chtk");
            ResourceLoader.ClearCache();

            var loaded = ResourceLoader.Load<TestTake>("user://films/take.chtk");

            Assert.AreEqual(3, loaded.Acts.Count);
            Assert.AreEqual("user://acts/2.chact", loaded.Acts[1].ResourcePath);
            Assert.AreEqual(2, loaded.Acts[1].Count);
            CollectionAssert.AreEqual(new[] { 0.3f, 0.6f }, loaded.Acts[2].Samples);
            Assert.AreSame(loaded.Acts[0], loaded.ByName["act1"]);
            StringAssert.Contains("[ext_resource", File.ReadAllText(OnDisk("films/take.chtk")));
        }

        [Test]
        public void TwoFilesReferencingTheSameFileShareOneInstance()
        {
            var shared = new TestAct { Count = 1 };
            ResourceSaver.Save(shared, "user://shared.chact", SaveFlags.ChangePath);

            ResourceSaver.Save(new TestTake { Featured = shared }, "user://a.chtk");
            ResourceSaver.Save(new TestTake { Featured = shared }, "user://b.chtk");
            ResourceLoader.ClearCache();

            var a = ResourceLoader.Load<TestTake>("user://a.chtk");
            var b = ResourceLoader.Load<TestTake>("user://b.chtk");

            Assert.AreSame(a.Featured, b.Featured);
        }

        [Test]
        public void EditingAnExternalFileIsPickedUpByTheNextLoad()
        {
            var act = new TestAct { Count = 1 };
            ResourceSaver.Save(act, "user://a.chact", SaveFlags.ChangePath);
            ResourceSaver.Save(new TestTake { Featured = act }, "user://t.chtk");

            act.Count = 2;
            ResourceSaver.Save(act, "user://a.chact");
            ResourceLoader.ClearCache();

            Assert.AreEqual(2, ResourceLoader.Load<TestTake>("user://t.chtk").Featured.Count);
        }

        [Test]
        public void AMissingExternalFileFailsTheLoadWithBothPathsInTheMessage()
        {
            var act = new TestAct();
            ResourceSaver.Save(act, "user://a.chact", SaveFlags.ChangePath);
            ResourceSaver.Save(new TestTake { Featured = act }, "user://t.chtk");
            File.Delete(OnDisk("a.chact"));
            ResourceLoader.ClearCache();

            var ex = Assert.Throws<ResourceFormatException>(() => ResourceLoader.Load<TestTake>("user://t.chtk"));
            StringAssert.Contains("a.chact", ex.Message);
            StringAssert.Contains("user://t.chtk", ex.Message);
        }

        [Test]
        public void FilesThatReferenceEachOtherAreRejectedInsteadOfLoopingForever()
        {
            File.WriteAllText(OnDisk("a.res"),
                "[unires format=1 type=\"TestNode\"]\n[ext_resource type=\"TestNode\" path=\"user://b.res\" id=\"1\"]\n[resource]\nChild = ExtResource(\"1\")\n");
            File.WriteAllText(OnDisk("b.res"),
                "[unires format=1 type=\"TestNode\"]\n[ext_resource type=\"TestNode\" path=\"user://a.res\" id=\"1\"]\n[resource]\nChild = ExtResource(\"1\")\n");

            var ex = Assert.Throws<ResourceFormatException>(() => ResourceLoader.Load("user://a.res"));
            StringAssert.Contains("refers to itself", ex.Message);
        }

        // ---- errors -------------------------------------------------------------------------

        [Test]
        public void ABadFileReportsItsPathAndPosition()
        {
            File.WriteAllText(OnDisk("bad.res"), "[unires format=1 type=\"SampleResource\"]\n[resource]\nHealth = \n");

            var ex = Assert.Throws<ResourceFormatException>(() => ResourceLoader.Load("user://bad.res"));

            Assert.AreEqual("user://bad.res", ex.Path);
            StringAssert.StartsWith("user://bad.res: line", ex.Message);
        }

        [Test]
        public void ABadFileIsNotCached()
        {
            File.WriteAllText(OnDisk("bad.res"), "garbage");
            Assert.Throws<ResourceFormatException>(() => ResourceLoader.Load("user://bad.res"));

            File.WriteAllText(OnDisk("bad.res"), ResourceSaver.SaveToString(new SampleResource { Health = 3 }));

            Assert.AreEqual(3, ResourceLoader.Load<SampleResource>("user://bad.res").Health);
        }

        // ---- string and stream ------------------------------------------------------------------

        [Test]
        public void StringExportAndImportRoundTrip()
        {
            var take = new TestTake { Title = "Exported" };
            take.Acts.Add(new TestAct { Count = 3, Origin = new Vector3(1, 2, 3) });

            string text = ResourceSaver.SaveToString(take);
            var imported = ResourceLoader.LoadFromString<TestTake>(text);

            Assert.AreEqual("Exported", imported.Title);
            Assert.AreEqual(3, imported.Acts[0].Count);
            Assert.AreEqual(new Vector3(1, 2, 3), imported.Acts[0].Origin);
            Assert.IsTrue(imported.IsBuiltIn);
        }

        [Test]
        public void ImportedTextStillLoadsItsExternalFiles()
        {
            var act = new TestAct { Count = 4 };
            ResourceSaver.Save(act, "user://a.chact", SaveFlags.ChangePath);
            string text = ResourceSaver.SaveToString(new TestTake { Featured = act });
            ResourceLoader.ClearCache();

            var imported = ResourceLoader.LoadFromString<TestTake>(text);

            Assert.AreEqual(4, imported.Featured.Count);
        }

        [Test]
        public void StreamExportAndImportRoundTripAndLeaveTheStreamOpen()
        {
            var resource = new SampleResource { Health = 8, ResourceName = "caf\u00e9" };

            using (var stream = new MemoryStream())
            {
                ResourceSaver.SaveToStream(resource, stream);
                Assert.IsTrue(stream.CanWrite);

                stream.Position = 0;
                var imported = ResourceLoader.LoadFromStream<SampleResource>(stream);

                Assert.IsTrue(stream.CanRead);
                Assert.AreEqual(8, imported.Health);
                Assert.AreEqual("caf\u00e9", imported.ResourceName);
            }
        }

        [Test]
        public void ImportingAnUnexpectedClassFromTextGivesAClearError()
        {
            string text = ResourceSaver.SaveToString(new SampleResource());

            var ex = Assert.Throws<UniResException>(() => ResourceLoader.LoadFromString<TestAct>(text));
            StringAssert.Contains("SampleResource", ex.Message);
        }

        [Test]
        public void ImportedTextWithAFormatErrorHasNoPath()
        {
            var ex = Assert.Throws<ResourceFormatException>(() => ResourceLoader.LoadFromString("nonsense"));
            Assert.IsNull(ex.Path);
        }
    }
}
