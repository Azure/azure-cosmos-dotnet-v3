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
#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
            if (processor == JsonProcessor.Newtonsoft)
            {
                return null; // default path
            }

            return EncryptionRequestOptionsExperimental.CreateRequestOptions(processor);
#else
            return null;
#endif
        }
    }
}
