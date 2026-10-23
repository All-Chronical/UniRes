using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace UniRes
{
    /// <summary>
    /// Reads one value from UniRes text into a neutral tree of plain objects:
    /// <c>null</c>, <see cref="bool"/>, <see cref="long"/>, <see cref="double"/>, <see cref="string"/>,
    /// Unity vector/colour structs, typed packed arrays, <see cref="List{T}"/> of object,
    /// <see cref="VariantDictionary"/> and <see cref="ResourceRef"/>.
    /// <see cref="VariantConverter"/> later maps that tree onto real C# member types.
    /// </summary>
    internal static class VariantParser
    {
        public static object Parse(TextScanner s)
        {
            s.SkipWhitespace();
            if (s.AtEnd)
                throw s.Fail("Expected a value but found the end of the file.");

            char c = s.Peek;
            if (c == '"')
                return s.ReadString();
            if (c == '[')
                return ParseList(s);
            if (c == '{')
                return ParseDictionary(s);
            if (c == '-' || c == '+' || char.IsDigit(c))
                return ParseNumber(s);
            if (TextScanner.IsIdentifierStart(c))
                return ParseNamed(s);

            throw s.Fail($"Expected a value but found {s.Describe()}.");
        }

        private static object ParseNumber(TextScanner s)
        {
            int start = s.Position;
            string token = s.ReadNumberToken(out bool isFloat);

            if (isFloat)
            {
                if (!TryParseDouble(token, out double d))
                    throw s.FailAt(start, $"'{token}' is not a valid number.");
                return d;
            }

            if (!long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long l))
                throw s.FailAt(start, $"'{token}' is out of range for a whole number.");

            return l;
        }

        private static object ParseList(TextScanner s)
        {
            s.Expect('[');
            var list = new List<object>();

            if (s.TryConsume(']'))
                return list;

            while (true)
            {
                list.Add(Parse(s));

                if (s.TryConsume(','))
                {
                    if (s.TryConsume(']'))
                        return list;
                    continue;
                }

                s.Expect(']');
                return list;
            }
        }

        private static object ParseDictionary(TextScanner s)
        {
            s.Expect('{');
            var dict = new VariantDictionary();

            if (s.TryConsume('}'))
                return dict;

            while (true)
            {
                object key = Parse(s);
                s.Expect(':');
                object value = Parse(s);
                dict.Entries.Add(new KeyValuePair<object, object>(key, value));

                if (s.TryConsume(','))
                {
                    if (s.TryConsume('}'))
                        return dict;
                    continue;
                }

                s.Expect('}');
                return dict;
            }
        }

        private static object ParseNamed(TextScanner s)
        {
            int start = s.Position;
            string name = s.ReadIdentifier();

            switch (name)
            {
                case "null": return null;
                case "true": return true;
                case "false": return false;
                case "inf": return double.PositiveInfinity;
                case "inf_neg": return double.NegativeInfinity;
                case "nan": return double.NaN;
            }

            if (!s.TryConsume('('))
                throw s.FailAt(start, $"Unknown value '{name}'.");

            switch (name)
            {
                case "Vector2":
                {
                    float[] v = ReadFloats(s, 2);
                    return new Vector2(v[0], v[1]);
                }
                case "Vector3":
                {
                    float[] v = ReadFloats(s, 3);
                    return new Vector3(v[0], v[1], v[2]);
                }
                case "Vector4":
                {
                    float[] v = ReadFloats(s, 4);
                    return new Vector4(v[0], v[1], v[2], v[3]);
                }
                case "Quaternion":
                {
                    float[] v = ReadFloats(s, 4);
                    return new Quaternion(v[0], v[1], v[2], v[3]);
                }
                case "Color":
                {
                    float[] v = ReadFloats(s, 4);
                    return new Color(v[0], v[1], v[2], v[3]);
                }
                case "Vector2i":
                {
                    int[] v = ReadFixedInts(s, 2);
                    return new Vector2Int(v[0], v[1]);
                }
                case "Vector3i":
                {
                    int[] v = ReadFixedInts(s, 3);
                    return new Vector3Int(v[0], v[1], v[2]);
                }
                case "PackedByteArray":
                    return ReadPackedBytes(s);
                case "PackedInt32Array":
                    return ReadItems(s, ReadInt32).ToArray();
                case "PackedInt64Array":
                    return ReadItems(s, ReadInt64).ToArray();
                case "PackedFloat32Array":
                    return ReadItems(s, t => (float)ReadDouble(t)).ToArray();
                case "PackedFloat64Array":
                    return ReadItems(s, ReadDouble).ToArray();
                case "PackedStringArray":
                    return ReadItems(s, t => t.ReadString()).ToArray();
                case "ExtResource":
                    return ReadReference(s, ResourceRefKind.External);
                case "SubResource":
                    return ReadReference(s, ResourceRefKind.Embedded);
                default:
                    throw s.FailAt(start, $"Unknown value type '{name}'.");
            }
        }

        private static ResourceRef ReadReference(TextScanner s, ResourceRefKind kind)
        {
            string id = s.ReadString();
            s.Expect(')');
            return new ResourceRef(kind, id);
        }

        private static byte[] ReadPackedBytes(TextScanner s)
        {
            s.SkipWhitespace();
            if (s.Peek == '"')
            {
                int start = s.Position;
                string base64 = s.ReadString();
                s.Expect(')');
                try
                {
                    return Convert.FromBase64String(base64);
                }
                catch (FormatException)
                {
                    throw s.FailAt(start, "The PackedByteArray text is not valid base64.");
                }
            }

            List<int> ints = ReadItems(s, ReadInt32);
            var bytes = new byte[ints.Count];
            for (int i = 0; i < bytes.Length; i++)
            {
                if (ints[i] < 0 || ints[i] > 255)
                    throw s.Fail($"{ints[i]} is not a valid byte.");
                bytes[i] = (byte)ints[i];
            }

            return bytes;
        }

        /// <summary>Reads comma-separated items up to and including the closing ')'.</summary>
        private static List<T> ReadItems<T>(TextScanner s, Func<TextScanner, T> readOne)
        {
            var items = new List<T>();
            if (s.TryConsume(')'))
                return items;

            while (true)
            {
                items.Add(readOne(s));

                if (s.TryConsume(','))
                    continue;

                s.Expect(')');
                return items;
            }
        }

        private static float[] ReadFloats(TextScanner s, int count)
        {
            var values = new float[count];
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                    s.Expect(',');
                values[i] = (float)ReadDouble(s);
            }

            s.Expect(')');
            return values;
        }

        private static int[] ReadFixedInts(TextScanner s, int count)
        {
            var values = new int[count];
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                    s.Expect(',');
                values[i] = ReadInt32(s);
            }

            s.Expect(')');
            return values;
        }

        private static double ReadDouble(TextScanner s)
        {
            s.SkipWhitespace();
            if (TextScanner.IsIdentifierStart(s.Peek))
            {
                int start = s.Position;
                string word = s.ReadIdentifier();
                switch (word)
                {
                    case "inf": return double.PositiveInfinity;
                    case "inf_neg": return double.NegativeInfinity;
                    case "nan": return double.NaN;
                    default: throw s.FailAt(start, $"Expected a number but found '{word}'.");
                }
            }

            int numberStart = s.Position;
            string token = s.ReadNumberToken(out _);
            if (!TryParseDouble(token, out double value))
                throw s.FailAt(numberStart, $"'{token}' is not a valid number.");

            return value;
        }

        private static long ReadInt64(TextScanner s)
        {
            int start = s.Position;
            string token = s.ReadNumberToken(out bool isFloat);
            if (isFloat)
                throw s.FailAt(start, $"Expected a whole number but found '{token}'.");

            if (!long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value))
                throw s.FailAt(start, $"'{token}' is out of range for a whole number.");

            return value;
        }

        private static int ReadInt32(TextScanner s)
        {
            int start = s.Position;
            long value = ReadInt64(s);
            if (value < int.MinValue || value > int.MaxValue)
                throw s.FailAt(start, $"{value} is out of range for a 32-bit whole number.");

            return (int)value;
        }

        private static bool TryParseDouble(string token, out double value)
        {
            try
            {
                return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            }
            catch (OverflowException)
            {
                value = 0;
                return false;
            }
        }
    }
}
