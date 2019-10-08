//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Data.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

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
                    cosmosResponseMessage.Status,
                    cosmosResponseMessage.Headers,
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
                    cosmosResponseMessage.Status,
                    cosmosResponseMessage.Headers,
                    databaseProperties,
                    database);
            });
        }

        internal Task<Response<T>> CreateItemResponseAsync<T>(
            Task<Response> cosmosResponseMessageTask,
            CancellationToken cancellationToken)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                T item = CosmosResponseFactory.ToObjectInternal<T>(cosmosResponseMessage, this.cosmosSerializer);
                return new Response<T>(cosmosResponseMessage, item);
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
            // helper?
            if (response.Status < 200 || response.Status >= 300)
            {
                string message = $"Response status code does not indicate success: {response.Status} Reason: ({response.ReasonPhrase}).";

                throw new CosmosException(
                        response,
                        message);
            }
            if (response.ContentStream == null)
            {
                return default(T);
            }

            return jsonSerializer.FromStream<T>(response.ContentStream);
        }
    }
}