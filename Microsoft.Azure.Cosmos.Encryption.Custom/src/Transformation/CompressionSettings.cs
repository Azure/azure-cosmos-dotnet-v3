// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;

    internal readonly struct CompressionSettings
    {
        private const int MaximumContentLength = 64 * 1024 * 1024;

        public CompressionSettings(
            CompressionCodecId codec,
            CompressionLevelSetting level,
            int minimumContentLength)
        {
            if (codec != CompressionCodecId.Raw &&
                codec != CompressionCodecId.Deflate &&
                codec != CompressionCodecId.Brotli)
            {
                throw new ArgumentException("Unsupported compression codec.", nameof(codec));
            }

            if (level != CompressionLevelSetting.Fastest &&
                level != CompressionLevelSetting.Balanced &&
                level != CompressionLevelSetting.SmallestSize)
            {
                throw new ArgumentException("Unsupported compression level.", nameof(level));
            }

            if (minimumContentLength < 0 || minimumContentLength > MaximumContentLength)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumContentLength));
            }

            this.Codec = codec;
            this.Level = level;
            this.MinimumContentLength = minimumContentLength;
        }

        public CompressionCodecId Codec { get; }

        public CompressionLevelSetting Level { get; }

        public int MinimumContentLength { get; }
    }
}
#endif
