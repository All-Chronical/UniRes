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

        /// <summary>
        /// Copies exported data from another resource of the same class, without changing this
        /// resource's path. Used by <see cref="ResourceLoader"/> when replacing a cached resource.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
        /// <exception cref="UniResException">The resources have different classes.</exception>
        public void CopyFrom(Resource source)
        {
            if (source == null)
                throw new System.ArgumentNullException(nameof(source));

            if (source.GetType() != GetType())
                throw new UniResException($"Cannot copy {source.GetType().Name} into {GetType().Name}; their classes must match.");

            foreach (ExportedMember member in ExportedMember.For(GetType()))
                member.SetValue(this, member.GetValue(source));
        }

        /// <summary>
        /// Gives this resource a file path without saving it. Resources saved inside other
        /// resources are then written as references to that path instead of being embedded, and
        /// <see cref="ResourceLoader.Load{T}"/> on the path returns this instance.
        /// </summary>
        /// <exception cref="UniResException">The path is empty or unsupported.</exception>
        public void TakeOverPath(string path)
        {
            // Validates the path (empty, unsupported scheme) before anything changes.
            ResourcePaths.ToFileSystemPath(path);

            string normalized = ResourcePaths.Normalize(path);
            if (!IsBuiltIn)
                ResourceCache.Remove(ResourcePath, this);

            ResourcePath = normalized;
            ResourceCache.TakeOver(normalized, this);
        }
    }
}
