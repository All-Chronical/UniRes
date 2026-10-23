using System;
using System.IO;
using System.Text;

namespace UniRes
{
    /// <summary>Options for <see cref="ResourceSaver.Save"/>.</summary>
    [Flags]
    public enum SaveFlags
    {
        None = 0,

        /// <summary>
        /// After saving, the resource takes the saved path as its own (see <see cref="Resource.TakeOverPath"/>),
        /// so it will be referenced by that path when saved inside other resources.
        /// </summary>
        ChangePath = 1,

        /// <summary>Embed resources with their own paths instead of writing external references.</summary>
        BundleResources = 2,

        /// <summary>Write external paths relative to the resource being saved when possible.</summary>
        RelativePaths = 4,
    }

    /// <summary>
    /// Saves resources to UniRes files, text or streams at runtime, in the Editor or in a built
    /// player. Mirrors Godot's <c>ResourceSaver</c>.
    /// </summary>
    /// <remarks>
    /// Saving writes the resource and everything embedded in it. Resources that already have their
    /// own path are only referenced, not saved again: save those yourself first. The text is built
    /// completely before any file is touched, so a resource that cannot be saved never damages an
    /// existing file.
    /// </remarks>
    public static class ResourceSaver
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>Writes <paramref name="resource"/> to a file, creating folders as needed.</summary>
        /// <exception cref="UniResException">The resource contains something that cannot be saved, or a loop.</exception>
        public static void Save(Resource resource, string path, SaveFlags flags = SaveFlags.None)
        {
            if (resource == null)
                throw new ArgumentNullException(nameof(resource));

            string fileSystemPath = ResourcePaths.ToFileSystemPath(path);
            string text = ResourceFileWriter.Write(
                resource,
                path,
                (flags & SaveFlags.BundleResources) != 0,
                (flags & SaveFlags.RelativePaths) != 0);

            string directory = Path.GetDirectoryName(Path.GetFullPath(fileSystemPath));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(fileSystemPath, text, Utf8);

            if ((flags & SaveFlags.ChangePath) != 0)
                resource.TakeOverPath(path);
        }

        /// <summary>Returns the UniRes text for a resource, for example to send it somewhere.</summary>
        public static string SaveToString(Resource resource, SaveFlags flags = SaveFlags.None)
        {
            if (resource == null)
                throw new ArgumentNullException(nameof(resource));

            return ResourceFileWriter.Write(
                resource,
                "",
                (flags & SaveFlags.BundleResources) != 0,
                (flags & SaveFlags.RelativePaths) != 0);
        }

        /// <summary>Writes a resource as UTF-8 UniRes text to a stream. The stream is left open.</summary>
        public static void SaveToStream(Resource resource, Stream stream, SaveFlags flags = SaveFlags.None)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            byte[] bytes = Utf8.GetBytes(SaveToString(resource, flags));
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}
