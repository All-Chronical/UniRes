using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;

namespace UniRes.Tests
{
    /// <summary>Serves "files" from a dictionary of path to text, like a tiny in-memory disk.</summary>
    internal sealed class MemoryDisk : IExternalResourceLoader
    {
        public readonly Dictionary<string, string> Files = new Dictionary<string, string>();
        public readonly List<string> Loads = new List<string>();

        public Resource Load(string path, Type expectedType)
        {
            Loads.Add(path);
            if (!Files.TryGetValue(path, out string text))
                throw new UniResException("File not found.");

            Resource resource = ResourceFileReader.Read(text, this);
            resource.ResourcePath = path;
            return resource;
        }

        /// <summary>Writes a resource into the disk and gives it that path, like a real save would.</summary>
        public void Save(Resource resource, string path)
        {
            Files[path] = ResourceFileWriter.Write(resource, path);
            resource.ResourcePath = path;
        }
    }

    public class ResourceFileTests
    {
        private static string Write(Resource resource, string path = "")
        {
            return ResourceFileWriter.Write(resource, path);
        }

        private static T Read<T>(string text, IExternalResourceLoader loader = null) where T : Resource
        {
            return (T)ResourceFileReader.Read(text, loader ?? new MemoryDisk());
        }

        // ---- single resource ----------------------------------------------------------------

        [Test]
        public void WritesAGodotStyleFile()
        {
            var resource = new SampleResource { Health = 5, Speed = 1.5f };

            string expected =
                "[unires format=1 type=\"SampleResource\"]\n" +
                "\n" +
                "[resource]\n" +
                "ResourceName = \"\"\n" +
                "Health = 5\n" +
                "secret = \"hidden\"\n" +
                "renamed = 1.5\n";

            Assert.AreEqual(expected, Write(resource));
        }

        [Test]
        public void ASingleResourceRoundTrips()
        {
            var resource = new ChildResource { ResourceName = "hero", Health = 9, Speed = 0.1f, Flag = true };
            resource.SetSecret("s3cret \"quoted\"\nnewline");

            var loaded = Read<ChildResource>(Write(resource));

            Assert.AreEqual("hero", loaded.ResourceName);
            Assert.AreEqual(9, loaded.Health);
            Assert.AreEqual(0.1f, loaded.Speed);
            Assert.IsTrue(loaded.Flag);
            Assert.AreEqual("s3cret \"quoted\"\nnewline", loaded.Secret);
            Assert.AreNotSame(resource, loaded);
        }

        [Test]
        public void UsesTheResourceTypeAttributeNameInTheFile()
        {
            StringAssert.StartsWith("[unires format=1 type=\"Custom_Name\"]", Write(new NamedResource()));
            Assert.IsInstanceOf<NamedResource>(Read<NamedResource>(Write(new NamedResource())));
        }

        [Test]
        public void MissingPropertiesKeepTheirDefaultsAndUnknownPropertiesAreIgnored()
        {
            string text =
                "[unires format=1 type=\"SampleResource\"]\n" +
                "[resource]\n" +
                "Health = 7\n" +
                "futureProperty = Vector3(1, 2, 3)\n" +
                "alsoNew = [1, 2, {\"a\": null}]\n";

            var loaded = Read<SampleResource>(text);

            Assert.AreEqual(7, loaded.Health);
            Assert.AreEqual("hidden", loaded.Secret);
            Assert.AreEqual("", loaded.ResourceName);
        }

        [Test]
        public void WindowsLineEndingsBomAndCommentsAreAccepted()
        {
            string text = "\uFEFF; a comment\r\n[unires format=1 type=\"SampleResource\"]\r\n\r\n[resource]\r\n; note\r\nHealth = 3 ; trailing\r\n";

            Assert.AreEqual(3, Read<SampleResource>(text).Health);
        }

        // ---- nesting ----------------------------------------------------------------------

        [Test]
        public void NestedResourcesAreWrittenAsSubResourceSections()
        {
            var take = new TestTake { Title = "Take 1" };
            take.Acts.Add(new TestAct { Count = 1 });
            take.Acts.Add(new TestAct { Count = 2 });

            string text = Write(take);

            StringAssert.Contains("[sub_resource type=\"TestAct\" id=\"TestAct_1\"]", text);
            StringAssert.Contains("[sub_resource type=\"TestAct\" id=\"TestAct_2\"]", text);
            StringAssert.Contains("Acts = [SubResource(\"TestAct_1\"), SubResource(\"TestAct_2\")]", text);
            Assert.Less(text.IndexOf("[sub_resource", StringComparison.Ordinal), text.IndexOf("[resource]", StringComparison.Ordinal));
        }

        [Test]
        public void NestedResourcesRoundTripWithTheirData()
        {
            var take = new TestTake { Title = "Take 1" };
            take.Acts.Add(new TestAct { Count = 1, Origin = new Vector3(1, 2, 3), Samples = new[] { 0.1f, 0.2f, 0.3f } });
            take.Acts.Add(new TestAct { Count = 2, ResourceName = "second" });
            take.ByName["first"] = take.Acts[0];
            take.Featured = take.Acts[1];

            var loaded = Read<TestTake>(Write(take));

            Assert.AreEqual("Take 1", loaded.Title);
            Assert.AreEqual(2, loaded.Acts.Count);
            Assert.AreEqual(1, loaded.Acts[0].Count);
            Assert.AreEqual(new Vector3(1, 2, 3), loaded.Acts[0].Origin);
            CollectionAssert.AreEqual(new[] { 0.1f, 0.2f, 0.3f }, loaded.Acts[0].Samples);
            Assert.AreEqual("second", loaded.Acts[1].ResourceName);
            Assert.AreSame(loaded.Acts[0], loaded.ByName["first"]);
            Assert.AreSame(loaded.Acts[1], loaded.Featured);
        }

        [Test]
        public void AResourceUsedTwiceIsWrittenOnceAndStaysShared()
        {
            var shared = new TestAct { Count = 5 };
            var take = new TestTake();
            take.Acts.Add(shared);
            take.Acts.Add(shared);
            take.Featured = shared;

            string text = Write(take);

            Assert.AreEqual(1, CountOccurrences(text, "[sub_resource"));

            var loaded = Read<TestTake>(text);
            Assert.AreSame(loaded.Acts[0], loaded.Acts[1]);
            Assert.AreSame(loaded.Acts[0], loaded.Featured);
        }

        [Test]
        public void DeepNestingWritesInnermostFirstAndRoundTrips()
        {
            var top = new TestNode { Depth = 0, Child = new TestNode { Depth = 1, Child = new TestNode { Depth = 2 } } };

            string text = Write(top);

            // The innermost resource is defined first, so it gets the first id.
            StringAssert.Contains("[sub_resource type=\"TestNode\" id=\"TestNode_1\"]\nResourceName = \"\"\nDepth = 2", text);
            Assert.Less(text.IndexOf("id=\"TestNode_1\"", StringComparison.Ordinal), text.IndexOf("id=\"TestNode_2\"", StringComparison.Ordinal));

            var loaded = Read<TestNode>(text);
            Assert.AreEqual(0, loaded.Depth);
            Assert.AreEqual(1, loaded.Child.Depth);
            Assert.AreEqual(2, loaded.Child.Child.Depth);
            Assert.IsNull(loaded.Child.Child.Child);
        }

        [Test]
        public void DifferentSubclassesInABaseTypeListKeepTheirTypes()
        {
            var take = new TestTake();
            take.Anything.Add(new TestAct { Count = 3 });
            take.Anything.Add(new SampleResource { Health = 4 });
            take.Anything.Add(null);

            var loaded = Read<TestTake>(Write(take));

            Assert.IsInstanceOf<TestAct>(loaded.Anything[0]);
            Assert.IsInstanceOf<SampleResource>(loaded.Anything[1]);
            Assert.AreEqual(4, ((SampleResource)loaded.Anything[1]).Health);
            Assert.IsNull(loaded.Anything[2]);
        }

        [Test]
        public void ALargeSampleArrayRoundTripsQuickly()
        {
            var samples = new float[28 * 120 * 60 * 5];
            var random = new System.Random(1);
            for (int i = 0; i < samples.Length; i++)
                samples[i] = (float)(random.NextDouble() * 20 - 10);

            var act = new TestAct { Samples = samples };

            var timer = Stopwatch.StartNew();
            string text = Write(act);
            var loaded = Read<TestAct>(text);
            timer.Stop();

            CollectionAssert.AreEqual(samples, loaded.Samples);
            Assert.Less(timer.ElapsedMilliseconds, 10000, "Round trip of " + samples.Length + " floats took too long.");
        }

        // ---- external references -------------------------------------------------------------

        [Test]
        public void ResourcesWithAPathAreWrittenAsExternalReferences()
        {
            var disk = new MemoryDisk();
            var act = new TestAct { Count = 8 };
            disk.Save(act, "user://acts/1.chact");

            var take = new TestTake();
            take.Acts.Add(act);

            string text = Write(take, "user://film.chtk");

            StringAssert.Contains("[ext_resource type=\"TestAct\" path=\"user://acts/1.chact\" id=\"1\"]", text);
            StringAssert.Contains("Acts = [ExtResource(\"1\")]", text);
            StringAssert.DoesNotContain("[sub_resource", text);
        }

        [Test]
        public void ExternalResourcesAreLoadedThroughTheLoader()
        {
            var disk = new MemoryDisk();
            var act = new TestAct { Count = 8, Samples = new[] { 1f, 2f } };
            disk.Save(act, "user://acts/1.chact");

            var take = new TestTake();
            take.Acts.Add(act);
            take.Featured = act;
            disk.Save(take, "user://film.chtk");

            var loaded = Read<TestTake>(disk.Files["user://film.chtk"], disk);

            Assert.AreEqual(8, loaded.Acts[0].Count);
            CollectionAssert.AreEqual(new[] { 1f, 2f }, loaded.Acts[0].Samples);
            Assert.AreSame(loaded.Acts[0], loaded.Featured);
            Assert.AreEqual("user://acts/1.chact", loaded.Acts[0].ResourcePath);
            Assert.AreEqual(1, disk.Loads.Count);
        }

        [Test]
        public void TheSameExternalFileIsReferencedOnce()
        {
            var disk = new MemoryDisk();
            var act = new TestAct();
            disk.Save(act, "a.chact");

            var take = new TestTake { Featured = act };
            take.Acts.Add(act);

            Assert.AreEqual(1, CountOccurrences(Write(take), "[ext_resource"));
        }

        [Test]
        public void ExternalAndEmbeddedResourcesCanBeMixed()
        {
            var disk = new MemoryDisk();
            var external = new TestAct { Count = 1 };
            disk.Save(external, "ext.chact");

            var take = new TestTake();
            take.Acts.Add(external);
            take.Acts.Add(new TestAct { Count = 2 });

            disk.Save(take, "film.chtk");
            var loaded = Read<TestTake>(disk.Files["film.chtk"], disk);

            Assert.AreEqual(1, loaded.Acts[0].Count);
            Assert.AreEqual(2, loaded.Acts[1].Count);
            Assert.AreEqual("ext.chact", loaded.Acts[0].ResourcePath);
            Assert.IsTrue(loaded.Acts[1].IsBuiltIn);
        }

        [Test]
        public void ANestedResourceWithTheSavePathIsEmbeddedNotReferenced()
        {
            var child = new TestNode { Depth = 1 };
            child.ResourcePath = "same.res";
            var root = new TestNode { Depth = 0, Child = child };

            string text = Write(root, "same.res");

            StringAssert.DoesNotContain("[ext_resource", text);
            StringAssert.Contains("[sub_resource", text);
        }

        [Test]
        public void ExternalFilesBackslashPathsCompareAsTheSamePath()
        {
            var child = new TestNode { Depth = 1 };
            child.ResourcePath = "dir\\same.res";

            string text = Write(new TestNode { Child = child }, "dir/same.res");

            StringAssert.DoesNotContain("[ext_resource", text);
        }

        [Test]
        public void ExternalFileThatCannotBeLoadedGivesAClearError()
        {
            string text =
                "[unires format=1 type=\"TestTake\"]\n" +
                "[ext_resource type=\"TestAct\" path=\"missing.chact\" id=\"1\"]\n" +
                "[resource]\n";

            var ex = Assert.Throws<ResourceFormatException>(() => Read<TestTake>(text));
            StringAssert.Contains("missing.chact", ex.Message);
            Assert.AreEqual(2, ex.Line);
        }

        [Test]
        public void ExternalFileOfTheWrongClassIsRejected()
        {
            var disk = new MemoryDisk();
            disk.Files["a.res"] = Write(new SampleResource());
            string text =
                "[unires format=1 type=\"TestTake\"]\n" +
                "[ext_resource type=\"TestAct\" path=\"a.res\" id=\"1\"]\n" +
                "[resource]\n";

            Assert.Throws<ResourceFormatException>(() => Read<TestTake>(text, disk));
        }

        // ---- loops -----------------------------------------------------------------------

        [Test]
        public void AResourceContainingItselfCannotBeSaved()
        {
            var node = new TestNode();
            node.Child = node;

            var ex = Assert.Throws<UniResException>(() => Write(node));
            StringAssert.Contains("contains itself", ex.Message);
        }

        [Test]
        public void ALoopThroughOtherResourcesCannotBeSaved()
        {
            var a = new TestNode();
            var b = new TestNode { Child = a };
            a.Child = b;

            Assert.Throws<UniResException>(() => Write(a));
        }

        // ---- errors -------------------------------------------------------------------------

        [Test]
        public void FileWithoutAHeaderIsRejected()
        {
            var ex = Assert.Throws<ResourceFormatException>(() => Read<SampleResource>("[resource]\nHealth = 1\n"));
            StringAssert.Contains("UniRes", ex.Message);
        }

        [Test]
        public void EmptyFileIsRejected()
        {
            Assert.Throws<ResourceFormatException>(() => Read<SampleResource>(""));
            Assert.Throws<ResourceFormatException>(() => Read<SampleResource>("  \n ; nothing\n"));
        }

        [Test]
        public void FileFromANewerFormatVersionIsRejected()
        {
            var ex = Assert.Throws<ResourceFormatException>(() => Read<SampleResource>("[unires format=99 type=\"SampleResource\"]\n[resource]\n"));
            StringAssert.Contains("99", ex.Message);
        }

        [Test]
        public void FileWithoutAResourceSectionIsRejected()
        {
            Assert.Throws<ResourceFormatException>(() => Read<SampleResource>("[unires format=1 type=\"SampleResource\"]\n"));
        }

        [Test]
        public void UnknownTypeNameIsRejectedWithItsPosition()
        {
            string text = "[unires format=1 type=\"NoSuchType\"]\n[resource]\n";

            var ex = Assert.Throws<ResourceFormatException>(() => Read<SampleResource>(text));
            StringAssert.Contains("NoSuchType", ex.Message);
            Assert.AreEqual(2, ex.Line);
        }

        [Test]
        public void ReferenceToAnUndefinedResourceIsRejected()
        {
            string text =
                "[unires format=1 type=\"TestTake\"]\n" +
                "[resource]\n" +
                "Featured = SubResource(\"TestAct_9\")\n";

            var ex = Assert.Throws<ResourceFormatException>(() => Read<TestTake>(text));
            StringAssert.Contains("TestAct_9", ex.Message);
            Assert.AreEqual(3, ex.Line);
        }

        [Test]
        public void ReferenceToASectionDefinedLaterIsRejected()
        {
            string text =
                "[unires format=1 type=\"TestTake\"]\n" +
                "[sub_resource type=\"TestNode\" id=\"A\"]\n" +
                "Child = SubResource(\"B\")\n" +
                "[sub_resource type=\"TestNode\" id=\"B\"]\n" +
                "[resource]\n";

            Assert.Throws<ResourceFormatException>(() => Read<TestTake>(text));
        }

        [Test]
        public void WrongValueTypeReportsThePropertyAndLine()
        {
            string text =
                "[unires format=1 type=\"SampleResource\"]\n" +
                "[resource]\n" +
                "ResourceName = \"ok\"\n" +
                "Health = \"lots\"\n";

            var ex = Assert.Throws<ResourceFormatException>(() => Read<SampleResource>(text));
            StringAssert.Contains("Health", ex.Message);
            Assert.AreEqual(4, ex.Line);
        }

        [Test]
        public void ExceptionFromAPropertySetterIsReportedWithTheProperty()
        {
            string text = "[unires format=1 type=\"ThrowingSetterResource\"]\n[resource]\nValue = -1\n";

            var ex = Assert.Throws<ResourceFormatException>(() => Read<ThrowingSetterResource>(text));
            StringAssert.Contains("Value", ex.Message);
            StringAssert.Contains("negative", ex.Message);
        }

        [Test]
        public void DuplicateSectionIdsAreRejected()
        {
            string text =
                "[unires format=1 type=\"TestTake\"]\n" +
                "[sub_resource type=\"TestAct\" id=\"A\"]\n" +
                "[sub_resource type=\"TestAct\" id=\"A\"]\n" +
                "[resource]\n";

            Assert.Throws<ResourceFormatException>(() => Read<TestTake>(text));
        }

        [Test]
        public void ContentAfterTheResourceSectionIsRejected()
        {
            string text =
                "[unires format=1 type=\"SampleResource\"]\n" +
                "[resource]\n" +
                "[sub_resource type=\"TestAct\" id=\"A\"]\n";

            Assert.Throws<ResourceFormatException>(() => Read<SampleResource>(text));
        }

        [Test]
        public void UnknownSectionAndMissingAttributesAreRejected()
        {
            Assert.Throws<ResourceFormatException>(() => Read<SampleResource>("[unires format=1 type=\"SampleResource\"]\n[bogus]\n[resource]\n"));
            Assert.Throws<ResourceFormatException>(() => Read<SampleResource>("[unires format=1]\n[resource]\n"));
            Assert.Throws<ResourceFormatException>(() => Read<SampleResource>("[unires type=\"SampleResource\"]\n[resource]\n"));
            Assert.Throws<ResourceFormatException>(() => Read<SampleResource>("[unires format=1 type=\"SampleResource\"\n[resource]\n"));
        }

        [Test]
        public void UnsupportedMemberTypeIsReportedWithClassAndMember()
        {
            var ex = Assert.Throws<UniResException>(() => Write(new UnsupportedMemberResource()));
            StringAssert.Contains("UnsupportedMemberResource.When", ex.Message);
        }

        private static int CountOccurrences(string text, string find)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(find, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += find.Length;
            }

            return count;
        }
    }

    public class UnsupportedMemberResource : Resource
    {
        [Export] public DateTime When = new DateTime(2020, 1, 1);
    }
}
