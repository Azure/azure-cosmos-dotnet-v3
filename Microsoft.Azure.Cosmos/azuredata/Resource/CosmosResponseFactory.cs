//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Cosmos.Scripts;
    using Azure.Cosmos.Serialization;

    internal class CosmosResponseFactory
    {
        /// <summary>
        /// Cosmos JSON converter. This allows custom JSON parsers.
        /// </summary>
        private readonly CosmosSerializer cosmosSerializer;

        /// <summary>
        /// This is used for all meta data types
        /// </summary>
        private readonly CosmosSerializer propertiesSerializer;

        internal CosmosResponseFactory(
            CosmosSerializer defaultJsonSerializer,
            CosmosSerializer userJsonSerializer)
        {
            this.propertiesSerializer = defaultJsonSerializer;
            this.cosmosSerializer = userJsonSerializer;
        }

        internal IReadOnlyList<T> CreateQueryFeedResponseWithPropertySerializer<T>(
            Response cosmosResponseMessage)
        {
            return this.CreateQueryFeedResponseWithSerializer<T>(cosmosResponseMessage, this.propertiesSerializer);
        }

        internal IReadOnlyList<T> CreateQueryFeedResponse<T>(
            Response cosmosResponseMessage)
        {
            return this.CreateQueryFeedResponseWithSerializer<T>(cosmosResponseMessage, this.cosmosSerializer);
        }

        private IReadOnlyList<T> CreateQueryFeedResponseWithSerializer<T>(
            Response cosmosResponseMessage,
            CosmosSerializer serializer)
        {
            FeedResponse<T> feedResponse = this.CreateQueryFeedResponseHelper<T>(
                cosmosResponseMessage,
                serializer);

            return feedResponse.Value.ToList().AsReadOnly();
        }

        private FeedResponse<T> CreateQueryFeedResponseHelper<T>(
            Response cosmosResponseMessage,
            CosmosSerializer serializer)
        {
            //Throw the exception
            cosmosResponseMessage.EnsureSuccessStatusCode();

            QueryResponse queryResponse = cosmosResponseMessage as QueryResponse;
            if (queryResponse != null)
            {
                return QueryResponse<T>.CreateResponse<T>(
                    cosmosQueryResponse: queryResponse,
                    jsonSerializer: serializer);
            }

            return ReadFeedResponse<T>.CreateResponse<T>(
                       cosmosResponseMessage,
                       serializer);
        }

        internal Task<ContainerResponse> CreateContainerResponseAsync(
            CosmosContainer container,
            Task<Response> cosmosResponseMessageTask,
            CancellationToken cancellationToken)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                CosmosContainerProperties containerProperties = CosmosResponseFactory.ToObjectInternal<CosmosContainerProperties>(
                    cosmosResponseMessage,
                    this.propertiesSerializer);

                return new ContainerResponse(
                    cosmosResponseMessage,
                    containerProperties,
                    container);
            });
        }

        internal Task<CosmosDatabaseResponse> CreateDatabaseResponseAsync(
            CosmosDatabase database,
            Task<Response> cosmosResponseMessageTask,
            CancellationToken cancellationToken)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                CosmosDatabaseProperties databaseProperties = CosmosResponseFactory.ToObjectInternal<CosmosDatabaseProperties>(
                    cosmosResponseMessage,
                    this.propertiesSerializer);

                return new CosmosDatabaseResponse(
                    cosmosResponseMessage,
                    databaseProperties,
                    database);
            });
        }

        internal Task<ItemResponse<T>> CreateItemResponseAsync<T>(
            Task<Response> cosmosResponseMessageTask,
            CancellationToken cancellationToken)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                T item = CosmosResponseFactory.ToObjectInternal<T>(cosmosResponseMessage, this.cosmosSerializer);
                return new ItemResponse<T>(cosmosResponseMessage, item);
            });
        }

        internal Task<ThroughputResponse> CreateThroughputResponseAsync(
            Task<Response> cosmosResponseMessageTask,
            CancellationToken cancellationToken)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                ThroughputProperties throughputProperties = CosmosResponseFactory.ToObjectInternal<ThroughputProperties>(
                    cosmosResponseMessage,
                    this.propertiesSerializer);

                return new ThroughputResponse(
                    cosmosResponseMessage,
                    throughputProperties);
            });
        }

        internal Task<UserResponse> CreateUserResponseAsync(
            CosmosUser user,
            Task<Response> cosmosResponseMessageTask,
            CancellationToken cancellationToken)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                UserProperties userProperties = CosmosResponseFactory.ToObjectInternal<UserProperties>(
                    cosmosResponseMessage,
                    this.propertiesSerializer);
                return new UserResponse(
                    cosmosResponseMessage,
                    userProperties,
                    user);
            });
        }

        internal Task<PermissionResponse> CreatePermissionResponseAsync(
            CosmosPermission permission,
            Task<Response> cosmosResponseMessageTask,
            CancellationToken cancellationToken)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                PermissionProperties permissionProperties = CosmosResponseFactory.ToObjectInternal<PermissionProperties>(
                    cosmosResponseMessage,
                    this.propertiesSerializer);
                return new PermissionResponse(
                    cosmosResponseMessage,
                    permissionProperties,
                    permission);
            });
        }

        internal Task<StoredProcedureExecuteResponse<T>> CreateStoredProcedureExecuteResponseAsync<T>(
            Task<Response> cosmosResponseMessageTask,
            CancellationToken cancellationToken)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                T item = CosmosResponseFactory.ToObjectInternal<T>(
                    cosmosResponseMessage,
                    this.cosmosSerializer);
                return new StoredProcedureExecuteResponse<T>(
                    cosmosResponseMessage,
                    item);
            });
        }

        internal Task<Response<StoredProcedureProperties>> CreateStoredProcedureResponseAsync(
            Task<Response> cosmosResponseMessageTask,
            CancellationToken cancellationToken)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                StoredProcedureProperties cosmosStoredProcedure = CosmosResponseFactory.ToObjectInternal<StoredProcedureProperties>(
                    cosmosResponseMessage,
                    this.propertiesSerializer);
                return Response.FromValue(cosmosStoredProcedure, cosmosResponseMessage);
            });
        }

        internal Task<Response<TriggerProperties>> CreateTriggerResponseAsync(
            Task<Response> cosmosResponseMessageTask,
            CancellationToken cancellationToken)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                TriggerProperties triggerProperties = CosmosResponseFactory.ToObjectInternal<TriggerProperties>(
                    cosmosResponseMessage,
                    this.propertiesSerializer);
                return Response.FromValue(triggerProperties, cosmosResponseMessage);
            });
        }

        internal Task<Response<UserDefinedFunctionProperties>> CreateUserDefinedFunctionResponseAsync(
            Task<Response> cosmosResponseMessageTask,
            CancellationToken cancellationToken)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                UserDefinedFunctionProperties settings = CosmosResponseFactory.ToObjectInternal<UserDefinedFunctionProperties>(
                    cosmosResponseMessage,
                    this.propertiesSerializer);
                return Response.FromValue(settings, cosmosResponseMessage);
            });
        }

        internal async Task<T> ProcessMessageAsync<T>(Task<Response> cosmosResponseTask, Func<Response, T> createResponse)
        {
            using (Response message = await cosmosResponseTask)
            {
                return createResponse(message);
            }
        }

        internal static T ToObjectInternal<T>(Response response, CosmosSerializer jsonSerializer)
        {
            //Throw the exception
            response.EnsureSuccessStatusCode();
            if (response.ContentStream == null)
            {
                return default(T);
            }

            return jsonSerializer.FromStream<T>(response.ContentStream);
        }
    }
}