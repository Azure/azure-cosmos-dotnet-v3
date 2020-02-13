//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Scripts;

    internal class CosmosResponseFactory
    {
        /// <summary>
        /// This is used for all meta data types
        /// </summary>
        private readonly CosmosSerializerCore serializerCore;

        internal CosmosResponseFactory(
            CosmosSerializerCore jsonSerializerCore)
        {
            this.serializerCore = jsonSerializerCore;
        }

        internal FeedResponse<T> CreateQueryFeedUserTypeResponse<T>(
            ResponseMessage responseMessage)
        {
            return this.CreateQueryFeedResponseHelper<T>(
                responseMessage,
                Documents.ResourceType.Document);
        }

        internal FeedResponse<T> CreateQueryFeedResponse<T>(
            ResponseMessage responseMessage,
            Documents.ResourceType resourceType)
        {
            return this.CreateQueryFeedResponseHelper<T>(
                responseMessage,
                resourceType);
        }

        private FeedResponse<T> CreateQueryFeedResponseHelper<T>(
            ResponseMessage cosmosResponseMessage,
            Documents.ResourceType resourceType)
        {
            //Throw the exception
            cosmosResponseMessage.EnsureSuccessStatusCode();

            QueryResponse queryResponse = cosmosResponseMessage as QueryResponse;
            if (queryResponse != null)
            {
                return QueryResponse<T>.CreateResponse<T>(
                    cosmosQueryResponse: queryResponse,
                    serializerCore: this.serializerCore);
            }

            return ReadFeedResponse<T>.CreateResponse<T>(
                       cosmosResponseMessage,
                       this.serializerCore,
                       resourceType);
        }

        internal Task<ItemResponse<T>> CreateItemResponseAsync<T>(
            Task<ResponseMessage> cosmosResponseMessageTask,
            Container container,
            ItemRequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, async (cosmosResponseMessage) =>
            {
                T item = await this.ToObjectInternalAsync<T>(
                    cosmosResponseMessage,
                    shouldPerformDecryption: true,
                    container,
                    requestOptions,
                    cancellationToken);

                return new ItemResponse<T>(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    item,
                    cosmosResponseMessage.Diagnostics);
            });
        }

        internal Task<ContainerResponse> CreateContainerResponseAsync(
            Container container,
            Task<ResponseMessage> cosmosResponseMessageTask)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, async (cosmosResponseMessage) =>
            {
                ContainerProperties containerProperties = await this.ToObjectInternalAsync<ContainerProperties>(cosmosResponseMessage);
                return new ContainerResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    containerProperties,
                    container,
                    cosmosResponseMessage.Diagnostics);
            });
        }

        internal Task<UserResponse> CreateUserResponseAsync(
            User user,
            Task<ResponseMessage> cosmosResponseMessageTask)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, async (cosmosResponseMessage) =>
            {
                UserProperties userProperties = await this.ToObjectInternalAsync<UserProperties>(cosmosResponseMessage);
                return new UserResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    userProperties,
                    user,
                    cosmosResponseMessage.Diagnostics);
            });
        }

        internal Task<PermissionResponse> CreatePermissionResponseAsync(
            Permission permission,
            Task<ResponseMessage> cosmosResponseMessageTask)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, async (cosmosResponseMessage) =>
            {
                PermissionProperties permissionProperties = await this.ToObjectInternalAsync<PermissionProperties>(cosmosResponseMessage);
                return new PermissionResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    permissionProperties,
                    permission,
                    cosmosResponseMessage.Diagnostics);
            });
        }

        internal Task<DataEncryptionKeyResponse> CreateDataEncryptionKeyResponseAsync(
            DataEncryptionKey dataEncryptionKey,
            Task<ResponseMessage> cosmosResponseMessageTask)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, async (cosmosResponseMessage) =>
            {
                DataEncryptionKeyProperties dekProperties = await this.ToObjectInternalAsync<DataEncryptionKeyProperties>(cosmosResponseMessage);

                return new DataEncryptionKeyResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    dekProperties,
                    dataEncryptionKey,
                    cosmosResponseMessage.Diagnostics);
            });
        }

        internal Task<DatabaseResponse> CreateDatabaseResponseAsync(
            Database database,
            Task<ResponseMessage> cosmosResponseMessageTask)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, async (cosmosResponseMessage) =>
            {
                DatabaseProperties databaseProperties = await this.ToObjectInternalAsync<DatabaseProperties>(cosmosResponseMessage);

                return new DatabaseResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    databaseProperties,
                    database,
                    cosmosResponseMessage.Diagnostics);
            });
        }

        internal Task<ThroughputResponse> CreateThroughputResponseAsync(
            Task<ResponseMessage> cosmosResponseMessageTask)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, async (cosmosResponseMessage) =>
            {
                ThroughputProperties throughputProperties = await this.ToObjectInternalAsync<ThroughputProperties>(cosmosResponseMessage);
                return new ThroughputResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    throughputProperties,
                    cosmosResponseMessage.Diagnostics);
            });
        }

        internal Task<StoredProcedureExecuteResponse<T>> CreateStoredProcedureExecuteResponseAsync<T>(Task<ResponseMessage> cosmosResponseMessageTask)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, async (cosmosResponseMessage) =>
            {
                T item = await this.ToObjectInternalAsync<T>(cosmosResponseMessage);
                return new StoredProcedureExecuteResponse<T>(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    item,
                    cosmosResponseMessage.Diagnostics);
            });
        }

        internal Task<StoredProcedureResponse> CreateStoredProcedureResponseAsync(Task<ResponseMessage> cosmosResponseMessageTask)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, async (cosmosResponseMessage) =>
            {
                StoredProcedureProperties cosmosStoredProcedure = await this.ToObjectInternalAsync<StoredProcedureProperties>(cosmosResponseMessage);
                return new StoredProcedureResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    cosmosStoredProcedure,
                    cosmosResponseMessage.Diagnostics);
            });
        }

        internal Task<TriggerResponse> CreateTriggerResponseAsync(Task<ResponseMessage> cosmosResponseMessageTask)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, async (cosmosResponseMessage) =>
            {
                TriggerProperties triggerProperties = await this.ToObjectInternalAsync<TriggerProperties>(cosmosResponseMessage);
                return new TriggerResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    triggerProperties,
                    cosmosResponseMessage.Diagnostics);
            });
        }

        internal Task<UserDefinedFunctionResponse> CreateUserDefinedFunctionResponseAsync(Task<ResponseMessage> cosmosResponseMessageTask)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, async (cosmosResponseMessage) =>
            {
                UserDefinedFunctionProperties settings = await this.ToObjectInternalAsync<UserDefinedFunctionProperties>(cosmosResponseMessage);
                return new UserDefinedFunctionResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    settings,
                    cosmosResponseMessage.Diagnostics);
            });
        }

        internal async Task<T> ProcessMessageAsync<T>(Task<ResponseMessage> cosmosResponseTask, Func<ResponseMessage, Task<T>> createResponse)
        {
            using (ResponseMessage message = await cosmosResponseTask)
            {
                //Throw the exception
                message.EnsureSuccessStatusCode();

                return await createResponse(message);
            }
        }

        private async Task<T> ToObjectInternalAsync<T>(
            ResponseMessage responseMessage,
            bool shouldPerformDecryption = false,
            Container container = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (responseMessage.Content == null)
            {
                return default(T);
            }

            Stream streamToDeserialize = responseMessage.Content;
            if (shouldPerformDecryption == true)
            {
                Debug.Assert(container != null);
                Debug.Assert(container is ContainerCore);

                streamToDeserialize = await ((ContainerCore)container).ClientContext.EncryptionProcessor.DecryptAsync(responseMessage.Content, container, cancellationToken);
            }

            return this.serializerCore.FromStream<T>(streamToDeserialize);
        }
    }
}