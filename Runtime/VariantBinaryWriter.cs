using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace UniRes
{
    /// <summary>
    /// Writes one C# value as binary data, mirroring <see cref="VariantWriter"/>.
    /// </summary>
    internal static class VariantBinaryWriter
    {
        public static void Write(BinaryWriter writer, object value, Type type, IBinaryResourceReferenceWriter references)
        {
            if (typeof(Resource).IsAssignableFrom(type))
            {
                if (value == null)
                {
                    writer.Write((byte)BinaryVariantType.Null);
                }
                else
                {
                    var (kind, id) = references.GetReference((Resource)value);
                    if (kind == ResourceRefKind.External)
                    {
                        writer.Write((byte)BinaryVariantType.ExternalResourceRef);
                        writer.Write(id);
                    }
                    else
                    {
                        writer.Write((byte)BinaryVariantType.EmbeddedResourceRef);
                        writer.Write(id);
                    }
                }
                return;
            }

            if (value == null)
            {
                if (type.IsValueType)
                    throw new UniResException($"A null value cannot be written for the value type {type.Name}.");

                writer.Write((byte)BinaryVariantType.Null);
                return;
            }

            if (type == typeof(bool))
            {
                writer.Write((byte)BinaryVariantType.Bool);
                writer.Write((bool)value);
            }
            else if (type == typeof(string))
            {
                writer.Write((byte)BinaryVariantType.String);
                writer.Write((string)value);
            }
            else if (type == typeof(float))
            {
                writer.Write((byte)BinaryVariantType.Float);
                writer.Write((float)value);
            }
            else if (type == typeof(double))
            {
                writer.Write((byte)BinaryVariantType.Double);
                writer.Write((double)value);
            }
            else if (IsWholeNumber(type) || type.IsEnum)
            {
                writer.Write((byte)BinaryVariantType.Int64);
                writer.Write(Convert.ToInt64(value, CultureInfo.InvariantCulture));
            }
            else if (type == typeof(Vector2))
            {
                var v = (Vector2)value;
                writer.Write((byte)BinaryVariantType.Vector2);
                writer.Write(v.x);
                writer.Write(v.y);
            }
            else if (type == typeof(Vector3))
            {
                var v = (Vector3)value;
                writer.Write((byte)BinaryVariantType.Vector3);
                writer.Write(v.x);
                writer.Write(v.y);
                writer.Write(v.z);
            }
            else if (type == typeof(Vector4))
            {
                var v = (Vector4)value;
                writer.Write((byte)BinaryVariantType.Vector4);
                writer.Write(v.x);
                writer.Write(v.y);
                writer.Write(v.z);
                writer.Write(v.w);
            }
            else if (type == typeof(Quaternion))
            {
                var q = (Quaternion)value;
                writer.Write((byte)BinaryVariantType.Quaternion);
                writer.Write(q.x);
                writer.Write(q.y);
                writer.Write(q.z);
                writer.Write(q.w);
            }
            else if (type == typeof(Color))
            {
                var c = (Color)value;
                writer.Write((byte)BinaryVariantType.Color);
                writer.Write(c.r);
                writer.Write(c.g);
                writer.Write(c.b);
                writer.Write(c.a);
            }
            else if (type == typeof(Vector2Int))
            {
                var v = (Vector2Int)value;
                writer.Write((byte)BinaryVariantType.Vector2Int);
                writer.Write(v.x);
                writer.Write(v.y);
            }
            else if (type == typeof(Vector3Int))
            {
                var v = (Vector3Int)value;
                writer.Write((byte)BinaryVariantType.Vector3Int);
                writer.Write(v.x);
                writer.Write(v.y);
                writer.Write(v.z);
            }
            else if (type.IsArray && type.GetArrayRank() == 1)
            {
                WriteArray(writer, (Array)value, type.GetElementType(), references);
            }
            else if (IsGeneric(type, typeof(List<>)))
            {
                WriteList(writer, (IList)value, type.GetGenericArguments()[0], references);
            }
            else if (IsGeneric(type, typeof(Dictionary<,>)))
            {
                WriteDictionary(writer, (IDictionary)value, type.GetGenericArguments()[0], type.GetGenericArguments()[1], references);
            }
            else
            {
                throw new UniResException($"The type {type.FullName} cannot be saved. Supported: bool, whole numbers, float, double, string, enums, Vector2/3/4, Vector2Int/3Int, Quaternion, Color, arrays, List<T>, Dictionary<K,V> and Resource classes.");
            }
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

        private static void WriteArray(BinaryWriter writer, Array array, Type elementType, IBinaryResourceReferenceWriter references)
        {
            if (elementType == typeof(byte))
            {
                var bytes = (byte[])array;
                writer.Write((byte)BinaryVariantType.ByteArray);
                writer.Write(bytes.Length);
                writer.Write(bytes);
            }
            else if (elementType == typeof(int))
            {
                var ints = (int[])array;
                writer.Write((byte)BinaryVariantType.Int32Array);
                writer.Write(ints.Length);
                byte[] raw = new byte[ints.Length * 4];
                Buffer.BlockCopy(ints, 0, raw, 0, raw.Length);
                writer.Write(raw);
            }
            else if (elementType == typeof(long))
            {
                var longs = (long[])array;
                writer.Write((byte)BinaryVariantType.Int64Array);
                writer.Write(longs.Length);
                byte[] raw = new byte[longs.Length * 8];
                Buffer.BlockCopy(longs, 0, raw, 0, raw.Length);
                writer.Write(raw);
            }
            else if (elementType == typeof(float))
            {
                var floats = (float[])array;
                writer.Write((byte)BinaryVariantType.FloatArray);
                writer.Write(floats.Length);
                byte[] raw = new byte[floats.Length * 4];
                Buffer.BlockCopy(floats, 0, raw, 0, raw.Length);
                writer.Write(raw);
            }
            else if (elementType == typeof(double))
            {
                var doubles = (double[])array;
                writer.Write((byte)BinaryVariantType.DoubleArray);
                writer.Write(doubles.Length);
                byte[] raw = new byte[doubles.Length * 8];
                Buffer.BlockCopy(doubles, 0, raw, 0, raw.Length);
                writer.Write(raw);
            }
            else if (elementType == typeof(string))
            {
                var strings = (string[])array;
                writer.Write((byte)BinaryVariantType.StringArray);
                writer.Write(strings.Length);
                for (int i = 0; i < strings.Length; i++)
                {
                    if (strings[i] == null)
                        throw new UniResException("A string array containing null cannot be saved.");
                    writer.Write(strings[i]);
                }
            }
            else
            {
                WriteSequence(writer, array, elementType, references);
            }
        }

        private static void WriteList(BinaryWriter writer, IList list, Type elementType, IBinaryResourceReferenceWriter references)
        {
            WriteSequence(writer, list, elementType, references);
        }

        private static void WriteSequence(BinaryWriter writer, IEnumerable items, Type elementType, IBinaryResourceReferenceWriter references)
        {
            writer.Write((byte)BinaryVariantType.GenericSequence);

            int count = 0;
            if (items is ICollection col)
                count = col.Count;
            else
            {
                foreach (var _ in items)
                    count++;
            }

            writer.Write(count);
            foreach (object item in items)
            {
                Write(writer, item, elementType, references);
            }
        }

        private static void WriteDictionary(BinaryWriter writer, IDictionary dictionary, Type keyType, Type valueType, IBinaryResourceReferenceWriter references)
        {
            writer.Write((byte)BinaryVariantType.Dictionary);
            writer.Write(dictionary.Count);
            foreach (DictionaryEntry entry in dictionary)
            {
                Write(writer, entry.Key, keyType, references);
                Write(writer, entry.Value, valueType, references);
            }
        }
    }
}
