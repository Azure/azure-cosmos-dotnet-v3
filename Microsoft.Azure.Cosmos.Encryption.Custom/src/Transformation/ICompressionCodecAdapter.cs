// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Buffers;

    internal interface ICompressionCodecAdapter
    {
        int Compress(
            ReadOnlySpan<byte> source,
            CompressionLevelSetting level,
            IBufferWriter<byte> destination);

        int Decompress(
            ReadOnlyMemory<byte> source,
            Span<byte> destination);
    }
}
#endif
