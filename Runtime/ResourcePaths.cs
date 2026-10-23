using System;
using System.IO;
using UnityEngine;

namespace UniRes
{
    /// <summary>
    /// Where resource paths point. Two kinds of path are understood:
    /// <list type="bullet">
    /// <item><description><c>user://folder/file.chtk</c> - inside <see cref="UserDataRoot"/>, the player's
    /// writable data folder. This is the portable choice and works in built players.</description></item>
    /// <item><description>Any ordinary file system path, absolute or relative to the working directory.</description></item>
    /// </list>
    /// </summary>
    public static class ResourcePaths
    {
        public const string UserScheme = "user://";

        private static string userDataRoot;

        /// <summary>
        /// Folder that <c>user://</c> paths resolve to. Defaults to
        /// <see cref="Application.persistentDataPath"/>, read the first time it is needed (do that
        /// on the main thread, or assign it yourself).
        /// </summary>
        public static string UserDataRoot
        {
            get
            {
                if (userDataRoot == null)
                    userDataRoot = Application.persistentDataPath;
                return userDataRoot;
            }
            set => userDataRoot = value;
        }

        /// <summary>Converts a resource path to a path the operating system understands.</summary>
        /// <exception cref="UniResException">The path is empty or uses an unsupported scheme.</exception>
        public static string ToFileSystemPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new UniResException("The resource path is empty.");

            if (path.StartsWith(UserScheme, StringComparison.Ordinal))
            {
                string relative = path.Substring(UserScheme.Length).TrimStart('/', '\\');
                return Path.Combine(UserDataRoot, relative);
            }

            if (path.Contains("://"))
            {
                string scheme = path.Substring(0, path.IndexOf("://", StringComparison.Ordinal) + 3);
                throw new UniResException($"Unsupported path scheme '{scheme}' in '{path}'. Use '{UserScheme}' or a file system path.");
            }

            return path;
        }

        /// <summary>
        /// Resolves an external resource path relative to the resource file that contains it,
        /// unless it is already a user-scheme, URI-scheme, or absolute file-system path.
        /// </summary>
        internal static string ResolveReference(string path, string containingResourcePath)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            string normalized = Normalize(path);
            if (normalized.StartsWith(UserScheme, StringComparison.Ordinal)
                || normalized.Contains("://")
                || Path.IsPathRooted(path))
            {
                return normalized;
            }

            if (string.IsNullOrEmpty(containingResourcePath))
                return normalized;

            string container = Normalize(containingResourcePath);
            if (container.StartsWith(UserScheme, StringComparison.Ordinal))
            {
                int lastSlash = container.LastIndexOf('/');
                string directory = lastSlash >= UserScheme.Length
                    ? container.Substring(UserScheme.Length, lastSlash - UserScheme.Length)
                    : "";
                return Normalize(UserScheme + (directory.Length == 0 ? "" : directory + "/") + normalized);
            }

            string absoluteContainer = Path.GetFullPath(ToFileSystemPath(container));
            string absoluteDirectory = Path.GetDirectoryName(absoluteContainer);
            return Normalize(Path.GetFullPath(Path.Combine(absoluteDirectory, path)));
        }

        /// <summary>Returns a path relative to the containing resource when both use the same root.</summary>
        internal static string MakeReferencePath(string resourcePath, string containingPath, bool relative)
        {
            string resource = Normalize(resourcePath);
            if (!relative || string.IsNullOrEmpty(containingPath))
                return resource;

            string container = Normalize(containingPath);
            bool resourceIsUserPath = resource.StartsWith(UserScheme, StringComparison.Ordinal);
            bool containerIsUserPath = container.StartsWith(UserScheme, StringComparison.Ordinal);

            if (resourceIsUserPath && containerIsUserPath)
            {
                string[] source = resource.Substring(UserScheme.Length).Split('/');
                string[] target = container.Substring(UserScheme.Length).Split('/');
                int targetCount = Math.Max(0, target.Length - 1);
                int common = 0;
                while (common < source.Length && common < targetCount
                    && string.Equals(source[common], target[common], StringComparison.Ordinal))
                {
                    common++;
                }

                var segments = new System.Collections.Generic.List<string>();
                for (int i = common; i < targetCount; i++)
                    segments.Add("..");
                for (int i = common; i < source.Length; i++)
                    segments.Add(source[i]);

                return string.Join("/", segments);
            }

            if (resourceIsUserPath != containerIsUserPath)
                return resource;

            string absoluteResource = Path.GetFullPath(ToFileSystemPath(resource));
            string absoluteContainer = Path.GetFullPath(ToFileSystemPath(container));
            string directory = Path.GetDirectoryName(absoluteContainer);
            return Normalize(Path.GetRelativePath(directory, absoluteResource));
        }

        /// <summary>Makes paths comparable: forward slashes and normalized user-scheme segments.</summary>
        internal static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "";

            string normalized = path.Replace('\\', '/');
            if (!normalized.StartsWith(UserScheme, StringComparison.Ordinal))
                return normalized;

            string[] parts = normalized.Substring(UserScheme.Length).Split('/');
            var segments = new System.Collections.Generic.List<string>();
            foreach (string part in parts)
            {
                if (part.Length == 0 || part == ".")
                    continue;

                if (part == "..")
                {
                    if (segments.Count > 0 && segments[segments.Count - 1] != "..")
                        segments.RemoveAt(segments.Count - 1);
                    else
                        segments.Add(part);
                }
                else
                {
                    segments.Add(part);
                }
            }

            return UserScheme + string.Join("/", segments);
        }
    }
}
