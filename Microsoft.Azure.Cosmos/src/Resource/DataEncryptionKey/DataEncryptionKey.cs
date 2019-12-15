//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;

    /// <summary>
    /// Provides operations for reading, re-wrapping, or deleting a specific data encryption key by Id.
    /// See <see cref="Database"/> for operations to create and enumerate data encryption keys.
    /// See <see href="http://tbd"/> for details of using client side encryption to encrypt sensitive information where
    /// the raw encryption keys are never revealed to the Azure Cosmos service.
    /// </summary>
    public abstract class DataEncryptionKey
    {
        /// <summary>
        /// The unique identifier of the data encryption key.
        /// </summary>
        public abstract string Id { get; }

        /// <summary>
        /// Reads the properties of a data encryption key from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the request.</param>
        /// <param name="cancellationToken">(Optional) Token representing request cancellation.</param>
        /// <returns>An awaitable response which wraps a <see cref="DataEncryptionKeyProperties"/> containing details of the data encryption key that was read.</returns>
        /// <exception cref="CosmosException">
        /// This exception can encapsulate many different types of errors.
        /// To determine the specific error always look at the StatusCode property.
        /// Some common codes you may get when reading a data encryption key are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term>
        ///         <description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term>
        ///         <description>
        ///         NotFound - This means the resource or parent resource you tried to read did not exist.
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>429</term>
        ///         <description>
        ///         TooManyRequests - This means you have exceeded the number of request units per second.
        ///         Consult the CosmosException.RetryAfter value to see how long you should wait before retrying this operation.
        ///         </description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// DataEncryptionKey key = this.database.GetDataEncryptionKey("keyId");
        /// DataEncryptionKeyProperties keyProperties = await key.ReadAsync();
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<DataEncryptionKeyResponse> ReadAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Wraps the raw data encryption key (after unwrapping using the old metadata if needed) using the provided
        /// metadata with the help of the key wrapping provider configured on the client via <see cref="CosmosClientBuilder.WithKeyWrapProvider"/>,
        /// and saves the re-wrapped data encryption key as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="newWrapMetadata">The metadata using which the data encryption key needs to now be wrapped.</param>
        /// <param name="requestOptions">(Optional) The options for the request.</param>
        /// <param name="cancellationToken">(Optional) Token representing request cancellation.</param>
        /// <returns>An awaitable response which wraps a <see cref="DataEncryptionKeyProperties"/> containing details of the data encryption key that was re-wrapped.</returns>
        /// <exception cref="CosmosException">
        /// This exception can encapsulate many different types of errors.
        /// To determine the specific error always look at the StatusCode property.
        /// Some common codes you may get when re-wrapping a data encryption key are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term>
        ///         <description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term>
        ///         <description>
        ///         NotFound - This means the resource or parent resource you tried to replace did not exist.
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <term>429</term>
        ///         <description>
        ///         TooManyRequests - This means you have exceeded the number of request units per second.
        ///         Consult the CosmosException.RetryAfter value to see how long you should wait before retrying this operation.
        ///         </description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>        
        /// <code language="c#">
        /// <![CDATA[
        /// AzureKeyVaultKeyWrapMetadata v2Metadata = new AzureKeyVaultKeyWrapMetadata("/path/to/my/master/key/v2");
        /// await key.RewrapAsync(v2Metadata);
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<DataEncryptionKeyResponse> RewrapAsync(
            KeyWrapMetadata newWrapMetadata,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Deletes a data encryption key from the Azure Cosmos DB service as an asynchronous operation.
        /// Note: If a data encryption key is deleted, then any data encrypted using the key will no longer be decryptable.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the user request.</param>
        /// <param name="cancellationToken">(Optional) Token representing request cancellation.</param>
        /// <returns>An awaitable response which will contain information about the status of the issued request.</returns>
        /// <exception cref="CosmosException">
        /// This exception can encapsulate many different types of errors.
        /// To determine the specific error always look at the StatusCode property.
        /// Some common codes you may get when deleting a data encryption key are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term>
        ///         <description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term>
        ///         <description>
        ///         NotFound - This means the resource or parent resource you tried to delete did not exist.
        ///         </description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// DataEncryptionKey key = this.database.GetDataEncryptionKey("keyId");
        /// await key.DeleteAsync();
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<DataEncryptionKeyResponse> DeleteAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
