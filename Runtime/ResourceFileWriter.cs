using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace UniRes
{
    /// <summary>
    /// Turns a resource and everything nested in it into UniRes file text.
    /// Resources without a file path are embedded as <c>[sub_resource]</c> sections (each written
    /// once, however often it is referenced); resources that have their own path are written as
    /// <c>[ext_resource]</c> references.
    /// </summary>
    internal sealed class ResourceFileWriter : IResourceReferenceWriter
    {
        public const int FormatVersion = 1;

        private readonly Resource root;
        private readonly string savePath;

        private readonly Dictionary<Resource, string> subIds = new Dictionary<Resource, string>(new ReferenceComparer());
        private readonly HashSet<Resource> inProgress = new HashSet<Resource>(new ReferenceComparer());
        private readonly List<string> subSections = new List<string>();
        private readonly Dictionary<string, int> subCounters = new Dictionary<string, int>(StringComparer.Ordinal);

        private readonly Dictionary<string, string> extIdsByPath = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<string> extLines = new List<string>();

        private ResourceFileWriter(Resource root, string savePath)
        {
            this.root = root;
            this.savePath = ResourcePaths.Normalize(savePath);
        }

        /// <param name="root">Resource to write.</param>
        /// <param name="savePath">
        /// Path the text will be saved to, or empty when it is not going to a file. A nested resource
        /// that already has this path is embedded rather than referenced.
        /// </param>
        public static string Write(Resource root, string savePath)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            return new ResourceFileWriter(root, savePath).Build();
        }

        private string Build()
        {
            string rootTypeName = ResourceRegistry.GetTypeName(root.GetType());

            inProgress.Add(root);
            string rootProperties = WriteProperties(root);
            inProgress.Remove(root);

            var sb = new StringBuilder();
            sb.Append("[unires format=").Append(FormatVersion).Append(" type=");
            VariantWriter.AppendQuoted(sb, rootTypeName);
            sb.Append("]\n\n");

            if (extLines.Count > 0)
            {
                foreach (string line in extLines)
                    sb.Append(line).Append('\n');
                sb.Append('\n');
            }

            foreach (string section in subSections)
                sb.Append(section).Append('\n');

            sb.Append("[resource]\n");
            sb.Append(rootProperties);
            return sb.ToString();
        }

        string IResourceReferenceWriter.GetReference(Resource resource)
        {
            if (inProgress.Contains(resource))
            {
                throw new UniResException($"A {resource.GetType().Name} contains itself (directly or through other resources). Resources must not form a loop.");
            }

            bool hasOwnFile = !resource.IsBuiltIn
                && !string.Equals(ResourcePaths.Normalize(resource.ResourcePath), savePath, StringComparison.Ordinal);

            return hasOwnFile ? ReferenceExternal(resource) : ReferenceEmbedded(resource);
        }

        private string ReferenceExternal(Resource resource)
        {
            string path = resource.ResourcePath;

            if (!extIdsByPath.TryGetValue(path, out string id))
            {
                id = (extIdsByPath.Count + 1).ToString();
                extIdsByPath[path] = id;

                var sb = new StringBuilder("[ext_resource type=");
                VariantWriter.AppendQuoted(sb, ResourceRegistry.GetTypeName(resource.GetType()));
                sb.Append(" path=");
                VariantWriter.AppendQuoted(sb, path);
                sb.Append(" id=");
                VariantWriter.AppendQuoted(sb, id);
                sb.Append(']');
                extLines.Add(sb.ToString());
            }

            return $"ExtResource(\"{id}\")";
        }

        private string ReferenceEmbedded(Resource resource)
        {
            if (!subIds.TryGetValue(resource, out string id))
            {
                string typeName = ResourceRegistry.GetTypeName(resource.GetType());

                // Properties are written first so that anything nested inside gets its section (and
                // number) before this one, which keeps ids in file order and every reference backward.
                inProgress.Add(resource);
                string properties = WriteProperties(resource);
                inProgress.Remove(resource);

                subCounters.TryGetValue(typeName, out int count);
                subCounters[typeName] = ++count;
                id = typeName + "_" + count;
                subIds[resource] = id;

                var sb = new StringBuilder("[sub_resource type=");
                VariantWriter.AppendQuoted(sb, typeName);
                sb.Append(" id=");
                VariantWriter.AppendQuoted(sb, id);
                sb.Append("]\n").Append(properties);
                subSections.Add(sb.ToString());
            }

            return $"SubResource(\"{id}\")";
        }

        private string WriteProperties(Resource resource)
        {
            var sb = new StringBuilder();

            foreach (ExportedMember member in ExportedMember.For(resource.GetType()))
            {
                sb.Append(member.Name).Append(" = ");

                try
                {
                    VariantWriter.Write(sb, member.GetValue(resource), member.ValueType, this);
                }
                catch (UniResException ex)
                {
                    throw new UniResException($"{resource.GetType().Name}.{member.Name}: {ex.Message}", ex);
                }

                sb.Append('\n');
            }

            return sb.ToString();
        }

        private sealed class ReferenceComparer : IEqualityComparer<Resource>
        {
            public bool Equals(Resource x, Resource y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(Resource obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
