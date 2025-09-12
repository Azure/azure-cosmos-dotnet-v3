// Test-only helper to create RequestOptions with json processor override.
// Centralizes the property bag key usage to avoid duplication across tests.
#nullable enable
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos;
    internal static class RequestOptionsOverrideHelper
    {
        internal static RequestOptions? Create(JsonProcessor processor)
        {
#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
            if (processor == JsonProcessor.Newtonsoft)
            {
                return null; // default path
            }
            return new ItemRequestOptions
            {
                Properties = new Dictionary<string, object>
                {
                    { "encryption-json-processor", processor }
                }
            };
#else
            return null;
#endif
        }
    }
}
