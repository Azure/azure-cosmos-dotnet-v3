// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Adapter interface that abstracts JSON processing for MDE encryption operations.
/// Enables pluggable JSON serialization implementations (Newtonsoft.Json, System.Text.Json streaming)
/// while providing a unified API for encrypt/decrypt operations in MdeEncryptionProcessor.
/// </summary>
/// <remarks>
/// Implementations:
/// - NewtonsoftAdapter: Uses Newtonsoft.Json with JObject for in-memory processing
/// - SystemTextJsonStreamAdapter: Uses System.Text.Json Utf8JsonReader/Writer for streaming (NET8+)
///
/// The adapter pattern allows runtime selection of JSON processors via EncryptionOptions.JsonProcessor
/// or RequestOptions property bag overrides, with lazy instantiation and caching in MdeEncryptionProcessor.
/// </remarks>
internal interface IMdeJsonProcessorAdapter
{
    /// <summary>
    /// Encrypts a JSON stream and returns the encrypted result as a new stream.
    /// </summary>
    /// <param name="input">Input stream containing JSON document to encrypt.</param>
    /// <param name="encryptor">Encryptor to use for encryption operations.</param>
    /// <param name="options">Encryption options including paths to encrypt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>New stream containing encrypted JSON document.</returns>
    Task<Stream> EncryptAsync(Stream input, Encryptor encryptor, EncryptionOptions options, CancellationToken cancellationToken);

    /// <summary>
    /// Encrypts a JSON stream and writes the encrypted result to an output stream.
    /// Only supported by SystemTextJsonStreamAdapter for true streaming encryption.
    /// </summary>
    /// <param name="input">Input stream containing JSON document to encrypt.</param>
    /// <param name="output">Output stream to write encrypted JSON document to.</param>
    /// <param name="encryptor">Encryptor to use for encryption operations.</param>
    /// <param name="options">Encryption options including paths to encrypt.</param>
    /// <param name="diagnosticsContext">Diagnostics context for telemetry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="NotSupportedException">Thrown by NewtonsoftAdapter as it requires in-memory processing.</exception>
    Task EncryptAsync(Stream input, Stream output, Encryptor encryptor, EncryptionOptions options, CosmosDiagnosticsContext diagnosticsContext, CancellationToken cancellationToken);

    /// <summary>
    /// Decrypts a JSON stream and returns the decrypted result as a new stream along with decryption context.
    /// </summary>
    /// <param name="input">Input stream containing potentially encrypted JSON document.</param>
    /// <param name="encryptor">Encryptor to use for decryption operations.</param>
    /// <param name="diagnosticsContext">Diagnostics context for telemetry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Tuple containing:
    /// - Stream: New stream with decrypted JSON (or original input if no MDE encryption detected)
    /// - DecryptionContext: Metadata about decryption operation (null if document wasn't encrypted)
    /// </returns>
    Task<(Stream, DecryptionContext)> DecryptAsync(Stream input, Encryptor encryptor, CosmosDiagnosticsContext diagnosticsContext, CancellationToken cancellationToken);

    /// <summary>
    /// Decrypts a JSON stream and writes the decrypted result to an output stream, returning decryption context.
    /// </summary>
    /// <param name="input">Input stream containing potentially encrypted JSON document.</param>
    /// <param name="output">Output stream to write decrypted JSON document to.</param>
    /// <param name="encryptor">Encryptor to use for decryption operations.</param>
    /// <param name="diagnosticsContext">Diagnostics context for telemetry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// DecryptionContext with metadata about decryption operation (null if document wasn't encrypted).
    /// If null is returned, input stream position is reset to 0 if seekable.
    /// </returns>
    Task<DecryptionContext> DecryptAsync(Stream input, Stream output, Encryptor encryptor, CosmosDiagnosticsContext diagnosticsContext, CancellationToken cancellationToken);
}