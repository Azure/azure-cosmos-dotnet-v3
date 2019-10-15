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
    using Microsoft.Azure.Cosmos;

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
            FeedResponse<T> feedResponse = this.CreateQueryFeedResponseHelper<T>(
                cosmosResponseMessage,
                true);

            return feedResponse.Value.ToList().AsReadOnly();
        }

        internal IReadOnlyList<T> CreateQueryFeedResponse<T>(
            Response cosmosResponseMessage)
        {
            FeedResponse<T> feedResponse = this.CreateQueryFeedResponseHelper<T>(
                cosmosResponseMessage,
                false);

            return feedResponse.Value.ToList().AsReadOnly();
        }

        private FeedResponse<T> CreateQueryFeedResponseHelper<T>(
            Response cosmosResponseMessage,
            bool usePropertySerializer)
        {
            //Throw the exception
            cosmosResponseMessage.EnsureSuccessStatusCode();

            // The property serializer should be used for internal
            // query operations like throughput since user serializer can break the logic
            CosmosSerializer serializer = usePropertySerializer ? this.propertiesSerializer : this.cosmosSerializer;

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
            Container container,
            Task<Response> cosmosResponseMessageTask,
            CancellationToken cancellationToken)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                ContainerProperties containerProperties = CosmosResponseFactory.ToObjectInternal<ContainerProperties>(
                    cosmosResponseMessage,
                    this.propertiesSerializer);

                return new ContainerResponse(
                    cosmosResponseMessage,
                    containerProperties,
                    container);
            });
        }

        internal Task<DatabaseResponse> CreateDatabaseResponseAsync(
            Database database,
            Task<Response> cosmosResponseMessageTask,
            CancellationToken cancellationToken)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                DatabaseProperties databaseProperties = CosmosResponseFactory.ToObjectInternal<DatabaseProperties>(
                    cosmosResponseMessage,
                    this.propertiesSerializer);

                return new DatabaseResponse(
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
            User user,
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
            Permission permission,
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