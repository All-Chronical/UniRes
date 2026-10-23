using System;

namespace UniRes
{
    /// <summary>
    /// Sets the type name written to files for a <see cref="Resource"/> class.
    /// Without it the C# class name is used. Giving a stable name lets you rename or
    /// move the class later without breaking files that were already saved.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ResourceTypeAttribute : Attribute
    {
        public ResourceTypeAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
