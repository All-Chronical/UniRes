using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace UniRes.Tests
{
    public class AllTypesBinaryResource : Resource
    {
        [Export] public bool BoolVal;
        [Export] public int IntVal;
        [Export] public long LongVal;
        [Export] public float FloatVal;
        [Export] public double DoubleVal;
        [Export] public string StringVal;
        [Export] public TestMode EnumVal;
        [Export] public Vector2 V2;
        [Export] public Vector3 V3;
        [Export] public Vector4 V4;
        [Export] public Quaternion Quat;
        [Export] public Color Col;
        [Export] public Vector2Int V2I;
        [Export] public Vector3Int V3I;

        [Export] public byte[] Bytes = Array.Empty<byte>();
        [Export] public int[] Ints = Array.Empty<int>();
        [Export] public long[] Longs = Array.Empty<long>();
        [Export] public float[] Floats = Array.Empty<float>();
        [Export] public double[] Doubles = Array.Empty<double>();
        [Export] public string[] Strings = Array.Empty<string>();

        [Export] public List<Vector3> VecList = new List<Vector3>();
        [Export] public Dictionary<string, int> IntDict = new Dictionary<string, int>();
    }

    public class ResourceBinaryTests
    {
        private string previousRoot;
        private string tempRoot;

        [SetUp]
        public void SetUp()
        {
            previousRoot = ResourcePaths.UserDataRoot;
            tempRoot = Path.Combine(Path.GetTempPath(), "UniResBinaryTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            ResourcePaths.UserDataRoot = tempRoot;
            ResourceLoader.ClearCache();
        }

        [TearDown]
        public void TearDown()
        {
            ResourceLoader.ClearCache();
            ResourcePaths.UserDataRoot = previousRoot;
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);
        }

        [Test]
        public void RoundTripsAllTypesViaBinaryBytes()
        {
            var source = new AllTypesBinaryResource
            {
                BoolVal = true,
                IntVal = 42,
                LongVal = 9876543210123L,
                FloatVal = 3.14159f,
                DoubleVal = 2.718281828459,
                StringVal = "Hello UniRes Binary!",
                EnumVal = TestMode.Fast,
                V2 = new Vector2(1.5f, 2.5f),
                V3 = new Vector3(3.5f, 4.5f, 5.5f),
                V4 = new Vector4(6.5f, 7.5f, 8.5f, 9.5f),
                Quat = new Quaternion(0.1f, 0.2f, 0.3f, 0.9f),
                Col = new Color(0.1f, 0.2f, 0.3f, 0.4f),
                V2I = new Vector2Int(10, 20),
                V3I = new Vector3Int(30, 40, 50),
                Bytes = new byte[] { 1, 2, 3, 255 },
                Ints = new int[] { 100, 200, 300 },
                Longs = new long[] { 1000L, 2000L, 3000L },
                Floats = new float[] { 1.1f, 2.2f, 3.3f, 4.4f },
                Doubles = new double[] { 1.11, 2.22, 3.33 },
                Strings = new string[] { "first", "second", "third" },
                VecList = new List<Vector3> { Vector3.up, Vector3.forward },
                IntDict = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } },
            };

            byte[] bytes = ResourceSaver.SaveToBytes(source);
            Assert.IsNotNull(bytes);
            Assert.Greater(bytes.Length, 8);

            // Assert magic bytes
            Assert.AreEqual((byte)'U', bytes[0]);
            Assert.AreEqual((byte)'N', bytes[1]);
            Assert.AreEqual((byte)'R', bytes[2]);
            Assert.AreEqual((byte)'E', bytes[3]);
            Assert.AreEqual((byte)'S', bytes[4]);
            Assert.AreEqual(1, bytes[5]);

            var loaded = ResourceLoader.LoadFromBytes<AllTypesBinaryResource>(bytes);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(source.BoolVal, loaded.BoolVal);
            Assert.AreEqual(source.IntVal, loaded.IntVal);
            Assert.AreEqual(source.LongVal, loaded.LongVal);
            Assert.AreEqual(source.FloatVal, loaded.FloatVal);
            Assert.AreEqual(source.DoubleVal, loaded.DoubleVal);
            Assert.AreEqual(source.StringVal, loaded.StringVal);
            Assert.AreEqual(source.EnumVal, loaded.EnumVal);
            Assert.AreEqual(source.V2, loaded.V2);
            Assert.AreEqual(source.V3, loaded.V3);
            Assert.AreEqual(source.V4, loaded.V4);
            Assert.AreEqual(source.Quat, loaded.Quat);
            Assert.AreEqual(source.Col, loaded.Col);
            Assert.AreEqual(source.V2I, loaded.V2I);
            Assert.AreEqual(source.V3I, loaded.V3I);
            Assert.AreEqual(source.Bytes, loaded.Bytes);
            Assert.AreEqual(source.Ints, loaded.Ints);
            Assert.AreEqual(source.Longs, loaded.Longs);
            Assert.AreEqual(source.Floats, loaded.Floats);
            Assert.AreEqual(source.Doubles, loaded.Doubles);
            Assert.AreEqual(source.Strings, loaded.Strings);
            Assert.AreEqual(source.VecList, loaded.VecList);
            Assert.AreEqual(source.IntDict, loaded.IntDict);
        }

        [Test]
        public void RoundTripsNestedSubResourcesInBinary()
        {
            var take = new TestTake { Title = "Binary Take" };
            var act1 = new TestAct { Count = 1, Origin = new Vector3(1, 2, 3), Samples = new float[] { 0.1f, 0.2f } };
            var act2 = new TestAct { Count = 2, Origin = new Vector3(4, 5, 6), Samples = new float[] { 0.3f, 0.4f } };
            take.Acts.Add(act1);
            take.Acts.Add(act2);
            take.Featured = act1; // Shared instance!

            byte[] bytes = ResourceSaver.SaveToBytes(take);
            var loaded = ResourceLoader.LoadFromBytes<TestTake>(bytes);

            Assert.AreEqual("Binary Take", loaded.Title);
            Assert.AreEqual(2, loaded.Acts.Count);
            Assert.AreEqual(1, loaded.Acts[0].Count);
            Assert.AreEqual(2, loaded.Acts[1].Count);
            Assert.AreEqual(loaded.Acts[0].Samples, new float[] { 0.1f, 0.2f });
            Assert.AreEqual(loaded.Acts[1].Samples, new float[] { 0.3f, 0.4f });

            // Shared reference must point to the identical instance!
            Assert.AreSame(loaded.Acts[0], loaded.Featured);
        }

        [Test]
        public void SavesAsBinaryWithSaveFlagsBinary()
        {
            var act = new TestAct { Count = 5, Samples = new float[] { 1f, 2f, 3f } };
            string path = "user://test_act.bin";
            ResourceSaver.Save(act, path, SaveFlags.Binary);

            string file = ResourcePaths.ToFileSystemPath(path);
            byte[] raw = File.ReadAllBytes(file);
            Assert.AreEqual((byte)'U', raw[0]);
            Assert.AreEqual((byte)'N', raw[1]);
            Assert.AreEqual((byte)'R', raw[2]);
            Assert.AreEqual((byte)'E', raw[3]);
            Assert.AreEqual((byte)'S', raw[4]);

            var loaded = ResourceLoader.Load<TestAct>(path);
            Assert.AreEqual(5, loaded.Count);
            Assert.AreEqual(act.Samples, loaded.Samples);
        }

        [Test]
        public void AutoDetectsBinaryForResExtension()
        {
            var act = new TestAct { Count = 99 };
            string path = "user://auto.res";
            ResourceSaver.Save(act, path); // No SaveFlags.Binary explicitly passed

            string file = ResourcePaths.ToFileSystemPath(path);
            byte[] raw = File.ReadAllBytes(file);
            Assert.AreEqual((byte)'U', raw[0]);

            var loaded = ResourceLoader.Load<TestAct>(path);
            Assert.AreEqual(99, loaded.Count);
        }

        [Test]
        public void ResourceLoaderAutoDetectsTextVsBinary()
        {
            var actText = new TestAct { Count = 10 };
            var actBin = new TestAct { Count = 20 };

            ResourceSaver.Save(actText, "user://first.tres");
            ResourceSaver.Save(actBin, "user://second.tres", SaveFlags.Binary); // .tres extension but binary content

            var loadedText = ResourceLoader.Load<TestAct>("user://first.tres");
            var loadedBin = ResourceLoader.Load<TestAct>("user://second.tres");

            Assert.AreEqual(10, loadedText.Count);
            Assert.AreEqual(20, loadedBin.Count);
        }

        [Test]
        public void RejectsCorruptBinaryHeader()
        {
            byte[] badBytes = new byte[] { (byte)'U', (byte)'N', (byte)'R', (byte)'E', (byte)'X', 1, 0, 0 };
            Assert.Throws<ResourceFormatException>(() => ResourceLoader.LoadFromBytes(badBytes));
        }

        [Test]
        public void RejectsUnsupportedBinaryVersion()
        {
            byte[] badVersion = new byte[] { (byte)'U', (byte)'N', (byte)'R', (byte)'E', (byte)'S', 99, 0, 0 };
            Assert.Throws<ResourceFormatException>(() => ResourceLoader.LoadFromBytes(badVersion));
        }

        [Test]
        public void DetectsCyclesInBinarySave()
        {
            var nodeA = new TestNode { Depth = 1 };
            var nodeB = new TestNode { Depth = 2 };
            nodeA.Child = nodeB;
            nodeB.Child = nodeA;

            var ex = Assert.Throws<UniResException>(() => ResourceSaver.SaveToBytes(nodeA));
            StringAssert.Contains("loop", ex.Message);
        }
    }
}
