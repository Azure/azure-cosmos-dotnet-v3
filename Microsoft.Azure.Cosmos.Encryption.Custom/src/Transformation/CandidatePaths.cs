// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json;

    /// <summary>
    /// Immutable, cached representation of candidate top-level encrypted JSON property paths.
    /// Paths are normalized (top-level only, leading '/') and de-duplicated then stored with
    /// pre-encoded UTF-8 bytes for allocation-free comparison via Utf8JsonReader.ValueTextEquals.
    /// Instances are cached (keyed by a canonical sorted path list) to avoid per-operation
    /// allocations in steady state when the same schema repeats.
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

        private static readonly ConcurrentDictionary<string, CandidatePaths> Cache = new (StringComparer.Ordinal);

        private readonly Entry[] topLevel;
        private readonly ulong lengthMask;          // bit n => exists candidate with utf8Len == n (<64)
        private readonly bool hasLongNames;         // at least one utf8Len >= 64
        private readonly string includesEmptyFullPath; // set when path "/" exists

        private CandidatePaths(Entry[] entries, ulong lengthMask, bool hasLong, string includesEmpty)
        {
            this.topLevel = entries;
            this.lengthMask = lengthMask;
            this.hasLongNames = hasLong;
            this.includesEmptyFullPath = includesEmpty;
        }

        public static CandidatePaths Build(IEnumerable<string> paths)
        {
            if (paths == null)
            {
                return new CandidatePaths(Array.Empty<Entry>(), 0UL, false, null);
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
                return new CandidatePaths(Array.Empty<Entry>(), 0UL, false, null);
            }

            prelim.Sort(StringComparer.Ordinal);
            int capacity = hasRoot ? 1 : 0; // root marker
            foreach (string s in prelim)
            {
                capacity += s.Length + 1; // '|' + path
            }

            StringBuilder sb = new (capacity);
            if (hasRoot)
            {
                sb.Append('/');
            }

            for (int i = 0; i < prelim.Count; i++)
            {
                if (i > 0 || hasRoot)
                {
                    sb.Append('|');
                }

                sb.Append(prelim[i]);
            }

            string key = sb.ToString();
            if (Cache.TryGetValue(key, out CandidatePaths cached))
            {
                return cached;
            }

            List<Entry> list = new (prelim.Count);
            ulong mask = 0UL;
            bool hasLong = false;
            foreach (string p in prelim)
            {
                ReadOnlySpan<char> nameSpan = p.AsSpan(1);
                int nameUtf8Len = Encoding.UTF8.GetByteCount(nameSpan);
                if ((uint)nameUtf8Len < 64)
                {
                    mask |= 1UL << nameUtf8Len;
                }
                else
                {
                    hasLong = true;
                }

                byte[] utf8Bytes = GetUtf8Bytes(p);
                list.Add(new Entry(p, nameUtf8Len, utf8Bytes));
            }

            CandidatePaths built = new CandidatePaths(list.ToArray(), mask, hasLong, hasRoot ? "/" : null);
            Cache.TryAdd(key, built);
            return built;
        }

        public bool TryMatch(ref Utf8JsonReader reader, int propNameUtf8Len, out string matchedFullPath)
        {
            if (propNameUtf8Len == 0 && this.includesEmptyFullPath != null)
            {
                matchedFullPath = this.includesEmptyFullPath;
                return true;
            }

            if (!this.IsLengthPossible(propNameUtf8Len))
            {
                matchedFullPath = null;
                return false;
            }

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

        private bool IsLengthPossible(int utf8Len)
        {
            return utf8Len < 64 ? (this.lengthMask & (1UL << utf8Len)) != 0 : this.hasLongNames;
        }

        // Cross-target helper: use string slice overload (available in netstandard2.0) for consistent behavior.
        private static byte[] GetUtf8Bytes(string originalFullPath)
        {
            return Encoding.UTF8.GetBytes(originalFullPath, 1, originalFullPath.Length - 1);
        }
    }
}
#endif