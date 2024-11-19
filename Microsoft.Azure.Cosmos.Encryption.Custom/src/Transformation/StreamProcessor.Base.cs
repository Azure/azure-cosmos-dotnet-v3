// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Text.Json;
    using Microsoft.Data.Encryption.Cryptography.Serializers;

    internal partial class StreamProcessor
    {
        private const int ControlledElementsDepth = 1;
        private static readonly SqlBitSerializer SqlBoolSerializer = new ();
        private static readonly SqlFloatSerializer SqlDoubleSerializer = new ();
        private static readonly SqlBigIntSerializer SqlLongSerializer = new ();
        private static readonly JsonReaderOptions JsonReaderOptions = new () { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

        internal static int InitialBufferSize { get; set; } = 16384;

        internal MdeEncryptor Encryptor { get; set; } = new MdeEncryptor();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ReadOnlySpan<byte> UnescapeValue(ref Utf8JsonReader reader, ArrayPoolManager arrayPoolManager)
        {
            if (!reader.ValueIsEscaped)
            {
                return reader.ValueSpan;
            }

            Span<byte> bytes = arrayPoolManager.Rent(reader.ValueSpan.Length);
            int size = reader.CopyString(bytes);

            return bytes[..size];
        }
    }
}
#endif