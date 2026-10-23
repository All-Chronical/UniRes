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

        /// <summary>Makes paths comparable: forward slashes only, empty for null.</summary>
        internal static string Normalize(string path)
        {
            return string.IsNullOrEmpty(path) ? "" : path.Replace('\\', '/');
        }
    }
}
