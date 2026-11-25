// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    internal enum CompressionLevelSetting : byte
    {
        Fastest = 0,
        Balanced = 1,
        SmallestSize = 2,
    }
}
#endif
