using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UniRes
{
    /// <summary>How <see cref="ResourceLoader"/> treats resources it has already loaded.</summary>
    public enum CacheMode
    {
        /// <summary>Return the already-loaded instance if there is one (default).</summary>
        Reuse,

        /// <summary>Read the file again and return a new instance, leaving the cache alone.</summary>
        Ignore,

        /// <summary>Read the file again, and make the new instance the cached one.</summary>
        Replace,
    }

    /// <summary>
    /// Loads resources from UniRes files, text or streams at runtime, in the Editor or in a
    /// built player. Mirrors Godot's <c>ResourceLoader</c>: loading the same path twice returns
    /// the same instance, and nested files are loaded automatically.
    /// </summary>
    /// <remarks>
    /// Format problems throw <see cref="ResourceFormatException"/> (a <see cref="UniResException"/>);
    /// file problems throw the usual <see cref="IOException"/> family.
    /// </remarks>
    public static class ResourceLoader
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        [ThreadStatic]
        private static List<string> loadingStack;

        /// <summary>Loads the resource saved at <paramref name="path"/>.</summary>
        /// <exception cref="UniResException">The content is invalid, or is not a <typeparamref name="T"/>.</exception>
        /// <exception cref="FileNotFoundException">There is no file at the path.</exception>
        public static T Load<T>(string path, CacheMode cacheMode = CacheMode.Reuse) where T : Resource
        {
            return CastTo<T>(Load(path, cacheMode), path);
        }

        /// <summary>Loads the resource saved at <paramref name="path"/>, whatever its class.</summary>
        public static Resource Load(string path, CacheMode cacheMode = CacheMode.Reuse)
        {
            string fileSystemPath = ResourcePaths.ToFileSystemPath(path);
            string key = ResourcePaths.Normalize(path);

            if (cacheMode == CacheMode.Reuse && ResourceCache.TryGet(key, out Resource cached))
                return cached;

            List<string> stack = loadingStack ?? (loadingStack = new List<string>());
            if (stack.Contains(key))
                throw new UniResException($"'{key}' refers to itself through its [ext_resource] sections: {string.Join(" -> ", stack)} -> {key}.");

            string text = File.ReadAllText(fileSystemPath, Utf8);

            stack.Add(key);
            Resource loaded;
            try
            {
                loaded = ResourceFileReader.Read(text, FileLoader.Instance);
            }
            catch (ResourceFormatException ex) when (string.IsNullOrEmpty(ex.Path))
            {
                ex.Path = key;
                throw;
            }
            finally
            {
                stack.RemoveAt(stack.Count - 1);
            }

            loaded.ResourcePath = key;
            if (cacheMode != CacheMode.Ignore)
                ResourceCache.Set(key, loaded);

            return loaded;
        }

        /// <summary>Loads a resource from UniRes text, for example text received from elsewhere.</summary>
        /// <remarks>
        /// The result has no path of its own. Any <c>[ext_resource]</c> in the text is still loaded from its path.
        /// </remarks>
        public static T LoadFromString<T>(string text) where T : Resource
        {
            return CastTo<T>(LoadFromString(text), "the text");
        }

        /// <inheritdoc cref="LoadFromString{T}"/>
        public static Resource LoadFromString(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            return ResourceFileReader.Read(text, FileLoader.Instance);
        }

        /// <summary>Loads a resource from a stream containing UniRes text (UTF-8). The stream is left open.</summary>
        public static T LoadFromStream<T>(Stream stream) where T : Resource
        {
            return CastTo<T>(LoadFromStream(stream), "the stream");
        }

        /// <inheritdoc cref="LoadFromStream{T}"/>
        public static Resource LoadFromStream(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            using (var reader = new StreamReader(stream, Utf8, true, 4096, true))
                return LoadFromString(reader.ReadToEnd());
        }

        /// <summary>True when a file exists at the path.</summary>
        public static bool Exists(string path)
        {
            return File.Exists(ResourcePaths.ToFileSystemPath(path));
        }

        /// <summary>
        /// Forgets every loaded resource, so the next load reads files again. Resources you already
        /// hold stay valid.
        /// </summary>
        public static void ClearCache()
        {
            ResourceCache.Clear();
        }

        private static T CastTo<T>(Resource resource, string source) where T : Resource
        {
            if (resource is T typed)
                return typed;

            throw new UniResException($"Expected a {typeof(T).Name} but {source} contains a {resource.GetType().Name}.");
        }

        /// <summary>Loads <c>[ext_resource]</c> files for the reader; they always use the cache.</summary>
        private sealed class FileLoader : IExternalResourceLoader
        {
            public static readonly FileLoader Instance = new FileLoader();

            public Resource Load(string path, Type expectedType)
            {
                return ResourceLoader.Load(path);
            }
        }
    }
}
