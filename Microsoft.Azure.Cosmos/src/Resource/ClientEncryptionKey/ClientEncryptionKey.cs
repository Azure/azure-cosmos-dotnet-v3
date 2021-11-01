﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides operations for reading a specific client data encryption key (aka ClientEncryptionKey) by Id.
    /// See <see cref="Database"/> for operations to create and enumerate client encryption keys.
    /// See https://aka.ms/CosmosClientEncryption for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
        abstract class ClientEncryptionKey
    {
        /// <summary>
        /// The unique identifier of the client encryption key.
        /// </summary>
        public abstract string Id { get; }

        /// <summary>
        /// Reads the properties of a client encryption key from the Azure Cosmos service as an asynchronous operation.
        /// This method is not meant to be invoked directly. Please see https://aka.ms/CosmosClientEncryption in order to use client-side encryption.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the request.</param>
        /// <param name="cancellationToken">(Optional) Token representing request cancellation.</param>
        /// <returns>An awaitable response which wraps a <see cref="ClientEncryptionKeyProperties"/> containing details of the client encryption key that was read.</returns>
        /// <exception cref="CosmosException">
        /// This exception can encapsulate many different types of errors.
        /// To determine the specific error always look at the StatusCode property.
        /// Some common codes you may get when reading a client encryption key are:
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
        public abstract Task<ClientEncryptionKeyResponse> ReadAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Replace a <see cref="ClientEncryptionKeyProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// This method is not meant to be invoked directly. Please see https://aka.ms/CosmosClientEncryption in order to use client-side encryption.
        /// </summary>
        /// <param name="clientEncryptionKeyProperties">The <see cref="ClientEncryptionKeyProperties"/> object.</param>
        /// <param name="requestOptions">(Optional) The options for the request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ClientEncryptionKeyResponse"/> which wraps a <see cref="ClientEncryptionKeyProperties"/> containing the replace resource record.
        /// </returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions</exception>
        public abstract Task<ClientEncryptionKeyResponse> ReplaceAsync(
            ClientEncryptionKeyProperties clientEncryptionKeyProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);
    }
}