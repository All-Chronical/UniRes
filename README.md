# UniRes

A runtime resource and serialization package for Unity.

Unity's `ScriptableObject` assets are authored and saved by the Editor. UniRes provides typed, nestable data containers that your application can save and load at runtime, in the Editor or in a built player.

The resource model and readable text layout are inspired by Godot's `Resource` and `.tres` workflow. UniRes has its own file format and is not compatible with Godot resource files.

- Define resources as ordinary C# classes deriving from `Resource`.
- Mark saved fields and properties with `[Export]`.
- Embed child resources in a file or reference them from their own files.
- Use `ResourceSaver` and `ResourceLoader` for files, strings, and streams.
- Store files as readable, diff-friendly text.

Requires Unity 2022.3 or newer. No external dependencies.

## Install

### As a Unity package

Add the package's repository URL through Unity Package Manager (`Window > Package Manager > + > Add package from git URL...`).

### As an embedded package

Copy or add the UniRes repository under the Unity project's `Packages/` folder. For example:

```sh
git submodule add <repository-url> Packages/UniRes
```

Unity loads the package from `Packages/UniRes`. Alternatively, add its local path as a package dependency in `Packages/manifest.json`.

## Quick start

Put each public resource class in its own `.cs` file in an assembly that references `UniRes.Runtime`.

```csharp
// ChildResource.cs
using UniRes;

public class ChildResource : Resource
{
    [Export] public int Value;
}
```

```csharp
// ParentResource.cs
using System.Collections.Generic;
using UniRes;

[ResourceType("Parent")] // Optional stable type name; defaults to "ParentResource".
public class ParentResource : Resource
{
    [Export] public string Title = "";
    [Export] public List<ChildResource> Children = new List<ChildResource>();
}
```

Create a resource, save it, and load it again at runtime:

```csharp
var parent = new ParentResource { Title = "Example" };
parent.Children.Add(new ChildResource { Value = 42 });

ResourceSaver.Save(parent, "user://example/parent.resource");

ParentResource loaded = ResourceLoader.Load<ParentResource>("user://example/parent.resource");
```

The saved text contains the parent and its embedded child:

```text
[unires format=1 type="Parent"]

[sub_resource type="ChildResource" id="ChildResource_1"]
ResourceName = ""
Value = 42

[resource]
ResourceName = ""
Title = "Example"
Children = [SubResource("ChildResource_1")]
```

## Embedded and referenced resources

A resource with no file path is embedded in its parent file as a `[sub_resource]`. To store a resource in its own file, save it with `SaveFlags.ChangePath`. The parent then stores an `[ext_resource]` reference, and loading the parent loads the referenced file too.

```csharp
var child = new ChildResource { Value = 42 };
ResourceSaver.Save(child, "user://example/child.resource", SaveFlags.ChangePath);

var parent = new ParentResource();
parent.Children.Add(child);
ResourceSaver.Save(parent, "user://example/parent.resource");
```

The parent file contains a reference rather than a copy of the child data:

```text
[ext_resource type="ChildResource" path="user://example/child.resource" id="1"]

[resource]
Children = [ExtResource("1")]
```

Saving a parent does not save or overwrite external resources it references. Save those resources separately. If an external resource has not been saved with a path, UniRes embeds it instead.

## Import and export text

Use the string methods to inspect or exchange resource text directly:

```csharp
string text = ResourceSaver.SaveToString(parent);
ParentResource imported = ResourceLoader.LoadFromString<ParentResource>(text);
```

Use streams when another API supplies a stream:

```csharp
using (var stream = new System.IO.MemoryStream())
{
    ResourceSaver.SaveToStream(parent, stream);
    stream.Position = 0;
    ParentResource imported = ResourceLoader.LoadFromStream<ParentResource>(stream);
}
```

These methods read and write the whole resource in one go. Streams do not make large resources incremental or reduce their in-memory size.

## API

| Call | Description |
|---|---|
| `ResourceSaver.Save(resource, path, flags)` | Save a resource to a file, creating parent folders. `SaveFlags.ChangePath` also assigns that file path to the resource. |
| `ResourceSaver.SaveToString(resource)` | Export a resource as text. |
| `ResourceSaver.SaveToStream(resource, stream)` | Write resource text to a stream, leaving the stream open. |
| `ResourceLoader.Load<T>(path, cacheMode)` | Load a resource by path, including nested external resources. |
| `ResourceLoader.LoadFromString<T>(text)` | Import resource text from a string. |
| `ResourceLoader.LoadFromStream<T>(stream)` | Import resource text from a stream, leaving the stream open. |
| `ResourceLoader.Exists(path)` | Check whether a file exists. |
| `ResourceLoader.ClearCache()` | Forget cached resources so the next load reads the files again. |
| `resource.TakeOverPath(path)` | Assign a resource a path without saving it. |
| `ResourceRegistry.Register<T>()` | Register a resource class explicitly. |
| `ResourcePaths.UserDataRoot` | Get or set the directory used by `user://` paths. Defaults to `Application.persistentDataPath`. |

`CacheMode` controls how `ResourceLoader.Load` handles cached files:

- `Reuse` (default): return the existing instance for the path, if loaded.
- `Ignore`: read the file again without changing the cache.
- `Replace`: read the file again and replace the cached instance.

Format errors throw `ResourceFormatException`, which includes a path when available and a 1-based line and column. It derives from `UniResException`. File-system errors use the standard `IOException` family.

## Paths

- `user://path/to/file` resolves under `ResourcePaths.UserDataRoot`, which defaults to the application's persistent data directory and is writable in builds.
- Other paths are ordinary file-system paths, absolute or relative to the working directory.
- UniRes does not provide a `res://` scheme. A built Unity player does not have a writable project-assets directory.

## Supported exported types

Put `[Export]` on a field or property to serialize it. Private members can be exported. Properties must have both a getter and a setter. `[Export("file_name")]` selects the property name used in the file.

| C# type | Text representation |
|---|---|
| `bool`, `int`, `long`, `short`, `byte`, `sbyte`, `ushort`, `uint` | `true`, `42` |
| `float`, `double` | `1.5`, `inf`, `inf_neg`, `nan` (round-trips exactly) |
| `string` | Quoted text with JSON-style escapes; `null` is distinct from an empty string |
| `enum` | Numeric value; a quoted enum name is also accepted when reading |
| `Vector2`, `Vector3`, `Vector4`, `Quaternion`, `Color` | `Vector3(1.0, 2.0, 3.0)` |
| `Vector2Int`, `Vector3Int` | `Vector3i(1, 2, 3)` |
| `byte[]` | `PackedByteArray("base64")` |
| `int[]`, `long[]`, `float[]`, `double[]`, `string[]` | Typed packed arrays such as `PackedFloat32Array(...)` |
| Other one-dimensional `T[]`, `List<T>` | `[a, b, c]` |
| `Dictionary<K, V>` | `{"key": value}` |
| Any `Resource` subclass | `SubResource("id")`, `ExtResource("id")`, or `null` |

Collections can be nested and can contain resources. A collection typed as `Resource` can contain instances of different resource subclasses. Dictionary keys should be strings or whole numbers.

When loading, fields absent from the file keep their C# defaults; file properties that no longer exist on the class are ignored. This supports adding and removing exported members over time.

## Resource class requirements

- Resource classes must be public, concrete classes with a parameterless constructor (it may be private). Field initializers run, so default collections can be initialized normally.
- Resource type names must be unique. The C# class name is used by default. Add `[ResourceType("StableName")]` to choose a stable serialized name, such as when a class may be renamed.
- Resource graphs must not contain cycles. A resource cannot contain itself, directly or indirectly. UniRes detects cycles and reports an error.
- Shared references remain shared: a resource referenced in more than one place is stored once per file.
- A class in an assembly that references `UniRes.Runtime` is discovered automatically. Otherwise, register it at startup with `ResourceRegistry.Register<T>()` or `ResourceRegistry.RegisterAssembly(...)`.

## Limits

- **Whole-resource serialization:** a file is parsed or written in one go. Large arrays work, but the whole resource is held in memory and text takes more space than a binary format.
- **No Unity object references yet:** `Texture2D`, `Mesh`, `GameObject`, and other engine objects are not serializable by the current value format. They can be stored as separate files and referenced by application-defined data, but UniRes does not resolve Unity object references.
- **No binary format yet:** files are text only; there is no compact binary counterpart.
- **IL2CPP stripping:** UniRes accesses exported members through reflection. Preserve resource classes and exported members with `[UnityEngine.Scripting.Preserve]` or a `link.xml` when managed stripping could remove them.

## Tests

`Tests/Runtime` contains NUnit tests for the Unity Test Framework. For an embedded package, open **Window > General > Test Runner** and run EditMode or PlayMode tests.

## License

No license has been selected. Add a `LICENSE` file before publishing the package.
