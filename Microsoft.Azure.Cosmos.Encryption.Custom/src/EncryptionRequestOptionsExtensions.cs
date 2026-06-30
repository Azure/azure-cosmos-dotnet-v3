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
        /// Selects the <see cref="JsonProcessor"/> used to encrypt or decrypt this encryption
        /// operation, overriding the container default for this call.
        /// </summary>
        /// <typeparam name="TRequestOptions">The concrete request-options type, preserved for fluent chaining.</typeparam>
        /// <param name="requestOptions">The request options to configure.</param>
        /// <param name="jsonProcessor">The JSON processor to use for this operation.</param>
        /// <returns>The same <paramref name="requestOptions"/> instance, to allow fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="requestOptions"/> is null.</exception>
        /// <remarks>
        /// <para>
        /// Works uniformly with <see cref="QueryRequestOptions"/>, <see cref="ChangeFeedRequestOptions"/>,
        /// and <see cref="ReadManyRequestOptions"/>: the selection is stored in
        /// <see cref="RequestOptions.Properties"/>, so it does not depend on (and is not blocked by) the
        /// sealed-ness of any of those types. The per-call selection takes precedence over the container
        /// default set via <c>WithEncryptor(container, encryptor, defaultJsonProcessor)</c>. It is the
        /// strongly-typed alternative to writing the <c>"encryption-json-processor"</c> property bag key
        /// directly.
        /// </para>
        /// <para>
        /// The method copies <see cref="RequestOptions.Properties"/> into a fresh dictionary rather than
        /// mutating the existing one, so a properties dictionary shared with other request-options
        /// instances is not affected. Calling it more than once on the same <paramref name="requestOptions"/>
        /// instance keeps only the last selection.
        /// </para>
        /// </remarks>
        public static TRequestOptions WithEncryptionJsonProcessor<TRequestOptions>(
            this TRequestOptions requestOptions,
            JsonProcessor jsonProcessor)
            where TRequestOptions : RequestOptions
        {
            ArgumentNullException.ThrowIfNull(requestOptions);

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
