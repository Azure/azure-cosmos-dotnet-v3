// Test-only helper to create RequestOptions with json processor override.
// Centralizes the property bag key usage to avoid duplication across tests.
#nullable enable
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests
{
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    internal static class RequestOptionsOverrideHelper
    {
        internal static RequestOptions? Create(JsonProcessor processor)
        {
#if NET8_0_OR_GREATER
#pragma warning disable COSMOSENC0001
            if (processor == JsonProcessor.Newtonsoft)
            {
                return null; // default path
            }

            return EncryptionRequestOptionsExperimental.CreateRequestOptions(processor);
#pragma warning restore COSMOSENC0001
#else
            return null;
#endif
        }
    }
}
