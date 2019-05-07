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

        internal CosmosFeedResponse<T> CreateResultSetQueryResponse<T>(
            CosmosResponseMessage cosmosResponseMessage)
        {
            return CosmosDefaultResultSetIterator<T>.CreateCosmosQueryResponse(
                cosmosResponseMessage,
                this.jsonSerializer);
        }

        internal Task<CosmosItemResponse<T>> CreateItemResponse<T>(
            Task<CosmosResponseMessage> cosmosResponseMessageTask)
        {
            return this.MessageHelper(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                T item = this.ToObjectInternal<T>(cosmosResponseMessage);
                return new CosmosItemResponse<T>(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    item);
            });
        }

        internal Task<CosmosContainerResponse> CreateContainerResponse(
            CosmosContainer container,
            Task<CosmosResponseMessage> cosmosResponseMessageTask)
        {
            return this.MessageHelper(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                CosmosContainerSettings settings = this.ToObjectInternal<CosmosContainerSettings>(cosmosResponseMessage);
                return new CosmosContainerResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    settings,
                    container);
            });
        }

        internal Task<CosmosDatabaseResponse> CreateDatabaseResponse(
            CosmosDatabase database,
            Task<CosmosResponseMessage> cosmosResponseMessageTask)
        {
            return this.MessageHelper(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                CosmosDatabaseSettings settings = this.ToObjectInternal<CosmosDatabaseSettings>(cosmosResponseMessage);
                return new CosmosDatabaseResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    settings,
                    database);
            });
        }

        internal Task<CosmosStoredProcedureExecuteResponse<T>> CreateStoredProcedureExecuteResponse<T>(Task<CosmosResponseMessage> cosmosResponseMessageTask)
        {
            return this.MessageHelper(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                T item = this.ToObjectInternal<T>(cosmosResponseMessage);
                return new CosmosStoredProcedureExecuteResponse<T>(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    item);
            });
        }

        internal Task<CosmosStoredProcedureResponse> CreateStoredProcedureResponse(Task<CosmosResponseMessage> cosmosResponseMessageTask)
        {
            return this.MessageHelper(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                CosmosStoredProcedureSettings cosmosStoredProcedure = this.ToObjectInternal<CosmosStoredProcedureSettings>(cosmosResponseMessage);
                return new CosmosStoredProcedureResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    cosmosStoredProcedure);
            });
        }

        internal Task<CosmosTriggerResponse> CreateTriggerResponse(Task<CosmosResponseMessage> cosmosResponseMessageTask)
        {
            return this.MessageHelper(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                CosmosTriggerSettings settings = this.ToObjectInternal<CosmosTriggerSettings>(cosmosResponseMessage);
                return new CosmosTriggerResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    settings);
            });
        }

        internal Task<CosmosUserDefinedFunctionResponse> CreateUserDefinedFunctionResponse(Task<CosmosResponseMessage> cosmosResponseMessageTask)
        {
            return this.MessageHelper(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                CosmosUserDefinedFunctionSettings settings = this.ToObjectInternal<CosmosUserDefinedFunctionSettings>(cosmosResponseMessage);
                return new CosmosUserDefinedFunctionResponse(
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