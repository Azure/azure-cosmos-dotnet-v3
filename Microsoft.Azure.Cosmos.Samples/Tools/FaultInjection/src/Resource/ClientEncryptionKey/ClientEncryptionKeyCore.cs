//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Provides operations for reading a specific client encryption key by Id.
    /// See <see cref="Cosmos.Database"/> for operations to create a client encryption key.
    /// </summary>
    internal class ClientEncryptionKeyCore : ClientEncryptionKey
    {
        /// <summary>
        /// Only used for unit testing
        /// </summary>
        protected ClientEncryptionKeyCore()
        {
        }

        public ClientEncryptionKeyCore(
            CosmosClientContext clientContext,
            DatabaseInternal database,
            string keyId)
        {
            this.Id = keyId;
            this.ClientContext = clientContext;
            this.LinkUri = ClientEncryptionKeyCore.CreateLinkUri(
                clientContext,
                database,
                keyId);
            this.Database = database;
        }

        /// <inheritdoc/>
        public override string Id { get; }

        /// <summary>
        /// Returns a reference to a database object that contains this encryption key. 
        /// </summary>
        public virtual Database Database { get; }

        public virtual string LinkUri { get; }

        public virtual CosmosClientContext ClientContext { get; }

        /// <inheritdoc/>
        public override async Task<ClientEncryptionKeyResponse> ReadAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            ClientEncryptionKeyResponse response = await this.ReadInternalAsync(
                requestOptions,
                trace: NoOpTrace.Singleton,
                cancellationToken: cancellationToken);

            return response;
        }

        /// <inheritdoc/>
        public override async Task<ClientEncryptionKeyResponse> ReplaceAsync(
            ClientEncryptionKeyProperties clientEncryptionKeyProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            ClientEncryptionKeyResponse response = await this.ReplaceInternalAsync(
                clientEncryptionKeyProperties,
                requestOptions,
                trace: NoOpTrace.Singleton,
                cancellationToken: cancellationToken);

            return response;
        }

        public static string CreateLinkUri(CosmosClientContext clientContext, DatabaseInternal database, string keyId)
        {
            return clientContext.CreateLink(
                parentLink: database.LinkUri,
                uriPathSegment: Paths.ClientEncryptionKeysPathSegment,
                id: keyId);
        }

        private async Task<ClientEncryptionKeyResponse> ReadInternalAsync(
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            ResponseMessage responseMessage = await this.ProcessStreamAsync(
                streamPayload: null,
                operationType: OperationType.Read,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            ClientEncryptionKeyResponse response = this.ClientContext.ResponseFactory.CreateClientEncryptionKeyResponse(this, responseMessage);
            Debug.Assert(response.Resource != null);

            return response;
        }

        private async Task<ClientEncryptionKeyResponse> ReplaceInternalAsync(
            ClientEncryptionKeyProperties clientEncryptionKeyProperties,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            ResponseMessage responseMessage = await this.ProcessStreamAsync(
                streamPayload: this.ClientContext.SerializerCore.ToStream(clientEncryptionKeyProperties),
                operationType: OperationType.Replace,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            ClientEncryptionKeyResponse response = this.ClientContext.ResponseFactory.CreateClientEncryptionKeyResponse(this, responseMessage);
            Debug.Assert(response.Resource != null);

            return response;
        }

        private Task<ResponseMessage> ProcessStreamAsync(
            Stream streamPayload,
            OperationType operationType,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.ProcessResourceOperationStreamAsync(
                resourceUri: this.LinkUri,
                resourceType: ResourceType.ClientEncryptionKey,
                operationType: operationType,
                cosmosContainerCore: null,
                feedRange: null,
                streamPayload: streamPayload,
                requestOptions: requestOptions,
                requestEnricher: null,
                trace: trace,
                cancellationToken: cancellationToken);
        }
    }
}