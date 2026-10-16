// Test-only helper to create RequestOptions with json processor override.
// Centralizes the property bag key usage to avoid duplication across tests.
#nullable enable
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption.Custom;

    internal static class RequestOptionsOverrideHelper
    {
        internal static RequestOptions? Create(JsonProcessor processor)
        {
#if NET8_0_OR_GREATER
            if (processor == JsonProcessor.Newtonsoft)
            {
                return null; // default path
            }

            // Inline the logic from EncryptionRequestOptionsExperimental.CreateRequestOptions()
            ItemRequestOptions requestOptions = new ItemRequestOptions
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, processor.ToString() }
                }
            };
            return requestOptions;
#else
            return null;
#endif
        }

        internal static EncryptionItemRequestOptions Create(EncryptionOptions encryptionOptions, JsonProcessor processor)
        {
            return new EncryptionItemRequestOptions
            {
                EncryptionOptions = encryptionOptions,
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, processor }
                }
            };
        }
    }
}
