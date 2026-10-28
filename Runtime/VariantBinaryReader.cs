using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UniRes
{
    /// <summary>
    /// Reads one binary variant value from a stream.
    /// </summary>
    internal static class VariantBinaryReader
    {
        public static object Read(BinaryReader reader)
        {
            var tag = (BinaryVariantType)reader.ReadByte();
            switch (tag)
            {
                case BinaryVariantType.Null:
                    return null;

                case BinaryVariantType.Bool:
                    return reader.ReadBoolean();

                case BinaryVariantType.Int64:
                    return reader.ReadInt64();

                case BinaryVariantType.Double:
                    return reader.ReadDouble();

                case BinaryVariantType.Float:
                    return reader.ReadSingle();

                case BinaryVariantType.String:
                    return reader.ReadString();

                case BinaryVariantType.Vector2:
                    return new Vector2(reader.ReadSingle(), reader.ReadSingle());

                case BinaryVariantType.Vector3:
                    return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

                case BinaryVariantType.Vector4:
                    return new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

                case BinaryVariantType.Quaternion:
                    return new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

                case BinaryVariantType.Color:
                    return new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

                case BinaryVariantType.Vector2Int:
                    return new Vector2Int(reader.ReadInt32(), reader.ReadInt32());

                case BinaryVariantType.Vector3Int:
                    return new Vector3Int(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());

                case BinaryVariantType.ByteArray:
                {
                    int len = reader.ReadInt32();
                    return reader.ReadBytes(len);
                }

                case BinaryVariantType.Int32Array:
                {
                    int len = reader.ReadInt32();
                    var array = new int[len];
                    byte[] raw = reader.ReadBytes(len * 4);
                    Buffer.BlockCopy(raw, 0, array, 0, raw.Length);
                    return array;
                }

                case BinaryVariantType.Int64Array:
                {
                    int len = reader.ReadInt32();
                    var array = new long[len];
                    byte[] raw = reader.ReadBytes(len * 8);
                    Buffer.BlockCopy(raw, 0, array, 0, raw.Length);
                    return array;
                }

                case BinaryVariantType.FloatArray:
                {
                    int len = reader.ReadInt32();
                    var array = new float[len];
                    byte[] raw = reader.ReadBytes(len * 4);
                    Buffer.BlockCopy(raw, 0, array, 0, raw.Length);
                    return array;
                }

                case BinaryVariantType.DoubleArray:
                {
                    int len = reader.ReadInt32();
                    var array = new double[len];
                    byte[] raw = reader.ReadBytes(len * 8);
                    Buffer.BlockCopy(raw, 0, array, 0, raw.Length);
                    return array;
                }

                case BinaryVariantType.StringArray:
                {
                    int len = reader.ReadInt32();
                    var array = new string[len];
                    for (int i = 0; i < len; i++)
                        array[i] = reader.ReadString();
                    return array;
                }

                case BinaryVariantType.GenericSequence:
                {
                    int len = reader.ReadInt32();
                    var list = new List<object>(len);
                    for (int i = 0; i < len; i++)
                        list.Add(Read(reader));
                    return list;
                }

                case BinaryVariantType.Dictionary:
                {
                    int len = reader.ReadInt32();
                    var dict = new VariantDictionary();
                    for (int i = 0; i < len; i++)
                    {
                        object key = Read(reader);
                        object val = Read(reader);
                        dict.Entries.Add(new KeyValuePair<object, object>(key, val));
                    }
                    return dict;
                }

                case BinaryVariantType.ExternalResourceRef:
                    return new ResourceRef(ResourceRefKind.External, reader.ReadString());

                case BinaryVariantType.EmbeddedResourceRef:
                    return new ResourceRef(ResourceRefKind.Embedded, reader.ReadString());

                default:
                    throw new ResourceFormatException($"Corrupted binary UniRes data: unknown variant type tag {(byte)tag}.");
            }
        }
    }
}
