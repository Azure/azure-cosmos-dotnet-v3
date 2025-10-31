# Pull Request: System.Text.Json Streaming API for Client Encryption

Fixes #4678

## Description

This PR adds support for the System.Text.Json streaming API in client-side encryption operations, allowing users to opt-in to streaming JSON processing for improved performance and reduced memory allocations when working with large encrypted documents.

## Key Changes

- **New opt-in mechanism** via `RequestOptions.Properties` to enable Stream processor
- **Dual JSON processor support**: Newtonsoft.Json (default) and System.Text.Json streaming (opt-in)
- **Adapter pattern implementation** for clean separation between processors
- **Enhanced diagnostics** with scope tracking to monitor which processor is used
- **Backward compatible**: Existing code continues to work without changes

---

## Usage

### Option 1: Opt-in via RequestOptions (Read Operations)

Users can enable the streaming JSON processor for encryption/decryption operations by setting a property in `RequestOptions`:

```csharp
using Microsoft.Azure.Cosmos.Encryption.Custom;

// Create request options with Stream processor override
var requestOptions = new ItemRequestOptions
{
    Properties = new Dictionary<string, object>
    {
        { "encryption-json-processor", JsonProcessor.Stream }
    }
};

// Use in read operations to decrypt with streaming API
var response = await encryptionContainer.ReadItemAsync<MyDoc>(
    id, 
    partitionKey, 
    requestOptions);
```

### Option 2: Opt-in via EncryptionOptions (Write Operations)

For write operations, specify the processor in `EncryptionOptions`:

```csharp
var encryptionOptions = new EncryptionOptions
{
    DataEncryptionKeyId = "myDekId",
    EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
    PathsToEncrypt = new List<string> { "/sensitiveField", "/ssn" },
    JsonProcessor = JsonProcessor.Stream  // Opt-in to streaming
};

var encryptionRequestOptions = new EncryptionItemRequestOptions
{
    EncryptionOptions = encryptionOptions
};

await encryptionContainer.CreateItemAsync(item, partitionKey, encryptionRequestOptions);
```

### Full Workflow Example

```csharp
// Setup (one-time)
var encryptionContainer = await database.GetContainer("myContainer")
    .WithClientEncryptionAsync(encryptor);

// Write with streaming
var createOptions = new EncryptionItemRequestOptions
{
    EncryptionOptions = new EncryptionOptions
    {
        DataEncryptionKeyId = "dek1",
        EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
        PathsToEncrypt = new[] { "/ssn", "/creditCard" },
        JsonProcessor = JsonProcessor.Stream
    }
};

var document = new { id = "1", pk = "user123", ssn = "123-45-6789", creditCard = "4111..." };
await encryptionContainer.CreateItemAsync(document, new PartitionKey("user123"), createOptions);

// Read with streaming
var readOptions = new ItemRequestOptions
{
    Properties = new Dictionary<string, object>
    {
        { "encryption-json-processor", JsonProcessor.Stream }
    }
};

var result = await encryptionContainer.ReadItemAsync<MyDoc>("1", new PartitionKey("user123"), readOptions);
Console.WriteLine($"Decrypted SSN: {result.Resource.ssn}");
```

---

## Property Bag Key

The property bag key for RequestOptions override:
- **Key**: `"encryption-json-processor"`
- **Value**: `JsonProcessor.Stream` (enum) or string `"Stream"` (case-insensitive)

---

## Benefits

### 1. Reduced Memory Allocations
Stream processing avoids materializing entire JSON documents in memory, especially beneficial for large documents with multiple encrypted fields.

### 2. Better Performance
For large documents (>100KB), streaming can significantly reduce GC pressure and improve throughput.

### 3. Modern API
Leverages System.Text.Json's high-performance streaming capabilities available in .NET 8.0+.

### 4. Observable Behavior
Operations emit diagnostic scopes (via `ActivitySource`) indicating which processor was used, making it easy to verify the streaming path is being used.

---

## Compatibility

| Aspect | Details |
|--------|---------|
| **Platform** | .NET 8.0+ for Stream processor; .NET 6.0, .NET Standard 2.0 continue to use Newtonsoft |
| **Algorithm** | Only `MdeAeadAes256CbcHmac256Randomized` supports streaming processor |
| **Legacy Algorithm** | `AEAes256CbcHmacSha256Randomized` always uses Newtonsoft.Json |
| **Default Behavior** | Newtonsoft.Json (no changes to existing code) |
| **Diagnostics** | Scope names: `EncryptionProcessor.Encrypt.Mde.Stream` or `EncryptionProcessor.Encrypt.Mde.Newtonsoft` |

### Algorithm Compatibility Matrix

| Encryption Algorithm | Newtonsoft.Json | System.Text.Json Stream |
|---------------------|-----------------|------------------------|
| MdeAeadAes256CbcHmac256Randomized | ✅ Supported (default) | ✅ Supported (opt-in, .NET 8.0+) |
| AEAes256CbcHmacSha256Randomized (legacy) | ✅ Supported | ❌ Not supported (throws `NotSupportedException`) |

---

## Migration Path

Existing code continues to work without any changes - **this is a fully backward-compatible, opt-in feature**.

### To adopt streaming:

1. **Update to .NET 8.0+** (if not already)
   ```xml
   <TargetFramework>net8.0</TargetFramework>
   ```

2. **Verify you're using MDE algorithm** (not legacy)
   ```csharp
   EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized
   ```

3. **Add property bag override** to RequestOptions where streaming is desired
   ```csharp
   var options = new ItemRequestOptions
   {
       Properties = new Dictionary<string, object>
       {
           { "encryption-json-processor", JsonProcessor.Stream }
       }
   };
   ```

4. **Monitor diagnostic scopes** to verify Stream processor is being used
   ```csharp
   // Use ActivityListener or OpenTelemetry to observe scope names
   // Look for: "EncryptionProcessor.Encrypt.Mde.Stream"
   ```

### Gradual rollout strategy:
- Start with read-heavy operations
- Monitor performance metrics
- Gradually expand to write operations
- Fall back to Newtonsoft if issues arise (just remove the property bag key)

---

## Breaking Changes

**None** - This is an additive, opt-in feature that maintains full backward compatibility.

---

## Testing

- ✅ Unit tests for all encryption/decryption paths
- ✅ Integration tests with emulator
- ✅ Backward compatibility tests (Newtonsoft remains default)
- ✅ Cross-processor compatibility (encrypt with Stream, decrypt with Newtonsoft and vice versa)
- ✅ Error handling tests (legacy algorithm with Stream processor)
- ✅ Diagnostics tests (scope names verification)
- ✅ Large payload tests (>250KB documents)
- ✅ Concurrency and cancellation tests

---

## Implementation Details

### Architecture

```
EncryptionProcessor
    ├── MdeEncryptionProcessor (handles MDE algorithm)
    │   ├── IMdeJsonProcessorAdapter (interface)
    │   ├── NewtonsoftAdapter (Newtonsoft.Json implementation)
    │   └── SystemTextJsonStreamAdapter (System.Text.Json streaming)
    └── AeAesEncryptionProcessor (legacy algorithm, Newtonsoft only)
```

### Key Components

1. **`JsonProcessorRequestOptionsExtensions`**: Centralized handling of processor selection via property bag
2. **`IMdeJsonProcessorAdapter`**: Interface for pluggable JSON processor implementations
3. **`CosmosDiagnosticsContext`**: Lightweight diagnostics with scope tracking and ActivitySource integration
4. **`StreamProcessor`**: System.Text.Json streaming implementation for encrypt/decrypt operations

### Diagnostic Scopes

Monitor which processor is used via diagnostic scopes:

| Operation | Scope Name Format |
|-----------|------------------|
| Encrypt | `EncryptionProcessor.Encrypt.Mde.{JsonProcessor}` |
| Decrypt | `EncryptionProcessor.Decrypt.Mde.{JsonProcessor}` |

Example scope names:
- `EncryptionProcessor.Encrypt.Mde.Stream`
- `EncryptionProcessor.Encrypt.Mde.Newtonsoft`
- `EncryptionProcessor.Decrypt.Mde.Stream`
- `EncryptionProcessor.Decrypt.Mde.Newtonsoft`

---

## Performance Considerations

### When to use Stream processor:

✅ **Good fit:**
- Large documents (>100KB)
- Memory-constrained environments
- High-throughput scenarios
- Multiple encrypted fields per document

⚠️ **May not benefit:**
- Very small documents (<10KB)
- Low-frequency operations
- .NET 6.0 or earlier (not available)

### Benchmarks

See `EncryptionBenchmark.cs` for performance comparisons between processors.

---

## Future Enhancements

Potential future work (not in this PR):
- Automatic processor selection based on document size
- Performance telemetry integration
- Additional streaming optimizations for nested objects
- Support for streaming in legacy algorithm (if needed)

---

## Related Issues

Closes #4678

---

## Checklist

- [x] Code changes
- [x] Unit tests added/updated
- [x] Integration tests added/updated
- [x] Documentation updated (this PR description)
- [x] Backward compatibility verified
- [x] Performance tests included
- [x] Diagnostics/observability implemented
