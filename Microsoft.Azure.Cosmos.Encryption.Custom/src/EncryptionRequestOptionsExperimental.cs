//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

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
        /// <para>
        /// Passing <see cref="JsonProcessor.Newtonsoft"/> removes any override and reverts to the default Newtonsoft-based pipeline.
        /// Passing <see cref="JsonProcessor.Stream"/> opts into the System.Text.Json streaming pipeline for improved performance.
        /// </para>
        /// <para>
        /// Note: The Stream processor only supports the <see cref="CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized"/> encryption algorithm.
        /// Attempting to decrypt data encrypted with legacy algorithms using the Stream processor will result in a <see cref="NotSupportedException"/>.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// var options = new ItemRequestOptions();
        /// options.ConfigureJsonProcessor(JsonProcessor.Stream);
        ///
        /// // Or use the factory method:
        /// var options = EncryptionRequestOptionsExperimental.CreateRequestOptions(JsonProcessor.Stream);
        /// </code>
        /// </example>
        [Experimental(JsonProcessorExperimentalDiagnosticId)]
        public static TRequestOptions ConfigureJsonProcessor<TRequestOptions>(this TRequestOptions requestOptions, JsonProcessor jsonProcessor)
            where TRequestOptions : RequestOptions
        {
            ArgumentValidation.ThrowIfNull(requestOptions);

            if (!Enum.IsDefined(typeof(JsonProcessor), jsonProcessor))
            {
                throw new ArgumentOutOfRangeException(nameof(jsonProcessor));
            }

            Dictionary<string, object> properties = requestOptions.Properties != null
                ? new Dictionary<string, object>(requestOptions.Properties)
                : new Dictionary<string, object>();

            if (jsonProcessor == JsonProcessor.Newtonsoft)
            {
                properties.Remove(RequestOptionsPropertiesExtensions.JsonProcessorPropertyBagKey);
                requestOptions.Properties = properties.Count > 0 ? properties : null;
                return requestOptions;
            }

            properties[RequestOptionsPropertiesExtensions.JsonProcessorPropertyBagKey] = jsonProcessor;
            requestOptions.Properties = properties;

            return requestOptions;
        }

        /// <summary>
        /// Creates a new <see cref="ItemRequestOptions"/> configured with the requested JSON processor.
        /// </summary>
        /// <param name="jsonProcessor">The desired JSON processor.</param>
        /// <returns>The configured <see cref="ItemRequestOptions"/> instance.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="jsonProcessor"/> is not a defined value.</exception>
        /// <remarks>
        /// This is a convenience method equivalent to creating an <see cref="ItemRequestOptions"/> and calling
        /// <see cref="ConfigureJsonProcessor{TRequestOptions}"/> on it.
        /// </remarks>
        [Experimental(JsonProcessorExperimentalDiagnosticId)]
        public static ItemRequestOptions CreateRequestOptions(JsonProcessor jsonProcessor)
        {
            ItemRequestOptions requestOptions = new ();
            requestOptions.ConfigureJsonProcessor(jsonProcessor);
            return requestOptions;
        }
    }
}
