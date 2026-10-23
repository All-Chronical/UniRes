using System;
using System.Collections.Generic;

namespace UniRes
{
    /// <summary>
    /// Remembers which resource instance was loaded from which path, so loading the same path
    /// again returns the same instance (as Godot does). Entries are weak: a resource nothing else
    /// holds can be collected, and loading it again reads the file again.
    /// </summary>
    internal static class ResourceCache
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, WeakReference<Resource>> Entries =
            new Dictionary<string, WeakReference<Resource>>(StringComparer.Ordinal);

        public static bool TryGet(string path, out Resource resource)
        {
            lock (Gate)
            {
                if (Entries.TryGetValue(path, out WeakReference<Resource> weak))
                {
                    if (weak.TryGetTarget(out resource))
                        return true;

                    Entries.Remove(path);
                }
            }

            resource = null;
            return false;
        }

        public static void Set(string path, Resource resource)
        {
            lock (Gate)
            {
                Entries[path] = new WeakReference<Resource>(resource);
            }
        }

        /// <summary>Assigns a path to a resource, displacing any different cached resource at that path.</summary>
        public static void TakeOver(string path, Resource resource)
        {
            lock (Gate)
            {
                if (Entries.TryGetValue(path, out WeakReference<Resource> weak)
                    && weak.TryGetTarget(out Resource previous)
                    && !ReferenceEquals(previous, resource)
                    && string.Equals(previous.ResourcePath, path, StringComparison.Ordinal))
                {
                    previous.ResourcePath = "";
                }

                Entries[path] = new WeakReference<Resource>(resource);
            }
        }

        public static bool Contains(string path)
        {
            return TryGet(path, out _);
        }

        /// <summary>Forgets a path, but only if it currently maps to this exact instance.</summary>
        public static void Remove(string path, Resource resource)
        {
            lock (Gate)
            {
                if (Entries.TryGetValue(path, out WeakReference<Resource> weak)
                    && weak.TryGetTarget(out Resource cached)
                    && ReferenceEquals(cached, resource))
                {
                    Entries.Remove(path);
                }
            }
        }

        public static void Clear()
        {
            lock (Gate)
            {
                Entries.Clear();
            }
        }
    }
}
