// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;

using System.IO;
using System.Threading;
using System.Threading.Tasks;

internal interface IMdeJsonProcessorAdapter
{
    Task<Stream> EncryptAsync(Stream input, Encryptor encryptor, EncryptionOptions options, CancellationToken cancellationToken);

    Task EncryptAsync(Stream input, Stream output, Encryptor encryptor, EncryptionOptions options, CancellationToken cancellationToken);

    Task<(Stream, DecryptionContext)> DecryptAsync(Stream input, Encryptor encryptor, CosmosDiagnosticsContext diagnosticsContext, CancellationToken cancellationToken);

    Task<DecryptionContext> DecryptAsync(Stream input, Stream output, Encryptor encryptor, CosmosDiagnosticsContext diagnosticsContext, CancellationToken cancellationToken);
}
#endif