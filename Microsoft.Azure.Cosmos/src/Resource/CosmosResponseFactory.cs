//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using System.Threading.Tasks;

    internal class CosmosResponseFactory
    {
        /// <summary>
        /// Cosmos JSON converter. This allows custom JSON parsers.
        /// </summary>
        private readonly CosmosJsonSerializer userJsonSerializer;

        /// <summary>
        /// This is used for all meta data types
        /// </summary>
        private readonly CosmosJsonSerializer defaultJsonSerializer;

        internal CosmosResponseFactory(
            CosmosJsonSerializer defaultJsonSerializer,
            CosmosJsonSerializer userJsonSerializer)
        {
            this.defaultJsonSerializer = defaultJsonSerializer;
            this.userJsonSerializer = userJsonSerializer;
        }

        internal FeedResponse<T> CreateResultSetQueryResponse<T>(
            CosmosResponseMessage cosmosResponseMessage)
        {
            return FeedIteratorCore<T>.CreateCosmosQueryResponse(
                cosmosResponseMessage,
                this.userJsonSerializer);
        }

        internal Task<ItemResponse<T>> CreateItemResponse<T>(
            Task<CosmosResponseMessage> cosmosResponseMessageTask)
        {
            return this.MessageHelper(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                T item = this.ToObjectInternal<T>(cosmosResponseMessage, this.userJsonSerializer);
                return new ItemResponse<T>(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    item);
            });
        }

        internal Task<ContainerResponse> CreateContainerResponse(
            CosmosContainer container,
            Task<CosmosResponseMessage> cosmosResponseMessageTask)
        {
            return this.MessageHelper(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                CosmosContainerSettings settings = this.ToObjectInternal<CosmosContainerSettings>(cosmosResponseMessage, this.defaultJsonSerializer);
                return new ContainerResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    settings,
                    container);
            });
        }

        internal Task<DatabaseResponse> CreateDatabaseResponse(
            CosmosDatabase database,
            Task<CosmosResponseMessage> cosmosResponseMessageTask)
        {
            return this.MessageHelper(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                CosmosDatabaseSettings settings = this.ToObjectInternal<CosmosDatabaseSettings>(cosmosResponseMessage, this.defaultJsonSerializer);
                return new DatabaseResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    settings,
                    database);
            });
        }

        internal Task<StoredProcedureResponse> CreateStoredProcedureResponse(
            CosmosStoredProcedure storedProcedure,
            Task<CosmosResponseMessage> cosmosResponseMessageTask)
        {
            return this.MessageHelper(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                CosmosStoredProcedureSettings settings = this.ToObjectInternal<CosmosStoredProcedureSettings>(cosmosResponseMessage, this.defaultJsonSerializer);
                return new StoredProcedureResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    settings,
                    storedProcedure);
            });
        }

        internal Task<TriggerResponse> CreateTriggerResponse(
            CosmosTrigger trigger,
            Task<CosmosResponseMessage> cosmosResponseMessageTask)
        {
            return this.MessageHelper(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                CosmosTriggerSettings settings = this.ToObjectInternal<CosmosTriggerSettings>(cosmosResponseMessage, this.defaultJsonSerializer);
                return new TriggerResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    settings,
                    trigger);
            });
        }

        internal Task<UserDefinedFunctionResponse> CreateUserDefinedFunctionResponse(
            CosmosUserDefinedFunction userDefinedFunction,
            Task<CosmosResponseMessage> cosmosResponseMessageTask)
        {
            return this.MessageHelper(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                CosmosUserDefinedFunctionSettings settings = this.ToObjectInternal<CosmosUserDefinedFunctionSettings>(cosmosResponseMessage, this.defaultJsonSerializer);
                return new UserDefinedFunctionResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    settings,
                    userDefinedFunction);
            });
        }

        internal Task<T> MessageHelper<T>(Task<CosmosResponseMessage> cosmosResponseTask, Func<CosmosResponseMessage, T> createResponse)
        {
            return cosmosResponseTask.ContinueWith((action) =>
            {
                using (CosmosResponseMessage message = action.Result)
                {
                    return createResponse(message);
                }
            });
        }

        internal T ToObjectInternal<T>(CosmosResponseMessage cosmosResponseMessage, CosmosJsonSerializer jsonSerializer)
        {
            // Not finding something is part of a normal work-flow and should not be an exception.
            // This prevents the unnecessary overhead of an exception
            if (cosmosResponseMessage.StatusCode == HttpStatusCode.NotFound)
            {
                return default(T);
            }

            //Throw the exception
            cosmosResponseMessage.EnsureSuccessStatusCode();

            if (cosmosResponseMessage.Content == null)
            {
                return default(T);
            }

            return jsonSerializer.FromStream<T>(cosmosResponseMessage.Content);
        }
    }
}