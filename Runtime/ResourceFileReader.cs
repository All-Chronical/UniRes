using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace UniRes
{
    /// <summary>
    /// Builds a resource, and everything nested in it, from UniRes file text.
    /// Sections must be in this order: <c>[unires]</c> header, any number of
    /// <c>[ext_resource]</c> and <c>[sub_resource]</c> sections, then <c>[resource]</c>.
    /// A section can only refer to resources defined above it.
    /// </summary>
    internal sealed class ResourceFileReader : IResourceReferenceResolver
    {
        private readonly TextScanner scanner;
        private readonly IExternalResourceLoader loader;
        private readonly Dictionary<string, Resource> externals = new Dictionary<string, Resource>(StringComparer.Ordinal);
        private readonly Dictionary<string, Resource> embedded = new Dictionary<string, Resource>(StringComparer.Ordinal);

        private ResourceFileReader(string text, IExternalResourceLoader loader)
        {
            scanner = new TextScanner(text);
            this.loader = loader;
        }

        /// <exception cref="ResourceFormatException">The text is not a valid UniRes file.</exception>
        public static Resource Read(string text, IExternalResourceLoader loader)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            if (loader == null)
                throw new ArgumentNullException(nameof(loader));

            return new ResourceFileReader(text, loader).ReadAll();
        }

        Resource IResourceReferenceResolver.Resolve(ResourceRef reference)
        {
            Dictionary<string, Resource> table = reference.Kind == ResourceRefKind.External ? externals : embedded;
            if (table.TryGetValue(reference.Id, out Resource resource))
                return resource;

            string section = reference.Kind == ResourceRefKind.External ? "ext_resource" : "sub_resource";
            throw new UniResException($"There is no [{section}] with id '{reference.Id}' defined above this point.");
        }

        private Resource ReadAll()
        {
            Resource root = null;
            string rootTypeName = null;
            bool sawHeader = false;

            while (true)
            {
                scanner.SkipWhitespace();
                if (scanner.AtEnd)
                    break;

                int sectionStart = scanner.Position;
                if (root != null)
                    throw scanner.Fail("Unexpected content after the [resource] section. It must be the last section.");

                scanner.Expect('[');
                string tag = scanner.ReadIdentifier();
                Dictionary<string, object> attributes = ReadAttributes();

                if (!sawHeader)
                {
                    if (tag != "unires")
                        throw scanner.FailAt(sectionStart, "Not a UniRes file: it must start with a [unires format=1 type=\"...\"] header.");

                    rootTypeName = ReadHeader(attributes, sectionStart);
                    sawHeader = true;
                    continue;
                }

                switch (tag)
                {
                    case "ext_resource":
                        ReadExternalSection(attributes, sectionStart);
                        break;
                    case "sub_resource":
                        ReadEmbeddedSection(attributes, sectionStart);
                        break;
                    case "resource":
                        root = CreateInstance(rootTypeName, sectionStart);
                        ReadProperties(root);
                        break;
                    case "unires":
                        throw scanner.FailAt(sectionStart, "The [unires] header can only appear once, at the top.");
                    default:
                        throw scanner.FailAt(sectionStart, $"Unknown section [{tag}].");
                }
            }

            if (!sawHeader)
                throw scanner.Fail("The file is empty. Expected a [unires format=1 type=\"...\"] header.");

            if (root == null)
                throw scanner.Fail("The file has no [resource] section.");

            return root;
        }

        private string ReadHeader(Dictionary<string, object> attributes, int sectionStart)
        {
            if (!(RequireAttribute(attributes, "format", "unires", sectionStart) is long version))
                throw scanner.FailAt(sectionStart, "[unires] 'format' must be a whole number.");

            if (version != ResourceFileWriter.FormatVersion)
                throw scanner.FailAt(sectionStart, $"Unsupported UniRes format version {version}. This version of UniRes reads format {ResourceFileWriter.FormatVersion}.");

            return RequireString(attributes, "type", "unires", sectionStart);
        }

        private void ReadExternalSection(Dictionary<string, object> attributes, int sectionStart)
        {
            string typeName = RequireString(attributes, "type", "ext_resource", sectionStart);
            string path = RequireString(attributes, "path", "ext_resource", sectionStart);
            string id = RequireString(attributes, "id", "ext_resource", sectionStart);

            if (externals.ContainsKey(id))
                throw scanner.FailAt(sectionStart, $"[ext_resource] id '{id}' is used more than once.");

            Resource loaded;
            try
            {
                Type expected = ResourceRegistry.FindType(typeName);
                loaded = loader.Load(path, expected);

                if (loaded == null || !expected.IsInstanceOfType(loaded))
                    throw new UniResException($"The file is not a {typeName}.");
            }
            catch (Exception ex) when (ex is UniResException || ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new ResourceFormatException($"Could not load [ext_resource] '{id}' from '{path}': {ex.Message}", LineOf(sectionStart), ColumnOf(sectionStart));
            }

            externals[id] = loaded;
        }

        private void ReadEmbeddedSection(Dictionary<string, object> attributes, int sectionStart)
        {
            string typeName = RequireString(attributes, "type", "sub_resource", sectionStart);
            string id = RequireString(attributes, "id", "sub_resource", sectionStart);

            if (embedded.ContainsKey(id))
                throw scanner.FailAt(sectionStart, $"[sub_resource] id '{id}' is used more than once.");

            Resource resource = CreateInstance(typeName, sectionStart);
            ReadProperties(resource);
            embedded[id] = resource;
        }

        private Resource CreateInstance(string typeName, int sectionStart)
        {
            try
            {
                return ResourceRegistry.Create(typeName);
            }
            catch (UniResException ex)
            {
                throw scanner.FailAt(sectionStart, ex.Message);
            }
        }

        private void ReadProperties(Resource target)
        {
            ExportedMember[] members = ExportedMember.For(target.GetType());

            while (true)
            {
                scanner.SkipWhitespace();
                if (scanner.AtEnd || scanner.Peek == '[')
                    return;

                string name = scanner.ReadIdentifier();
                scanner.Expect('=');
                scanner.SkipWhitespace();
                int valueStart = scanner.Position;
                object parsed = VariantParser.Parse(scanner);

                ExportedMember member = Find(members, name);
                if (member == null)
                    continue;

                try
                {
                    member.SetValue(target, VariantConverter.ToType(parsed, member.ValueType, this));
                }
                catch (TargetInvocationException ex)
                {
                    throw scanner.FailAt(valueStart, $"Property '{name}': {(ex.InnerException ?? ex).Message}");
                }
                catch (UniResException ex)
                {
                    throw scanner.FailAt(valueStart, $"Property '{name}': {ex.Message}");
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

        /// <summary>Reads <c>key=value</c> pairs up to and including the closing ']' of a section header.</summary>
        private Dictionary<string, object> ReadAttributes()
        {
            var attributes = new Dictionary<string, object>(StringComparer.Ordinal);

            while (true)
            {
                scanner.SkipWhitespace();
                if (scanner.AtEnd)
                    throw scanner.Fail("The section header is missing its closing ']'.");

                if (scanner.Peek == ']')
                {
                    scanner.Advance();
                    return attributes;
                }

                string key = scanner.ReadIdentifier();
                scanner.Expect('=');
                attributes[key] = VariantParser.Parse(scanner);
            }
        }

        private object RequireAttribute(Dictionary<string, object> attributes, string name, string section, int sectionStart)
        {
            if (!attributes.TryGetValue(name, out object value))
                throw scanner.FailAt(sectionStart, $"[{section}] is missing the '{name}' attribute.");

            return value;
        }

        private string RequireString(Dictionary<string, object> attributes, string name, string section, int sectionStart)
        {
            if (!(RequireAttribute(attributes, name, section, sectionStart) is string value))
                throw scanner.FailAt(sectionStart, $"[{section}] '{name}' must be a quoted string.");

            return value;
        }

        private int LineOf(int position)
        {
            return scanner.FailAt(position, "").Line;
        }

        private int ColumnOf(int position)
        {
            return scanner.FailAt(position, "").Column;
        }
    }
}
