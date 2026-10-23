# Changelog

## [0.1.0] - Unreleased

First version.

### Added

- `Resource` base class with `[Export]` fields and properties, and `[ResourceType]` for stable type names.
- Automatic discovery of resource classes, plus `ResourceRegistry` for manual registration.
- Godot-style text file format with `[sub_resource]` (embedded) and `[ext_resource]` (referenced by path) sections.
- Values: bool, whole numbers, float, double, string, enum, Unity vectors, quaternion and colour, packed arrays, lists, dictionaries and resource references.
- `ResourceSaver` and `ResourceLoader` for files (`user://` or file system paths), strings and streams, with `CacheMode`.
- Errors with file path, line and column.
- NUnit tests for the Unity Test Framework.
