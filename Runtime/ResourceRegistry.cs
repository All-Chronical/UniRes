using System;
using System.Collections.Generic;
using System.Reflection;

namespace UniRes
{
    /// <summary>
    /// Maps the type names written in files to <see cref="Resource"/> classes.
    /// Classes are found automatically in any loaded assembly that references
    /// UniRes.Runtime, so registering is only needed for classes outside such assemblies.
    /// </summary>
    public static class ResourceRegistry
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, Type> TypesByName = new Dictionary<string, Type>(StringComparer.Ordinal);
        private static readonly Dictionary<Type, string> NamesByType = new Dictionary<Type, string>();
        private static readonly HashSet<string> AmbiguousNames = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<Assembly> ScannedAssemblies = new HashSet<Assembly>();

        /// <summary>Registers a resource class explicitly.</summary>
        public static void Register<T>() where T : Resource
        {
            Register(typeof(T));
        }

        /// <summary>Registers a resource class explicitly.</summary>
        /// <exception cref="UniResException">The type is not a concrete resource class, or its name is taken.</exception>
        public static void Register(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (!IsConcreteResource(type))
                throw new UniResException($"{type.FullName} cannot be registered: it must be a non-abstract, non-generic class deriving from {nameof(Resource)}.");

            lock (Gate)
            {
                Add(type, throwOnConflict: true);
            }
        }

        /// <summary>Registers every concrete resource class in an assembly.</summary>
        public static void RegisterAssembly(Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            lock (Gate)
            {
                ScannedAssemblies.Remove(assembly);
                Scan(assembly);
            }
        }

        /// <summary>Returns the name used for a resource class in files.</summary>
        public static string GetTypeName(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            lock (Gate)
            {
                if (!NamesByType.TryGetValue(type, out string name))
                {
                    if (!IsConcreteResource(type))
                        throw new UniResException($"{type.FullName} is not a concrete {nameof(Resource)} class.");

                    Add(type, throwOnConflict: true);
                    name = NamesByType[type];
                }

                if (AmbiguousNames.Contains(name))
                    throw AmbiguousError(name);

                return name;
            }
        }

        /// <summary>Finds the resource class for a type name from a file.</summary>
        /// <exception cref="UniResException">No class has that name, or several do.</exception>
        public static Type FindType(string typeName)
        {
            if (typeName == null)
                throw new ArgumentNullException(nameof(typeName));

            lock (Gate)
            {
                if (!TypesByName.ContainsKey(typeName) && !AmbiguousNames.Contains(typeName))
                    ScanLoadedAssemblies();

                if (AmbiguousNames.Contains(typeName))
                    throw AmbiguousError(typeName);

                if (TypesByName.TryGetValue(typeName, out Type type))
                    return type;
            }

            throw new UniResException($"Unknown resource type '{typeName}'. Its class must live in an assembly that references UniRes.Runtime, or be registered with {nameof(ResourceRegistry)}.{nameof(Register)}.");
        }

        /// <summary>Creates an empty instance of a resource class.</summary>
        public static Resource Create(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (!IsConcreteResource(type))
                throw new UniResException($"{type.FullName} is not a concrete {nameof(Resource)} class.");

            try
            {
                return (Resource)Activator.CreateInstance(type, true);
            }
            catch (MissingMethodException ex)
            {
                throw new UniResException($"{type.FullName} needs a parameterless constructor to be loaded.", ex);
            }
        }

        /// <summary>Creates an empty instance of the resource class with the given file type name.</summary>
        public static Resource Create(string typeName)
        {
            return Create(FindType(typeName));
        }

        private static bool IsConcreteResource(Type type)
        {
            return type.IsClass
                && !type.IsAbstract
                && !type.IsGenericTypeDefinition
                && typeof(Resource).IsAssignableFrom(type);
        }

        private static void Add(Type type, bool throwOnConflict)
        {
            if (NamesByType.ContainsKey(type))
                return;

            string name = type.GetCustomAttribute<ResourceTypeAttribute>(false)?.Name;
            if (string.IsNullOrEmpty(name))
                name = type.Name;

            if (TypesByName.TryGetValue(name, out Type existing))
            {
                if (throwOnConflict)
                    throw new UniResException($"Resource type name '{name}' is used by both {existing.FullName} and {type.FullName}. Give one of them a unique [{nameof(ResourceTypeAttribute)}].");

                AmbiguousNames.Add(name);
                return;
            }

            TypesByName[name] = type;
            NamesByType[type] = name;
        }

        private static void ScanLoadedAssemblies()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                Scan(assembly);
        }

        private static void Scan(Assembly assembly)
        {
            if (!ScannedAssemblies.Add(assembly))
                return;

            if (assembly.IsDynamic || !ReferencesUniRes(assembly))
                return;

            foreach (Type type in GetTypesSafely(assembly))
            {
                if (type != null && IsConcreteResource(type))
                    Add(type, throwOnConflict: false);
            }
        }

        private static bool ReferencesUniRes(Assembly assembly)
        {
            Assembly own = typeof(Resource).Assembly;
            if (assembly == own)
                return true;

            string ownName = own.GetName().Name;
            foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
            {
                if (reference.Name == ownName)
                    return true;
            }

            return false;
        }

        private static Type[] GetTypesSafely(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types;
            }
        }

        private static UniResException AmbiguousError(string name)
        {
            return new UniResException($"More than one resource class is named '{name}'. Give each a unique [{nameof(ResourceTypeAttribute)}] name.");
        }
    }
}
