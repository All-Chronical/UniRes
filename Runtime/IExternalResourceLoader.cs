using System;

namespace UniRes
{
    /// <summary>
    /// Loads the resources that a file refers to with <c>[ext_resource]</c>. The file reader
    /// calls it while reading, so referenced files are fully loaded before the file that uses them.
    /// </summary>
    internal interface IExternalResourceLoader
    {
        /// <summary>Loads the resource stored at <paramref name="path"/>.</summary>
        /// <param name="path">Path exactly as written in the file.</param>
        /// <param name="expectedType">Resource class the file says it is.</param>
        Resource Load(string path, Type expectedType);
    }
}
