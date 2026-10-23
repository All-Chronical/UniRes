using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace UniRes.Tests
{
    public enum TestMode
    {
        None = 0,
        Fast = 1,
        Slow = 2,
    }

    /// <summary>Gives out ids when writing and finds resources by id when reading.</summary>
    internal sealed class StubReferences : IResourceReferenceWriter, IResourceReferenceResolver
    {
        public readonly Dictionary<string, Resource> ById = new Dictionary<string, Resource>();

        public string GetReference(Resource resource)
        {
            string id = "r" + ById.Count;
            ById[id] = resource;
            return $"SubResource(\"{id}\")";
        }

        public Resource Resolve(ResourceRef reference)
        {
            if (!ById.TryGetValue(reference.Id, out Resource resource))
                throw new UniResException($"No resource with id '{reference.Id}'.");
            return resource;
        }
    }

    public class VariantTests
    {
        private static string WriteValue<T>(T value, StubReferences refs = null)
        {
            var sb = new StringBuilder();
            VariantWriter.Write(sb, value, typeof(T), refs ?? new StubReferences());
            return sb.ToString();
        }

        private static T ReadValue<T>(string text, StubReferences refs = null)
        {
            var scanner = new TextScanner(text);
            object parsed = VariantParser.Parse(scanner);
            scanner.SkipWhitespace();
            Assert.IsTrue(scanner.AtEnd, "Unread text left over: " + text);
            return (T)VariantConverter.ToType(parsed, typeof(T), refs ?? new StubReferences());
        }

        private static T RoundTrip<T>(T value)
        {
            return ReadValue<T>(WriteValue(value));
        }

        // ---- exact text ------------------------------------------------------------------

        [Test]
        public void WritesGodotStyleText()
        {
            Assert.AreEqual("true", WriteValue(true));
            Assert.AreEqual("42", WriteValue(42));
            Assert.AreEqual("-7", WriteValue(-7L));
            Assert.AreEqual("1.0", WriteValue(1f));
            Assert.AreEqual("2.5", WriteValue(2.5));
            Assert.AreEqual("\"hi\"", WriteValue("hi"));
            Assert.AreEqual("null", WriteValue<string>(null));
            Assert.AreEqual("Vector3(1.0, 2.0, 3.5)", WriteValue(new Vector3(1, 2, 3.5f)));
            Assert.AreEqual("Vector2i(3, -4)", WriteValue(new Vector2Int(3, -4)));
            Assert.AreEqual("Color(1.0, 0.5, 0.0, 1.0)", WriteValue(new Color(1, 0.5f, 0, 1)));
            Assert.AreEqual("PackedFloat32Array(1.0, 2.5)", WriteValue(new[] { 1f, 2.5f }));
            Assert.AreEqual("PackedInt32Array(1, 2)", WriteValue(new[] { 1, 2 }));
            Assert.AreEqual("PackedByteArray(\"AQID\")", WriteValue(new byte[] { 1, 2, 3 }));
            Assert.AreEqual("PackedStringArray(\"a\", \"b\")", WriteValue(new[] { "a", "b" }));
            Assert.AreEqual("[1, 2]", WriteValue(new List<int> { 1, 2 }));
            Assert.AreEqual("{\"a\": 1, \"b\": 2}", WriteValue(new Dictionary<string, int> { { "a", 1 }, { "b", 2 } }));
            Assert.AreEqual("1", WriteValue(TestMode.Fast));
        }

        // ---- round trips -----------------------------------------------------------------

        [Test]
        public void ScalarsRoundTrip()
        {
            Assert.AreEqual(true, RoundTrip(true));
            Assert.AreEqual(false, RoundTrip(false));
            Assert.AreEqual(0, RoundTrip(0));
            Assert.AreEqual(int.MinValue, RoundTrip(int.MinValue));
            Assert.AreEqual(long.MaxValue, RoundTrip(long.MaxValue));
            Assert.AreEqual(uint.MaxValue, RoundTrip(uint.MaxValue));
            Assert.AreEqual((byte)255, RoundTrip((byte)255));
            Assert.AreEqual((short)-5, RoundTrip((short)-5));
            Assert.AreEqual(TestMode.Slow, RoundTrip(TestMode.Slow));
        }

        [Test]
        public void FloatsRoundTripExactly()
        {
            float[] samples =
            {
                0f, 1f, -1f, 0.1f, 1f / 3f, 123456789f, 1e-10f, 3.4028235e38f, -1.17549435e-38f,
                float.Epsilon, 16777217f, 0.30000001192092896f,
            };

            foreach (float sample in samples)
                Assert.AreEqual(sample, RoundTrip(sample), "float " + sample.ToString("R", CultureInfo.InvariantCulture));
        }

        [Test]
        public void DoublesRoundTripExactly()
        {
            double[] samples = { 0d, 0.1, 1d / 3d, 1e300, -2.2250738585072014e-308, double.Epsilon, Math.PI };

            foreach (double sample in samples)
                Assert.AreEqual(sample, RoundTrip(sample), "double " + sample.ToString("R", CultureInfo.InvariantCulture));
        }

        [Test]
        public void NonFiniteNumbersRoundTrip()
        {
            Assert.AreEqual(float.PositiveInfinity, RoundTrip(float.PositiveInfinity));
            Assert.AreEqual(float.NegativeInfinity, RoundTrip(float.NegativeInfinity));
            Assert.IsTrue(float.IsNaN(RoundTrip(float.NaN)));
            Assert.AreEqual(double.NegativeInfinity, RoundTrip(double.NegativeInfinity));
            Assert.AreEqual("inf", WriteValue(float.PositiveInfinity));
            Assert.AreEqual("inf_neg", WriteValue(double.NegativeInfinity));
            Assert.AreEqual("nan", WriteValue(float.NaN));
        }

        [Test]
        public void NegativeZeroKeepsItsSign()
        {
            float result = RoundTrip(-0f);
            Assert.IsTrue(float.IsNegative(result));
        }

        [Test]
        public void StringsRoundTripWithEscapes()
        {
            string[] samples =
            {
                "", "plain", "with \"quotes\"", "back\\slash", "line\nbreak\r\n", "tab\there",
                "unicode: h\u00e9llo \u65e5\u672c \ud83c\udfac", "control\u0001char", "[brackets], {braces}; semi: colon",
            };

            foreach (string sample in samples)
                Assert.AreEqual(sample, RoundTrip(sample));
        }

        [Test]
        public void NullStringIsDistinctFromEmptyString()
        {
            Assert.IsNull(RoundTrip<string>(null));
            Assert.AreEqual("", RoundTrip(""));
        }

        [Test]
        public void UnityStructsRoundTrip()
        {
            Assert.AreEqual(new Vector2(1.5f, -2f), RoundTrip(new Vector2(1.5f, -2f)));
            Assert.AreEqual(new Vector3(0.1f, 1f / 3f, -7e-5f), RoundTrip(new Vector3(0.1f, 1f / 3f, -7e-5f)));
            Assert.AreEqual(new Vector4(1, 2, 3, 4), RoundTrip(new Vector4(1, 2, 3, 4)));
            Assert.AreEqual(new Vector2Int(5, -6), RoundTrip(new Vector2Int(5, -6)));
            Assert.AreEqual(new Vector3Int(7, 8, 9), RoundTrip(new Vector3Int(7, 8, 9)));

            var q = new Quaternion(0.1f, 0.2f, 0.3f, 0.9f);
            Quaternion q2 = RoundTrip(q);
            Assert.AreEqual((q.x, q.y, q.z, q.w), (q2.x, q2.y, q2.z, q2.w));

            var c = new Color(0.25f, 0.5f, 0.75f, 1f);
            Color c2 = RoundTrip(c);
            Assert.AreEqual((c.r, c.g, c.b, c.a), (c2.r, c2.g, c2.b, c2.a));
        }

        [Test]
        public void PackedArraysRoundTrip()
        {
            CollectionAssert.AreEqual(new[] { 1f, 2.5f, 1f / 3f }, RoundTrip(new[] { 1f, 2.5f, 1f / 3f }));
            CollectionAssert.AreEqual(new[] { 1, -2, 3 }, RoundTrip(new[] { 1, -2, 3 }));
            CollectionAssert.AreEqual(new[] { long.MinValue, 0L }, RoundTrip(new[] { long.MinValue, 0L }));
            CollectionAssert.AreEqual(new[] { 0.1, 0.2 }, RoundTrip(new[] { 0.1, 0.2 }));
            CollectionAssert.AreEqual(new byte[] { 0, 1, 254, 255 }, RoundTrip(new byte[] { 0, 1, 254, 255 }));
            CollectionAssert.AreEqual(new[] { "a", "b\"c", "" }, RoundTrip(new[] { "a", "b\"c", "" }));
        }

        [Test]
        public void EmptyCollectionsRoundTrip()
        {
            Assert.AreEqual(0, RoundTrip(new float[0]).Length);
            Assert.AreEqual(0, RoundTrip(new byte[0]).Length);
            Assert.AreEqual(0, RoundTrip(new string[0]).Length);
            Assert.AreEqual(0, RoundTrip(new List<int>()).Count);
            Assert.AreEqual(0, RoundTrip(new Dictionary<string, int>()).Count);
            Assert.AreEqual("PackedFloat32Array()", WriteValue(new float[0]));
            Assert.AreEqual("{}", WriteValue(new Dictionary<string, int>()));
        }

        [Test]
        public void GenericAndNestedCollectionsRoundTrip()
        {
            CollectionAssert.AreEqual(new[] { new Vector3(1, 2, 3), Vector3.zero }, RoundTrip(new[] { new Vector3(1, 2, 3), Vector3.zero }));
            CollectionAssert.AreEqual(new List<string> { "x", "y" }, RoundTrip(new List<string> { "x", "y" }));

            var nested = new List<List<int>> { new List<int> { 1, 2 }, new List<int>(), new List<int> { 3 } };
            List<List<int>> nested2 = RoundTrip(nested);
            Assert.AreEqual(3, nested2.Count);
            CollectionAssert.AreEqual(new[] { 1, 2 }, nested2[0]);
            CollectionAssert.AreEqual(new[] { 3 }, nested2[2]);

            var byName = new Dictionary<string, int> { { "one", 1 }, { "two", 2 } };
            CollectionAssert.AreEquivalent(byName, RoundTrip(byName));

            var byNumber = new Dictionary<int, string> { { 1, "a" }, { -2, "b" } };
            CollectionAssert.AreEquivalent(byNumber, RoundTrip(byNumber));

            var dictOfLists = new Dictionary<string, float[]> { { "k", new[] { 1f, 2f } } };
            CollectionAssert.AreEqual(new[] { 1f, 2f }, RoundTrip(dictOfLists)["k"]);
        }

        [Test]
        public void ResourceReferencesRoundTripThroughTheirReferenceText()
        {
            var refs = new StubReferences();
            var target = new SampleResource { Health = 3 };

            string text = WriteValue<Resource>(target, refs);
            Assert.AreEqual("SubResource(\"r0\")", text);
            Assert.AreSame(target, ReadValue<Resource>(text, refs));
            Assert.AreSame(target, ReadValue<SampleResource>(text, refs));
            Assert.IsNull(ReadValue<SampleResource>("null", refs));
            Assert.AreEqual("null", WriteValue<SampleResource>(null, refs));
        }

        [Test]
        public void ListsOfResourcesWriteAReferenceEach()
        {
            var refs = new StubReferences();
            var list = new List<SampleResource> { new SampleResource(), null, new SampleResource() };

            Assert.AreEqual("[SubResource(\"r0\"), null, SubResource(\"r1\")]", WriteValue(list, refs));
        }

        [Test]
        public void ChangingTheCultureDoesNotChangeTheOutput()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                Assert.AreEqual("Vector3(1.5, -2.0, 0.25)", WriteValue(new Vector3(1.5f, -2f, 0.25f)));
                Assert.AreEqual("2.5", WriteValue(2.5));
                Assert.AreEqual(2.5, ReadValue<double>("2.5"));
                Assert.AreEqual(new Vector2Int(-1, 2), ReadValue<Vector2Int>("Vector2i(-1, 2)"));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        // ---- hand-written text ------------------------------------------------------------

        [Test]
        public void ReaderIgnoresWhitespaceCommentsAndTrailingCommas()
        {
            string text = "[\n  1, ; first\n  2,\n\t3,\n]";
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, ReadValue<int[]>(text));

            var map = ReadValue<Dictionary<string, int>>("{\n \"a\" : 1 ,\n \"b\":2,\n}");
            Assert.AreEqual(2, map["b"]);
        }

        [Test]
        public void ReaderAcceptsAWholeNumberWhereAFloatIsExpected()
        {
            Assert.AreEqual(2f, ReadValue<float>("2"));
            Assert.AreEqual(2d, ReadValue<double>("2"));
            Assert.AreEqual(new Vector2(1, 2), ReadValue<Vector2>("Vector2(1, 2)"));
            CollectionAssert.AreEqual(new[] { 1f, 2f }, ReadValue<float[]>("PackedFloat32Array(1, 2)"));
            CollectionAssert.AreEqual(new[] { 1f, 2f }, ReadValue<float[]>("[1, 2.0]"));
        }

        [Test]
        public void ReaderAcceptsEnumNames()
        {
            Assert.AreEqual(TestMode.Slow, ReadValue<TestMode>("\"Slow\""));
        }

        [Test]
        public void ReaderAcceptsPackedByteArrayAsNumbers()
        {
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, ReadValue<byte[]>("PackedByteArray(1, 2, 3)"));
        }

        [Test]
        public void ReaderAcceptsAPackedArrayForAListMember()
        {
            CollectionAssert.AreEqual(new List<int> { 1, 2 }, ReadValue<List<int>>("PackedInt32Array(1, 2)"));
        }

        // ---- errors -----------------------------------------------------------------------

        [Test]
        public void ParseErrorsReportLineAndColumn()
        {
            var scanner = new TextScanner("\n\n  Foo(1)");
            var ex = Assert.Throws<ResourceFormatException>(() => VariantParser.Parse(scanner));

            Assert.AreEqual(3, ex.Line);
            Assert.AreEqual(3, ex.Column);
            StringAssert.Contains("Foo", ex.Message);
        }

        [TestCase("[1, 2")]
        [TestCase("\"unterminated")]
        [TestCase("Vector3(1, 2)")]
        [TestCase("Vector3(1, 2, 3")]
        [TestCase("{\"a\" 1}")]
        [TestCase("PackedByteArray(\"!!!not base64!!!\")")]
        [TestCase("PackedInt32Array(1.5)")]
        [TestCase("PackedInt32Array(99999999999)")]
        [TestCase("PackedByteArray(300)")]
        [TestCase("bogus")]
        [TestCase("\"bad \\q escape\"")]
        [TestCase("1e")]
        [TestCase("")]
        public void MalformedTextThrowsAFormatException(string text)
        {
            Assert.Throws<ResourceFormatException>(() => VariantParser.Parse(new TextScanner(text)));
        }

        [Test]
        public void WholeNumberOutOfRangeIsRejected()
        {
            Assert.Throws<ResourceFormatException>(() => VariantParser.Parse(new TextScanner("99999999999999999999")));
        }

        [TestCase("\"text\"", typeof(int))]
        [TestCase("1", typeof(string))]
        [TestCase("1", typeof(bool))]
        [TestCase("1.5", typeof(int))]
        [TestCase("null", typeof(int))]
        [TestCase("300", typeof(byte))]
        [TestCase("\"Nope\"", typeof(TestMode))]
        [TestCase("Vector2(1, 2)", typeof(Vector3))]
        [TestCase("1", typeof(Vector3))]
        [TestCase("[1]", typeof(Dictionary<string, int>))]
        [TestCase("{\"a\": 1}", typeof(List<int>))]
        [TestCase("{\"a\": 1, \"a\": 2}", typeof(Dictionary<string, int>))]
        [TestCase("[\"x\"]", typeof(int[]))]
        public void WrongTypeThrowsAClearError(string text, Type target)
        {
            var scanner = new TextScanner(text);
            object parsed = VariantParser.Parse(scanner);

            Assert.Throws<UniResException>(() => VariantConverter.ToType(parsed, target, new StubReferences()));
        }

        [Test]
        public void ReferenceToTheWrongResourceClassIsRejected()
        {
            var refs = new StubReferences();
            refs.ById["r0"] = new NamedResource();

            var ex = Assert.Throws<UniResException>(() => ReadValue<SampleResource>("SubResource(\"r0\")", refs));
            StringAssert.Contains("NamedResource", ex.Message);
        }

        [Test]
        public void ResourceMemberRejectsNonReferences()
        {
            Assert.Throws<UniResException>(() => ReadValue<SampleResource>("5"));
        }

        [Test]
        public void UnsupportedTypesAreRejectedOnWriteAndRead()
        {
            Assert.Throws<UniResException>(() => WriteValue(new object()));
            Assert.Throws<UniResException>(() => WriteValue(new DateTime(2020, 1, 1)));
            Assert.Throws<UniResException>(() => ReadValue<DateTime>("1"));
        }

        [Test]
        public void StringArrayWithNullCannotBeSaved()
        {
            Assert.Throws<UniResException>(() => WriteValue(new string[] { "a", null }));
        }
    }
}
