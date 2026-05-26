Preview features are treated as a separate branch and will not be included in the official release until the feature is ready. Each preview release lists all the additional features that are enabled.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

### Unreleased

#### Updates
- [#5903](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5903) Optimizes legacy-algorithm detection on the `JsonProcessor.Stream` decrypt opt-in path. Replaces the `Newtonsoft.Json.Linq.JObject` peek (which allocated a full DOM and an `EncryptionPropertiesWrapper` on every call) with a `System.Text.Json.Utf8JsonReader`-based detector that classifies the document without allocating. Documents that classify as MDE or unencrypted route directly to the streaming MDE processor; documents that classify as legacy AE-AES, malformed, or come from async-only streams continue to fall through to the original `JObject` peek path so backwards compatibility is byte-for-byte preserved. 37 parity tests assert identical observable behaviour vs the non-opt-in path on valid inputs.

#### Fixes (this PR)
- Detector buffer rented from `ArrayPool<byte>.Shared` for non-`MemoryStream` inputs is now returned with `clearArray: true`, matching the package-wide convention used by `ArrayPoolManager`, `RentArrayBufferWriter`, and `JsonArrayPool`. The detector reads plaintext document bytes including encrypted payloads, so the previous `clearArray: false` return would have left those bytes visible to subsequent tenants of the shared pool.
- Stream-opt-in legacy-fallthrough no longer mutates `RequestOptions.Properties` to strip the `JsonProcessor` entry. The previous "strip-then-restore-in-finally" pattern was not thread-safe: a caller sharing one `ItemRequestOptions` across concurrent decrypt calls could have a second call observe the stripped state during the first call's `await` window and silently route to the wrong adapter. The Newtonsoft-adapter override is now passed explicitly via a new internal `MdeEncryptionProcessor.DecryptAsync` overload so caller-owned state is never mutated.

  **Documented observable difference for `JsonProcessor.Stream` opt-in callers**: when an input is *malformed* (e.g. a corrupt `_ed` ciphertext, leading garbage bytes before the JSON document, or a document that the strict `System.Text.Json` parser rejects), the streaming MDE adapter surfaces a `System.Text.Json.JsonException` where the default Newtonsoft adapter would have surfaced a `Newtonsoft.Json.JsonException` or `System.FormatException`. **Both paths reject the same set of inputs** — only the exception **type** differs because the two adapters fail at different layers (STJ model deserializer vs Newtonsoft base64 decoder). This is a property of the underlying `JsonProcessor.Stream` adapter selection, not of this PR, and exists on every release that includes the `JsonProcessor.Stream` opt-in. Callers that wrap decryption in `catch` blocks should match on `JsonException` broadly or catch `Exception` and inspect the message.

  **Known pre-existing `JsonProcessor.Stream` *encrypt* limitations surfaced by this PR's cross-adapter parity testing (NOT introduced by this PR):** the Stream encrypter copies source bytes verbatim for whole-property encryption targets that are arrays or dictionaries, rather than normalising through a canonical JSON writer. Two observable consequences for the `JsonProcessor.Stream` *encrypt* path only (the Newtonsoft encrypter handles both cases correctly, and either decrypt path can read the resulting documents):
    1. Dictionary keys or values containing JSON metacharacters (`"`, `\`) round-trip with an extra leading backslash.
    2. String values inside arrays or dictionaries that are written using `\uXXXX` escape sequences are recovered as the literal escape text (e.g. `"\u0041"` round-trips as the 6-character string `\u0041` instead of `"A"`).
  Top-level string fields are unaffected. Both limitations are captured as `[Ignore]`-flagged regression-tracker tests in `CrossAdapterEncryptDecryptParityTests` and `CrossAdapterAdversarialParityTests`; remove the `[Ignore]` once the Stream encrypter is fixed to normalise nested-container values.

  **Known pre-existing `JsonProcessor.Stream` *decrypt* adapter divergences surfaced by this PR's audit-derived parity probes (NOT introduced by this PR — both predate `LegacyAlgorithmDetector` and live in the `SystemTextJsonStreamAdapter` and `NewtonsoftAdapter` themselves):**
    3. `_ei._ef` as a JSON-numeric-string (`"3"`) instead of a JSON number (`3`): the Newtonsoft decrypt path silently accepts the string and coerces it via `JsonSerializer` to the integer value, completing the decrypt as if `_ef = 3` had been on the wire. The Stream decrypt path strictly rejects the type mismatch and throws a `System.Text.Json.JsonException`. A document with `_ef = "3"` therefore decrypts on the default path but throws on the Stream opt-in path.
    4. Input-stream disposal on successful MDE decrypt: the Newtonsoft adapter (`NewtonsoftAdapter.DecryptAsync`) disposes the caller's input stream before returning; the Stream adapter (`SystemTextJsonStreamAdapter.DecryptAsync`) returns without disposing it. Callers that opt into `JsonProcessor.Stream` and rely on the documented "input stream is disposed" lifecycle of the Newtonsoft path will leak the input handle on every successful decrypt.
  Both limitations are captured as `[Ignore]`-flagged regression-tracker tests in `CrossAdapterAuditFindingsTests` (`WireFormat_EfAsStringTypedThree_AdapterDivergence_KnownLimitation` and `StreamOptIn_MdeDecrypt_InputDisposalMismatchWithNewtonsoft_KnownLimitation`); remove the `[Ignore]` once the respective adapters are aligned.

### <a name="1.0.0-preview08"/> [1.0.0-preview08](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom/1.0.0-preview08) - 2024-09-11

#### Updates
- [#4673]: Updates `Microsoft.Data.Encryption.Cryptography` dependency to v1.2.0.

### <a name="1.0.0-preview07"/> [1.0.0-preview07](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom/1.0.0-preview07) - 2024-06-12

#### Fixes 
- [#4546](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/4546) Updates package reference Microsoft.Azure.Cosmos to version 3.41.0-preview and 3.40.0 for preview and stable version support.

### <a name="1.0.0-preview06"/> [1.0.0-preview06](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom/1.0.0-preview06) - 2023-06-28

#### Fixes 
- [#3956](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3956) Updates package reference Microsoft.Azure.Cosmos to version 3.35.1-preview.

### <a name="1.0.0-preview05"/> [1.0.0-preview05](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom/1.0.0-preview05) - 2023-04-27

#### Fixes 
- [#3809](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3809) Adds api FetchDataEncryptionKeyWithoutRawKeyAsync and FetchDataEncryptionKey to get DEK without and with raw key respectively.

### <a name="1.0.0-preview04"/> [1.0.0-preview04](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom/1.0.0-preview04) - 2022-08-16

#### Fixes 
- [#3386](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3386) Fixes custom serializer issue with DataEncryptionKeyContainer operations.

### <a name="1.0.0-preview03"/> [1.0.0-preview03](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom/1.0.0-preview03) - 2022-04-15
- [#3145](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3145) Adds dependency on latest Microsoft.Azure.Cosmos preview (3.26.0-preview).

### <a name="1.0.0-preview02"/> [1.0.0-preview02](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom/1.0.0-preview02) - 2021-10-29

#### Fixes 
- [#2834](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2834) Adds fix for deserialization issue for invalid date type.


### <a name="1.0.0-preview"/> [1.0.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom/1.0.0-preview) - 2021-10-20
- First preview of custom client-side encryption feature. See https://aka.ms/CosmosClientEncryption for more information on client-side encryption support in Azure Cosmos DB.