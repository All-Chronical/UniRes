using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace UniRes
{
    /// <summary>
    /// Writes a resource and everything nested in it to a compact binary UniRes byte array.
    /// </summary>
    internal sealed class ResourceBinaryWriter : IBinaryResourceReferenceWriter
    {
        public const byte FormatVersion = 1;
        public static readonly byte[] Magic = new byte[] { (byte)'U', (byte)'N', (byte)'R', (byte)'E', (byte)'S' };

        private readonly Resource root;
        private readonly string savePath;
        private readonly bool bundleResources;
        private readonly bool relativePaths;

        private readonly Dictionary<Resource, string> subIds = new Dictionary<Resource, string>(new ReferenceComparer());
        private readonly HashSet<Resource> inProgress = new HashSet<Resource>(new ReferenceComparer());
        private readonly List<SubResourceEntry> subResources = new List<SubResourceEntry>();
        private readonly Dictionary<string, int> subCounters = new Dictionary<string, int>(StringComparer.Ordinal);

        private readonly Dictionary<string, string> extIdsByPath = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<ExtResourceEntry> extResources = new List<ExtResourceEntry>();

        private struct ExtResourceEntry
        {
            public string Id;
            public string TypeName;
            public string Path;
        }

        private struct SubResourceEntry
        {
            public string Id;
            public string TypeName;
            public byte[] PropertiesPayload;
        }

        private ResourceBinaryWriter(Resource root, string savePath, bool bundleResources, bool relativePaths)
        {
            this.root = root;
            this.savePath = ResourcePaths.Normalize(savePath);
            this.bundleResources = bundleResources;
            this.relativePaths = relativePaths;
        }

        public static byte[] Write(Resource root, string savePath, bool bundleResources = false, bool relativePaths = false)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            return new ResourceBinaryWriter(root, savePath, bundleResources, relativePaths).Build();
        }

        private byte[] Build()
        {
            string rootTypeName = ResourceRegistry.GetTypeName(root.GetType());

            inProgress.Add(root);
            byte[] rootProperties = WriteProperties(root);
            inProgress.Remove(root);

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms, Encoding.UTF8))
            {
                // 1. Magic
                writer.Write(Magic);

                // 2. Version
                writer.Write(FormatVersion);

                // 3. Flags (ushort, 0 for now)
                writer.Write((ushort)0);

                // 4. Root type name
                writer.Write(rootTypeName);

                // 5. External resources
                writer.Write(extResources.Count);
                foreach (ExtResourceEntry ext in extResources)
                {
                    writer.Write(ext.Id);
                    writer.Write(ext.TypeName);
                    writer.Write(ext.Path);
                }

                // 6. Sub resources
                writer.Write(subResources.Count);
                foreach (SubResourceEntry sub in subResources)
                {
                    writer.Write(sub.Id);
                    writer.Write(sub.TypeName);
                    writer.Write(sub.PropertiesPayload.Length);
                    writer.Write(sub.PropertiesPayload);
                }

                // 7. Root properties
                writer.Write(rootProperties.Length);
                writer.Write(rootProperties);

                writer.Flush();
                return ms.ToArray();
            }
        }

        (ResourceRefKind Kind, string Id) IBinaryResourceReferenceWriter.GetReference(Resource resource)
        {
            if (inProgress.Contains(resource))
            {
                throw new UniResException($"A {resource.GetType().Name} contains itself (directly or through other resources). Resources must not form a loop.");
            }

            bool hasOwnFile = !bundleResources
                && !resource.IsBuiltIn
                && !string.Equals(ResourcePaths.Normalize(resource.ResourcePath), savePath, StringComparison.Ordinal);

            return hasOwnFile ? ReferenceExternal(resource) : ReferenceEmbedded(resource);
        }

        private (ResourceRefKind Kind, string Id) ReferenceExternal(Resource resource)
        {
            string resourcePath = ResourcePaths.Normalize(resource.ResourcePath);

            if (!extIdsByPath.TryGetValue(resourcePath, out string id))
            {
                id = (extIdsByPath.Count + 1).ToString();
                extIdsByPath[resourcePath] = id;

                string path = ResourcePaths.MakeReferencePath(resourcePath, savePath, relativePaths);
                extResources.Add(new ExtResourceEntry
                {
                    Id = id,
                    TypeName = ResourceRegistry.GetTypeName(resource.GetType()),
                    Path = path,
                });
            }

            return (ResourceRefKind.External, id);
        }

        private (ResourceRefKind Kind, string Id) ReferenceEmbedded(Resource resource)
        {
            if (!subIds.TryGetValue(resource, out string id))
            {
                string typeName = ResourceRegistry.GetTypeName(resource.GetType());

                inProgress.Add(resource);
                byte[] properties = WriteProperties(resource);
                inProgress.Remove(resource);

                subCounters.TryGetValue(typeName, out int count);
                subCounters[typeName] = ++count;
                id = typeName + "_" + count;
                subIds[resource] = id;

                subResources.Add(new SubResourceEntry
                {
                    Id = id,
                    TypeName = typeName,
                    PropertiesPayload = properties,
                });
            }

            return (ResourceRefKind.Embedded, id);
        }

        private byte[] WriteProperties(Resource resource)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms, Encoding.UTF8))
            {
                ExportedMember[] members = ExportedMember.For(resource.GetType());
                writer.Write(members.Length);

                foreach (ExportedMember member in members)
                {
                    writer.Write(member.Name);
                    try
                    {
                        VariantBinaryWriter.Write(writer, member.GetValue(resource), member.ValueType, this);
                    }
                    catch (UniResException ex)
                    {
                        throw new UniResException($"{resource.GetType().Name}.{member.Name}: {ex.Message}", ex);
                    }
                }

                writer.Flush();
                return ms.ToArray();
            }
        }

        private sealed class ReferenceComparer : IEqualityComparer<Resource>
        {
            public bool Equals(Resource x, Resource y) => ReferenceEquals(x, y);
            public int GetHashCode(Resource obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
