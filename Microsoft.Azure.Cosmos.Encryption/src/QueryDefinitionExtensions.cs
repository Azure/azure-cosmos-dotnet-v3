//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Extension methods for <see cref="QueryDefinition"/> to support client-side encryption.
    /// </summary>
    [CLSCompliant(false)]
    public static class QueryDefinitionExtensions
    {
        /// <summary>
        /// Adds a parameter to a SQL query definition. This supports parameters corresponding to encrypted properties.
        /// </summary>
        /// <param name="queryDefinition">Query definition created using <see cref="EncryptionContainerExtensions.CreateQueryDefinition"/>.</param>
        /// <param name="name">Name of the parameter.</param>
        /// <param name="value">Value of the parameter.</param>
        /// <param name="path">Path of the parameter in the items being queried.</param>
        /// <param name="cancellationToken">Token for request cancellation.</param>
        /// <returns>Query definition with parameter added.</returns>
        /// <example>
        /// This example shows how to add a parameter corresponding to an encrypted property to a query definition.
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// QueryDefinition queryDefinitionWithEncryptedParameter = container.CreateQueryDefinition(
        ///     "SELECT * FROM c where c.SensitiveProperty = @FirstParameter");
        /// await queryDefinitionWithEncryptedParameter.AddParameterAsync(
        ///     name: "@FirstParameter",
        ///     value: "sensitive value",
        ///     path: "/SensitiveProperty");
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// Only equality comparisons are supported in the filter condition on encrypted properties.
        /// These also require the property being filtered upon to be encrypted using <see cref="EncryptionType.Deterministic"/> encryption.
        /// See <see href="https://aka.ms/CosmosClientEncryption">client-side encryption documentation</see> for more details.
        /// </remarks>
        public static async Task<QueryDefinition> AddParameterAsync(
            this QueryDefinition queryDefinition,
            string name,
            object value,
            string path,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (queryDefinition == null)
            {
                throw new ArgumentNullException(nameof(queryDefinition));
            }

            if (string.IsNullOrWhiteSpace(path) || path[0] != '/' || path.LastIndexOf('/') != 0)
            {
                throw new InvalidOperationException($"Invalid path {path ?? string.Empty}, {nameof(path)}. ");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            // if null use as-is
            if (value == null)
            {
                queryDefinition.WithParameter(name, value);
                return queryDefinition;
            }

            QueryDefinition queryDefinitionwithEncryptedValues = queryDefinition;

            if (queryDefinition is EncryptionQueryDefinition encryptionQueryDefinition)
            {
                EncryptionContainer encryptionContainer = (EncryptionContainer)encryptionQueryDefinition.Container;

                // get the path's encryption setting.
                EncryptionSettings encryptionSettings = await encryptionContainer.GetOrUpdateEncryptionSettingsFromCacheAsync(obsoleteEncryptionSettings: null, cancellationToken: cancellationToken);
                EncryptionSettingForProperty settingsForProperty = encryptionSettings.GetEncryptionSettingForProperty(path.Substring(1));

                if (settingsForProperty == null)
                {
                    // property not encrypted.
                    queryDefinitionwithEncryptedValues.WithParameter(name, value);
                    return queryDefinitionwithEncryptedValues;
                }

                if (settingsForProperty.EncryptionType == Data.Encryption.Cryptography.EncryptionType.Randomized)
                {
                    throw new ArgumentException($"Unsupported argument with Path: {path} for query. Executing queries on encrypted paths requires the use of deterministic encryption type. Please refer to https://aka.ms/CosmosClientEncryption for more details.");
                }

                Stream valueStream = encryptionContainer.CosmosSerializer.ToStream(value);

                Stream encryptedValueStream = await EncryptionProcessor.EncryptValueStreamAsync(
                    valueStream,
                    settingsForProperty,
                    path == "/id",
                    cancellationToken);

                queryDefinitionwithEncryptedValues.WithParameterStream(name, encryptedValueStream);

                return queryDefinitionwithEncryptedValues;
            }
            else
            {
                throw new ArgumentException("Executing queries on encrypted paths requires the use of an encryption-enabled client. Please refer to https://aka.ms/CosmosClientEncryption for more details.");
            }
        }
    }
}
