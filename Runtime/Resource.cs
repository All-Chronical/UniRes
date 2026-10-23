namespace UniRes
{
    /// <summary>
    /// Base class for data containers that can be saved to and loaded from a UniRes file.
    /// Derive from it and mark the data to keep with <see cref="ExportAttribute"/>.
    /// A resource may hold other resources, which are saved nested inside it or as
    /// references to their own files.
    /// </summary>
    /// <remarks>
    /// Derived classes need a parameterless constructor (it may be non-public).
    /// </remarks>
    public abstract class Resource
    {
        /// <summary>Optional human-readable name.</summary>
        [Export]
        public string ResourceName { get; set; } = "";

        /// <summary>
        /// Path of the file this resource was loaded from or saved as. Empty for a
        /// resource that only exists in memory, or that is embedded in another resource.
        /// </summary>
        public string ResourcePath { get; internal set; } = "";

        /// <summary>True when this resource has no file of its own.</summary>
        public bool IsBuiltIn => string.IsNullOrEmpty(ResourcePath);
    }
}
