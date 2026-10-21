// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;

    internal static class EnvelopeHeaderWriter
    {
        public static void Write(Span<byte> destination, byte innerType, CompressionCodecId codec)
        {
            if (destination.Length < EnvelopeHeaderConstants.HeaderSize)
            {
                throw new ArgumentException("Destination is too small.", nameof(destination));
            }

            if (innerType < EnvelopeHeaderConstants.MinValidInnerTypeMarker ||
                innerType > EnvelopeHeaderConstants.MaxValidInnerTypeMarker)
            {
                throw new ArgumentOutOfRangeException(nameof(innerType));
            }

            if (codec != CompressionCodecId.Raw &&
                codec != CompressionCodecId.Deflate &&
                codec != CompressionCodecId.Brotli)
            {
                throw new ArgumentOutOfRangeException(nameof(codec));
            }

            EnvelopeHeaderConstants.Magic.CopyTo(destination);
            destination[EnvelopeHeaderConstants.FormatEpochOffset] = EnvelopeHeaderConstants.EpochByte;
            destination[EnvelopeHeaderConstants.InnerTypeOffset] = innerType;
            destination[EnvelopeHeaderConstants.CodecOffset] = (byte)codec;
        }
    }
}
#endif
