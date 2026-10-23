using System.Collections.Generic;

namespace UniRes
{
    internal enum ResourceRefKind
    {
        /// <summary>A resource stored in its own file: <c>ExtResource("id")</c>.</summary>
        External,

        /// <summary>A resource embedded in the same file: <c>SubResource("id")</c>.</summary>
        Embedded,
    }

    /// <summary>A parsed <c>ExtResource("id")</c> or <c>SubResource("id")</c> value, not yet resolved.</summary>
    internal sealed class ResourceRef
    {
        public ResourceRef(ResourceRefKind kind, string id)
        {
            Kind = kind;
            Id = id;
        }

        public ResourceRefKind Kind { get; }

        public string Id { get; }
    }

    /// <summary>A parsed <c>{ key: value }</c> value. Entries keep file order.</summary>
    internal sealed class VariantDictionary
    {
        public List<KeyValuePair<object, object>> Entries { get; } = new List<KeyValuePair<object, object>>();
    }

    /// <summary>Turns a resource into the text of a reference to it while a file is being written.</summary>
    internal interface IResourceReferenceWriter
    {
        /// <summary>Returns <c>ExtResource("..")</c> or <c>SubResource("..")</c> for a resource.</summary>
        string GetReference(Resource resource);
    }

    /// <summary>Finds the resource a parsed reference points at while a file is being read.</summary>
    internal interface IResourceReferenceResolver
    {
        Resource Resolve(ResourceRef reference);
    }
}
