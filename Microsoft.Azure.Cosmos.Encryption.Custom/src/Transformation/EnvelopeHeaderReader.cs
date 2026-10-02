// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;

    internal static class EnvelopeHeaderReader
    {
        public static bool TryReadV4Header(
            ReadOnlySpan<byte> plaintext,
            out EnvelopeHeader header,
            out int consumed)
        {
            header = default;
            consumed = 0;

            if (plaintext.Length < EnvelopeHeaderConstants.HeaderSize ||
                !plaintext.StartsWith(EnvelopeHeaderConstants.Magic))
            {
                return false;
            }

            if (plaintext[EnvelopeHeaderConstants.FormatEpochOffset] != EnvelopeHeaderConstants.EpochByte)
            {
                return false;
            }

            byte innerType = plaintext[EnvelopeHeaderConstants.InnerTypeOffset];
            if (innerType < EnvelopeHeaderConstants.MinValidInnerTypeMarker ||
                innerType > EnvelopeHeaderConstants.MaxValidInnerTypeMarker)
            {
                return false;
            }

            byte codec = plaintext[EnvelopeHeaderConstants.CodecOffset];
            if (codec != (byte)CompressionCodecId.Raw &&
                codec != (byte)CompressionCodecId.Deflate &&
                codec != (byte)CompressionCodecId.Brotli)
            {
                return false;
            }

            header = new EnvelopeHeader(innerType, (CompressionCodecId)codec);
            consumed = EnvelopeHeaderConstants.HeaderSize;
            return true;
        }
    }
}
#endif
