//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Experimental helpers for configuring which JSON processor pipeline is used by the encryption stack.
    /// </summary>
    public static class EncryptionRequestOptionsExperimental
    {
        private const string JsonProcessorExperimentalDiagnosticId = "COSMOSENC0001";

        /// <summary>
        /// Configures the JSON processor that should be used for encryption and decryption operations.
        /// </summary>
        /// <typeparam name="TRequestOptions">The concrete <see cref="RequestOptions"/> type being configured.</typeparam>
        /// <param name="requestOptions">The request options instance to configure.</param>
        /// <param name="jsonProcessor">The desired JSON processor.</param>
        /// <returns>The same <paramref name="requestOptions"/> instance for fluent usage.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="requestOptions"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="jsonProcessor"/> is not a defined value.</exception>
    /// <remarks>
    /// Passing <see cref="JsonProcessor.Newtonsoft"/> removes any override and reverts to the default Newtonsoft-based pipeline.
    /// Passing <see cref="JsonProcessor.Stream"/> opts into the System.Text.Json streaming pipeline.
    /// </remarks>
        [Experimental(JsonProcessorExperimentalDiagnosticId)]
        public static TRequestOptions ConfigureJsonProcessor<TRequestOptions>(this TRequestOptions requestOptions, JsonProcessor jsonProcessor)
            where TRequestOptions : RequestOptions
        {
            ArgumentNullException.ThrowIfNull(requestOptions);

            if (!Enum.IsDefined(typeof(JsonProcessor), jsonProcessor))
            {
                throw new ArgumentOutOfRangeException(nameof(jsonProcessor));
            }

            Dictionary<string, object> properties = requestOptions.Properties != null
                ? new Dictionary<string, object>(requestOptions.Properties)
                : new Dictionary<string, object>();

            if (jsonProcessor == JsonProcessor.Newtonsoft)
            {
                properties.Remove(JsonProcessorPropertyBag.JsonProcessorPropertyBagKey);
                requestOptions.Properties = properties.Count > 0 ? properties : null;
                return requestOptions;
            }

            properties[JsonProcessorPropertyBag.JsonProcessorPropertyBagKey] = jsonProcessor;
            requestOptions.Properties = properties;

            return requestOptions;
        }

        /// <summary>
        /// Creates a new <see cref="ItemRequestOptions"/> configured with the requested JSON processor.
        /// </summary>
        /// <param name="jsonProcessor">The desired JSON processor.</param>
        /// <returns>The configured <see cref="ItemRequestOptions"/> instance.</returns>
        [Experimental(JsonProcessorExperimentalDiagnosticId)]
        public static ItemRequestOptions CreateRequestOptions(JsonProcessor jsonProcessor)
        {
            ItemRequestOptions requestOptions = new ();
            requestOptions.ConfigureJsonProcessor(jsonProcessor);
            return requestOptions;
        }
    }
}
#endif
