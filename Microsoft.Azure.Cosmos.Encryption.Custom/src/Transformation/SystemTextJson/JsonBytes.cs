// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation.SystemTextJson
{
    using System;

    internal class JsonBytes
    {
        internal byte[] Bytes { get; private set; }

        internal int Offset { get; private set; }

        internal int Length { get; private set; }

        public JsonBytes(byte[] bytes, int offset, int length)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfNegative(length);
            if (bytes.Length < offset + length)
            {
                throw new ArgumentOutOfRangeException(null, "Offset + Length > bytes.Length");
            }

            this.Bytes = bytes;
            this.Offset = offset;
            this.Length = length;
        }
    }
}
#endif