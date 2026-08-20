//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Extension methods on <see cref="RequestOptions"/> for configuring client-side-encryption
    /// JSON processing on a per-operation basis.
    /// </summary>
    public static class EncryptionRequestOptionsExtensions
    {
        /// <summary>
        /// Selects the <see cref="JsonProcessor"/> used by this encryption operation, overriding the
        /// container response-decryption default for this call.
        /// </summary>
        /// <typeparam name="TRequestOptions">The concrete request-options type, preserved for fluent chaining.</typeparam>
        /// <param name="requestOptions">The request options to configure.</param>
        /// <param name="jsonProcessor">The JSON processor to use for this operation.</param>
        /// <returns>The same <paramref name="requestOptions"/> instance, to allow fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="requestOptions"/> is null.</exception>
        /// <remarks>
        /// <para>
        /// Works uniformly with write and feed request options. Encryption write options retain the
        /// selection internally so the Cosmos bulk executor does not reject it as a user property; other
        /// request-option types store it in <see cref="RequestOptions.Properties"/>. The per-call selection
        /// takes precedence over the container default set via
        /// <see cref="EncryptionContainerExtensions.UseStreamingJsonProcessingByDefault(Container)"/>.
        /// </para>
        /// <para>
        /// For request options that use <see cref="RequestOptions.Properties"/>, the method copies the
        /// dictionary rather than mutating it, so a dictionary shared with other request-options instances
        /// is not affected. Calling it more than once on the same <paramref name="requestOptions"/> instance
        /// keeps only the last selection.
        /// </para>
        /// </remarks>
        public static TRequestOptions WithEncryptionJsonProcessor<TRequestOptions>(
            this TRequestOptions requestOptions,
            JsonProcessor jsonProcessor)
            where TRequestOptions : RequestOptions
        {
            ArgumentNullException.ThrowIfNull(requestOptions);
            if (!Enum.IsDefined(typeof(JsonProcessor), jsonProcessor))
            {
                throw new ArgumentOutOfRangeException(nameof(jsonProcessor), jsonProcessor, "Unsupported JSON processor.");
            }

            if (requestOptions is EncryptionItemRequestOptions itemOptions)
            {
                itemOptions.JsonProcessorOverride = jsonProcessor;
                return requestOptions;
            }

            if (requestOptions is EncryptionTransactionalBatchItemRequestOptions batchItemOptions)
            {
                batchItemOptions.JsonProcessorOverride = jsonProcessor;
                return requestOptions;
            }

            Dictionary<string, object> properties = requestOptions.Properties is null
                ? new Dictionary<string, object>()
                : new Dictionary<string, object>(requestOptions.Properties);

            properties[JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey] = jsonProcessor;
            requestOptions.Properties = properties;

            return requestOptions;
        }
    }
}
#endif
