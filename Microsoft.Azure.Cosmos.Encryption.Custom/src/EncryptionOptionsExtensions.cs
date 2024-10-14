// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;

    internal static class EncryptionOptionsExtensions
    {
        internal static void Validate(this EncryptionOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.DataEncryptionKeyId))
            {
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
                throw new ArgumentNullException(nameof(options.DataEncryptionKeyId));
#pragma warning restore CA2208 // Instantiate argument exceptions correctly
            }

            if (string.IsNullOrWhiteSpace(options.EncryptionAlgorithm))
            {
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
                throw new ArgumentNullException(nameof(options.EncryptionAlgorithm));
#pragma warning restore CA2208 // Instantiate argument exceptions correctly
            }

            if (options.PathsToEncrypt == null)
            {
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
                throw new ArgumentNullException(nameof(options.PathsToEncrypt));
#pragma warning restore CA2208 // Instantiate argument exceptions correctly
            }

            options.CompressionOptions?.Validate();
        }

        internal static void Validate(this CompressionOptions options)
        {
            if (options.MinimalCompressedLength < 0)
            {
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
                throw new ArgumentOutOfRangeException(nameof(options.MinimalCompressedLength));
#pragma warning restore CA2208 // Instantiate argument exceptions correctly
            }
        }
    }
}
