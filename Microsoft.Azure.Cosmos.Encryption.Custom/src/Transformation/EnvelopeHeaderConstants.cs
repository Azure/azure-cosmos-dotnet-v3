// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;

    internal static class EnvelopeHeaderConstants
    {
        private static readonly byte[] MagicBytes = { 0x43, 0x45, 0xC5, 0x01, 0x56, 0x34 };

        public const byte EpochByte = 0x04;
        public const int MagicLength = 6;
        public const int FormatEpochOffset = MagicLength;
        public const int InnerTypeOffset = FormatEpochOffset + 1;
        public const int CodecOffset = InnerTypeOffset + 1;
        public const int HeaderSize = CodecOffset + 1;
        public const byte MinValidInnerTypeMarker = 2;
        public const byte MaxValidInnerTypeMarker = 7;

        public static ReadOnlySpan<byte> Magic => MagicBytes;
    }
}
#endif
