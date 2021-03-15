//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Cosmos.Scripts;

    internal sealed class CosmosResponseFactoryCore : CosmosResponseFactoryInternal
    {
        /// <summary>
        /// This is used for all meta data types
        /// </summary>
        private readonly CosmosSerializerCore serializerCore;

        public CosmosResponseFactoryCore(
            CosmosSerializerCore jsonSerializerCore)
        {
            this.serializerCore = jsonSerializerCore;
        }

        public override FeedResponse<T> CreateItemFeedResponse<T>(ResponseMessage responseMessage)
        {
            return this.CreateQueryFeedResponseHelper<T>(
                responseMessage);
        }

        public override FeedResponse<T> CreateChangeFeedUserTypeResponse<T>(
            ResponseMessage responseMessage)
        {
            return this.CreateChangeFeedResponseHelper<T>(
                responseMessage);
        }

        public override FeedResponse<T> CreateQueryFeedUserTypeResponse<T>(
            ResponseMessage responseMessage)
        {
            return this.CreateQueryFeedResponseHelper<T>(
                responseMessage);
        }

        public override FeedResponse<T> CreateQueryFeedResponse<T>(
            ResponseMessage responseMessage,
            Documents.ResourceType resourceType)
        {
            return this.CreateQueryFeedResponseHelper<T>(
                responseMessage);
        }

        private FeedResponse<T> CreateQueryFeedResponseHelper<T>(
            ResponseMessage cosmosResponseMessage)
        {
            if (cosmosResponseMessage is QueryResponse queryResponse)
            {
                return QueryResponse<T>.CreateResponse<T>(
                    cosmosQueryResponse: queryResponse,
                    serializerCore: this.serializerCore);
            }

            return ReadFeedResponse<T>.CreateResponse<T>(
                       cosmosResponseMessage,
                       this.serializerCore);
        }

        private FeedResponse<T> CreateChangeFeedResponseHelper<T>(
            ResponseMessage cosmosResponseMessage)
        {
            return ReadFeedResponse<T>.CreateResponse<T>(
                       cosmosResponseMessage,
                       this.serializerCore);
        }

        public override ItemResponse<T> CreateItemResponse<T>(
            ResponseMessage responseMessage)
        {
            return this.ProcessMessage(responseMessage, (cosmosResponseMessage) =>
            {
                T item = this.ToObjectpublic<T>(cosmosResponseMessage);
                return new ItemResponse<T>(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    item,
                    cosmosResponseMessage.Trace);
            });
        }

        public override ContainerResponse CreateContainerResponse(
            Container container,
            ResponseMessage responseMessage)
        {
            return this.ProcessMessage(responseMessage, (cosmosResponseMessage) =>
            {
                ContainerProperties containerProperties = this.ToObjectpublic<ContainerProperties>(cosmosResponseMessage);
                return new ContainerResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    containerProperties,
                    container,
                    cosmosResponseMessage.Diagnostics);
            });
        }

        public override UserResponse CreateUserResponse(
            User user,
            ResponseMessage responseMessage)
        {
            return this.ProcessMessage(responseMessage, (cosmosResponseMessage) =>
            {
                UserProperties userProperties = this.ToObjectpublic<UserProperties>(cosmosResponseMessage);
                return new UserResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    userProperties,
                    user,
                    cosmosResponseMessage.Diagnostics);
            });
        }

        public override PermissionResponse CreatePermissionResponse(
            Permission permission,
            ResponseMessage responseMessage)
        {
            return this.ProcessMessage<PermissionResponse>(responseMessage, (cosmosResponseMessage) =>
            {
                PermissionProperties permissionProperties = this.ToObjectpublic<PermissionProperties>(cosmosResponseMessage);
                return new PermissionResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    permissionProperties,
                    permission,
                    cosmosResponseMessage.Diagnostics);
            });
        }

        public override ClientEncryptionKeyResponse CreateClientEncryptionKeyResponse(
            ClientEncryptionKey clientEncryptionKey,
            ResponseMessage responseMessage)
        {
            return this.ProcessMessage(responseMessage, (cosmosResponseMessage) =>
            {
                ClientEncryptionKeyProperties cekProperties = this.ToObjectpublic<ClientEncryptionKeyProperties>(cosmosResponseMessage);
                return new ClientEncryptionKeyResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    cekProperties,
                    clientEncryptionKey,
                    cosmosResponseMessage.Diagnostics);
            });
        }

        public override DatabaseResponse CreateDatabaseResponse(
            Database database,
            ResponseMessage responseMessage)
        {
            return this.ProcessMessage(responseMessage, (cosmosResponseMessage) =>
            {
                DatabaseProperties databaseProperties = this.ToObjectpublic<DatabaseProperties>(cosmosResponseMessage);

                return new DatabaseResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    databaseProperties,
                    database,
                    cosmosResponseMessage.Diagnostics);
            });
        }

        public override ThroughputResponse CreateThroughputResponse(
            ResponseMessage responseMessage)
        {
            return this.ProcessMessage(responseMessage, (cosmosResponseMessage) =>
            {
                ThroughputProperties throughputProperties = this.ToObjectpublic<ThroughputProperties>(cosmosResponseMessage);
                return new ThroughputResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    throughputProperties,
                    cosmosResponseMessage.Diagnostics);
            });
        }

        public override StoredProcedureExecuteResponse<T> CreateStoredProcedureExecuteResponse<T>(ResponseMessage responseMessage)
        {
            return this.ProcessMessage(responseMessage, (cosmosResponseMessage) =>
            {
                T item = this.ToObjectpublic<T>(cosmosResponseMessage);
                return new StoredProcedureExecuteResponse<T>(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    item,
                    cosmosResponseMessage.Diagnostics);
            });
        }

        public override StoredProcedureResponse CreateStoredProcedureResponse(ResponseMessage responseMessage)
        {
            return this.ProcessMessage(responseMessage, (cosmosResponseMessage) =>
            {
                StoredProcedureProperties cosmosStoredProcedure = this.ToObjectpublic<StoredProcedureProperties>(cosmosResponseMessage);
                return new StoredProcedureResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    cosmosStoredProcedure,
                    cosmosResponseMessage.Diagnostics);
            });
        }

        public override TriggerResponse CreateTriggerResponse(ResponseMessage responseMessage)
        {
            return this.ProcessMessage(responseMessage, (cosmosResponseMessage) =>
            {
                TriggerProperties triggerProperties = this.ToObjectpublic<TriggerProperties>(cosmosResponseMessage);
                return new TriggerResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    triggerProperties,
                    cosmosResponseMessage.Diagnostics);
            });
        }

        public override UserDefinedFunctionResponse CreateUserDefinedFunctionResponse(
            ResponseMessage responseMessage)
        {
            return this.ProcessMessage(responseMessage, (cosmosResponseMessage) =>
            {
                UserDefinedFunctionProperties settings = this.ToObjectpublic<UserDefinedFunctionProperties>(cosmosResponseMessage);
                return new UserDefinedFunctionResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    settings,
                    cosmosResponseMessage.Diagnostics);
            });
        }

        public T ProcessMessage<T>(ResponseMessage responseMessage, Func<ResponseMessage, T> createResponse)
        {
            using (ResponseMessage message = responseMessage)
            {
                //Throw the exception
                message.EnsureSuccessStatusCode();

                return createResponse(message);
            }
        }

        public T ToObjectpublic<T>(ResponseMessage responseMessage)
        {
            if (responseMessage.Content == null)
            {
                return default;
            }

            return this.serializerCore.FromStream<T>(responseMessage.Content);
        }
    }
}