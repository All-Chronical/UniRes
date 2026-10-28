using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace UniRes
{
    /// <summary>
    /// Reads a resource and everything nested in it from a binary UniRes stream or byte array.
    /// </summary>
    internal sealed class ResourceBinaryReader : IResourceReferenceResolver
    {
        private readonly IExternalResourceLoader loader;
        private readonly string sourcePath;
        private readonly Dictionary<string, Resource> externals = new Dictionary<string, Resource>(StringComparer.Ordinal);
        private readonly Dictionary<string, Resource> embedded = new Dictionary<string, Resource>(StringComparer.Ordinal);

        private ResourceBinaryReader(IExternalResourceLoader loader, string sourcePath)
        {
            this.loader = loader;
            this.sourcePath = sourcePath;
        }

        public static Resource Read(Stream stream, IExternalResourceLoader loader, string sourcePath = null)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (loader == null)
                throw new ArgumentNullException(nameof(loader));

            return new ResourceBinaryReader(loader, sourcePath).ReadAll(stream);
        }

        public static Resource Read(byte[] bytes, IExternalResourceLoader loader, string sourcePath = null)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            using (var ms = new MemoryStream(bytes))
                return Read(ms, loader, sourcePath);
        }

        Resource IResourceReferenceResolver.Resolve(ResourceRef reference)
        {
            Dictionary<string, Resource> table = reference.Kind == ResourceRefKind.External ? externals : embedded;
            if (table.TryGetValue(reference.Id, out Resource resource))
                return resource;

            string section = reference.Kind == ResourceRefKind.External ? "ext_resource" : "sub_resource";
            throw new UniResException($"There is no [{section}] with id '{reference.Id}' defined in this file.");
        }

        private Resource ReadAll(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                byte[] magic = reader.ReadBytes(ResourceBinaryWriter.Magic.Length);
                if (magic.Length != ResourceBinaryWriter.Magic.Length || !MatchesMagic(magic))
                    throw new ResourceFormatException("Not a binary UniRes file: invalid magic header.");

                byte version = reader.ReadByte();
                if (version != ResourceBinaryWriter.FormatVersion)
                    throw new ResourceFormatException($"Unsupported binary UniRes format version {version}. This version of UniRes reads format {ResourceBinaryWriter.FormatVersion}.");

                ushort flags = reader.ReadUInt16();
                string rootTypeName = reader.ReadString();

                // External resources
                int extCount = reader.ReadInt32();
                for (int i = 0; i < extCount; i++)
                {
                    string id = reader.ReadString();
                    string typeName = reader.ReadString();
                    string path = reader.ReadString();

                    if (externals.ContainsKey(id))
                        throw new ResourceFormatException($"[ext_resource] id '{id}' is used more than once.");

                    Resource loadedExt;
                    try
                    {
                        Type expected = ResourceRegistry.FindType(typeName);
                        string resolvedPath = ResourcePaths.ResolveReference(path, sourcePath);
                        loadedExt = loader.Load(resolvedPath, expected);

                        if (loadedExt == null || !expected.IsInstanceOfType(loadedExt))
                            throw new UniResException($"The file is not a {typeName}.");
                    }
                    catch (Exception ex) when (ex is UniResException || ex is IOException || ex is UnauthorizedAccessException)
                    {
                        throw new ResourceFormatException($"Could not load [ext_resource] '{id}' from '{path}': {ex.Message}");
                    }

                    externals[id] = loadedExt;
                }

                // Sub resources
                int subCount = reader.ReadInt32();
                for (int i = 0; i < subCount; i++)
                {
                    string id = reader.ReadString();
                    string typeName = reader.ReadString();
                    int payloadLength = reader.ReadInt32();
                    byte[] payload = reader.ReadBytes(payloadLength);

                    if (embedded.ContainsKey(id))
                        throw new ResourceFormatException($"[sub_resource] id '{id}' is used more than once.");

                    Resource subResource = CreateInstance(typeName);
                    using (var pms = new MemoryStream(payload))
                    using (var preader = new BinaryReader(pms, Encoding.UTF8))
                    {
                        ReadProperties(subResource, preader);
                    }

                    embedded[id] = subResource;
                }

                // Root resource
                int rootPayloadLength = reader.ReadInt32();
                byte[] rootPayload = reader.ReadBytes(rootPayloadLength);

                Resource root = CreateInstance(rootTypeName);
                using (var pms = new MemoryStream(rootPayload))
                using (var preader = new BinaryReader(pms, Encoding.UTF8))
                {
                    ReadProperties(root, preader);
                }

                return root;
            }
        }

        private static bool MatchesMagic(byte[] header)
        {
            for (int i = 0; i < ResourceBinaryWriter.Magic.Length; i++)
            {
                if (header[i] != ResourceBinaryWriter.Magic[i])
                    return false;
            }
            return true;
        }

        private Resource CreateInstance(string typeName)
        {
            return ResourceRegistry.Create(typeName);
        }

        private void ReadProperties(Resource target, BinaryReader reader)
        {
            int propCount = reader.ReadInt32();
            ExportedMember[] members = ExportedMember.For(target.GetType());

            for (int i = 0; i < propCount; i++)
            {
                string name = reader.ReadString();
                object parsed = VariantBinaryReader.Read(reader);

                ExportedMember member = Find(members, name);
                if (member == null)
                    continue;

                try
                {
                    member.SetValue(target, VariantConverter.ToType(parsed, member.ValueType, this));
                }
                catch (TargetInvocationException ex)
                {
                    throw new ResourceFormatException($"Property '{name}': {(ex.InnerException ?? ex).Message}");
                }
                catch (UniResException ex)
                {
                    throw new ResourceFormatException($"Property '{name}': {ex.Message}");
                }
            }
        }

        private static ExportedMember Find(ExportedMember[] members, string name)
        {
            foreach (ExportedMember member in members)
            {
                if (member.Name == name)
                    return member;
            }

            return null;
        }
    }
}
