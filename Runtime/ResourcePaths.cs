using System;
using System.Collections.Generic;

namespace UniRes
{
    /// <summary>
    /// Path helpers shared by the file format and the loader/saver.
    /// </summary>
    internal static class ResourcePaths
    {
        /// <summary>Makes paths comparable: forward slashes only, empty for null.</summary>
        public static string Normalize(string path)
        {
            return string.IsNullOrEmpty(path) ? "" : path.Replace('\\', '/');
        }
    }
}
