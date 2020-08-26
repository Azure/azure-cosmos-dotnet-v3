// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.Core.Utf8;

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

            public static readonly UtfAllString[] UtfAllStringValues = Utf16Values.Select(x => UtfAllString.Create(x)).ToArray();

            public static class TokenBuffers
            {
                public static readonly Utf8String String0 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("$s"));
                public static readonly Utf8String String1 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("$t"));
                public static readonly Utf8String String2 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("$v"));
                public static readonly Utf8String String3 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("_attachments"));
                public static readonly Utf8String String4 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("_etag"));
                public static readonly Utf8String String5 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("_rid"));
                public static readonly Utf8String String6 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("_self"));
                public static readonly Utf8String String7 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("_ts"));
                public static readonly Utf8String String8 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("attachments/"));
                public static readonly Utf8String String9 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("coordinates"));
                public static readonly Utf8String String10 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("geometry"));
                public static readonly Utf8String String11 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("GeometryCollection"));
                public static readonly Utf8String String12 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("id"));
                public static readonly Utf8String String13 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("inE"));
                public static readonly Utf8String String14 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("inV"));
                public static readonly Utf8String String15 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("label"));
                public static readonly Utf8String String16 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("LineString"));
                public static readonly Utf8String String17 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("link"));
                public static readonly Utf8String String18 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("MultiLineString"));
                public static readonly Utf8String String19 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("MultiPoint"));
                public static readonly Utf8String String20 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("MultiPolygon"));
                public static readonly Utf8String String21 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("name"));
                public static readonly Utf8String String22 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("outE"));
                public static readonly Utf8String String23 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("outV"));
                public static readonly Utf8String String24 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("Point"));
                public static readonly Utf8String String25 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("Polygon"));
                public static readonly Utf8String String26 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("properties"));
                public static readonly Utf8String String27 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("type"));
                public static readonly Utf8String String28 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("value"));
                public static readonly Utf8String String29 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("Feature"));
                public static readonly Utf8String String30 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("FeatureCollection"));
                public static readonly Utf8String String31 = Utf8String.UnsafeFromUtf8BytesNoValidation(Encoding.UTF8.GetBytes("_id"));
            }

            public static int? GetSystemStringId(Utf8Span buffer)
            {
                return buffer.Length switch
                {
                    2 => GetSystemStringIdLength2(buffer.Span),
                    12 => GetSystemStringIdLength12(buffer.Span),
                    5 => GetSystemStringIdLength5(buffer.Span),
                    4 => GetSystemStringIdLength4(buffer.Span),
                    3 => GetSystemStringIdLength3(buffer.Span),
                    11 => GetSystemStringIdLength11(buffer.Span),
                    8 => GetSystemStringIdLength8(buffer.Span),
                    18 => GetSystemStringIdLength18(buffer.Span),
                    10 => GetSystemStringIdLength10(buffer.Span),
                    15 => GetSystemStringIdLength15(buffer.Span),
                    7 => GetSystemStringIdLength7(buffer.Span),
                    17 => GetSystemStringIdLength17(buffer.Span),
                    _ => null,
                };
            }

            private static int? GetSystemStringIdLength2(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(TokenBuffers.String0.Span.Span))
                {
                    return 0;
                }

                if (buffer.SequenceEqual(TokenBuffers.String1.Span.Span))
                {
                    return 1;
                }

                if (buffer.SequenceEqual(TokenBuffers.String2.Span.Span))
                {
                    return 2;
                }

                if (buffer.SequenceEqual(TokenBuffers.String12.Span.Span))
                {
                    return 12;
                }

                return null;
            }

            private static int? GetSystemStringIdLength12(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(TokenBuffers.String3.Span.Span))
                {
                    return 3;
                }

                if (buffer.SequenceEqual(TokenBuffers.String8.Span.Span))
                {
                    return 8;
                }

                if (buffer.SequenceEqual(TokenBuffers.String20.Span.Span))
                {
                    return 20;
                }

                return null;
            }

            private static int? GetSystemStringIdLength5(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(TokenBuffers.String4.Span.Span))
                {
                    return 4;
                }

                if (buffer.SequenceEqual(TokenBuffers.String6.Span.Span))
                {
                    return 6;
                }

                if (buffer.SequenceEqual(TokenBuffers.String15.Span.Span))
                {
                    return 15;
                }

                if (buffer.SequenceEqual(TokenBuffers.String24.Span.Span))
                {
                    return 24;
                }

                if (buffer.SequenceEqual(TokenBuffers.String28.Span.Span))
                {
                    return 28;
                }

                return null;
            }

            private static int? GetSystemStringIdLength4(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(TokenBuffers.String5.Span.Span))
                {
                    return 5;
                }

                if (buffer.SequenceEqual(TokenBuffers.String17.Span.Span))
                {
                    return 17;
                }

                if (buffer.SequenceEqual(TokenBuffers.String21.Span.Span))
                {
                    return 21;
                }

                if (buffer.SequenceEqual(TokenBuffers.String22.Span.Span))
                {
                    return 22;
                }

                if (buffer.SequenceEqual(TokenBuffers.String23.Span.Span))
                {
                    return 23;
                }

                if (buffer.SequenceEqual(TokenBuffers.String27.Span.Span))
                {
                    return 27;
                }

                return null;
            }

            private static int? GetSystemStringIdLength3(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(TokenBuffers.String7.Span.Span))
                {
                    return 7;
                }

                if (buffer.SequenceEqual(TokenBuffers.String13.Span.Span))
                {
                    return 13;
                }

                if (buffer.SequenceEqual(TokenBuffers.String14.Span.Span))
                {
                    return 14;
                }

                if (buffer.SequenceEqual(TokenBuffers.String31.Span.Span))
                {
                    return 31;
                }

                return null;
            }

            private static int? GetSystemStringIdLength11(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(TokenBuffers.String9.Span.Span))
                {
                    return 9;
                }

                return null;
            }

            private static int? GetSystemStringIdLength8(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(TokenBuffers.String10.Span.Span))
                {
                    return 10;
                }

                return null;
            }

            private static int? GetSystemStringIdLength18(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(TokenBuffers.String11.Span.Span))
                {
                    return 11;
                }

                return null;
            }

            private static int? GetSystemStringIdLength10(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(TokenBuffers.String16.Span.Span))
                {
                    return 16;
                }

                if (buffer.SequenceEqual(TokenBuffers.String19.Span.Span))
                {
                    return 19;
                }

                if (buffer.SequenceEqual(TokenBuffers.String26.Span.Span))
                {
                    return 26;
                }

                return null;
            }

            private static int? GetSystemStringIdLength15(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(TokenBuffers.String18.Span.Span))
                {
                    return 18;
                }

                return null;
            }

            private static int? GetSystemStringIdLength7(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(TokenBuffers.String25.Span.Span))
                {
                    return 25;
                }

                if (buffer.SequenceEqual(TokenBuffers.String29.Span.Span))
                {
                    return 29;
                }

                return null;
            }

            private static int? GetSystemStringIdLength17(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(TokenBuffers.String30.Span.Span))
                {
                    return 30;
                }

                return null;
            }
        }

        /// <summary>
        /// Gets the SystemStringId for a particular system string.
        /// </summary>
        /// <param name="utf8Span">The system string to get the enum id for.</param>
        /// <param name="systemStringId">The id of the system string if found.</param>
        /// <returns>The SystemStringId for a particular system string.</returns>
        public static bool TryGetSystemStringId(Utf8Span utf8Span, out int systemStringId)
        {
            int? id = SystemStrings.GetSystemStringId(utf8Span);
            if (!id.HasValue)
            {
                systemStringId = default;
                return false;
            }

            systemStringId = id.Value;
            return true;
        }

        public static bool TryGetSystemStringById(int id, out UtfAllString systemString)
        {
            if (id >= SystemStrings.UtfAllStringValues.Length)
            {
                systemString = default;
                return false;
            }

            systemString = SystemStrings.UtfAllStringValues[id];
            return true;
        }
    }
}
