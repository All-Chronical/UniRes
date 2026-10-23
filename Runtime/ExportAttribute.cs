using System;

namespace UniRes
{
    /// <summary>
    /// Marks a field or property of a <see cref="Resource"/> to be saved and loaded.
    /// Unmarked members are ignored. The member name is used as the property name in
    /// the file unless <paramref name="name"/> is given.
    /// </summary>
    /// <remarks>
    /// Properties need both a getter and a setter (either may be non-public).
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class ExportAttribute : Attribute
    {
        public ExportAttribute()
        {
        }

        public ExportAttribute(string name)
        {
            Name = name;
        }

        /// <summary>Property name used in the file, or null to use the member name.</summary>
        public string Name { get; }
    }
}
