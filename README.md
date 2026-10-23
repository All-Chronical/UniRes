# UniRes

Godot-style custom resources for Unity.

Unity's `ScriptableObject` assets can only be created and saved by the Editor, so a built game can't write one out or read one in. **UniRes** gives you typed, nestable data containers that can be **saved and loaded at runtime**, in the Editor or in a built player, with an API modelled on Godot's `Resource`, `ResourceSaver` and `ResourceLoader`.

- Declare a resource as a plain C# class and mark what to keep with `[Export]`.
- Resources can hold other resources. Nested ones are **embedded** in the same file, or **referenced** by path when they have a file of their own. Both are what Godot's `.tres` does.
- Files are readable, diff-friendly text.
- Load the same path twice and you get the same instance.
- Export and import also work to and from strings and streams, for sharing or sending data.

Requires Unity 2022.3 or newer. No dependencies.

## Install

As a Git submodule inside a Unity project:

```
git submodule add <repo-url> Packages/UniRes
```

Unity picks up anything in `Packages/` as an embedded package. Or copy the folder there, or add the repo's Git URL in the Package Manager.

## Quick start

```csharp
using System.Collections.Generic;
using UniRes;
using UnityEngine;

public class Act : Resource
{
    [Export] public float[] Samples = new float[0];
    [Export] public Vector3 Origin;
}

[ResourceType("Take")]            // optional: a stable name for files (default is the class name)
public class Take : Resource
{
    [Export] public string Title = "";
    [Export] public List<Act> Acts = new List<Act>();
}
```

```csharp
var take = new Take { Title = "Take 1" };
take.Acts.Add(new Act { Origin = new Vector3(0, 1, 0), Samples = new[] { 0.1f, 0.2f } });

ResourceSaver.Save(take, "user://films/take1.chtk");

var loaded = ResourceLoader.Load<Take>("user://films/take1.chtk");
```

That writes:

```
[unires format=1 type="Take"]

[sub_resource type="Act" id="Act_1"]
ResourceName = ""
Samples = PackedFloat32Array(0.1, 0.2)
Origin = Vector3(0.0, 1.0, 0.0)

[resource]
ResourceName = ""
Title = "Take 1"
Acts = [SubResource("Act_1")]
```

### Embedded and external resources

A resource that has no path of its own is written inside the file that holds it. Give a resource a path and it is written as a reference instead, and loaded from its own file:

```csharp
ResourceSaver.Save(act, "user://acts/1.chact", SaveFlags.ChangePath); // act now "lives" at that path
take.Acts.Add(act);
ResourceSaver.Save(take, "user://films/take1.chtk");
```

```
[ext_resource type="Act" path="user://acts/1.chact" id="1"]

[resource]
Acts = [ExtResource("1")]
```

Saving a resource does not re-save the external files it references. Save those yourself first.

## API

| Call | What it does |
|---|---|
| `ResourceSaver.Save(resource, path, flags)` | Write a resource to a file. Creates folders. `SaveFlags.ChangePath` also gives the resource that path. |
| `ResourceSaver.SaveToString(resource)` / `SaveToStream(resource, stream)` | Export as text. |
| `ResourceLoader.Load<T>(path, cacheMode)` | Load a file. Nested files load automatically. |
| `ResourceLoader.LoadFromString<T>(text)` / `LoadFromStream<T>(stream)` | Import from text. |
| `ResourceLoader.Exists(path)` | Whether a file exists. |
| `ResourceLoader.ClearCache()` | Forget loaded instances so the next `Load` reads files again. |
| `resource.TakeOverPath(path)` | Give a resource a path without saving it. |
| `ResourceRegistry.Register<T>()` | Register a class by hand (see below). |
| `ResourcePaths.UserDataRoot` | Folder `user://` points to. Defaults to `Application.persistentDataPath`. |

`CacheMode` is `Reuse` (default: same path gives the same instance), `Ignore` (read again, leave cache alone) or `Replace` (read again and cache the new instance).

Problems throw `UniResException`. A bad file throws `ResourceFormatException` (a `UniResException`) with the file path, line and column. Missing files throw the normal `IOException` family.

### Paths

- `user://anything/file.ext` is inside `ResourcePaths.UserDataRoot`. This is the portable choice and is writable in builds.
- Any other path is an ordinary file system path.

There is no `res://`: a built Unity player has no writable project folder.

## What can be exported

`[Export]` goes on a field or property (it may be private). Properties need a getter and a setter. Use `[Export("name")]` to choose the name written in the file.

| C# type | File form |
|---|---|
| `bool`, `int`, `long`, `short`, `byte`, `sbyte`, `ushort`, `uint` | `true`, `42` |
| `float`, `double` | `1.5`, `inf`, `inf_neg`, `nan` (round-trips exactly) |
| `string` | `"text"` with JSON-style escapes (`null` is kept distinct from `""`) |
| `enum` | its number (a name like `"Fast"` is also accepted when reading) |
| `Vector2` `Vector3` `Vector4` `Quaternion` `Color` | `Vector3(1.0, 2.0, 3.0)` |
| `Vector2Int` `Vector3Int` | `Vector3i(1, 2, 3)` |
| `byte[]` | `PackedByteArray("base64")` |
| `int[]` `long[]` `float[]` `double[]` `string[]` | `PackedInt32Array(...)`, `PackedFloat32Array(...)`, ... |
| other `T[]`, `List<T>` | `[a, b, c]` |
| `Dictionary<K, V>` | `{"key": value}` |
| any `Resource` class | `SubResource("id")` or `ExtResource("id")`, or `null` |

`T`, `K` and `V` can be any of the above, including nested collections and resources. A `List<Resource>` can hold different subclasses.

When reading, properties missing from the file keep their defaults, and properties the class no longer has are ignored, so classes can evolve without breaking old files.

## Rules and limits

- **Parameterless constructor.** Resource classes need one (it may be private). Field initialisers run, so defaults like `= new List<Act>()` work.
- **Type names must be unique.** The class name is the type name unless you set `[ResourceType("Name")]`. Set one if you may rename or move the class later.
- **No loops.** A resource can't contain itself, directly or through others. Saving or loading such data throws.
- **Shared instances stay shared.** A resource referenced from two places is written once.
- **Whole-file read/write.** A file is read and written in one go, like Godot's text resources. Large arrays work (a million floats round-trips in about a second) but are held fully in memory, and text is larger than a binary form would be.
- **Not yet supported:** Unity objects such as `Texture2D`, `Mesh` or `GameObject`; `ulong`, `decimal`, `char`, `DateTime`; a compact binary file form (Godot's `.res`). Dictionary keys should be strings or whole numbers.

### Finding your classes

UniRes finds resource classes automatically in any loaded assembly that references `UniRes.Runtime`. A class in an assembly that doesn't (for example one with `autoReferenced` off and no reference to UniRes) must be registered once at startup with `ResourceRegistry.Register<T>()` or `RegisterAssembly(...)`.

### IL2CPP and code stripping

UniRes reads and writes members by reflection. In builds with managed code stripping, the linker may remove classes or members nothing refers to directly. Keep your resource classes with `[UnityEngine.Scripting.Preserve]` on the class and its `[Export]` members, or list them in a `link.xml`.

## Tests

`Tests/Runtime` holds NUnit tests for the Unity Test Framework (Window > General > Test Runner > EditMode or PlayMode). For an embedded package in `Packages/` they are picked up automatically.

## License

No license has been chosen yet. Add a `LICENSE` file before publishing.
