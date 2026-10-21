// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    internal readonly struct EnvelopeHeader
    {
        public EnvelopeHeader(byte innerType, CompressionCodecId codec)
        {
            this.InnerType = innerType;
            this.Codec = codec;
        }

        public byte InnerType { get; }

        public CompressionCodecId Codec { get; }
    }
}
#endif
