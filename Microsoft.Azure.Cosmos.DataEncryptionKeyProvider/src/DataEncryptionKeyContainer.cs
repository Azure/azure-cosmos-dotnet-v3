//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.DataEncryptionKeyProvider
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Container for data encryption keys. Provides methods to create, read, re-wrap and enumerate data encryption keys.
    /// See https://aka.ms/CosmosClientEncryption for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
    public abstract class DataEncryptionKeyContainer
    {
        /// <summary>
        /// Returns an iterator that can be iterated to get properties of data encryption keys.
        /// </summary>
        /// <param name="startId">(Optional) Starting value of the range (inclusive) of ids of data encryption keys for which properties needs to be returned.</param>
        /// <param name="endId">(Optional) Ending value of the range (inclusive) of ids of data encryption keys for which properties needs to be returned.</param>
        /// <param name="isDescending">Whether the results should be returned sorted in descending order of id.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the request. Set <see cref="QueryRequestOptions.MaxItemCount"/> to restrict the number of results returned.</param>
        /// <returns>An iterator over data encryption keys.</returns>
        /// <example>
        /// This create the type feed iterator for containers with queryDefinition as input.
        /// <code language="c#">
        /// <![CDATA[
        /// FeedIterator<DataEncryptionKeyProperties> resultSet = this.cosmosDatabase.GetDataEncryptionKeyIterator();
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     foreach (DataEncryptionKeyProperties properties in await feedIterator.ReadNextAsync())
        ///     {
        ///         Console.WriteLine(properties.Id);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <see cref="DataEncryptionKey.ReadDataEncryptionKeyAsync" /> is recommended for single data encryption key look-up.
        /// </remarks>
        public abstract FeedIterator<DataEncryptionKeyProperties> GetDataEncryptionKeyIterator(
            string startId = null,
            string endId = null,
            bool isDescending = false,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        /// Generates a data encryption key, wraps it using the key wrap metadata provided
        /// with the key wrapping provider in the EncryptionSerializer configured on the client via <see cref="CosmosClientBuilder.WithCustomSerializer"/>,
        /// and saves the wrapped data encryption key as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="id">Unique identifier for the data encryption key.</param>
        /// <param name="encryptionAlgorithm">Encryption algorithm that will be used along with this data encryption key to encrypt/decrypt data.</param>
        /// <param name="encryptionKeyWrapMetadata">Metadata used by the configured key wrapping provider in order to wrap the key.</param>
        /// <param name="requestOptions">(Optional) The options for the request.</param>
        /// <param name="cancellationToken">(Optional) Token representing request cancellation.</param>
        /// <returns>An awaitable response which wraps a <see cref="DataEncryptionKeyProperties"/> containing the read resource record.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="id"/> is not set.</exception>
        /// <exception cref="CosmosException">
        /// This exception can encapsulate many different types of errors.
        /// To determine the specific error always look at the StatusCode property.
        /// Some common codes you may get when creating a data encryption key are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an id was not supplied for the new encryption key.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means an <see cref="DataEncryptionKeyProperties"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// AzureKeyVaultKeyWrapMetadata wrapMetadata = new AzureKeyVaultKeyWrapMetadata("/path/to/my/akv/secret/v1");
        /// await this.cosmosDatabase.CreateDataEncryptionKeyAsync("myKey", wrapMetadata);
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ItemResponse<DataEncryptionKeyProperties>> CreateDataEncryptionKeyAsync(
            string id,
            CosmosEncryptionAlgorithm encryptionAlgorithm,
            EncryptionKeyWrapMetadata encryptionKeyWrapMetadata,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));


        /// <summary>
        /// Reads the properties of a data encryption key from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="id">Unique identifier of the data encryption key.</param>
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
        public abstract Task<ItemResponse<DataEncryptionKeyProperties>> ReadDataEncryptionKeyAsync(
            string id,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Wraps the raw data encryption key (after unwrapping using the old metadata if needed) using the provided
        /// metadata with the help of the key wrapping provider in the EncryptionSerializer configured on the client via
        /// <see cref="CosmosClientBuilder.WithCustomSerializer"/>, and saves the re-wrapped data encryption key as an asynchronous
        /// operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="id">Unique identifier of the data encryption key.</param>
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
        public abstract Task<ItemResponse<DataEncryptionKeyProperties>> RewrapDataEncryptionKeyAsync(
            string id,
            EncryptionKeyWrapMetadata newWrapMetadata,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
