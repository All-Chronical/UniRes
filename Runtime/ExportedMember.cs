using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace UniRes
{
    /// <summary>
    /// One field or property of a resource class that is marked with
    /// <see cref="ExportAttribute"/>, with uniform get/set access.
    /// </summary>
    internal sealed class ExportedMember
    {
        private const BindingFlags DeclaredInstance =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly Dictionary<Type, ExportedMember[]> Cache = new Dictionary<Type, ExportedMember[]>();

        private readonly FieldInfo field;
        private readonly PropertyInfo property;

        private ExportedMember(string name, FieldInfo field)
        {
            Name = name;
            this.field = field;
            ValueType = field.FieldType;
        }

        private ExportedMember(string name, PropertyInfo property)
        {
            Name = name;
            this.property = property;
            ValueType = property.PropertyType;
        }

        /// <summary>Property name used in the file.</summary>
        public string Name { get; }

        /// <summary>Declared C# type of the member.</summary>
        public Type ValueType { get; }

        public object GetValue(object target)
        {
            return field != null ? field.GetValue(target) : property.GetValue(target);
        }

        public void SetValue(object target, object value)
        {
            if (field != null)
                field.SetValue(target, value);
            else
                property.SetValue(target, value);
        }

        /// <summary>
        /// Exported members of a resource class: base class members first, and within each
        /// class fields before properties, each in declaration order.
        /// </summary>
        public static ExportedMember[] For(Type resourceType)
        {
            lock (Cache)
            {
                if (!Cache.TryGetValue(resourceType, out ExportedMember[] members))
                {
                    members = Build(resourceType);
                    Cache[resourceType] = members;
                }

                return members;
            }
        }

        private static ExportedMember[] Build(Type resourceType)
        {
            var chain = new List<Type>();
            for (Type t = resourceType; t != null && t != typeof(object); t = t.BaseType)
                chain.Insert(0, t);

            var result = new List<ExportedMember>();
            var names = new HashSet<string>(StringComparer.Ordinal);

            foreach (Type type in chain)
            {
                IEnumerable<MemberInfo> candidates = type.GetMembers(DeclaredInstance)
                    .Where(m => m is FieldInfo || m is PropertyInfo)
                    .OrderBy(m => m.MetadataToken);

                foreach (MemberInfo member in candidates)
                {
                    ExportAttribute export = member.GetCustomAttribute<ExportAttribute>(false);
                    if (export == null)
                        continue;

                    string name = string.IsNullOrEmpty(export.Name) ? member.Name : export.Name;
                    if (!IsValidName(name))
                        throw new UniResException($"{type.FullName}.{member.Name}: '{name}' is not a valid export name. Use letters, digits and underscores, not starting with a digit.");

                    if (!names.Add(name))
                        throw new UniResException($"{resourceType.FullName} exports the name '{name}' more than once.");

                    if (member is FieldInfo fieldInfo)
                    {
                        result.Add(new ExportedMember(name, fieldInfo));
                    }
                    else
                    {
                        var propertyInfo = (PropertyInfo)member;
                        if (propertyInfo.GetIndexParameters().Length != 0
                            || propertyInfo.GetGetMethod(true) == null
                            || propertyInfo.GetSetMethod(true) == null)
                        {
                            throw new UniResException($"{type.FullName}.{member.Name} is marked [{nameof(ExportAttribute)}] but is not a readable and writable property.");
                        }

                        result.Add(new ExportedMember(name, propertyInfo));
                    }
                }
            }

            return result.ToArray();
        }

        private static bool IsValidName(string name)
        {
            if (string.IsNullOrEmpty(name) || char.IsDigit(name[0]))
                return false;

            foreach (char c in name)
            {
                if (!(char.IsLetterOrDigit(c) || c == '_'))
                    return false;
            }

            return true;
        }
    }
}
