Preview features are treated as a separate branch and will not be included in the official release until the feature is ready. Each preview release lists all the additional features that are enabled.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

### Unreleased

#### Updates
- [#5903](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5903) Optimizes legacy-algorithm detection on the `JsonProcessor.Stream` decrypt opt-in path. Replaces the `Newtonsoft.Json.Linq.JObject` peek (which allocated a full DOM and an `EncryptionPropertiesWrapper` on every call) with a `System.Text.Json.Utf8JsonReader`-based detector that classifies the document without allocating. Documents that classify as MDE or unencrypted route directly to the streaming MDE processor; documents that classify as legacy AE-AES, malformed, or come from async-only streams continue to fall through to the original `JObject` peek path so backwards compatibility is byte-for-byte preserved. 37 parity tests assert identical observable behaviour vs the non-opt-in path on valid inputs.

#### Fixes (this PR)
- Detector buffer rented from `ArrayPool<byte>.Shared` for non-`MemoryStream` inputs is now returned with `clearArray: true`, matching the package-wide convention used by `ArrayPoolManager`, `RentArrayBufferWriter`, and `JsonArrayPool`. The detector reads plaintext document bytes including encrypted payloads, so the previous `clearArray: false` return would have left those bytes visible to subsequent tenants of the shared pool.
- Stream-opt-in legacy-fallthrough no longer mutates `RequestOptions.Properties` to strip the `JsonProcessor` entry. The previous "strip-then-restore-in-finally" pattern was not thread-safe: a caller sharing one `ItemRequestOptions` across concurrent decrypt calls could have a second call observe the stripped state during the first call's `await` window and silently route to the wrong adapter. The Newtonsoft-adapter override is now passed explicitly via a new internal `MdeEncryptionProcessor.DecryptAsync` overload so caller-owned state is never mutated.
- Stream-opt-in detector no longer pre-buffers the entire payload for non-`MemoryStream` inputs (e.g. `FileStream`, `NetworkStream`, or custom wrappers). Previously the detector synchronously read up to `int.MaxValue` bytes into a rented array before classifying — defeating the streaming property of the input and risking OOM on multi-MB documents. The detector now short-circuits to `Unknown` for any non-`MemoryStream` input and falls through to the original JObject-peek path, which handles non-`MemoryStream` inputs via incremental `StreamReader` / `JsonTextReader` reads. The fast path remains in effect for the common Cosmos SDK case (request-response payloads backed by `MemoryStream`).
- Stream-opt-in detector no longer accesses `Stream.Position` or `Stream.Length` on non-`MemoryStream` inputs. Streams that throw `NotSupportedException` on metadata reads (e.g. live `NetworkStream`, async-only browser streams) previously threw out of `TryDetectAlgorithm` before any catch could engage; they now short-circuit to `Unknown` and route through the JObject-peek path.
- `EncryptionProperties.EncryptionFormatVersion` is annotated with `[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]` on `net8.0` so the `System.Text.Json` deserializer accepts `_ei._ef = "3"` (JSON-numeric-string) identically to Newtonsoft's permissive coercion. Documents written with the string form now decrypt on both adapters. The attribute is guarded `#if NET8_0_OR_GREATER` because the `JsonNumberHandling` type does not exist in the netstandard2.0 build's pinned `System.Text.Json` version; the streaming adapter is also net8.0-only, so no observable difference exists on netstandard2.0.
- `SystemTextJsonStreamAdapter.DecryptAsync(Stream, Encryptor, ...)` now disposes the caller's input stream on successful decrypt, matching `NewtonsoftAdapter.DecryptAsync`'s documented stream-ownership contract. Callers that opt into `JsonProcessor.Stream` no longer leak the input handle on every successful decrypt.
- `StreamProcessor` encrypt path now re-encodes escaped property names and string values through `Utf8JsonWriter`'s canonical escape handling instead of copying the raw `Utf8JsonReader.ValueSpan` bytes verbatim. This fixes a class of round-trip bugs where source JSON containing `\uXXXX` escapes or escaped metacharacters in property names / top-level string values (outside whole-property encryption targets) was re-emitted with the raw escape sequences doubled (e.g. `\u0041` round-tripped as the 6-character literal `\u0041` instead of `A`). The `[Ignore]`-flagged regression-tracker tests for these two cases in `CrossAdapterEncryptDecryptParityTests` and `CrossAdapterAdversarialParityTests` have been un-ignored and now run as positive regressions.

  **Documented observable difference for `JsonProcessor.Stream` opt-in callers**: when an input is *malformed* (e.g. a corrupt `_ed` ciphertext, leading garbage bytes before the JSON document, or a document that the strict `System.Text.Json` parser rejects), the streaming MDE adapter surfaces a `System.Text.Json.JsonException` where the default Newtonsoft adapter would have surfaced a `Newtonsoft.Json.JsonException` or `System.FormatException`. **Both paths reject the same set of inputs** — only the exception **type** differs because the two adapters fail at different layers (STJ model deserializer vs Newtonsoft base64 decoder). This is a property of the underlying `JsonProcessor.Stream` adapter selection, not of this PR, and exists on every release that includes the `JsonProcessor.Stream` opt-in. Callers that wrap decryption in `catch` blocks should match on `JsonException` broadly or catch `Exception` and inspect the message.

  **Known pre-existing `JsonProcessor.Stream` *encrypt* limitations surfaced by this PR's cross-adapter parity testing (NOT introduced by this PR; remaining after the encrypt-path escape fix above):** the Stream encrypter copies source bytes verbatim for whole-property encryption targets that are arrays or dictionaries, rather than normalising through a canonical JSON writer. Two observable consequences for the `JsonProcessor.Stream` *encrypt* path only when the encryption target is itself an array or a dictionary (the Newtonsoft encrypter handles both cases correctly, and either decrypt path can read the resulting documents):
    1. Dictionary keys or values containing JSON metacharacters (`"`, `\`) round-trip with an extra leading backslash when the dictionary is itself a whole-property encryption target.
    2. String values inside arrays or dictionaries that are written using `\uXXXX` escape sequences are recovered as the literal escape text (e.g. `"\u0041"` round-trips as the 6-character string `\u0041` instead of `"A"`) when the array or dictionary is itself a whole-property encryption target.
  Top-level string fields and nested property names outside whole-property encryption targets are unaffected (the escape-handling fix above covers those). Both remaining limitations are captured as `[Ignore]`-flagged regression-tracker tests in `CrossAdapterEncryptDecryptParityTests` and `CrossAdapterAdversarialParityTests`; remove the `[Ignore]` once the Stream encrypter is fixed to normalise nested-container values.

  **Known pre-existing `JsonProcessor.Stream` *decrypt* adapter divergences not addressed by this PR:** Newtonsoft's `JsonSerializer` is more permissive than `System.Text.Json` on several numeric and encoding edge cases. The following observable differences exist on master, were verified by audit subagents during this PR, and have NOT been fixed because they require either tightening the Newtonsoft path (a breaking change for documents in production) or loosening the strict Stream path (a JSON-contract change). They are documented here so callers opting into `JsonProcessor.Stream` can discover them without reading test source:
    - **Non-finite numbers** (`NaN`, `+Infinity`, `-Infinity`): Newtonsoft accepts them on both encrypt and decrypt; the streaming encrypter rejects them with `InvalidOperationException` to keep JSON-contract compatibility (RFC 8259 forbids these tokens). A document Newtonsoft-encrypted with a non-finite sensitive number cannot be decrypted via the Stream opt-in path.
    - **Integers larger than `Int64.MaxValue`** (e.g. `BigInteger`): Newtonsoft decrypt throws when materialising the JSON number; Stream decrypt silently coerces via `double` (lossy). A document containing an out-of-range integer in a sensitive numeric field will decrypt to a slightly different value on the two paths.
    - **UTF-8 BOM** at the start of the document: Newtonsoft accepts it; the streaming `Utf8JsonReader` rejects it. A document prefixed with BOM bytes will decrypt on Newtonsoft but throw `System.Text.Json.JsonException` on Stream.
    - **Lone UTF-16 surrogates** in string values: Newtonsoft accepts them; the streaming adapter's `Utf8JsonWriter` rejects them on the encrypt-side or recovery-side write.
    - **Exponent / scale lexical form of decimals** (e.g. `1E+3` vs `1000`): both paths recover the same numeric value, but the recovered JSON representation may differ.
    - **Whitespace-padded numeric string `_ei._ef`** (e.g. `"3 "`, `" 3 "`): Newtonsoft trims surrounding whitespace before numeric coercion; the streaming adapter (which uses `JsonNumberHandling.AllowReadingFromString`) requires the strict `NumberStyles.Float` form. Real encrypters always write the canonical integer `3`, so this is only reachable on tampered/hand-crafted documents.
    - **Non-string `_ei._en`** (DEK ID written as JSON number or boolean, e.g. `_en = 123` or `_en = true`): Newtonsoft coerces non-string scalars to their string representation; the streaming adapter rejects the type mismatch. Real encrypters always write `_en` as the configured DEK ID string, so this is only reachable on tampered/hand-crafted documents.
    - **Non-string entries in `_ei._ep`** (encrypted-paths array containing JSON numbers, e.g. `[123]` or `["/path", 456]`): Newtonsoft coerces numeric entries to their string representation; the streaming adapter rejects the type mismatch on the first non-string element. Real encrypters always write `_ep` as an array of JSON-path strings, so this is only reachable on tampered/hand-crafted documents.
    - **Duplicate JSON keys inside `_ei`** (e.g. `_ef` appearing twice): Newtonsoft is effectively last-wins (it applies each duplicate in document order and keeps the last value); the streaming `Utf8JsonReader`-driven deserializer surfaces parse-time errors on the FIRST invalid duplicate before a later valid duplicate could shadow it. Real encrypters never write duplicate keys.
  Callers that must guarantee byte-for-byte identical recovery across both adapters should restrict sensitive numeric fields to finite `Int64`-range integers and finite `double`-range floats, and ensure documents do not begin with a BOM. All `_ei`-shape divergences listed above (whitespace-padded `_ef`, non-string `_en`, non-string `_ep` entries, duplicate keys) are reachable only on tampered or hand-crafted documents; documents produced by `EncryptionProcessor.EncryptAsync` with either `JsonProcessor.Newtonsoft` or `JsonProcessor.Stream` always use canonical wire-format values that round-trip identically across both adapters.

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