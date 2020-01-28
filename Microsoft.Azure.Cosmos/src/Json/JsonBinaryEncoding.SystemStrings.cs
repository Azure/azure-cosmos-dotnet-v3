// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.Core.Collections;

    internal static partial class JsonBinaryEncoding
    {
        private static class SystemStrings
        {
            /// <summary>
            /// List is system strings
            /// </summary>
            public static readonly string[] Utf16Values = new string[]
            {
                "$s",
                "$t",
                "$v",
                "_attachments",
                "_etag",
                "_rid",
                "_self",
                "_ts",
                "attachments/",
                "coordinates",
                "geometry",
                "GeometryCollection",
                "id",
                "inE",
                "inV",
                "label",
                "LineString",
                "link",
                "MultiLineString",
                "MultiPoint",
                "MultiPolygon",
                "name",
                "outE",
                "outV",
                "Point",
                "Polygon",
                "properties",
                "type",
                "value",
                "Feature",
                "FeatureCollection",
                "_id",
            };

            public static readonly ReadOnlyMemory<byte>[] Utf8Values = Utf16Values.Select(x => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(x)).ToArray();

            public sealed class Dictionary
            {
                public static readonly Dictionary Singleton = new Dictionary();

                private readonly Trie<byte, int> systemStrings;

                private Dictionary()
                {
                    this.systemStrings = new Trie<byte, int>();
                    for (int index = 0; index < SystemStrings.Utf16Values.Length; index++)
                    {
                        ReadOnlySpan<byte> systemString = SystemStrings.Utf8Values[index].Span;
                        this.systemStrings.AddOrUpdate(systemString, index);
                    }
                }

                public bool TryGetValue(ReadOnlySpan<byte> utf8String, out int systemStringId)
                {
                    return this.systemStrings.TryGetValue(utf8String, out systemStringId);
                }
            }
        }

        /// <summary>
        /// Gets the SystemStringId for a particular system string.
        /// </summary>
        /// <param name="utf8String">The system string to get the enum id for.</param>
        /// <param name="systemStringId">The id of the system string if found.</param>
        /// <returns>The SystemStringId for a particular system string.</returns>
        public static bool TryGetSystemStringId(ReadOnlySpan<byte> utf8String, out int systemStringId)
        {
            return SystemStrings.Dictionary.Singleton.TryGetValue(utf8String, out systemStringId);
        }

        public static bool TryGetSystemStringById(int id, out string systemString)
        {
            if (id >= SystemStrings.Utf16Values.Length)
            {
                systemString = default;
                return false;
            }

            systemString = SystemStrings.Utf16Values[id];
            return true;
        }

        public static bool TryGetUtf8SystemStringById(int id, out ReadOnlyMemory<byte> utf8SystemString)
        {
            if (id >= SystemStrings.Utf8Values.Length)
            {
                utf8SystemString = default;
                return false;
            }

            utf8SystemString = SystemStrings.Utf8Values[id];
            return true;
        }
    }
}
