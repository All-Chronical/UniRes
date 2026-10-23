using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace UniRes
{
    /// <summary>
    /// Maps the neutral value tree from <see cref="VariantParser"/> onto the C# type of an
    /// exported member. Failures throw <see cref="UniResException"/> without a file position;
    /// the file reader adds the position of the property being read.
    /// </summary>
    internal static class VariantConverter
    {
        public static object ToType(object parsed, Type target, IResourceReferenceResolver resolver)
        {
            if (typeof(Resource).IsAssignableFrom(target))
                return ToResource(parsed, target, resolver);

            if (parsed == null)
            {
                if (target.IsValueType)
                    throw Mismatch(parsed, target);
                return null;
            }

            if (parsed.GetType() == target)
                return parsed;

            if (target == typeof(bool))
                throw Mismatch(parsed, target);

            if (target == typeof(float))
                return TryGetNumber(parsed, out double f) ? (float)f : throw Mismatch(parsed, target);

            if (target == typeof(double))
                return TryGetNumber(parsed, out double d) ? d : throw Mismatch(parsed, target);

            if (IsWholeNumber(target))
                return ToWholeNumber(parsed, target);

            if (target.IsEnum)
                return ToEnum(parsed, target);

            if (target.IsArray && target.GetArrayRank() == 1)
                return ToArray(parsed, target, resolver);

            if (target.IsGenericType && target.GetGenericTypeDefinition() == typeof(List<>))
                return ToList(parsed, target, resolver);

            if (target.IsGenericType && target.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                return ToDictionary(parsed, target, resolver);

            if (target == typeof(string) || IsSupportedStruct(target))
                throw Mismatch(parsed, target);

            throw new UniResException($"The type {target.FullName} cannot be loaded. Supported: bool, whole numbers, float, double, string, enums, Vector2/3/4, Vector2Int/3Int, Quaternion, Color, arrays, List<T>, Dictionary<K,V> and Resource classes.");
        }

        private static object ToResource(object parsed, Type target, IResourceReferenceResolver resolver)
        {
            if (parsed == null)
                return null;

            if (!(parsed is ResourceRef reference))
                throw Mismatch(parsed, target);

            Resource resource = resolver.Resolve(reference);
            if (resource != null && !target.IsInstanceOfType(resource))
                throw new UniResException($"Expected a {target.Name} but the referenced resource '{reference.Id}' is a {resource.GetType().Name}.");

            return resource;
        }

        private static object ToWholeNumber(object parsed, Type target)
        {
            if (!TryGetWholeNumber(parsed, out long value))
                throw Mismatch(parsed, target);

            try
            {
                return Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
            }
            catch (OverflowException)
            {
                throw new UniResException($"{value} is out of range for {target.Name}.");
            }
        }

        private static object ToEnum(object parsed, Type target)
        {
            if (parsed is string name)
            {
                try
                {
                    return Enum.Parse(target, name, false);
                }
                catch (ArgumentException)
                {
                    throw new UniResException($"'{name}' is not a value of {target.Name}.");
                }
            }

            if (!TryGetWholeNumber(parsed, out long number))
                throw Mismatch(parsed, target);

            return Enum.ToObject(target, number);
        }

        private static object ToArray(object parsed, Type target, IResourceReferenceResolver resolver)
        {
            Type elementType = target.GetElementType();
            IList items = AsSequence(parsed, target);

            Array array = Array.CreateInstance(elementType, items.Count);
            for (int i = 0; i < items.Count; i++)
                array.SetValue(ToType(items[i], elementType, resolver), i);

            return array;
        }

        private static object ToList(object parsed, Type target, IResourceReferenceResolver resolver)
        {
            Type elementType = target.GetGenericArguments()[0];
            IList items = AsSequence(parsed, target);

            var list = (IList)Activator.CreateInstance(target);
            foreach (object item in items)
                list.Add(ToType(item, elementType, resolver));

            return list;
        }

        private static object ToDictionary(object parsed, Type target, IResourceReferenceResolver resolver)
        {
            if (!(parsed is VariantDictionary source))
                throw Mismatch(parsed, target);

            Type[] arguments = target.GetGenericArguments();
            var dictionary = (IDictionary)Activator.CreateInstance(target);

            foreach (KeyValuePair<object, object> entry in source.Entries)
            {
                object key = ToType(entry.Key, arguments[0], resolver);
                if (key == null)
                    throw new UniResException("A dictionary key cannot be null.");

                if (dictionary.Contains(key))
                    throw new UniResException($"The dictionary key '{key}' appears more than once.");

                dictionary.Add(key, ToType(entry.Value, arguments[1], resolver));
            }

            return dictionary;
        }

        private static IList AsSequence(object parsed, Type target)
        {
            if (parsed is List<object> list)
                return list;

            if (parsed is Array array && array.Rank == 1)
                return array;

            throw Mismatch(parsed, target);
        }

        private static bool TryGetNumber(object parsed, out double value)
        {
            switch (parsed)
            {
                case double d: value = d; return true;
                case float f: value = f; return true;
                default:
                    if (TryGetWholeNumber(parsed, out long l))
                    {
                        value = l;
                        return true;
                    }

                    value = 0;
                    return false;
            }
        }

        private static bool TryGetWholeNumber(object parsed, out long value)
        {
            switch (parsed)
            {
                case long l: value = l; return true;
                case int i: value = i; return true;
                case byte b: value = b; return true;
                default: value = 0; return false;
            }
        }

        private static bool IsWholeNumber(Type type)
        {
            return type == typeof(int) || type == typeof(long) || type == typeof(short)
                || type == typeof(byte) || type == typeof(sbyte) || type == typeof(ushort)
                || type == typeof(uint);
        }

        private static bool IsSupportedStruct(Type type)
        {
            return type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4)
                || type == typeof(Quaternion) || type == typeof(Color)
                || type == typeof(Vector2Int) || type == typeof(Vector3Int);
        }

        private static UniResException Mismatch(object parsed, Type target)
        {
            return new UniResException($"Expected {DescribeType(target)} but found {DescribeValue(parsed)}.");
        }

        private static string DescribeType(Type type)
        {
            if (typeof(Resource).IsAssignableFrom(type))
                return $"a reference to a {type.Name}";
            if (type == typeof(bool))
                return "true or false";
            if (type == typeof(string))
                return "a string";
            if (type == typeof(float) || type == typeof(double))
                return "a number";
            if (IsWholeNumber(type) || type.IsEnum)
                return "a whole number";
            if (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)))
                return "a list";
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                return "a dictionary";
            return type.Name;
        }

        private static string DescribeValue(object parsed)
        {
            switch (parsed)
            {
                case null: return "null";
                case bool _: return "a boolean";
                case long _: return "a whole number";
                case double _: return "a decimal number";
                case string _: return "a string";
                case List<object> _: return "a list";
                case VariantDictionary _: return "a dictionary";
                case ResourceRef _: return "a resource reference";
                default: return parsed.GetType().Name;
            }
        }
    }
}
