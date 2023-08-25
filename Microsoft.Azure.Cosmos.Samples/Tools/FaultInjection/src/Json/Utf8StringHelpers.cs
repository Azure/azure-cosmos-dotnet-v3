// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Runtime.InteropServices;
    using System.Text;

    internal static class Utf8StringHelpers
    {
        public static string ToString(ReadOnlyMemory<byte> buffer)
        {
            string jsonText;
            if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> bufferSegment))
            {
                jsonText = Encoding.UTF8.GetString(bufferSegment.Array, bufferSegment.Offset, buffer.Length);
            }
            else
            {
                unsafe
                {
                    ReadOnlySpan<byte> result = buffer.Span;
                    fixed (byte* bytePointer = result)
                    {
                        jsonText = Encoding.UTF8.GetString(bytePointer, result.Length);
                    }
                }
            }

            return jsonText;
        }
    }
}
