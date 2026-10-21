// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;

    internal static class V4CompressionEnvelope
    {
        private const int MaximumPayloadLength = 64 * 1024 * 1024;

        private static readonly ICompressionCodecAdapter Deflate = new DeflateCompressorAdapter();
        private static readonly ICompressionCodecAdapter Brotli = new BrotliCompressorAdapter();

        public static bool IsStructurallyValidV4Envelope(ReadOnlySpan<byte> plaintext)
        {
            return TryParseFrame(plaintext, out _, out _, out _, out _);
        }

        public static (byte[] Buffer, int Length) Create(
            ReadOnlySpan<byte> payload,
            TypeMarker typeMarker,
            CompressionSettings settings,
            Func<int, int> getEncryptByteCount,
            ArrayPoolManager pool)
        {
            if (getEncryptByteCount == null)
            {
                throw new ArgumentNullException(nameof(getEncryptByteCount));
            }

            if (pool == null)
            {
                throw new ArgumentNullException(nameof(pool));
            }

            if (payload.Length > MaximumPayloadLength)
            {
                throw new ArgumentOutOfRangeException(nameof(payload));
            }

            if (payload.IsEmpty ||
                settings.Codec == CompressionCodecId.Raw ||
                payload.Length < settings.MinimumContentLength)
            {
                return CreateRaw(payload, typeMarker, pool);
            }

            // Start at half the payload size, with a 256-byte floor and a 64 KiB cap on the initial
            // pooled rent. The writer grows if the compressed output requires more space.
            using RentArrayBufferWriter compressedWriter = new (
                Math.Min(Math.Max(payload.Length / 2, 256), 64 * 1024));
            ICompressionCodecAdapter adapter = GetAdapter(settings.Codec);
            int compressedLength = adapter.Compress(payload, settings.Level, compressedWriter);
            if (compressedLength != compressedWriter.BytesWritten)
            {
                throw new InvalidOperationException("Compression adapter returned an inconsistent byte count.");
            }

            if (!CompressionHelper.IsCompressedEnvelopeStrictlySmaller(
                payload.Length,
                compressedLength,
                getEncryptByteCount))
            {
                return CreateRaw(payload, typeMarker, pool);
            }

            int rawLengthBytes = UleB128.GetEncodedLength((uint)payload.Length);
            int compressedLengthBytes = UleB128.GetEncodedLength((uint)compressedLength);
            int envelopeLength = checked(
                EnvelopeHeaderConstants.HeaderSize +
                rawLengthBytes +
                compressedLengthBytes +
                compressedLength);
            byte[] envelope = pool.Rent(envelopeLength);
            EnvelopeHeaderWriter.Write(envelope, (byte)typeMarker, settings.Codec);
            int offset = EnvelopeHeaderConstants.HeaderSize;
            offset += UleB128.Write((uint)payload.Length, envelope.AsSpan(offset));
            offset += UleB128.Write((uint)compressedLength, envelope.AsSpan(offset));
            compressedWriter.WrittenSpan.CopyTo(envelope.AsSpan(offset, compressedLength));
            return (envelope, envelopeLength);
        }

        public static DecodedEnvelope Decode(
            byte[] plaintext,
            int plaintextLength,
            byte outerTypeMarker,
            DecompressionBudget budget,
            ArrayPoolManager pool)
        {
            if (plaintext == null)
            {
                throw new ArgumentNullException(nameof(plaintext));
            }

            if (plaintextLength < 0 || plaintextLength > plaintext.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(plaintextLength));
            }

            if (budget == null)
            {
                throw new ArgumentNullException(nameof(budget));
            }

            if (pool == null)
            {
                throw new ArgumentNullException(nameof(pool));
            }

            ReadOnlySpan<byte> envelope = plaintext.AsSpan(0, plaintextLength);
            if (!TryParseFrame(
                envelope,
                out EnvelopeHeader header,
                out int payloadOffset,
                out int rawLength,
                out int payloadLength))
            {
                throw new InvalidOperationException("Encrypted envelope failed integrity validation.");
            }

            if (header.InnerType != outerTypeMarker)
            {
                throw new InvalidOperationException(
                    "Envelope inner type marker does not match the outer type marker.");
            }

            TypeMarker typeMarker = (TypeMarker)header.InnerType;
            if (header.Codec == CompressionCodecId.Raw)
            {
                ChargeBudget(budget, rawLength);
                return new DecodedEnvelope(typeMarker, plaintext, payloadOffset, rawLength);
            }

            ChargeBudget(budget, rawLength);
            byte[] decoded = pool.Rent(rawLength);
            ReadOnlyMemory<byte> compressedBody = new (
                plaintext,
                payloadOffset,
                payloadLength);
            int written = GetAdapter(header.Codec).Decompress(
                compressedBody,
                decoded.AsSpan(0, rawLength));
            if (written != rawLength)
            {
                throw new InvalidOperationException(
                    "Decompressed content did not match the declared length.");
            }

            return new DecodedEnvelope(typeMarker, decoded, 0, rawLength);
        }

        private static (byte[] Buffer, int Length) CreateRaw(
            ReadOnlySpan<byte> payload,
            TypeMarker typeMarker,
            ArrayPoolManager pool)
        {
            int envelopeLength = checked(EnvelopeHeaderConstants.HeaderSize + payload.Length);
            byte[] envelope = pool.Rent(envelopeLength);
            EnvelopeHeaderWriter.Write(envelope, (byte)typeMarker, CompressionCodecId.Raw);
            payload.CopyTo(envelope.AsSpan(EnvelopeHeaderConstants.HeaderSize, payload.Length));
            return (envelope, envelopeLength);
        }

        private static ICompressionCodecAdapter GetAdapter(CompressionCodecId codec)
        {
            return codec switch
            {
                CompressionCodecId.Deflate => Deflate,
                CompressionCodecId.Brotli => Brotli,
                _ => throw new InvalidOperationException("Encrypted envelope failed integrity validation."),
            };
        }

        private static bool TryParseFrame(
            ReadOnlySpan<byte> plaintext,
            out EnvelopeHeader header,
            out int payloadOffset,
            out int rawLength,
            out int payloadLength)
        {
            header = default;
            payloadOffset = 0;
            rawLength = 0;
            payloadLength = 0;

            if (!EnvelopeHeaderReader.TryReadV4Header(plaintext, out header, out int bodyOffset))
            {
                return false;
            }

            TypeMarker typeMarker = (TypeMarker)header.InnerType;
            if (header.Codec == CompressionCodecId.Raw)
            {
                int bodyLength = plaintext.Length - bodyOffset;
                if (!IsPlausibleSerializedLength(typeMarker, bodyLength))
                {
                    return false;
                }

                payloadOffset = bodyOffset;
                rawLength = bodyLength;
                payloadLength = bodyLength;
                return true;
            }

            ReadOnlySpan<byte> body = plaintext.Slice(bodyOffset);
            if (!UleB128.TryRead(body, out uint rawLengthValue, out int rawLengthBytes) ||
                !UleB128.TryRead(
                    body.Slice(rawLengthBytes),
                    out uint compressedLengthValue,
                    out int compressedLengthBytes) ||
                rawLengthValue == 0 ||
                rawLengthValue > MaximumPayloadLength ||
                compressedLengthValue == 0 ||
                compressedLengthValue > MaximumPayloadLength ||
                !IsPlausibleSerializedLength(typeMarker, (int)rawLengthValue))
            {
                return false;
            }

            int compressedOffset = rawLengthBytes + compressedLengthBytes;
            if (body.Length - compressedOffset != (int)compressedLengthValue)
            {
                return false;
            }

            payloadOffset = bodyOffset + compressedOffset;
            rawLength = (int)rawLengthValue;
            payloadLength = (int)compressedLengthValue;
            return true;
        }

        private static bool IsPlausibleSerializedLength(TypeMarker typeMarker, int length)
        {
            return typeMarker switch
            {
                TypeMarker.String => length >= 0 && length <= MaximumPayloadLength,
                TypeMarker.Long or TypeMarker.Double or TypeMarker.Boolean => length == 8,
                TypeMarker.Array or TypeMarker.Object => length >= 2 && length <= MaximumPayloadLength,
                _ => false,
            };
        }

        private static void ChargeBudget(DecompressionBudget budget, int length)
        {
            if (length < 0 || length > MaximumPayloadLength)
            {
                throw new InvalidOperationException("Encrypted envelope failed integrity validation.");
            }

            if (!budget.TryCharge(length))
            {
                throw new InvalidOperationException(
                    "Aggregate decompressed content exceeds the request budget.");
            }
        }
    }
}
#endif
