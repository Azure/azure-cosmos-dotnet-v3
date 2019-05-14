//------------------------------------------------------------
//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Scripts;

    internal class CosmosResponseFactory
    {
        /// <summary>
        /// Cosmos JSON converter. This allows custom JSON parsers.
        /// </summary>
        private CosmosJsonSerializer jsonSerializer { get; }

        internal CosmosResponseFactory(CosmosJsonSerializer cosmosJsonSerializer)
        {
            this.jsonSerializer = cosmosJsonSerializer;
        }

        internal FeedResponse<T> CreateResultSetQueryResponse<T>(
            CosmosResponseMessage cosmosResponseMessage)
        {
            return FeedIteratorCore<T>.CreateCosmosQueryResponse(
                cosmosResponseMessage,
                this.jsonSerializer);
        }

        internal Task<ItemResponse<T>> CreateItemResponse<T>(
            Task<CosmosResponseMessage> cosmosResponseMessageTask)
        {
            return this.MessageHelper(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                T item = this.ToObjectInternal<T>(cosmosResponseMessage);
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
                CosmosContainerSettings settings = this.ToObjectInternal<CosmosContainerSettings>(cosmosResponseMessage);
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
                CosmosDatabaseSettings settings = this.ToObjectInternal<CosmosDatabaseSettings>(cosmosResponseMessage);
                return new DatabaseResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    settings,
                    database);
            });
        }

        internal Task<StoredProcedureExecuteResponse<T>> CreateStoredProcedureExecuteResponse<T>(Task<CosmosResponseMessage> cosmosResponseMessageTask)
        {
            return this.MessageHelper(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                T item = this.ToObjectInternal<T>(cosmosResponseMessage);
                return new StoredProcedureExecuteResponse<T>(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    item);
            });
        }

        internal Task<StoredProcedureResponse> CreateStoredProcedureResponse(Task<CosmosResponseMessage> cosmosResponseMessageTask)
        {
            return this.MessageHelper(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                CosmosStoredProcedureSettings cosmosStoredProcedure = this.ToObjectInternal<CosmosStoredProcedureSettings>(cosmosResponseMessage);
                return new StoredProcedureResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    cosmosStoredProcedure);
            });
        }

        internal Task<TriggerResponse> CreateTriggerResponse(Task<CosmosResponseMessage> cosmosResponseMessageTask)
        {
            return this.MessageHelper(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                CosmosTriggerSettings settings = this.ToObjectInternal<CosmosTriggerSettings>(cosmosResponseMessage);
                return new TriggerResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    settings);
            });
        }

        internal Task<UserDefinedFunctionResponse> CreateUserDefinedFunctionResponse(Task<CosmosResponseMessage> cosmosResponseMessageTask)
        {
            return this.MessageHelper(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                CosmosUserDefinedFunctionSettings settings = this.ToObjectInternal<CosmosUserDefinedFunctionSettings>(cosmosResponseMessage);
                return new UserDefinedFunctionResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    settings);
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

        internal T ToObjectInternal<T>(CosmosResponseMessage cosmosResponseMessage)
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

            return this.jsonSerializer.FromStream<T>(cosmosResponseMessage.Content);
        }
    }
}