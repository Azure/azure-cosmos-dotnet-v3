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
            ArgumentValidation.ThrowIfNullOrWhiteSpace(options.DataEncryptionKeyId, nameof(options.DataEncryptionKeyId));
            ArgumentValidation.ThrowIfNullOrWhiteSpace(options.EncryptionAlgorithm, nameof(options.EncryptionAlgorithm));
            ArgumentValidation.ThrowIfNull(options.PathsToEncrypt, nameof(options.PathsToEncrypt));

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
        }
    }
}
