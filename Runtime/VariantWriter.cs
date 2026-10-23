using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace UniRes
{
    /// <summary>
    /// Writes one C# value as UniRes text, using the declared member type to pick the form
    /// (for example <c>float[]</c> becomes <c>PackedFloat32Array(...)</c>).
    /// </summary>
    internal static class VariantWriter
    {
        public static void Write(StringBuilder sb, object value, Type type, IResourceReferenceWriter references)
        {
            if (typeof(Resource).IsAssignableFrom(type))
            {
                if (value == null)
                    sb.Append("null");
                else
                    sb.Append(references.GetReference((Resource)value));
                return;
            }

            if (value == null)
            {
                if (type.IsValueType)
                    throw new UniResException($"A null value cannot be written for the value type {type.Name}.");

                sb.Append("null");
                return;
            }

            if (type == typeof(bool))
                sb.Append((bool)value ? "true" : "false");
            else if (type == typeof(string))
                AppendQuoted(sb, (string)value);
            else if (type == typeof(float))
                sb.Append(FormatFloat((float)value));
            else if (type == typeof(double))
                sb.Append(FormatDouble((double)value));
            else if (IsWholeNumber(type))
                sb.Append(Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
            else if (type.IsEnum)
                sb.Append(Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
            else if (type == typeof(Vector2))
                WriteFloats(sb, "Vector2", ((Vector2)value).x, ((Vector2)value).y);
            else if (type == typeof(Vector3))
                WriteFloats(sb, "Vector3", ((Vector3)value).x, ((Vector3)value).y, ((Vector3)value).z);
            else if (type == typeof(Vector4))
                WriteFloats(sb, "Vector4", ((Vector4)value).x, ((Vector4)value).y, ((Vector4)value).z, ((Vector4)value).w);
            else if (type == typeof(Quaternion))
                WriteFloats(sb, "Quaternion", ((Quaternion)value).x, ((Quaternion)value).y, ((Quaternion)value).z, ((Quaternion)value).w);
            else if (type == typeof(Color))
                WriteFloats(sb, "Color", ((Color)value).r, ((Color)value).g, ((Color)value).b, ((Color)value).a);
            else if (type == typeof(Vector2Int))
                WriteInts(sb, "Vector2i", ((Vector2Int)value).x, ((Vector2Int)value).y);
            else if (type == typeof(Vector3Int))
                WriteInts(sb, "Vector3i", ((Vector3Int)value).x, ((Vector3Int)value).y, ((Vector3Int)value).z);
            else if (type.IsArray && type.GetArrayRank() == 1)
                WriteArray(sb, (Array)value, type.GetElementType(), references);
            else if (IsGeneric(type, typeof(List<>)))
                WriteList(sb, (IList)value, type.GetGenericArguments()[0], references);
            else if (IsGeneric(type, typeof(Dictionary<,>)))
                WriteDictionary(sb, (IDictionary)value, type.GetGenericArguments()[0], type.GetGenericArguments()[1], references);
            else
                throw new UniResException($"The type {type.FullName} cannot be saved. Supported: bool, whole numbers, float, double, string, enums, Vector2/3/4, Vector2Int/3Int, Quaternion, Color, arrays, List<T>, Dictionary<K,V> and Resource classes.");
        }

        /// <summary>Formats a float so it reads back to exactly the same value.</summary>
        public static string FormatFloat(float value)
        {
            if (float.IsNaN(value))
                return "nan";
            if (float.IsPositiveInfinity(value))
                return "inf";
            if (float.IsNegativeInfinity(value))
                return "inf_neg";

            string text = value.ToString("R", CultureInfo.InvariantCulture);
            // The reader parses as double and then narrows, so verify that exact path.
            if ((float)double.Parse(text, CultureInfo.InvariantCulture) != value)
                text = value.ToString("G9", CultureInfo.InvariantCulture);

            return EnsureFloatLooking(text);
        }

        /// <summary>Formats a double so it reads back to exactly the same value.</summary>
        public static string FormatDouble(double value)
        {
            if (double.IsNaN(value))
                return "nan";
            if (double.IsPositiveInfinity(value))
                return "inf";
            if (double.IsNegativeInfinity(value))
                return "inf_neg";

            string text = value.ToString("R", CultureInfo.InvariantCulture);
            if (double.Parse(text, CultureInfo.InvariantCulture) != value)
                text = value.ToString("G17", CultureInfo.InvariantCulture);

            return EnsureFloatLooking(text);
        }

        public static void AppendQuoted(StringBuilder sb, string text)
        {
            sb.Append('"');
            foreach (char c in text)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }

            sb.Append('"');
        }

        /// <summary>Whole numbers must not read back as whole numbers when a float was meant.</summary>
        private static string EnsureFloatLooking(string text)
        {
            foreach (char c in text)
            {
                if (c == '.' || c == 'E' || c == 'e')
                    return text;
            }

            return text + ".0";
        }

        private static bool IsWholeNumber(Type type)
        {
            return type == typeof(int) || type == typeof(long) || type == typeof(short)
                || type == typeof(byte) || type == typeof(sbyte) || type == typeof(ushort)
                || type == typeof(uint);
        }

        private static bool IsGeneric(Type type, Type definition)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == definition;
        }

        private static void WriteFloats(StringBuilder sb, string name, params float[] values)
        {
            sb.Append(name).Append('(');
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(FormatFloat(values[i]));
            }

            sb.Append(')');
        }

        private static void WriteInts(StringBuilder sb, string name, params int[] values)
        {
            sb.Append(name).Append('(');
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(values[i].ToString(CultureInfo.InvariantCulture));
            }

            sb.Append(')');
        }

        private static void WriteArray(StringBuilder sb, Array array, Type elementType, IResourceReferenceWriter references)
        {
            if (elementType == typeof(byte))
            {
                sb.Append("PackedByteArray(\"").Append(Convert.ToBase64String((byte[])array)).Append("\")");
            }
            else if (elementType == typeof(int))
            {
                WritePacked(sb, "PackedInt32Array", array, item => ((int)item).ToString(CultureInfo.InvariantCulture));
            }
            else if (elementType == typeof(long))
            {
                WritePacked(sb, "PackedInt64Array", array, item => ((long)item).ToString(CultureInfo.InvariantCulture));
            }
            else if (elementType == typeof(float))
            {
                WritePacked(sb, "PackedFloat32Array", array, item => FormatFloat((float)item));
            }
            else if (elementType == typeof(double))
            {
                WritePacked(sb, "PackedFloat64Array", array, item => FormatDouble((double)item));
            }
            else if (elementType == typeof(string))
            {
                sb.Append("PackedStringArray(");
                for (int i = 0; i < array.Length; i++)
                {
                    if (i > 0)
                        sb.Append(", ");
                    if (array.GetValue(i) == null)
                        throw new UniResException("A string array containing null cannot be saved.");
                    AppendQuoted(sb, (string)array.GetValue(i));
                }

                sb.Append(')');
            }
            else
            {
                WriteSequence(sb, array, elementType, references);
            }
        }

        private static void WritePacked(StringBuilder sb, string name, Array array, Func<object, string> format)
        {
            sb.Append(name).Append('(');
            for (int i = 0; i < array.Length; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(format(array.GetValue(i)));
            }

            sb.Append(')');
        }

        private static void WriteList(StringBuilder sb, IList list, Type elementType, IResourceReferenceWriter references)
        {
            WriteSequence(sb, list, elementType, references);
        }

        private static void WriteSequence(StringBuilder sb, IEnumerable items, Type elementType, IResourceReferenceWriter references)
        {
            sb.Append('[');
            bool first = true;
            foreach (object item in items)
            {
                if (!first)
                    sb.Append(", ");
                first = false;
                Write(sb, item, elementType, references);
            }

            sb.Append(']');
        }

        private static void WriteDictionary(StringBuilder sb, IDictionary dictionary, Type keyType, Type valueType, IResourceReferenceWriter references)
        {
            sb.Append('{');
            bool first = true;
            foreach (DictionaryEntry entry in dictionary)
            {
                if (!first)
                    sb.Append(", ");
                first = false;
                Write(sb, entry.Key, keyType, references);
                sb.Append(": ");
                Write(sb, entry.Value, valueType, references);
            }

            sb.Append('}');
        }
    }
}
