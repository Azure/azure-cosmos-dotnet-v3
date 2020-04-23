//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
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

        internal FeedResponse<T> CreateChangeFeedUserTypeResponse<T>(
            ResponseMessage responseMessage)
        {
            return this.CreateChangeFeedResponseHelper<T>(
                responseMessage,
                Documents.ResourceType.Document);
        }

        internal FeedResponse<T> CreateChangeFeedUserTypeResponse<T>(
            ResponseMessage responseMessage,
            Documents.ResourceType resourceType)
        {
            return this.CreateChangeFeedResponseHelper<T>(
                responseMessage,
                resourceType);
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

        private FeedResponse<T> CreateChangeFeedResponseHelper<T>(
            ResponseMessage cosmosResponseMessage,
            Documents.ResourceType resourceType)
        {
            return ReadFeedResponse<T>.CreateResponse<T>(
                       cosmosResponseMessage,
                       this.serializerCore,
                       resourceType);
        }

        internal Task<ItemResponse<T>> CreateItemResponseAsync<T>(
            Task<ResponseMessage> cosmosResponseMessageTask)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                T item = this.ToObjectInternal<T>(cosmosResponseMessage);
                DecryptionInfo decryptionInfo = null;
                if (cosmosResponseMessage is ItemResponse itemResponse)
                {
                    decryptionInfo = itemResponse.DecryptionInfo;
                }

                return new ItemResponse<T>(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    item,
                    cosmosResponseMessage.Diagnostics,
                    decryptionInfo);
            });
        }

        internal Task<ContainerResponse> CreateContainerResponseAsync(
            Container container,
            Task<ResponseMessage> cosmosResponseMessageTask)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                ContainerProperties containerProperties = this.ToObjectInternal<ContainerProperties>(cosmosResponseMessage);
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
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                UserProperties userProperties = this.ToObjectInternal<UserProperties>(cosmosResponseMessage);
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
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                PermissionProperties permissionProperties = this.ToObjectInternal<PermissionProperties>(cosmosResponseMessage);
                return new PermissionResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    permissionProperties,
                    permission,
                    cosmosResponseMessage.Diagnostics);
            });
        }

        internal Task<DatabaseResponse> CreateDatabaseResponseAsync(
            Database database,
            Task<ResponseMessage> cosmosResponseMessageTask)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                DatabaseProperties databaseProperties = this.ToObjectInternal<DatabaseProperties>(cosmosResponseMessage);

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
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                ThroughputProperties throughputProperties = this.ToObjectInternal<ThroughputProperties>(cosmosResponseMessage);
                return new ThroughputResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    throughputProperties,
                    cosmosResponseMessage.Diagnostics);
            });
        }

        internal Task<StoredProcedureExecuteResponse<T>> CreateStoredProcedureExecuteResponseAsync<T>(Task<ResponseMessage> cosmosResponseMessageTask)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                T item = this.ToObjectInternal<T>(cosmosResponseMessage);
                return new StoredProcedureExecuteResponse<T>(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    item,
                    cosmosResponseMessage.Diagnostics);
            });
        }

        internal Task<StoredProcedureResponse> CreateStoredProcedureResponseAsync(Task<ResponseMessage> cosmosResponseMessageTask)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                StoredProcedureProperties cosmosStoredProcedure = this.ToObjectInternal<StoredProcedureProperties>(cosmosResponseMessage);
                return new StoredProcedureResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    cosmosStoredProcedure,
                    cosmosResponseMessage.Diagnostics);
            });
        }

        internal Task<TriggerResponse> CreateTriggerResponseAsync(Task<ResponseMessage> cosmosResponseMessageTask)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                TriggerProperties triggerProperties = this.ToObjectInternal<TriggerProperties>(cosmosResponseMessage);
                return new TriggerResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    triggerProperties,
                    cosmosResponseMessage.Diagnostics);
            });
        }

        internal Task<UserDefinedFunctionResponse> CreateUserDefinedFunctionResponseAsync(Task<ResponseMessage> cosmosResponseMessageTask)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                UserDefinedFunctionProperties settings = this.ToObjectInternal<UserDefinedFunctionProperties>(cosmosResponseMessage);
                return new UserDefinedFunctionResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    settings,
                    cosmosResponseMessage.Diagnostics);
            });
        }

        internal async Task<T> ProcessMessageAsync<T>(Task<ResponseMessage> cosmosResponseTask, Func<ResponseMessage, T> createResponse)
        {
            using (ResponseMessage message = await cosmosResponseTask)
            {
                //Throw the exception
                message.EnsureSuccessStatusCode();

                return createResponse(message);
            }
        }

        internal T ToObjectInternal<T>(ResponseMessage responseMessage)
        {
            if (responseMessage.Content == null)
            {
                return default(T);
            }

            return this.serializerCore.FromStream<T>(responseMessage.Content);
        }
    }
}