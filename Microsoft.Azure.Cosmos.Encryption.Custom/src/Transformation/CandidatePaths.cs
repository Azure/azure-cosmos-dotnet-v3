// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json;

    /// <summary>
    /// Immutable representation of candidate top-level encrypted JSON property paths.
    /// Paths are normalized (top-level only, leading '/') and de-duplicated then stored with
    /// pre-encoded UTF-8 bytes for allocation-free comparison via Utf8JsonReader.ValueTextEquals.
    /// Simplified (no caching / length bitmask) because typical candidate counts are small.
    /// </summary>
    internal sealed class CandidatePaths
    {
        private readonly struct Entry
        {
            public readonly string FullPath;      // e.g. "/foo"
            public readonly int NameCharLen;      // FullPath.Length - 1
            public readonly int NameUtf8Len;      // UTF8 byte length of property name (no leading '/')
            public readonly byte[] NameUtf8Bytes; // Cached UTF8 bytes for fast comparison

            public Entry(string fullPath, int nameUtf8Len, byte[] nameUtf8Bytes)
            {
                this.FullPath = fullPath;
                this.NameCharLen = fullPath.Length - 1;
                this.NameUtf8Len = nameUtf8Len;
                this.NameUtf8Bytes = nameUtf8Bytes;
            }
        }

        private readonly Entry[] topLevel;
        private readonly string includesEmptyFullPath; // set when path "/" exists

        private CandidatePaths(Entry[] entries, string includesEmpty)
        {
            this.topLevel = entries;
            this.includesEmptyFullPath = includesEmpty;
        }

        public static CandidatePaths Build(IEnumerable<string> paths)
        {
            if (paths == null)
            {
                return new CandidatePaths(Array.Empty<Entry>(), null);
            }

            List<string> prelim = new ();
            bool hasRoot = false; // "/"
            foreach (string p in paths)
            {
                if (string.IsNullOrEmpty(p) || p[0] != '/')
                {
                    continue;
                }

                if (p.Length == 1)
                {
                    hasRoot = true;
                    continue;
                }

                ReadOnlySpan<char> nameSpan = p.AsSpan(1);
                if (nameSpan.IndexOf('/') >= 0)
                {
                    continue; // only top-level
                }

                prelim.Add(p);
            }

            if (prelim.Count == 0 && !hasRoot)
            {
                return new CandidatePaths(Array.Empty<Entry>(), null);
            }

            prelim.Sort(StringComparer.Ordinal); // deterministic order
            List<Entry> list = new (prelim.Count);
            foreach (string p in prelim)
            {
                ReadOnlySpan<char> nameSpan = p.AsSpan(1);
                int nameUtf8Len = Encoding.UTF8.GetByteCount(nameSpan); // still store for quick length compare
                byte[] utf8Bytes = GetUtf8Bytes(p);
                list.Add(new Entry(p, nameUtf8Len, utf8Bytes));
            }

            return new CandidatePaths(list.ToArray(), hasRoot ? "/" : null);
        }

        public bool TryMatch(ref Utf8JsonReader reader, int propNameUtf8Len, out string matchedFullPath)
        {
            if (propNameUtf8Len == 0 && this.includesEmptyFullPath != null)
            {
                matchedFullPath = this.includesEmptyFullPath;
                return true;
            }

            // Linear scan (typical candidate count is small). Length check still prunes quickly.
            foreach (ref readonly Entry e in this.topLevel.AsSpan())
            {
                if (e.NameUtf8Len != propNameUtf8Len)
                {
                    continue;
                }

                if (reader.ValueTextEquals(e.NameUtf8Bytes.AsSpan()))
                {
                    matchedFullPath = e.FullPath;
                    return true;
                }
            }

            matchedFullPath = null;
            return false;
        }

        // Cross-target helper: use string slice overload (available in netstandard2.0) for consistent behavior.
        private static byte[] GetUtf8Bytes(string originalFullPath)
        {
            return Encoding.UTF8.GetBytes(originalFullPath, 1, originalFullPath.Length - 1);
        }
    }
}
#endif
