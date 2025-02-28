// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

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

            if (options.PathsToEncrypt is not HashSet<string> && options.PathsToEncrypt.Distinct().Count() != options.PathsToEncrypt.Count())
            {
                throw new InvalidOperationException("Duplicate paths in PathsToEncrypt passed via EncryptionOptions.");
            }

            foreach (string path in options.PathsToEncrypt)
            {
                if (string.IsNullOrWhiteSpace(path) || path[0] != '/' || path.IndexOf('/', 1) != -1)
                {
                    throw new InvalidOperationException($"Invalid path {path ?? string.Empty}, {nameof(options.PathsToEncrypt)}");
                }

                if (path.AsSpan(1).Equals("id".AsSpan(), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"{nameof(options.PathsToEncrypt)} includes a invalid path: '{path}'.");
                }
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
