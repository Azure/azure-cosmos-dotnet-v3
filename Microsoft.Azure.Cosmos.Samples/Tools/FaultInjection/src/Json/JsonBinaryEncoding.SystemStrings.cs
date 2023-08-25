// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is auto-generated code. Modify: JsonBinaryEncoding.SystemStrings.tt: 86

namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Immutable;
    using Microsoft.Azure.Cosmos.Core.Utf8;

    internal static partial class JsonBinaryEncoding
    {
        public static class SystemStrings
        {
            /// <summary>
            /// List of system strings
            /// </summary>
            public static readonly ImmutableArray<UtfAllString> Strings = new UtfAllString[]
            {
                UtfAllString.Create("$s"),
                UtfAllString.Create("$t"),
                UtfAllString.Create("$v"),
                UtfAllString.Create("_attachments"),
                UtfAllString.Create("_etag"),
                UtfAllString.Create("_rid"),
                UtfAllString.Create("_self"),
                UtfAllString.Create("_ts"),
                UtfAllString.Create("attachments/"),
                UtfAllString.Create("coordinates"),
                UtfAllString.Create("geometry"),
                UtfAllString.Create("GeometryCollection"),
                UtfAllString.Create("id"),
                UtfAllString.Create("url"),
                UtfAllString.Create("Value"),
                UtfAllString.Create("label"),
                UtfAllString.Create("LineString"),
                UtfAllString.Create("link"),
                UtfAllString.Create("MultiLineString"),
                UtfAllString.Create("MultiPoint"),
                UtfAllString.Create("MultiPolygon"),
                UtfAllString.Create("name"),
                UtfAllString.Create("Name"),
                UtfAllString.Create("Type"),
                UtfAllString.Create("Point"),
                UtfAllString.Create("Polygon"),
                UtfAllString.Create("properties"),
                UtfAllString.Create("type"),
                UtfAllString.Create("value"),
                UtfAllString.Create("Feature"),
                UtfAllString.Create("FeatureCollection"),
                UtfAllString.Create("_id"),
                UtfAllString.Create("$id"),
                UtfAllString.Create("$pk"),
                UtfAllString.Create("_isEdge"),
                UtfAllString.Create("_partitionKey"),
                UtfAllString.Create("_type"),
                UtfAllString.Create("_value"),
                UtfAllString.Create("data"),
                UtfAllString.Create("Data"),
                UtfAllString.Create("entity"),
                UtfAllString.Create("isDeleted"),
                UtfAllString.Create("IsDeleted"),
                UtfAllString.Create("key"),
                UtfAllString.Create("Key"),
                UtfAllString.Create("Location"),
                UtfAllString.Create("partition"),
                UtfAllString.Create("partition_id"),
                UtfAllString.Create("partitionKey"),
                UtfAllString.Create("PartitionKey"),
                UtfAllString.Create("pk"),
                UtfAllString.Create("state"),
                UtfAllString.Create("State"),
                UtfAllString.Create("status"),
                UtfAllString.Create("Status"),
                UtfAllString.Create("subscriptionId"),
                UtfAllString.Create("SubscriptionId"),
                UtfAllString.Create("tenantId"),
                UtfAllString.Create("TenantId"),
                UtfAllString.Create("timestamp"),
                UtfAllString.Create("Timestamp"),
                UtfAllString.Create("ttl"),
                UtfAllString.Create("userId"),
                UtfAllString.Create("UserId"),
            }.ToImmutableArray();

            public static int? GetSystemStringId(Utf8Span buffer)
            {
                return buffer.Length switch
                {
                    2 => GetSystemStringIdLength2(buffer.Span),
                    3 => GetSystemStringIdLength3(buffer.Span),
                    4 => GetSystemStringIdLength4(buffer.Span),
                    5 => GetSystemStringIdLength5(buffer.Span),
                    6 => GetSystemStringIdLength6(buffer.Span),
                    7 => GetSystemStringIdLength7(buffer.Span),
                    8 => GetSystemStringIdLength8(buffer.Span),
                    9 => GetSystemStringIdLength9(buffer.Span),
                    10 => GetSystemStringIdLength10(buffer.Span),
                    11 => GetSystemStringIdLength11(buffer.Span),
                    12 => GetSystemStringIdLength12(buffer.Span),
                    13 => GetSystemStringIdLength13(buffer.Span),
                    14 => GetSystemStringIdLength14(buffer.Span),
                    15 => GetSystemStringIdLength15(buffer.Span),
                    17 => GetSystemStringIdLength17(buffer.Span),
                    18 => GetSystemStringIdLength18(buffer.Span),
                    _ => null,
                };
            }

            private static int? GetSystemStringIdLength2(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(Strings[0].Utf8String.Span.Span))
                {
                    return 0;
                }

                if (buffer.SequenceEqual(Strings[1].Utf8String.Span.Span))
                {
                    return 1;
                }

                if (buffer.SequenceEqual(Strings[2].Utf8String.Span.Span))
                {
                    return 2;
                }

                if (buffer.SequenceEqual(Strings[12].Utf8String.Span.Span))
                {
                    return 12;
                }

                if (buffer.SequenceEqual(Strings[50].Utf8String.Span.Span))
                {
                    return 50;
                }

                return null;
            }
            private static int? GetSystemStringIdLength3(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(Strings[7].Utf8String.Span.Span))
                {
                    return 7;
                }

                if (buffer.SequenceEqual(Strings[13].Utf8String.Span.Span))
                {
                    return 13;
                }

                if (buffer.SequenceEqual(Strings[31].Utf8String.Span.Span))
                {
                    return 31;
                }

                if (buffer.SequenceEqual(Strings[32].Utf8String.Span.Span))
                {
                    return 32;
                }

                if (buffer.SequenceEqual(Strings[33].Utf8String.Span.Span))
                {
                    return 33;
                }

                if (buffer.SequenceEqual(Strings[43].Utf8String.Span.Span))
                {
                    return 43;
                }

                if (buffer.SequenceEqual(Strings[44].Utf8String.Span.Span))
                {
                    return 44;
                }

                if (buffer.SequenceEqual(Strings[61].Utf8String.Span.Span))
                {
                    return 61;
                }

                return null;
            }
            private static int? GetSystemStringIdLength4(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(Strings[5].Utf8String.Span.Span))
                {
                    return 5;
                }

                if (buffer.SequenceEqual(Strings[17].Utf8String.Span.Span))
                {
                    return 17;
                }

                if (buffer.SequenceEqual(Strings[21].Utf8String.Span.Span))
                {
                    return 21;
                }

                if (buffer.SequenceEqual(Strings[22].Utf8String.Span.Span))
                {
                    return 22;
                }

                if (buffer.SequenceEqual(Strings[23].Utf8String.Span.Span))
                {
                    return 23;
                }

                if (buffer.SequenceEqual(Strings[27].Utf8String.Span.Span))
                {
                    return 27;
                }

                if (buffer.SequenceEqual(Strings[38].Utf8String.Span.Span))
                {
                    return 38;
                }

                if (buffer.SequenceEqual(Strings[39].Utf8String.Span.Span))
                {
                    return 39;
                }

                return null;
            }
            private static int? GetSystemStringIdLength5(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(Strings[4].Utf8String.Span.Span))
                {
                    return 4;
                }

                if (buffer.SequenceEqual(Strings[6].Utf8String.Span.Span))
                {
                    return 6;
                }

                if (buffer.SequenceEqual(Strings[14].Utf8String.Span.Span))
                {
                    return 14;
                }

                if (buffer.SequenceEqual(Strings[15].Utf8String.Span.Span))
                {
                    return 15;
                }

                if (buffer.SequenceEqual(Strings[24].Utf8String.Span.Span))
                {
                    return 24;
                }

                if (buffer.SequenceEqual(Strings[28].Utf8String.Span.Span))
                {
                    return 28;
                }

                if (buffer.SequenceEqual(Strings[36].Utf8String.Span.Span))
                {
                    return 36;
                }

                if (buffer.SequenceEqual(Strings[51].Utf8String.Span.Span))
                {
                    return 51;
                }

                if (buffer.SequenceEqual(Strings[52].Utf8String.Span.Span))
                {
                    return 52;
                }

                return null;
            }
            private static int? GetSystemStringIdLength6(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(Strings[37].Utf8String.Span.Span))
                {
                    return 37;
                }

                if (buffer.SequenceEqual(Strings[40].Utf8String.Span.Span))
                {
                    return 40;
                }

                if (buffer.SequenceEqual(Strings[53].Utf8String.Span.Span))
                {
                    return 53;
                }

                if (buffer.SequenceEqual(Strings[54].Utf8String.Span.Span))
                {
                    return 54;
                }

                if (buffer.SequenceEqual(Strings[62].Utf8String.Span.Span))
                {
                    return 62;
                }

                if (buffer.SequenceEqual(Strings[63].Utf8String.Span.Span))
                {
                    return 63;
                }

                return null;
            }
            private static int? GetSystemStringIdLength7(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(Strings[25].Utf8String.Span.Span))
                {
                    return 25;
                }

                if (buffer.SequenceEqual(Strings[29].Utf8String.Span.Span))
                {
                    return 29;
                }

                if (buffer.SequenceEqual(Strings[34].Utf8String.Span.Span))
                {
                    return 34;
                }

                return null;
            }
            private static int? GetSystemStringIdLength8(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(Strings[10].Utf8String.Span.Span))
                {
                    return 10;
                }

                if (buffer.SequenceEqual(Strings[45].Utf8String.Span.Span))
                {
                    return 45;
                }

                if (buffer.SequenceEqual(Strings[57].Utf8String.Span.Span))
                {
                    return 57;
                }

                if (buffer.SequenceEqual(Strings[58].Utf8String.Span.Span))
                {
                    return 58;
                }

                return null;
            }
            private static int? GetSystemStringIdLength9(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(Strings[41].Utf8String.Span.Span))
                {
                    return 41;
                }

                if (buffer.SequenceEqual(Strings[42].Utf8String.Span.Span))
                {
                    return 42;
                }

                if (buffer.SequenceEqual(Strings[46].Utf8String.Span.Span))
                {
                    return 46;
                }

                if (buffer.SequenceEqual(Strings[59].Utf8String.Span.Span))
                {
                    return 59;
                }

                if (buffer.SequenceEqual(Strings[60].Utf8String.Span.Span))
                {
                    return 60;
                }

                return null;
            }
            private static int? GetSystemStringIdLength10(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(Strings[16].Utf8String.Span.Span))
                {
                    return 16;
                }

                if (buffer.SequenceEqual(Strings[19].Utf8String.Span.Span))
                {
                    return 19;
                }

                if (buffer.SequenceEqual(Strings[26].Utf8String.Span.Span))
                {
                    return 26;
                }

                return null;
            }
            private static int? GetSystemStringIdLength11(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(Strings[9].Utf8String.Span.Span))
                {
                    return 9;
                }

                return null;
            }
            private static int? GetSystemStringIdLength12(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(Strings[3].Utf8String.Span.Span))
                {
                    return 3;
                }

                if (buffer.SequenceEqual(Strings[8].Utf8String.Span.Span))
                {
                    return 8;
                }

                if (buffer.SequenceEqual(Strings[20].Utf8String.Span.Span))
                {
                    return 20;
                }

                if (buffer.SequenceEqual(Strings[47].Utf8String.Span.Span))
                {
                    return 47;
                }

                if (buffer.SequenceEqual(Strings[48].Utf8String.Span.Span))
                {
                    return 48;
                }

                if (buffer.SequenceEqual(Strings[49].Utf8String.Span.Span))
                {
                    return 49;
                }

                return null;
            }
            private static int? GetSystemStringIdLength13(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(Strings[35].Utf8String.Span.Span))
                {
                    return 35;
                }

                return null;
            }
            private static int? GetSystemStringIdLength14(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(Strings[55].Utf8String.Span.Span))
                {
                    return 55;
                }

                if (buffer.SequenceEqual(Strings[56].Utf8String.Span.Span))
                {
                    return 56;
                }

                return null;
            }
            private static int? GetSystemStringIdLength15(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(Strings[18].Utf8String.Span.Span))
                {
                    return 18;
                }

                return null;
            }
            private static int? GetSystemStringIdLength17(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(Strings[30].Utf8String.Span.Span))
                {
                    return 30;
                }

                return null;
            }
            private static int? GetSystemStringIdLength18(ReadOnlySpan<byte> buffer)
            {
                if (buffer.SequenceEqual(Strings[11].Utf8String.Span.Span))
                {
                    return 11;
                }

                return null;
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
                if (id >= SystemStrings.Strings.Length)
                {
                    systemString = default;
                    return false;
                }

                systemString = SystemStrings.Strings[id];
                return true;
            }
        }
    }
}
