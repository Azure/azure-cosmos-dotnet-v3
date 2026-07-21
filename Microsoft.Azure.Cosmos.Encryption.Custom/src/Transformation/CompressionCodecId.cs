// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    internal enum CompressionCodecId : byte
    {
        Raw = 0,
        Deflate = 1,
        Brotli = 2,
    }
}
#endif
