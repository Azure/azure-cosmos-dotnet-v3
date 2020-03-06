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

            public static class TokenBuffers
            {
                public static readonly ReadOnlyMemory<byte> String0 = Encoding.UTF8.GetBytes("$s");
                public static readonly ReadOnlyMemory<byte> String1 = Encoding.UTF8.GetBytes("$t");
                public static readonly ReadOnlyMemory<byte> String2 = Encoding.UTF8.GetBytes("$v");
                public static readonly ReadOnlyMemory<byte> String3 = Encoding.UTF8.GetBytes("_attachments");
                public static readonly ReadOnlyMemory<byte> String4 = Encoding.UTF8.GetBytes("_etag");
                public static readonly ReadOnlyMemory<byte> String5 = Encoding.UTF8.GetBytes("_rid");
                public static readonly ReadOnlyMemory<byte> String6 = Encoding.UTF8.GetBytes("_self");
                public static readonly ReadOnlyMemory<byte> String7 = Encoding.UTF8.GetBytes("_ts");
                public static readonly ReadOnlyMemory<byte> String8 = Encoding.UTF8.GetBytes("attachments/");
                public static readonly ReadOnlyMemory<byte> String9 = Encoding.UTF8.GetBytes("coordinates");
                public static readonly ReadOnlyMemory<byte> String10 = Encoding.UTF8.GetBytes("geometry");
                public static readonly ReadOnlyMemory<byte> String11 = Encoding.UTF8.GetBytes("GeometryCollection");
                public static readonly ReadOnlyMemory<byte> String12 = Encoding.UTF8.GetBytes("id");
                public static readonly ReadOnlyMemory<byte> String13 = Encoding.UTF8.GetBytes("inE");
                public static readonly ReadOnlyMemory<byte> String14 = Encoding.UTF8.GetBytes("inV");
                public static readonly ReadOnlyMemory<byte> String15 = Encoding.UTF8.GetBytes("label");
                public static readonly ReadOnlyMemory<byte> String16 = Encoding.UTF8.GetBytes("LineString");
                public static readonly ReadOnlyMemory<byte> String17 = Encoding.UTF8.GetBytes("link");
                public static readonly ReadOnlyMemory<byte> String18 = Encoding.UTF8.GetBytes("MultiLineString");
                public static readonly ReadOnlyMemory<byte> String19 = Encoding.UTF8.GetBytes("MultiPoint");
                public static readonly ReadOnlyMemory<byte> String20 = Encoding.UTF8.GetBytes("MultiPolygon");
                public static readonly ReadOnlyMemory<byte> String21 = Encoding.UTF8.GetBytes("name");
                public static readonly ReadOnlyMemory<byte> String22 = Encoding.UTF8.GetBytes("outE");
                public static readonly ReadOnlyMemory<byte> String23 = Encoding.UTF8.GetBytes("outV");
                public static readonly ReadOnlyMemory<byte> String24 = Encoding.UTF8.GetBytes("Point");
                public static readonly ReadOnlyMemory<byte> String25 = Encoding.UTF8.GetBytes("Polygon");
                public static readonly ReadOnlyMemory<byte> String26 = Encoding.UTF8.GetBytes("properties");
                public static readonly ReadOnlyMemory<byte> String27 = Encoding.UTF8.GetBytes("type");
                public static readonly ReadOnlyMemory<byte> String28 = Encoding.UTF8.GetBytes("value");
                public static readonly ReadOnlyMemory<byte> String29 = Encoding.UTF8.GetBytes("Feature");
                public static readonly ReadOnlyMemory<byte> String30 = Encoding.UTF8.GetBytes("FeatureCollection");
                public static readonly ReadOnlyMemory<byte> String31 = Encoding.UTF8.GetBytes("_id");
            }

            public static int? GetSystemStringId(ReadOnlySpan<byte> buffer)
            {
                switch (buffer.Length)
                {
                    case 2:
                        return GetSystemStringIdLength2(buffer);
                    case 12:
                        return GetSystemStringIdLength12(buffer);
                    case 5:
                        return GetSystemStringIdLength5(buffer);
                    case 4:
                        return GetSystemStringIdLength4(buffer);
                    case 3:
                        return GetSystemStringIdLength3(buffer);
                    case 11:
                        return GetSystemStringIdLength11(buffer);
                    case 8:
                        return GetSystemStringIdLength8(buffer);
                    case 18:
                        return GetSystemStringIdLength18(buffer);
                    case 10:
                        return GetSystemStringIdLength10(buffer);
                    case 15:
                        return GetSystemStringIdLength15(buffer);
                    case 7:
                        return GetSystemStringIdLength7(buffer);
                    case 17:
                        return GetSystemStringIdLength17(buffer);
                }
                return null;
            }

            private static int? GetSystemStringIdLength2(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(TokenBuffers.String0.Span))
                {
                    return 0;
                }

                if (buffer.SequenceEqual(TokenBuffers.String1.Span))
                {
                    return 1;
                }

                if (buffer.SequenceEqual(TokenBuffers.String2.Span))
                {
                    return 2;
                }

                if (buffer.SequenceEqual(TokenBuffers.String12.Span))
                {
                    return 12;
                }

                return null;
            }

            private static int? GetSystemStringIdLength12(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(TokenBuffers.String3.Span))
                {
                    return 3;
                }

                if (buffer.SequenceEqual(TokenBuffers.String8.Span))
                {
                    return 8;
                }

                if (buffer.SequenceEqual(TokenBuffers.String20.Span))
                {
                    return 20;
                }

                return null;
            }

            private static int? GetSystemStringIdLength5(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(TokenBuffers.String4.Span))
                {
                    return 4;
                }

                if (buffer.SequenceEqual(TokenBuffers.String6.Span))
                {
                    return 6;
                }

                if (buffer.SequenceEqual(TokenBuffers.String15.Span))
                {
                    return 15;
                }

                if (buffer.SequenceEqual(TokenBuffers.String24.Span))
                {
                    return 24;
                }

                if (buffer.SequenceEqual(TokenBuffers.String28.Span))
                {
                    return 28;
                }

                return null;
            }

            private static int? GetSystemStringIdLength4(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(TokenBuffers.String5.Span))
                {
                    return 5;
                }

                if (buffer.SequenceEqual(TokenBuffers.String17.Span))
                {
                    return 17;
                }

                if (buffer.SequenceEqual(TokenBuffers.String21.Span))
                {
                    return 21;
                }

                if (buffer.SequenceEqual(TokenBuffers.String22.Span))
                {
                    return 22;
                }

                if (buffer.SequenceEqual(TokenBuffers.String23.Span))
                {
                    return 23;
                }

                if (buffer.SequenceEqual(TokenBuffers.String27.Span))
                {
                    return 27;
                }

                return null;
            }

            private static int? GetSystemStringIdLength3(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(TokenBuffers.String7.Span))
                {
                    return 7;
                }

                if (buffer.SequenceEqual(TokenBuffers.String13.Span))
                {
                    return 13;
                }

                if (buffer.SequenceEqual(TokenBuffers.String14.Span))
                {
                    return 14;
                }

                if (buffer.SequenceEqual(TokenBuffers.String31.Span))
                {
                    return 31;
                }

                return null;
            }

            private static int? GetSystemStringIdLength11(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(TokenBuffers.String9.Span))
                {
                    return 9;
                }

                return null;
            }

            private static int? GetSystemStringIdLength8(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(TokenBuffers.String10.Span))
                {
                    return 10;
                }

                return null;
            }

            private static int? GetSystemStringIdLength18(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(TokenBuffers.String11.Span))
                {
                    return 11;
                }

                return null;
            }

            private static int? GetSystemStringIdLength10(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(TokenBuffers.String16.Span))
                {
                    return 16;
                }

                if (buffer.SequenceEqual(TokenBuffers.String19.Span))
                {
                    return 19;
                }

                if (buffer.SequenceEqual(TokenBuffers.String26.Span))
                {
                    return 26;
                }

                return null;
            }

            private static int? GetSystemStringIdLength15(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(TokenBuffers.String18.Span))
                {
                    return 18;
                }

                return null;
            }

            private static int? GetSystemStringIdLength7(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(TokenBuffers.String25.Span))
                {
                    return 25;
                }

                if (buffer.SequenceEqual(TokenBuffers.String29.Span))
                {
                    return 29;
                }

                return null;
            }

            private static int? GetSystemStringIdLength17(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(TokenBuffers.String30.Span))
                {
                    return 30;
                }

                return null;
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
            int? id = SystemStrings.GetSystemStringId(utf8String);
            if (!id.HasValue)
            {
                systemStringId = default;
                return false;
            }

            systemStringId = id.Value;
            return true;
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
